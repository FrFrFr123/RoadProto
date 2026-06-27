using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RoadProto.Terrain.UI.Agent;

public static class AgentLogFormatter
{
    private const string LogLineMarker = "---";

    public static string Format(string stage, string message)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return string.IsNullOrWhiteSpace(message) ? "日志：-" : message;
        }

        var normalizedMessage = message ?? string.Empty;
        var formatted = stage switch
        {
            "BackendEnsureStarted" => "后端检查：开始检查并按需启动 Agent 后端。",
            "BackendEnsureCompleted" => $"后端检查完成：{normalizedMessage}",
            "ProviderSettingsLoadStarted" => "模型配置：开始读取已保存的 Provider 设置。",
            "ProviderSettingsNotFound" => "模型配置：未找到已保存配置，暂用默认值。",
            "PanelOpened" => "面板已打开：Agent 控制台初始化完成。",
            "ProviderSettingsLoaded" => $"模型配置已读取：{normalizedMessage}",
            "ProviderSettingsSaved" => $"模型配置已保存：{normalizedMessage}",
            "EntryRouted" => FormatEntryRoute(normalizedMessage),
            "ContinuationRerouted" => $"补充输入已重新路由：{FormatRouteMessage(normalizedMessage)}",
            "InputReceived" => $"输入已接收：{ValueOrDefault(normalizedMessage, "后端开始处理用户表达。")}",
            "AgentRouted" => $"Agent 已选择：{ValueOrDefault(normalizedMessage, "roadproto_engineering_agent。")}",
            "SkillRouted" => $"Skill 已选择：{ValueOrDefault(normalizedMessage, "subgrade_template。")}",
            "IntentRecognized" => FormatDiagnosticStage("意图已识别", normalizedMessage, "模型输出已解析。"),
            "SchemaValidated" => FormatDiagnosticStage("Schema 已校验", normalizedMessage, "模型输出结构符合约束。"),
            "RulesApplied" => FormatDiagnosticStage("规则已应用", normalizedMessage, "Intent / Skill / Tool 规则校验完成。"),
            "PlanOutput" => FormatDiagnosticStage("计划输出", normalizedMessage, "后端未返回计划字段。"),
            "ToolArgumentsPrepared" => FormatDiagnosticStage("工具参数", normalizedMessage, "后端未返回工具参数。"),
            "AwaitingUserInput" => IsTaskStateMessage(normalizedMessage)
                ? FormatRunUpdated(normalizedMessage)
                : $"等待补充信息：{normalizedMessage}",
            "AwaitingUserConfirmation" => $"等待用户确认：{ValueOrDefault(normalizedMessage, "确认前不会写入 CAD。")}",
            "UserConfirmed" => $"用户已确认：准备调用本地工具 {normalizedMessage}",
            "ToolDispatchRequested" => FormatDiagnosticStage("工具调度已请求", normalizedMessage, "正在交给 RoadProto 本地 Adapter。"),
            "LocalToolValidationCompleted" => $"本地工具校验完成：{normalizedMessage}",
            "ToolResultPosted" => $"工具结果已回传：{FormatRunState(normalizedMessage)}",
            "RuntimeFactsAnswered" => "已由运行时事实层回答：未调用大模型或 CAD 工具。",
            "ConversationModelRequested" => "已调用大模型生成对话回复：未进入工程执行工作流。",
            "RunUpdated" => FormatRunUpdated(normalizedMessage),
            "RunSucceeded" => $"任务已成功完成：{normalizedMessage}",
            "RunFailed" => $"任务失败：{normalizedMessage}",
            "UserCancelled" => "用户已取消：当前任务终止。",
            _ => $"{TranslateStage(stage)}：{normalizedMessage}"
        };

        return IndentLogLine(formatted);
    }

    private static string IndentLogLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "  --- -";
        }

        var lines = NormalizeNewLines(text).Split('\n');
        if (lines.Length == 1)
        {
            return "  " + EnsureLogLineMarker(lines[0]);
        }

        return "  " + EnsureLogLineMarker(lines[0]) + Environment.NewLine + IndentDetailLines(lines.Skip(1));
    }

    private static string IndentDetailLines(IEnumerable<string> lines)
    {
        return string.Join(Environment.NewLine, lines.Select(line => "    " + EnsureLogLineMarker(line)));
    }

    private static string EnsureLogLineMarker(string line)
    {
        var trimmed = (line ?? string.Empty).TrimStart();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return LogLineMarker + " -";
        }

        return trimmed.StartsWith(LogLineMarker, StringComparison.Ordinal)
            ? trimmed
            : LogLineMarker + " " + trimmed;
    }

    private static string NormalizeNewLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string FormatDiagnosticStage(string label, string message, string fallback)
    {
        var value = ValueOrDefault(message, fallback);
        var normalized = NormalizeNewLines(value);
        return normalized.TrimStart().StartsWith(LogLineMarker, StringComparison.Ordinal)
            ? $"{label}：{Environment.NewLine}{normalized}"
            : $"{label}：{value}";
    }

    private static string FormatEntryRoute(string message)
    {
        return $"入口路由：{FormatRouteMessage(message)}";
    }

    private static string FormatRouteMessage(string message)
    {
        var separator = message.IndexOf(':');
        if (separator < 0)
        {
            return message;
        }

        var routeType = message.Substring(0, separator).Trim();
        var reason = message.Substring(separator + 1).Trim();
        return $"{TranslateRouteType(routeType)}。{reason}";
    }

    private static string FormatRunUpdated(string message)
    {
        var match = Regex.Match(message, @"Task\s+(?<taskId>\S+)\s+(?<state>\S+)", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return $"任务状态已更新：{message}";
        }

        return $"任务状态已更新：{FormatRunState(match.Groups["state"].Value)}。TaskId={match.Groups["taskId"].Value}";
    }

    private static bool IsTaskStateMessage(string message)
    {
        return Regex.IsMatch(message, @"^Task\s+\S+\s+\S+", RegexOptions.CultureInvariant);
    }

    private static string ValueOrDefault(string message, string fallback)
    {
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    private static string FormatRunState(string state)
    {
        return state switch
        {
            "AwaitingUserInput" => "等待用户补充信息",
            "AwaitingUserConfirmation" => "等待用户确认",
            "DispatchingTool" => "正在调度本地工具",
            "Succeeded" => "任务已成功完成",
            "Failed" => "任务失败",
            "Cancelled" => "任务已取消",
            _ => string.IsNullOrWhiteSpace(state) ? "未知状态" : state
        };
    }

    private static string TranslateRouteType(string routeType)
    {
        return routeType switch
        {
            "ChatOnly" => "闲聊",
            "HelpOnly" => "咨询解释",
            "WorkflowCandidate" => "工作流候选",
            "WorkflowCommand" => "工程工作流",
            "UnsupportedWorkflow" => "未接入的工程对象",
            _ => string.IsNullOrWhiteSpace(routeType) ? "未知路由" : routeType
        };
    }

    private static string TranslateStage(string stage)
    {
        return string.IsNullOrWhiteSpace(stage)
            ? "日志"
            : stage;
    }
}
