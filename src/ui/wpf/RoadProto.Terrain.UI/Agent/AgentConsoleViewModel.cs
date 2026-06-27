using RoadProto.Terrain.UI.Agent.Bridge;
using RoadProto.Terrain.UI.Agent.Backend;
using RoadProto.Terrain.UI.Agent.Diagnostics;
using RoadProto.Terrain.UI.Agent.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RoadProto.Terrain.UI.Agent;

public sealed class AgentConsoleViewModel : INotifyPropertyChanged
{
    private readonly AgentBackendClient _client;
    private readonly AgentBackendSupervisor _supervisor;
    private readonly AgentTraceLogger _logger;
    private readonly AgentLocalToolBridge _toolBridge = new();
    private readonly AgentCadContextProvider _cadContextProvider = new();
    private readonly HashSet<string> _loggedEventKeys = new(StringComparer.Ordinal);
    private AgentRunDto? _currentRun;
    private string? _sessionId;
    private string _backendStatus = "未连接";
    private string _traceId = "-";
    private string _selectedProvider = "GPT";
    private string _providerBaseUrl = "https://api.openai.com/v1";
    private string _providerModel = "gpt-4.1";
    private bool _canConfirm;
    private bool _canPickTarget;
    private bool _providerEnabled = true;
    private bool _isApplyingProviderView;

    public AgentConsoleViewModel()
    {
        var options = new AgentBackendOptions();
        _client = new AgentBackendClient(options);
        _supervisor = new AgentBackendSupervisor(options, _client);
        _logger = new AgentTraceLogger(options.RoadProtoLogDirectory);
        Providers.Add("GPT");
        Providers.Add("DeepSeek");
        Providers.Add("Qwen");
        Providers.Add("GLM");
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(MessagesText));
        LogLines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Providers { get; } = new();

