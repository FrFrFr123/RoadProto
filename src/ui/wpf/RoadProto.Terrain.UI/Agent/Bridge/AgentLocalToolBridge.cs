using Autodesk.AutoCAD.ApplicationServices;
using RoadProto.Terrain.UI.Agent.Models;
using RoadProto.Terrain.UI.Bridge;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace RoadProto.Terrain.UI.Agent.Bridge;

public sealed class AgentLocalToolBridge
{
    private const string SubgradeCreateToolName = "SubgradeTemplate.Create";
    private const string SubgradeModifyToolName = "SubgradeTemplate.Modify";
    private const string SubgradeDeleteToolName = "SubgradeTemplate.Delete";
    private const string SubgradeQueryToolName = "SubgradeTemplate.Query";

    public async Task<AgentToolResultDto> DispatchAsync(AgentRunDto run, CancellationToken cancellationToken = default)
    {
        if (run.DispatchedToolCall == null)
        {
            return Failure("后端没有下发本地工具调用。");
        }

        if (!IsSupportedSubgradeTool(run.DispatchedToolCall.ToolName))
        {
            return Failure($"不支持的 Agent 工具: {run.DispatchedToolCall.ToolName}");
        }

        var document = CoreApplication.DocumentManager.MdiActiveDocument;
        if (document == null)
        {
            return Failure("当前没有可用的 AutoCAD 文档，无法写入路基模板。");
        }

        var arguments = ParseSubgradeArguments(run.DispatchedToolCall.ArgumentsJson);
        if (!string.Equals(run.DispatchedToolCall.ToolName, SubgradeCreateToolName, StringComparison.OrdinalIgnoreCase))
        {
            var requestPath = Path.Combine(Path.GetTempPath(), $"RoadProtoAgentSubgradeTool_{Guid.NewGuid():N}.request");
            var toolResultPath = Path.Combine(Path.GetTempPath(), $"RoadProtoAgentSubgradeTool_{Guid.NewGuid():N}.result");
            WriteSubgradeToolRequest(requestPath, run, run.DispatchedToolCall.ToolName, arguments, toolResultPath);
            var nativeRequestPath = requestPath.Replace('\\', '/');
            document.SendStringToExecute($"RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE \"{nativeRequestPath}\"\n", true, false, true);
            return await WaitForNativeResultAsync(toolResultPath, cancellationToken).ConfigureAwait(true);
        }

        var validationMessage = ValidateCreateArguments(arguments);
        if (validationMessage != null)
        {
            return Failure(validationMessage);
        }

        var responsePath = Path.Combine(Path.GetTempPath(), $"RoadProtoAgentSubgrade_{Guid.NewGuid():N}.response");
        var resultPath = Path.Combine(Path.GetTempPath(), $"RoadProtoAgentSubgrade_{Guid.NewGuid():N}.result");
        var response = CreateSubgradeResponse(run, arguments, resultPath);
        SubgradeTemplateDialogFile.WriteResponse(responsePath, response);

        var commandPath = responsePath.Replace('\\', '/');
        document.SendStringToExecute($"RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE \"{commandPath}\"\n", true, false, true);

        return await WaitForNativeResultAsync(resultPath, cancellationToken).ConfigureAwait(true);
    }

