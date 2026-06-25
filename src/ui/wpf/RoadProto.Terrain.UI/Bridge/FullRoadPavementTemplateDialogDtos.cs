using System.Collections.Generic;
using System.Linq;

namespace RoadProto.Terrain.UI.Bridge;

public enum FullRoadPavementTemplateDialogAction
{
    None,
    PickReferenceSubgradeTemplate,
}

public sealed class FullRoadPavementComponentDto
{
    public SubgradeSide Side { get; set; } = SubgradeSide.Right;
    public SubgradeComponentType Type { get; set; } = SubgradeComponentType.TravelLane;
    public int SameSideTypeOrdinal { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double FixedSlope { get; set; }
    public SubgradeSlopeMode SlopeMode { get; set; } = SubgradeSlopeMode.Fixed;
    public int ColorR { get; set; } = 120;
    public int ColorG { get; set; } = 120;
    public int ColorB { get; set; } = 120;
    public List<SubgradeStationValueDto> WideningTable { get; set; } = new();
    public List<SubgradeStationValueDto> VariableSlopeTable { get; set; } = new();
    public bool HasInnerCurb { get; set; }
    public double InnerCurbWidth { get; set; }
    public double InnerCurbHeight { get; set; }
    public double InnerCurbEmbedDepth { get; set; }
    public bool HasOuterCurb { get; set; }
    public double OuterCurbWidth { get; set; }
    public double OuterCurbHeight { get; set; }
    public double OuterCurbEmbedDepth { get; set; }
    public PavementLayerTemplateDto Pavement { get; set; } = new();

    public string SideLabel => Side == SubgradeSide.Left ? "左侧" : "右侧";
    public string TypeLabel => SubgradeTemplateLabels.ComponentTypeLabel(Type);
    public string DisplayName => $"{SideLabel}{TypeLabel}";

    public FullRoadPavementComponentDto Clone()
        => new()
        {
            Side = Side,
            Type = Type,
            SameSideTypeOrdinal = SameSideTypeOrdinal,
            Width = Width,
            Height = Height,
            FixedSlope = FixedSlope,
            SlopeMode = SlopeMode,
            ColorR = ColorR,
            ColorG = ColorG,
            ColorB = ColorB,
            WideningTable = WideningTable.ConvertAll(row => row.Clone()),
            VariableSlopeTable = VariableSlopeTable.ConvertAll(row => row.Clone()),
            HasInnerCurb = HasInnerCurb,
            InnerCurbWidth = InnerCurbWidth,
            InnerCurbHeight = InnerCurbHeight,
            InnerCurbEmbedDepth = InnerCurbEmbedDepth,
            HasOuterCurb = HasOuterCurb,
            OuterCurbWidth = OuterCurbWidth,
            OuterCurbHeight = OuterCurbHeight,
            OuterCurbEmbedDepth = OuterCurbEmbedDepth,
            Pavement = PavementLayerTemplateLabels.CloneTemplate(Pavement),
        };
}

public sealed class FullRoadPavementTemplateDialogRequest
{
    public FullRoadPavementTemplateDialogAction Action { get; set; } = FullRoadPavementTemplateDialogAction.None;
    public string Handle { get; set; } = string.Empty;
    public string ResponsePath { get; set; } = string.Empty;
    public double InsertionX { get; set; }
    public double InsertionY { get; set; }
    public double InsertionZ { get; set; }
    public string TemplateName { get; set; } = "整幅路路面结构层模板";
    public double DisplayScale { get; set; } = 10.0;
    public string ReferenceSubgradeTemplateHandle { get; set; } = string.Empty;
    public string ReferenceSubgradeTemplateName { get; set; } = string.Empty;
    public SubgradeRoadGrade ReferenceRoadGrade { get; set; } = SubgradeRoadGrade.Expressway;
    public int CurrentComponentIndex { get; set; } = -1;
    public bool ApplyDefaultPresets { get; set; }
    public List<FullRoadPavementComponentDto> Components { get; set; } = new();
}

public sealed class FullRoadPavementTemplateDialogResponse
{
    public bool Accepted { get; set; }
    public FullRoadPavementTemplateDialogAction Action { get; set; } = FullRoadPavementTemplateDialogAction.None;
    public string Handle { get; set; } = string.Empty;
    public double InsertionX { get; set; }
    public double InsertionY { get; set; }
    public double InsertionZ { get; set; }
    public string TemplateName { get; set; } = "整幅路路面结构层模板";
    public double DisplayScale { get; set; } = 10.0;
    public string ReferenceSubgradeTemplateHandle { get; set; } = string.Empty;
    public string ReferenceSubgradeTemplateName { get; set; } = string.Empty;
    public SubgradeRoadGrade ReferenceRoadGrade { get; set; } = SubgradeRoadGrade.Expressway;
    public int CurrentComponentIndex { get; set; } = -1;
    public List<FullRoadPavementComponentDto> Components { get; set; } = new();

    public FullRoadPavementTemplateDialogRequest ToRequest(string responsePath)
        => new()
        {
            Action = Action,
            Handle = Handle,
            ResponsePath = responsePath,
            InsertionX = InsertionX,
            InsertionY = InsertionY,
            InsertionZ = InsertionZ,
            TemplateName = TemplateName,
            DisplayScale = DisplayScale,
            ReferenceSubgradeTemplateHandle = ReferenceSubgradeTemplateHandle,
            ReferenceSubgradeTemplateName = ReferenceSubgradeTemplateName,
            ReferenceRoadGrade = ReferenceRoadGrade,
            CurrentComponentIndex = CurrentComponentIndex,
            ApplyDefaultPresets = false,
            Components = Components.Select(component => component.Clone()).ToList(),
        };
}