    public ObservableCollection<string> Messages { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public string MessagesText => string.Join(Environment.NewLine, Messages);

    public string LogText => string.Join(Environment.NewLine, LogLines);

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetField(ref _selectedProvider, value))
            {
                if (!_isApplyingProviderView)
                {
                    ApplyProviderDefaults(value);
                }
            }
        }
    }

    public string ProviderBaseUrl
    {
        get => _providerBaseUrl;
        set => SetField(ref _providerBaseUrl, value);
    }

    public string ProviderModel
    {
        get => _providerModel;
        set => SetField(ref _providerModel, value);
    }

    public bool ProviderEnabled
    {
        get => _providerEnabled;
        set => SetField(ref _providerEnabled, value);
    }

    public string RoadProtoLogDirectory => _logger.LogDirectory;

    public string BackendStatus
    {
        get => _backendStatus;
        private set => SetField(ref _backendStatus, value);
    }

    public string TraceId
    {
        get => _traceId;
        private set => SetField(ref _traceId, value);
    }

    public bool CanConfirm
    {
        get => _canConfirm;
        private set => SetField(ref _canConfirm, value);
    }

    public bool CanPickTarget
    {
        get => _canPickTarget;
        private set => SetField(ref _canPickTarget, value);
    }

    public async Task InitializeAsync()
    {
        BackendStatus = "启动中";
        Log("BackendEnsureStarted", "开始检查并启动 Agent 后端。");
        var result = await _supervisor.EnsureBackendAsync().ConfigureAwait(true);
        BackendStatus = result;
        Log("BackendEnsureCompleted", result);
        Log("ProviderSettingsLoadStarted", "开始读取已保存的模型配置。");
        await LoadProviderSettingsAsync().ConfigureAwait(true);
        Log("PanelOpened", "Agent 控制台已打开。");
    }

    public async Task SendAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var cadContext = _cadContextProvider.CaptureForMessage(message);
        await SendWithCadContextAsync(message, cadContext, message).ConfigureAwait(true);
    }

    public async Task PickTargetAsync()
    {
        var cadContext = _cadContextProvider.PickSubgradeTemplate();
        if (!cadContext.HasCurrentTemplate)
        {
            AddAgentMessage("未选择有效的路基模板。");
            Log("CadContextPickCancelled", "用户未点选有效路基模板。");
            return;
        }

        await SendWithCadContextAsync("选中的路基模板", cadContext, "点选路基模板").ConfigureAwait(true);
    }

    private async Task SendWithCadContextAsync(string message, AgentCadContext cadContext, string visibleUserMessage)
    {
        StartVisibleUserTurn(visibleUserMessage);
        if (cadContext.HasCurrentTemplate)
        {
            Log("CadContextCaptured", $"当前选择集检测到路基模板 Handle={cadContext.CurrentTemplateHandle ?? "-"}。");
        }

        var run = _currentRun?.State == "AwaitingUserInput"
            ? await _client.PostUserInputAsync(
                _currentRun.TaskId,
                message,
                cadContext.CurrentTemplateHandle,
                cadContext.CurrentTemplateName).ConfigureAwait(true)
            : await _client.StartRunAsync(
                _sessionId,
                message,
                cadContext.CurrentTemplateHandle,
                cadContext.CurrentTemplateName).ConfigureAwait(true);
        ApplyRun(run);
    }

    public async Task ConfirmAsync()
    {
        if (_currentRun == null)
        {
            return;
        }

        var run = await _client.ConfirmAsync(_currentRun.TaskId).ConfigureAwait(true);
        _currentRun = run;
        CanConfirm = false;
        ApplyRun(run);
        AddAgentMessage("已确认，正在调用 RoadProto 本地工具。");

        var toolResult = await _toolBridge.DispatchAsync(run).ConfigureAwait(true);
        Log("LocalToolValidationCompleted", toolResult.Message);
        var completed = await _client.PostToolResultAsync(run.TaskId, toolResult).ConfigureAwait(true);
        _currentRun = completed;
        RememberSession(completed);
        AddAgentMessage(toolResult.Succeeded
            ? $"{toolResult.Message} Handle: {toolResult.EntityId ?? "-"}"
            : toolResult.Message);
    }

    public async Task CancelAsync()
    {
        if (_currentRun == null)
        {
            return;
        }

        var run = await _client.CancelAsync(_currentRun.TaskId).ConfigureAwait(true);
        _currentRun = run;
        CanConfirm = false;
        ApplyRun(run);
        AddAgentMessage("已取消本次操作。");
    }

    private void ApplyRun(AgentRunDto run)
    {
        _currentRun = run;
        RememberSession(run);
        TraceId = run.TraceId;
        CanConfirm = run.State == "AwaitingUserConfirmation";
        CanPickTarget = run.State == "AwaitingUserInput" && NeedsTemplateTarget(run.FollowUpMessage ?? run.Plan?.FollowUpMessage);
        LogRunEvents(run);

        if (run.State == "AwaitingUserInput")
        {
            var followUpMessage = run.FollowUpMessage;
            if (string.IsNullOrWhiteSpace(followUpMessage))
            {
                followUpMessage = run.Plan?.FollowUpMessage;
            }

            AddAgentMessage(followUpMessage ?? "请补充必要信息。");
            return;
        }

        if (run.Plan != null && run.State == "AwaitingUserConfirmation")
        {
            AddAgentMessage(DescribePlan(run.Plan));
            if (!string.IsNullOrWhiteSpace(run.Plan.RiskLevel))
            {
                AddAgentMessage($"风险等级：{TranslateRiskLevel(run.Plan.RiskLevel)}；将调用：{TranslateToolName(run.Plan.ToolName)}。");
            }
        }
        else if (run.EntryRoute != null && run.ToolResult != null)
        {
            AddAgentMessage(run.ToolResult.Message);
            return;
        }
        else
        {
            AddAgentMessage(run.State);
        }

        if (run.State == "Succeeded" && run.ToolResult != null)
        {
            AddAgentMessage(run.ToolResult.Message);
        }
    }

    private void RememberSession(AgentRunDto run)
    {
        if (!string.IsNullOrWhiteSpace(run.SessionId))
        {
            _sessionId = run.SessionId;
        }
    }

    public async Task SaveProviderAsync(string apiKey)
    {
        var providerView = await _client.SaveModelProviderAsync(
            SelectedProvider,
            new ModelProviderUpdateDto
            {
                BaseUrl = ProviderBaseUrl,
                Model = ProviderModel,
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
                IsEnabled = ProviderEnabled,
            }).ConfigureAwait(true);
        ApplyProviderView(providerView);
        AddAgentMessage($"{providerView.Provider} 配置已保存，API Key: {(providerView.HasApiKey ? "已配置" : "未配置")}。");
        Log("ProviderSettingsSaved", $"{providerView.Provider} {providerView.Model}");
    }

    private void StartVisibleUserTurn(string message)
    {
        if (Messages.Count > 0)
        {
            Messages.Add(string.Empty);
            Messages.Add(string.Empty);
        }

        if (LogLines.Count > 0)
        {
            LogLines.Add(string.Empty);
            LogLines.Add(string.Empty);
        }

        AddConversationLine("我", message);
    }

    private void AddAgentMessage(string message)
    {
        AddConversationLine("Agent", message);
    }

    private void AddConversationLine(string speaker, string message)
    {
        Messages.Add($"--- {speaker}: {message}");
    }

    private async Task LoadProviderSettingsAsync()
    {
        var providerMap = await _client.GetModelProvidersAsync().ConfigureAwait(true);
        var savedProviders = providerMap.Values
            .Where(provider => provider != null)
            .OrderByDescending(GetProviderUpdatedAt)
            .ToList();
        var providerView = savedProviders.FirstOrDefault(provider => provider.HasApiKey)
            ?? savedProviders.FirstOrDefault(provider => provider.IsEnabled);

        if (providerView == null)
        {
            Log("ProviderSettingsNotFound", "未找到已保存的模型配置，保留默认值。");
            return;
        }

        ApplyProviderView(providerView);
        Log("ProviderSettingsLoaded", $"{providerView.Provider} {providerView.Model}");
    }

    private void ApplyProviderView(ModelProviderViewDto providerView)
    {
        _isApplyingProviderView = true;
        try
        {
            var provider = NormalizeProvider(providerView.Provider);
            if (!Providers.Contains(provider))
            {
                Providers.Add(provider);
            }

            SelectedProvider = provider;
            ProviderBaseUrl = providerView.BaseUrl;
            ProviderModel = providerView.Model;
            ProviderEnabled = providerView.IsEnabled;
        }
        finally
        {
            _isApplyingProviderView = false;
        }
    }

    private static DateTimeOffset GetProviderUpdatedAt(ModelProviderViewDto provider)
    {
        return DateTimeOffset.TryParse(
            provider.UpdatedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var timestamp)
            ? timestamp
            : DateTimeOffset.MinValue;
    }

    private static string NormalizeProvider(string provider)
    {
        return provider switch
        {
            "DeepSeek" => "DeepSeek",
            "Qwen" => "Qwen",
            "GLM" => "GLM",
            "GPT" => "GPT",
            _ => string.IsNullOrWhiteSpace(provider) ? "GPT" : provider,
        };
    }

    private static bool NeedsTemplateTarget(string? followUpMessage)
    {
        if (string.IsNullOrWhiteSpace(followUpMessage))
        {
            return false;
        }

        return ContainsOrdinal(followUpMessage, "路基模板目标")
            || ContainsOrdinal(followUpMessage, "操作的路基模板")
            || ContainsOrdinal(followUpMessage, "哪个路基模板")
            || ContainsOrdinal(followUpMessage, "目标模板");
    }

    private static bool ContainsOrdinal(string? text, string value)
    {
        return text != null && text.IndexOf(value, StringComparison.Ordinal) >= 0;
    }

    private void ApplyProviderDefaults(string provider)
    {
        switch (provider)
        {
            case "DeepSeek":
                ProviderBaseUrl = "https://api.deepseek.com";
                ProviderModel = "deepseek-chat";
                break;
            case "Qwen":
                ProviderBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
                ProviderModel = "qwen-plus";
                break;
            case "GLM":
                ProviderBaseUrl = "https://open.bigmodel.cn/api/paas/v4";
                ProviderModel = "glm-4";
                break;
            default:
                ProviderBaseUrl = "https://api.openai.com/v1";
                ProviderModel = "gpt-4.1";
                break;
        }
    }

    public void Log(string stage, string message)
    {
        LogLines.Add(AgentLogFormatter.Format(stage, message));
        _logger.Write(TraceId, stage, message);
    }

    private void LogRunEvents(AgentRunDto run)
    {
        foreach (var flowEvent in run.Events ?? Enumerable.Empty<AgentRunEventDto>())
        {
            var key = $"{run.TaskId}|{flowEvent.Timestamp}|{flowEvent.Stage}|{flowEvent.Message}";
            if (_loggedEventKeys.Add(key))
            {
                Log(flowEvent.Stage, flowEvent.Message);
            }
        }
    }

    private static string DescribePlan(AgentPlanDto plan)
    {
        return $"已识别：{TranslateSkillName(plan.SkillId)} / {TranslateIntentName(plan.IntentId)}。{plan.Summary}";
    }

    private static string TranslateSkillName(string skillId)
    {
        return skillId switch
        {
            "subgrade_template" => "路基模板",
            _ => string.IsNullOrWhiteSpace(skillId) ? "未识别能力" : skillId,
        };
    }

    private static string TranslateIntentName(string intentId)
    {
        return intentId switch
        {
            "subgrade_template.create" => "创建路基模板",
            "subgrade_template.modify" => "修改路基模板",
            "subgrade_template.delete" => "删除路基模板",
            "subgrade_template.query" => "查询路基模板",
            _ => string.IsNullOrWhiteSpace(intentId) ? "未识别动作" : intentId,
        };
    }

    private static string TranslateRiskLevel(string riskLevel)
    {
        return riskLevel switch
        {
            "low" => "低",
            "medium" => "中",
            "high" => "高",
            _ => string.IsNullOrWhiteSpace(riskLevel) ? "未标注" : riskLevel,
        };
    }

    private static string TranslateToolName(string toolName)
    {
        return toolName switch
        {
            "SubgradeTemplate.Create" => "创建路基模板工具",
            "SubgradeTemplate.Modify" => "修改路基模板工具",
            "SubgradeTemplate.Delete" => "删除路基模板工具",
            "SubgradeTemplate.Query" => "查询路基模板工具",
            _ => string.IsNullOrWhiteSpace(toolName) ? "未指定工具" : toolName,
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