    private static bool IsSupportedSubgradeTool(string toolName)
    {
        return string.Equals(toolName, SubgradeCreateToolName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, SubgradeModifyToolName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, SubgradeDeleteToolName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, SubgradeQueryToolName, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSubgradeToolRequest(
        string requestPath,
        AgentRunDto run,
        string toolName,
        SubgradeTemplateCreateArgumentsDto arguments,
        string resultPath)
    {
        var operation = toolName switch
        {
            SubgradeModifyToolName => "modify",
            SubgradeDeleteToolName => "delete",
            SubgradeQueryToolName => "query",
            _ => "create",
        };
        File.WriteAllLines(requestPath, new[]
        {
            Write("operation", operation),
            Write("traceId", run.TraceId),
            Write("taskId", run.TaskId),
            Write("agentId", run.Plan?.AgentId ?? string.Empty),
            Write("skillId", run.Plan?.SkillId ?? string.Empty),
            Write("intentId", run.Plan?.IntentId ?? string.Empty),
            Write("toolName", toolName),
            Write("targetMode", string.Empty),
            Write("targetHandle", arguments.TargetHandle ?? string.Empty),
            Write("targetName", arguments.TargetName ?? string.Empty),
            Write("templateName", arguments.TemplateName ?? string.Empty),
            Write("roadGrade", arguments.RoadGrade ?? string.Empty),
            Write("laneWidth", Format(arguments.LaneWidth)),
            Write("laneWidthDelta", Format(arguments.LaneWidthDelta)),
            Write("hardShoulderWidth", Format(arguments.HardShoulderWidth)),
            Write("earthShoulderWidth", Format(arguments.EarthShoulderWidth)),
            Write("medianWidth", Format(arguments.MedianWidth)),
            Write("slopeRatio", Format(arguments.SlopeRatio)),
            Write("unit", arguments.Unit ?? string.Empty),
            Write("resultPath", resultPath),
        }, Encoding.UTF8);
    }

    private static SubgradeTemplateDialogResponse CreateSubgradeResponse(
        AgentRunDto run,
        SubgradeTemplateCreateArgumentsDto arguments,
        string resultPath)
    {
        var response = new SubgradeTemplateDialogResponse
        {
            Accepted = true,
            AgentTraceId = run.TraceId,
            AgentTaskId = run.TaskId,
            AgentResultPath = resultPath,
            TemplateName = arguments.TemplateName ?? string.Empty,
            DisplayScale = arguments.DisplayScale ?? 10.0,
            RoadGrade = ParseRoadGrade(arguments.RoadGrade),
            InsertionX = 0.0,
            InsertionY = 0.0,
            InsertionZ = 0.0,
        };

        foreach (var component in arguments.Components)
        {
            response.Components.Add(MapComponent(component));
        }

        return response;
    }

    private static SubgradeComponentDto MapComponent(SubgradeTemplateComponentArgumentDto component)
        => new()
        {
            Side = ParseEnum(component.Side, SubgradeSide.Right),
            Type = ParseEnum(component.Type, SubgradeComponentType.TravelLane),
            Width = component.Width,
            Height = component.Height,
            FixedSlope = component.FixedSlope,
            SlopeMode = ParseEnum(component.SlopeMode, SubgradeSlopeMode.Fixed),
            ColorR = component.ColorR,
            ColorG = component.ColorG,
            ColorB = component.ColorB,
            WideningTable = MapStationRows(component.WideningTable),
            VariableSlopeTable = MapStationRows(component.VariableSlopeTable),
            HasInnerCurb = component.HasInnerCurb,
            InnerCurbWidth = component.InnerCurbWidth,
            InnerCurbHeight = component.InnerCurbHeight,
            InnerCurbEmbedDepth = component.InnerCurbEmbedDepth,
            HasOuterCurb = component.HasOuterCurb,
            OuterCurbWidth = component.OuterCurbWidth,
            OuterCurbHeight = component.OuterCurbHeight,
            OuterCurbEmbedDepth = component.OuterCurbEmbedDepth,
            PavementLayerLinked = component.PavementLayerLinked,
            PavementLayerHandle = component.PavementLayerHandle ?? string.Empty,
            PavementLayerName = component.PavementLayerName ?? string.Empty,
            PavementLayerThickness = component.PavementLayerThickness,
        };

    private static List<SubgradeStationValueDto> MapStationRows(List<SubgradeTemplateStationValueArgumentDto>? rows)
    {
        var result = new List<SubgradeStationValueDto>();
        if (rows == null)
        {
            return result;
        }

        foreach (var row in rows)
        {
            result.Add(new SubgradeStationValueDto { Station = row.Station, Value = row.Value });
        }

        return result;
    }

    private static async Task<AgentToolResultDto> WaitForNativeResultAsync(
        string resultPath,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(resultPath))
            {
                return ReadNativeResult(resultPath);
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(true);
        }

        return Failure("已下发路基模板创建命令，但 30 秒内没有收到 C++ 工具结果。");
    }

    private static AgentToolResultDto ReadNativeResult(string resultPath)
    {
        var values = ReadValues(resultPath);
        TryDelete(resultPath);
        return new AgentToolResultDto
        {
            Succeeded = GetBool(values, "succeeded"),
            EntityId = Get(values, "entityId"),
            TemplateName = Get(values, "templateName"),
            Message = Get(values, "message", "路基模板工具已返回结果。"),
        };
    }

    private static SubgradeTemplateCreateArgumentsDto ParseSubgradeArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SubgradeTemplateCreateArgumentsDto();
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (SubgradeTemplateCreateArgumentsDto)new DataContractJsonSerializer(typeof(SubgradeTemplateCreateArgumentsDto)).ReadObject(stream)!;
    }

