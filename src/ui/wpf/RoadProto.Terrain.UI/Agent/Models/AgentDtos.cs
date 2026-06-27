using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RoadProto.Terrain.UI.Agent.Models;

[DataContract]
public sealed class AgentHealthDto
{
    [DataMember(Name = "status")]
    public string Status { get; set; } = string.Empty;

    [DataMember(Name = "processId")]
    public int ProcessId { get; set; }

    [DataMember(Name = "settingsPath")]
    public string SettingsPath { get; set; } = string.Empty;

    [DataMember(Name = "backendLogDirectory")]
    public string BackendLogDirectory { get; set; } = string.Empty;

    [DataMember(Name = "roadProtoLogDirectory")]
    public string RoadProtoLogDirectory { get; set; } = string.Empty;
}

[DataContract]
public sealed class StartAgentRunRequestDto
{
    [DataMember(Name = "sessionId")]
    public string? SessionId { get; set; }

    [DataMember(Name = "message")]
    public string Message { get; set; } = string.Empty;

    [DataMember(Name = "currentTemplateHandle", EmitDefaultValue = false)]
    public string? CurrentTemplateHandle { get; set; }

    [DataMember(Name = "currentTemplateName", EmitDefaultValue = false)]
    public string? CurrentTemplateName { get; set; }
}

[DataContract]
public sealed class AgentRunUserInputRequestDto
{
    [DataMember(Name = "message")]
    public string Message { get; set; } = string.Empty;

    [DataMember(Name = "currentTemplateHandle", EmitDefaultValue = false)]
    public string? CurrentTemplateHandle { get; set; }

    [DataMember(Name = "currentTemplateName", EmitDefaultValue = false)]
    public string? CurrentTemplateName { get; set; }
}

[DataContract]
public sealed class AgentRunDto
{
    [DataMember(Name = "traceId")]
    public string TraceId { get; set; } = string.Empty;

