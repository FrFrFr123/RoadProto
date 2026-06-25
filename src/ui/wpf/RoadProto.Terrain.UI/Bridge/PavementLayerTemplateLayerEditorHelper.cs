using System;
using System.Collections.Generic;
using System.Linq;

namespace RoadProto.Terrain.UI.Bridge;

public static class PavementLayerTemplateLayerEditorHelper
{
    public static PavementLayerTemplateLayerDto CreateDefaultLayer(int index)
    {
        var values = Enum.GetValues(typeof(PavementLayerType)).Cast<PavementLayerType>().ToList();
        var type = values[Math.Max(0, Math.Min(index, values.Count - 1))];
        var color = PavementLayerTemplateLabels.DefaultColorForLayerIndex(index);
        return new PavementLayerTemplateLayerDto
        {
            Type = type,
            Name = PavementLayerTemplateLabels.LayerTypeLabel(type),
            UniformThickness = true,
            Thickness = 0.04,
            InnerThickness = 0.04,
            OuterThickness = 0.04,
            ColorR = color.R,
            ColorG = color.G,
            ColorB = color.B,
            HatchPattern = "SOLID",
            HatchAngle = 0.0,
            HatchScale = 1.0,
        };
    }

    public static PavementLayerTemplateLayerDto NormalizeLayer(PavementLayerTemplateLayerDto layer, int index)
    {
        var copy = layer.Clone();
        if (string.IsNullOrWhiteSpace(copy.Name))
        {
            copy.Name = PavementLayerTemplateLabels.LayerTypeLabel(copy.Type);
        }
        if (copy.ColorR < 0 || copy.ColorG < 0 || copy.ColorB < 0)
        {
            var color = PavementLayerTemplateLabels.DefaultColorForLayerIndex(index);
            copy.ColorR = color.R;
            copy.ColorG = color.G;
            copy.ColorB = color.B;
        }
        copy.HatchPattern = PavementLayerTemplateLabels.NormalizeHatchPattern(copy.HatchPattern);
        copy.HatchAngle = PavementLayerTemplateLabels.NormalizeHatchAngle(copy.HatchAngle);
        copy.HatchScale = PavementLayerTemplateLabels.NormalizeHatchScale(copy.HatchScale);
        if (copy.Thickness <= 0)
        {
            copy.Thickness = 0.04;
        }
        if (copy.InnerThickness <= 0)
        {
            copy.InnerThickness = copy.Thickness;
        }
        if (copy.OuterThickness <= 0)
        {
            copy.OuterThickness = copy.Thickness;
        }
        return copy;
    }

    public static PavementLayerTemplateDto NormalizeTemplateForComponent(
        PavementLayerTemplateDto source,
        string componentName,
        double componentWidth,
        double displayScale)
    {
        var template = PavementLayerTemplateLabels.CloneTemplate(source);
        if (string.IsNullOrWhiteSpace(template.TemplateName))
        {
            template.TemplateName = $"{componentName}结构层";
        }
        if (template.DisplayScale <= 0)
        {
            template.DisplayScale = displayScale <= 0 ? 10.0 : displayScale;
        }
        if (template.PreviewWidth <= 0)
        {
            template.PreviewWidth = Math.Max(0.1, componentWidth);
        }
        template.Layers = template.Layers
            .Select((layer, index) => NormalizeLayer(layer, index))
            .ToList();
        return template;
    }

    public static void ApplyDefaultPresetIfNeeded(FullRoadPavementComponentDto component, double displayScale)
    {
        if (component.Pavement.Layers.Count > 0)
        {
            component.Pavement = NormalizeTemplateForComponent(component.Pavement, component.DisplayName, component.Width, displayScale);
            return;
        }

        if (component.Type != SubgradeComponentType.TravelLane && component.Type != SubgradeComponentType.HardShoulder)
        {
            component.Pavement = NormalizeTemplateForComponent(component.Pavement, component.DisplayName, component.Width, displayScale);
            component.Pavement.Layers.Clear();
            return;
        }

        var presetType = component.Type == SubgradeComponentType.TravelLane
            ? PavementLayerTemplateRoadSegmentType.MainlineLane
            : PavementLayerTemplateRoadSegmentType.MainlineShoulder;
        var preset = PavementLayerTemplatePresetFactory.Create(PavementSurfaceType.Asphalt, presetType);
        preset.TemplateName = $"{component.DisplayName}结构层";
        preset.PreviewWidth = Math.Max(0.1, component.Width);
        preset.DisplayScale = displayScale <= 0 ? 10.0 : displayScale;
        component.Pavement = NormalizeTemplateForComponent(preset, component.DisplayName, component.Width, displayScale);
    }

    public static List<PavementLayerTemplateLayerDto> CloneNormalizedLayers(IEnumerable<PavementLayerTemplateLayerDto> layers)
        => layers.Select((layer, index) => NormalizeLayer(layer, index)).ToList();
}