    private static string? ValidateCreateArguments(SubgradeTemplateCreateArgumentsDto arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.TemplateName))
        {
            return "缺少模板名称。默认值应由 Agent 规则文件提供，RoadProto 本地不补默认模板名称。";
        }

        if (string.IsNullOrWhiteSpace(arguments.RoadGrade))
        {
            return "缺少道路等级。默认值应由 Agent 规则文件提供，RoadProto 本地只负责执行和校验。";
        }

        if (!IsKnownRoadGrade(arguments.RoadGrade))
        {
            return $"不支持的道路等级编码: {arguments.RoadGrade}。RoadProto 本地只负责执行和校验，不猜测道路等级。";
        }

        if (!IsPositive(arguments.DisplayScale ?? 10.0))
        {
            return "显示比例必须大于 0。默认值应由 Agent 规则文件提供，RoadProto 本地只负责执行和校验。";
        }

        if (arguments.Components == null || arguments.Components.Count == 0)
        {
            return "缺少路基模板部件列表。默认部件应由 Agent 规则文件提供，RoadProto 本地不补默认部件。";
        }

        if (!string.Equals(arguments.Unit, "m", StringComparison.OrdinalIgnoreCase))
        {
            return "当前 RoadProto 本地工具仅执行和校验 m 单位参数；单位默认值应由 Agent 规则文件提供。";
        }

        for (var i = 0; i < arguments.Components.Count; i++)
        {
            var component = arguments.Components[i];
            var validation = ValidateComponent(component, i);
            if (validation != null)
            {
                return validation;
            }
        }

        return null;
    }

    private static string? ValidateComponent(SubgradeTemplateComponentArgumentDto component, int index)
    {
        var label = $"第 {index + 1} 个路基部件";
        if (!IsKnownEnum<SubgradeSide>(component.Side))
        {
            return $"{label}侧别无效: {component.Side}。";
        }

        if (!IsKnownEnum<SubgradeComponentType>(component.Type))
        {
            return $"{label}类型无效: {component.Type}。";
        }

        if (!IsPositive(component.Width))
        {
            return $"{label}宽度必须大于 0。";
        }

        if (!IsFinite(component.Height) || !IsFinite(component.FixedSlope))
        {
            return $"{label}高度或固定坡度不是有效数字。";
        }

        if (!IsKnownEnum<SubgradeSlopeMode>(component.SlopeMode))
        {
            return $"{label}坡度方式无效: {component.SlopeMode}。";
        }

        if (!IsColor(component.ColorR) || !IsColor(component.ColorG) || !IsColor(component.ColorB))
        {
            return $"{label}颜色 RGB 必须在 0 到 255 之间。";
        }

        if (!ValidateCurb(component.HasInnerCurb, component.InnerCurbWidth, component.InnerCurbHeight, component.InnerCurbEmbedDepth)
            || !ValidateCurb(component.HasOuterCurb, component.OuterCurbWidth, component.OuterCurbHeight, component.OuterCurbEmbedDepth))
        {
            return $"{label}路缘石参数无效。启用时宽度、高度和埋深不能为负。";
        }

        if (!ValidateRows(component.WideningTable) || !ValidateRows(component.VariableSlopeTable))
        {
            return $"{label}变宽表或坡度变化表包含无效数字。";
        }

        if (!IsFinite(component.PavementLayerThickness) || component.PavementLayerThickness < 0.0)
        {
            return $"{label}结构层厚度不能为负。";
        }

        return null;
    }

    private static bool ValidateRows(List<SubgradeTemplateStationValueArgumentDto>? rows)
        => rows == null || rows.TrueForAll(row => IsFinite(row.Station) && IsFinite(row.Value));

    private static bool ValidateCurb(bool enabled, double width, double height, double embedDepth)
        => !enabled || (IsFinite(width) && IsFinite(height) && IsFinite(embedDepth) && width >= 0.0 && height >= 0.0 && embedDepth >= 0.0);

    private static bool IsColor(int value)
        => value is >= 0 and <= 255;

    private static bool IsPositive(double? value)
    {
        return value.HasValue
            && !double.IsNaN(value.Value)
            && !double.IsInfinity(value.Value)
            && value.Value > 0.0;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static SubgradeRoadGrade ParseRoadGrade(string? roadGrade)
    {
        return roadGrade switch
        {
            "Expressway" => SubgradeRoadGrade.Expressway,
            "FirstClass" => SubgradeRoadGrade.FirstClass,
            "SecondClass" => SubgradeRoadGrade.SecondClass,
            "ThirdClass" => SubgradeRoadGrade.ThirdClass,
            "FourthClass" => SubgradeRoadGrade.FourthClass,
            "UrbanExpressway" => SubgradeRoadGrade.UrbanExpressway,
            "UrbanArterial" => SubgradeRoadGrade.UrbanArterial,
            "UrbanSubArterial" => SubgradeRoadGrade.UrbanSubArterial,
            "UrbanBranch" => SubgradeRoadGrade.UrbanBranch,
            _ => SubgradeRoadGrade.Expressway,
        };
    }

    private static bool IsKnownRoadGrade(string? roadGrade)
    {
        return roadGrade is "Expressway"
            or "FirstClass"
            or "SecondClass"
            or "ThirdClass"
            or "FourthClass"
            or "UrbanExpressway"
            or "UrbanArterial"
            or "UrbanSubArterial"
            or "UrbanBranch";
    }

    private static bool IsKnownEnum<T>(string? value)
        where T : struct
        => !string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<T>(value, true, out _)
            && Enum.IsDefined(typeof(T), Enum.Parse(typeof(T), value, true));

    private static T ParseEnum<T>(string? value, T fallback)
        where T : struct
        => IsKnownEnum<T>(value) ? (T)Enum.Parse(typeof(T), value!, true) : fallback;

    private static string Write(string key, string value)
        => $"{key}={Escape(value)}";

    private static string Format(double? value)
        => value.HasValue ? value.Value.ToString("0.########", CultureInfo.InvariantCulture) : string.Empty;

    private static string Escape(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value)
        {
            if (ch == '\n')
            {
                builder.Append("%0A");
            }
            else if (ch == '\r')
            {
                builder.Append("%0D");
            }
            else if (ch == '%' || ch == '=')
            {
                builder.Append('%');
                builder.Append(((int)ch).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> ReadValues(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
        {
            var separator = rawLine.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            values[rawLine.Substring(0, separator)] = Unescape(rawLine.Substring(separator + 1));
        }

        return values;
    }

    private static string Get(Dictionary<string, string> values, string key, string fallback = "")
        => values.TryGetValue(key, out var value) ? value : fallback;

    private static bool GetBool(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value)
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    private static string Unescape(string value)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '%' && i + 2 < value.Length
                && int.TryParse(value.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                builder.Append((char)code);
                i += 2;
            }
            else
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }

    private static AgentToolResultDto Failure(string message)
        => new() { Succeeded = false, Message = message };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
