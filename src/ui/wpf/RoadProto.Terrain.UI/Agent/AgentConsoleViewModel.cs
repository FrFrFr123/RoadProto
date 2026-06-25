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
    private readonly HashSet<string> _loggedEventKeys = new(StringComparer.Ordinal);
    private AgentRunDto? _currentRun;
    private string _backendStatus = "未连接";
    private string _traceId = "-";
    private string _selectedProvider = "GPT";
    private string _providerBaseUrl = "https://api.openai.com/v1";
    private string _providerModel = "gpt-4.1";
    private bool _canConfirm;
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

    public async Task InitializeAsync()
    {
        BackendStatus = "启动中";
        var result = await _supervisor.EnsureBackendAsync().ConfigureAwait(true);
        BackendStatus = result;
        await LoadProviderSettingsAsync().ConfigureAwait(true);
        Log("PanelOpened", "Agent 控制台已打开。");
    }

    public async Task SendAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Messages.Add("我: " + message);
        var run = _currentRun?.State == "AwaitingUserInput"
            ? await _client.PostUserInputAsync(_currentRun.TaskId, message).ConfigureAwait(true)
            : await _client.StartRunAsync(null, message).ConfigureAwait(true);
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
        Messages.Add("Agent: 已确认，正在调用 RoadProto 本地工具。");

        var toolResult = await _toolBridge.DispatchAsync(run).ConfigureAwait(true);
        Log("LocalToolValidationCompleted", toolResult.Message);
        var completed = await _client.PostToolResultAsync(run.TaskId, toolResult).ConfigureAwait(true);
        _currentRun = completed;
        Messages.Add(toolResult.Succeeded
            ? $"Agent: {toolResult.Message} Handle: {toolResult.EntityId ?? "-"}"
            : $"Agent: {toolResult.Message}");
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
        Messages.Add("Agent: 已取消本次操作。");
    }

    private void ApplyRun(AgentRunDto run)
    {
        _currentRun = run;
        TraceId = run.TraceId;
        CanConfirm = run.State == "AwaitingUserConfirmation";
        LogRunEvents(run);

        if (run.State == "AwaitingUserInput")
        {
            var followUpMessage = !string.IsNullOrWhiteSpace(run.FollowUpMessage)
                ? run.FollowUpMessage
                : run.Plan?.FollowUpMessage ?? "请补充必要信息。";
            Messages.Add("Agent: " + followUpMessage);
            return;
        }

        if (run.Plan != null && run.State == "AwaitingUserConfirmation")
        {
            Messages.Add("Agent: " + DescribePlan(run.Plan));
            if (!string.IsNullOrWhiteSpace(run.Plan.RiskLevel))
            {
                Messages.Add($"Agent: 风险等级：{TranslateRiskLevel(run.Plan.RiskLevel)}；将调用：{TranslateToolName(run.Plan.ToolName)}。");
            }
        }
        else if (run.EntryRoute != null && run.ToolResult != null)
        {
            Messages.Add("Agent: " + run.ToolResult.Message);
            return;
        }
        else
        {
            Messages.Add("Agent: " + run.State);
        }

        if (run.State == "Succeeded" && run.ToolResult != null)
        {
            Messages.Add("Agent: " + run.ToolResult.Message);
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
        Messages.Add($"Agent: {providerView.Provider} 配置已保存，API Key: {(providerView.HasApiKey ? "已配置" : "未配置")}。");
        Log("ProviderSettingsSaved", $"{providerView.Provider} {providerView.Model}");
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