    [DataMember(Name = "sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [DataMember(Name = "taskId")]
    public string TaskId { get; set; } = string.Empty;

    [DataMember(Name = "state")]
    public string State { get; set; } = string.Empty;

    [DataMember(Name = "followUpMessage")]
    public string? FollowUpMessage { get; set; }

    [DataMember(Name = "entryRoute")]
    public AgentEntryRouteDto? EntryRoute { get; set; }

    [DataMember(Name = "plan")]
    public AgentPlanDto? Plan { get; set; }

    [DataMember(Name = "dispatchedToolCall")]
    public AgentToolCallDto? DispatchedToolCall { get; set; }

    [DataMember(Name = "toolResult")]
    public AgentToolResultDto? ToolResult { get; set; }

    [DataMember(Name = "events")]
    public List<AgentRunEventDto> Events { get; set; } = new();
}

[DataContract]
public sealed class AgentRunEventDto
{
    [DataMember(Name = "timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [DataMember(Name = "stage")]
    public string Stage { get; set; } = string.Empty;

    [DataMember(Name = "message")]
    public string Message { get; set; } = string.Empty;
}

[DataContract]
public sealed class AgentEntryRouteDto
{
    [DataMember(Name = "routeType")]
    public string RouteType { get; set; } = string.Empty;

    [DataMember(Name = "confidence")]
    public double Confidence { get; set; }

    [DataMember(Name = "reason")]
    public string Reason { get; set; } = string.Empty;

    [DataMember(Name = "candidateAgentId")]
    public string? CandidateAgentId { get; set; }

    [DataMember(Name = "candidateSkillId")]
    public string? CandidateSkillId { get; set; }

    [DataMember(Name = "candidateIntentIds")]
    public List<string> CandidateIntentIds { get; set; } = new();

    [DataMember(Name = "followUpMessage")]
    public string? FollowUpMessage { get; set; }

    [DataMember(Name = "shouldCallModelForIntent")]
    public bool ShouldCallModelForIntent { get; set; }

    [DataMember(Name = "shouldCallTool")]
    public bool ShouldCallTool { get; set; }
}

[DataContract]
public sealed class AgentPlanDto
{
    [DataMember(Name = "agentId")]
    public string AgentId { get; set; } = string.Empty;

    [DataMember(Name = "skillId")]
    public string SkillId { get; set; } = string.Empty;

    [DataMember(Name = "intentId")]
    public string IntentId { get; set; } = string.Empty;

    [DataMember(Name = "toolName")]
    public string ToolName { get; set; } = string.Empty;

    [DataMember(Name = "summary")]
    public string Summary { get; set; } = string.Empty;

    [DataMember(Name = "riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [DataMember(Name = "requiresApproval")]
    public bool RequiresApproval { get; set; }

    [DataMember(Name = "confirmationItems")]
    public List<string> ConfirmationItems { get; set; } = new();

    [DataMember(Name = "followUpMessage")]
    public string? FollowUpMessage { get; set; }

    [DataMember(Name = "resultMessage")]
    public string? ResultMessage { get; set; }
}

[DataContract]
public sealed class AgentToolCallDto
{
    [DataMember(Name = "toolName")]
    public string ToolName { get; set; } = string.Empty;

    [DataMember(Name = "argumentsJson")]
    public string ArgumentsJson { get; set; } = string.Empty;
}

[DataContract]
public sealed class ModelProviderUpdateDto
{
    [DataMember(Name = "baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [DataMember(Name = "model")]
    public string Model { get; set; } = string.Empty;

    [DataMember(Name = "apiKey")]
    public string? ApiKey { get; set; }

    [DataMember(Name = "isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

[DataContract]
public sealed class ModelProviderViewDto
{
    [DataMember(Name = "provider")]
    public string Provider { get; set; } = string.Empty;

    [DataMember(Name = "baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [DataMember(Name = "model")]
    public string Model { get; set; } = string.Empty;

    [DataMember(Name = "hasApiKey")]
    public bool HasApiKey { get; set; }

    [DataMember(Name = "isEnabled")]
    public bool IsEnabled { get; set; }

    [DataMember(Name = "updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

[DataContract]
public sealed class AgentToolResultDto
{
    [DataMember(Name = "succeeded")]
    public bool Succeeded { get; set; }

    [DataMember(Name = "entityId")]
    public string? EntityId { get; set; }

    [DataMember(Name = "templateName")]
    public string? TemplateName { get; set; }

    [DataMember(Name = "message")]
    public string Message { get; set; } = string.Empty;
}

[DataContract]
public sealed class SubgradeTemplateCreateArgumentsDto
{
    [DataMember(Name = "Operation")]
    public string Operation { get; set; } = "create";

    [DataMember(Name = "TemplateName")]
    public string? TemplateName { get; set; }

    [DataMember(Name = "RoadGrade")]
    public string? RoadGrade { get; set; }

    [DataMember(Name = "TargetHandle")]
    public string? TargetHandle { get; set; }

    [DataMember(Name = "TargetName")]
    public string? TargetName { get; set; }

    [DataMember(Name = "TargetMode")]
    public string? TargetMode { get; set; }

    [DataMember(Name = "TargetRef")]
    public string? TargetRef { get; set; }

    [DataMember(Name = "SideScope")]
    public string? SideScope { get; set; }

    [DataMember(Name = "LaneWidth")]
    public double? LaneWidth { get; set; }

    [DataMember(Name = "LaneWidthDelta")]
    public double? LaneWidthDelta { get; set; }

    [DataMember(Name = "HardShoulderWidth")]
    public double? HardShoulderWidth { get; set; }

    [DataMember(Name = "EarthShoulderWidth")]
    public double? EarthShoulderWidth { get; set; }

    [DataMember(Name = "SlopeRatio")]
    public double? SlopeRatio { get; set; }

    [DataMember(Name = "DisplayScale")]
    public double? DisplayScale { get; set; }

    [DataMember(Name = "MedianWidth")]
    public double? MedianWidth { get; set; }

    [DataMember(Name = "Unit")]
    public string? Unit { get; set; }

    [DataMember(Name = "Components")]
    public List<SubgradeTemplateComponentArgumentDto> Components { get; set; } = new();

    [DataMember(Name = "ComponentOperations")]
    public List<SubgradeTemplateComponentOperationArgumentDto> ComponentOperations { get; set; } = new();
}

[DataContract]
public sealed class SubgradeTemplateComponentOperationArgumentDto
{
    [DataMember(Name = "Operation")]
    public string Operation { get; set; } = string.Empty;

    [DataMember(Name = "SideScope")]
    public string? SideScope { get; set; }

    [DataMember(Name = "ComponentType")]
    public string? ComponentType { get; set; }

    [DataMember(Name = "Occurrence")]
    public string? Occurrence { get; set; }

    [DataMember(Name = "PositionMode")]
    public string? PositionMode { get; set; }

    [DataMember(Name = "AnchorType")]
    public string? AnchorType { get; set; }

    [DataMember(Name = "Patch")]
    public SubgradeTemplateComponentPatchArgumentDto Patch { get; set; } = new();
}

[DataContract]
public sealed class SubgradeTemplateComponentPatchArgumentDto
{
    [DataMember(Name = "Type")]
    public string? Type { get; set; }

    [DataMember(Name = "Width")]
    public double? Width { get; set; }

    [DataMember(Name = "WidthDelta")]
    public double? WidthDelta { get; set; }

    [DataMember(Name = "Height")]
    public double? Height { get; set; }

    [DataMember(Name = "FixedSlope")]
    public double? FixedSlope { get; set; }

    [DataMember(Name = "SlopeMode")]
    public string? SlopeMode { get; set; }

    [DataMember(Name = "WideningTable")]
    public List<SubgradeTemplateStationValueArgumentDto> WideningTable { get; set; } = new();

    [DataMember(Name = "VariableSlopeTable")]
    public List<SubgradeTemplateStationValueArgumentDto> VariableSlopeTable { get; set; } = new();

    [DataMember(Name = "ColorR")]
    public int? ColorR { get; set; }

    [DataMember(Name = "ColorG")]
    public int? ColorG { get; set; }

    [DataMember(Name = "ColorB")]
    public int? ColorB { get; set; }

    [DataMember(Name = "HasInnerCurb")]
    public bool? HasInnerCurb { get; set; }

    [DataMember(Name = "InnerCurbWidth")]
    public double? InnerCurbWidth { get; set; }

    [DataMember(Name = "InnerCurbHeight")]
    public double? InnerCurbHeight { get; set; }

    [DataMember(Name = "InnerCurbEmbedDepth")]
    public double? InnerCurbEmbedDepth { get; set; }

    [DataMember(Name = "HasOuterCurb")]
    public bool? HasOuterCurb { get; set; }

    [DataMember(Name = "OuterCurbWidth")]
    public double? OuterCurbWidth { get; set; }

    [DataMember(Name = "OuterCurbHeight")]
    public double? OuterCurbHeight { get; set; }

    [DataMember(Name = "OuterCurbEmbedDepth")]
    public double? OuterCurbEmbedDepth { get; set; }

    [DataMember(Name = "PavementLayerLinked")]
    public bool? PavementLayerLinked { get; set; }

    [DataMember(Name = "PavementLayerHandle")]
    public string? PavementLayerHandle { get; set; }

    [DataMember(Name = "PavementLayerName")]
    public string? PavementLayerName { get; set; }

    [DataMember(Name = "PavementLayerThickness")]
    public double? PavementLayerThickness { get; set; }
}

[DataContract]
public sealed class SubgradeTemplateComponentArgumentDto
{
    [DataMember(Name = "Side")]
    public string Side { get; set; } = string.Empty;

    [DataMember(Name = "Type")]
    public string Type { get; set; } = string.Empty;

    [DataMember(Name = "Width")]
    public double Width { get; set; }

    [DataMember(Name = "Height")]
    public double Height { get; set; }

    [DataMember(Name = "FixedSlope")]
    public double FixedSlope { get; set; }

    [DataMember(Name = "SlopeMode")]
    public string SlopeMode { get; set; } = "Fixed";

    [DataMember(Name = "ColorR")]
    public int ColorR { get; set; }

    [DataMember(Name = "ColorG")]
    public int ColorG { get; set; }

    [DataMember(Name = "ColorB")]
    public int ColorB { get; set; }

    [DataMember(Name = "WideningTable")]
    public List<SubgradeTemplateStationValueArgumentDto> WideningTable { get; set; } = new();

    [DataMember(Name = "VariableSlopeTable")]
    public List<SubgradeTemplateStationValueArgumentDto> VariableSlopeTable { get; set; } = new();

    [DataMember(Name = "HasInnerCurb")]
    public bool HasInnerCurb { get; set; }

    [DataMember(Name = "InnerCurbWidth")]
    public double InnerCurbWidth { get; set; }

    [DataMember(Name = "InnerCurbHeight")]
    public double InnerCurbHeight { get; set; }

    [DataMember(Name = "InnerCurbEmbedDepth")]
    public double InnerCurbEmbedDepth { get; set; }

    [DataMember(Name = "HasOuterCurb")]
    public bool HasOuterCurb { get; set; }

    [DataMember(Name = "OuterCurbWidth")]
    public double OuterCurbWidth { get; set; }

    [DataMember(Name = "OuterCurbHeight")]
    public double OuterCurbHeight { get; set; }

    [DataMember(Name = "OuterCurbEmbedDepth")]
    public double OuterCurbEmbedDepth { get; set; }

    [DataMember(Name = "PavementLayerLinked")]
    public bool PavementLayerLinked { get; set; }

    [DataMember(Name = "PavementLayerHandle")]
    public string? PavementLayerHandle { get; set; }

    [DataMember(Name = "PavementLayerName")]
    public string? PavementLayerName { get; set; }

    [DataMember(Name = "PavementLayerThickness")]
    public double PavementLayerThickness { get; set; }
}

[DataContract]
public sealed class SubgradeTemplateStationValueArgumentDto
{
    [DataMember(Name = "Station")]
    public double Station { get; set; }

    [DataMember(Name = "Value")]
    public double Value { get; set; }
}
