using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RoadProto.Terrain.UI.Bridge;

public static class FullRoadPavementTemplateDialogFile
{
    public static FullRoadPavementTemplateDialogRequest ReadRequest(string path)
    {
        var values = ReadValues(path);
        var request = new FullRoadPavementTemplateDialogRequest
        {
            Action = ParseAction(Get(values, "action")),
            Handle = Get(values, "handle"),
            ResponsePath = Get(values, "responsePath"),
            InsertionX = GetDouble(values, "insertionX"),
            InsertionY = GetDouble(values, "insertionY"),
            InsertionZ = GetDouble(values, "insertionZ"),
            TemplateName = Get(values, "templateName", "整幅路路面结构层模板"),
            DisplayScale = GetDouble(values, "displayScale", 10.0),
            ReferenceSubgradeTemplateHandle = Get(values, "referenceSubgradeTemplateHandle"),
            ReferenceSubgradeTemplateName = Get(values, "referenceSubgradeTemplateName"),
            ReferenceRoadGrade = ParseEnum(Get(values, "referenceRoadGrade", "Expressway"), SubgradeRoadGrade.Expressway),
            CurrentComponentIndex = GetInt(values, "currentComponentIndex", -1),
            ApplyDefaultPresets = GetBool(values, "applyDefaultPresets"),
        };

        var componentCount = Math.Max(0, GetInt(values, "componentCount"));
        for (var i = 0; i < componentCount; i++)
        {
            request.Components.Add(ReadComponent(values, $"component.{i}"));
        }

        return request;
    }

    public static void WriteResponse(string path, FullRoadPavementTemplateDialogResponse response)
    {
        var lines = new List<string>
        {
            Write("accepted", response.Accepted),
            Write("action", ActionCode(response.Action)),
            Write("handle", response.Handle),
        };

        if (response.Accepted || response.Action != FullRoadPavementTemplateDialogAction.None)
        {
            lines.Add(Write("insertionX", response.InsertionX));
            lines.Add(Write("insertionY", response.InsertionY));
            lines.Add(Write("insertionZ", response.InsertionZ));
            lines.Add(Write("templateName", response.TemplateName));
            lines.Add(Write("displayScale", response.DisplayScale));
            lines.Add(Write("referenceSubgradeTemplateHandle", response.ReferenceSubgradeTemplateHandle));
            lines.Add(Write("referenceSubgradeTemplateName", response.ReferenceSubgradeTemplateName));
            lines.Add(Write("referenceRoadGrade", response.ReferenceRoadGrade.ToString()));
            lines.Add(Write("currentComponentIndex", response.CurrentComponentIndex));
            lines.Add(Write("componentCount", response.Components.Count));
            for (var i = 0; i < response.Components.Count; i++)
            {
                WriteComponent(lines, $"component.{i}", response.Components[i]);
            }
        }

        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static FullRoadPavementComponentDto ReadComponent(Dictionary<string, string> values, string prefix)
    {
        var component = new FullRoadPavementComponentDto
        {
            Side = ParseEnum(Get(values, $"{prefix}.side", "Right"), SubgradeSide.Right),
            Type = ParseEnum(Get(values, $"{prefix}.type", "TravelLane"), SubgradeComponentType.TravelLane),
            SameSideTypeOrdinal = GetInt(values, $"{prefix}.sameSideTypeOrdinal"),
            Width = GetDouble(values, $"{prefix}.width"),
            Height = GetDouble(values, $"{prefix}.height"),
            FixedSlope = GetDouble(values, $"{prefix}.fixedSlope"),
            SlopeMode = ParseEnum(Get(values, $"{prefix}.slopeMode", "Fixed"), SubgradeSlopeMode.Fixed),
            ColorR = GetInt(values, $"{prefix}.colorR", 120),
            ColorG = GetInt(values, $"{prefix}.colorG", 120),
            ColorB = GetInt(values, $"{prefix}.colorB", 120),
            HasInnerCurb = GetBool(values, $"{prefix}.hasInnerCurb"),
            InnerCurbWidth = GetDouble(values, $"{prefix}.innerCurbWidth"),
            InnerCurbHeight = GetDouble(values, $"{prefix}.innerCurbHeight"),
            InnerCurbEmbedDepth = GetDouble(values, $"{prefix}.innerCurbEmbedDepth"),
            HasOuterCurb = GetBool(values, $"{prefix}.hasOuterCurb"),
            OuterCurbWidth = GetDouble(values, $"{prefix}.outerCurbWidth"),
            OuterCurbHeight = GetDouble(values, $"{prefix}.outerCurbHeight"),
            OuterCurbEmbedDepth = GetDouble(values, $"{prefix}.outerCurbEmbedDepth"),
        };

        ReadStationValues(values, $"{prefix}.widening", component.WideningTable);
        ReadStationValues(values, $"{prefix}.variableSlope", component.VariableSlopeTable);
        component.Pavement = ReadPavement(values, $"{prefix}.pavement", component.Width);
        return component;
    }

    private static PavementLayerTemplateDto ReadPavement(Dictionary<string, string> values, string prefix, double previewWidth)
    {
        var pavement = new PavementLayerTemplateDto
        {
            TemplateName = Get(values, $"{prefix}.templateName", "路面结构层模板"),
            DisplayScale = GetDouble(values, $"{prefix}.displayScale", 10.0),
            PreviewWidth = GetDouble(values, $"{prefix}.previewWidth", Math.Max(0.1, previewWidth)),
            DisplayMode = ParseEnum(Get(values, $"{prefix}.displayMode", "Color"), PavementLayerTemplateDisplayMode.Color),
            ShowAllGeneralParameters = GetBool(values, $"{prefix}.showAllGeneralParameters"),
            StructureCode = Get(values, $"{prefix}.structureCode"),
            SubgradeMoistureTypes = ParseEnumList<PavementSubgradeMoistureType>(Get(values, $"{prefix}.subgradeMoistureTypes")),
            PavementType = ParseEnum(Get(values, $"{prefix}.pavementType", "Asphalt"), PavementSurfaceType.Asphalt),
            SubgradeSoilGroups = ParseEnumList<PavementSubgradeSoilGroup>(Get(values, $"{prefix}.subgradeSoilGroups")),
            DesignDeflection = Get(values, $"{prefix}.designDeflection"),
            CumulativeAxleLoads = Get(values, $"{prefix}.cumulativeAxleLoads"),
        };

        var layerCount = Math.Max(0, GetInt(values, $"{prefix}.layerCount"));
        for (var i = 0; i < layerCount; i++)
        {
            pavement.Layers.Add(ReadLayer(values, $"{prefix}.layer.{i}", i));
        }
        return pavement;
    }

    private static PavementLayerTemplateLayerDto ReadLayer(Dictionary<string, string> values, string prefix, int index)
    {
        var type = ParseEnum(Get(values, $"{prefix}.type", "UpperSurface"), PavementLayerType.UpperSurface);
        var thickness = GetDouble(values, $"{prefix}.thickness", 0.04);
        var defaultColor = PavementLayerTemplateLabels.DefaultColorForLayerIndex(index);
        return new PavementLayerTemplateLayerDto
        {
            Type = type,
            Name = Get(values, $"{prefix}.name", PavementLayerTemplateLabels.LayerTypeLabel(type)),
            UniformThickness = GetBool(values, $"{prefix}.uniformThickness", true),
            Thickness = thickness,
            InnerThickness = GetDouble(values, $"{prefix}.innerThickness", thickness),
            OuterThickness = GetDouble(values, $"{prefix}.outerThickness", thickness),
            InnerWidening = GetDouble(values, $"{prefix}.innerWidening"),
            OuterWidening = GetDouble(values, $"{prefix}.outerWidening"),
            InnerSlope = GetDouble(values, $"{prefix}.innerSlope"),
            OuterSlope = GetDouble(values, $"{prefix}.outerSlope"),
            ColorR = GetInt(values, $"{prefix}.colorR", defaultColor.R),
            ColorG = GetInt(values, $"{prefix}.colorG", defaultColor.G),
            ColorB = GetInt(values, $"{prefix}.colorB", defaultColor.B),
            HatchPattern = PavementLayerTemplateLabels.NormalizeHatchPattern(Get(values, $"{prefix}.hatchPattern", "SOLID")),
            HatchAngle = PavementLayerTemplateLabels.NormalizeHatchAngle(GetDouble(values, $"{prefix}.hatchAngle")),
            HatchScale = PavementLayerTemplateLabels.NormalizeHatchScale(GetDouble(values, $"{prefix}.hatchScale", 1.0)),
        };
    }

    private static void WriteComponent(List<string> lines, string prefix, FullRoadPavementComponentDto component)
    {
        lines.Add(Write($"{prefix}.side", component.Side.ToString()));
        lines.Add(Write($"{prefix}.type", component.Type.ToString()));
        lines.Add(Write($"{prefix}.sameSideTypeOrdinal", component.SameSideTypeOrdinal));
        lines.Add(Write($"{prefix}.width", component.Width));
        lines.Add(Write($"{prefix}.height", component.Height));
        lines.Add(Write($"{prefix}.fixedSlope", component.FixedSlope));
        lines.Add(Write($"{prefix}.slopeMode", component.SlopeMode.ToString()));
        lines.Add(Write($"{prefix}.colorR", ClampColor(component.ColorR)));
        lines.Add(Write($"{prefix}.colorG", ClampColor(component.ColorG)));
        lines.Add(Write($"{prefix}.colorB", ClampColor(component.ColorB)));
        WriteStationValues(lines, $"{prefix}.widening", component.WideningTable);
        WriteStationValues(lines, $"{prefix}.variableSlope", component.VariableSlopeTable);
        lines.Add(Write($"{prefix}.hasInnerCurb", component.HasInnerCurb));
        lines.Add(Write($"{prefix}.innerCurbWidth", component.InnerCurbWidth));
        lines.Add(Write($"{prefix}.innerCurbHeight", component.InnerCurbHeight));
        lines.Add(Write($"{prefix}.innerCurbEmbedDepth", component.InnerCurbEmbedDepth));
        lines.Add(Write($"{prefix}.hasOuterCurb", component.HasOuterCurb));
        lines.Add(Write($"{prefix}.outerCurbWidth", component.OuterCurbWidth));
        lines.Add(Write($"{prefix}.outerCurbHeight", component.OuterCurbHeight));
        lines.Add(Write($"{prefix}.outerCurbEmbedDepth", component.OuterCurbEmbedDepth));
        WritePavement(lines, $"{prefix}.pavement", component.Pavement);
    }

    private static void WritePavement(List<string> lines, string prefix, PavementLayerTemplateDto pavement)
    {
        lines.Add(Write($"{prefix}.templateName", pavement.TemplateName));
        lines.Add(Write($"{prefix}.displayScale", pavement.DisplayScale));
        lines.Add(Write($"{prefix}.previewWidth", pavement.PreviewWidth));
        lines.Add(Write($"{prefix}.displayMode", pavement.DisplayMode.ToString()));
        lines.Add(Write($"{prefix}.showAllGeneralParameters", pavement.ShowAllGeneralParameters));
        lines.Add(Write($"{prefix}.structureCode", pavement.StructureCode));
        lines.Add(Write($"{prefix}.subgradeMoistureTypes", JoinEnumList(pavement.SubgradeMoistureTypes)));
        lines.Add(Write($"{prefix}.pavementType", pavement.PavementType.ToString()));
        lines.Add(Write($"{prefix}.subgradeSoilGroups", JoinEnumList(pavement.SubgradeSoilGroups)));
        lines.Add(Write($"{prefix}.designDeflection", pavement.DesignDeflection));
        lines.Add(Write($"{prefix}.cumulativeAxleLoads", pavement.CumulativeAxleLoads));
        lines.Add(Write($"{prefix}.layerCount", pavement.Layers.Count));
        for (var i = 0; i < pavement.Layers.Count; i++)
        {
            WriteLayer(lines, $"{prefix}.layer.{i}", pavement.Layers[i]);
        }
    }

    private static void WriteLayer(List<string> lines, string prefix, PavementLayerTemplateLayerDto layer)
    {
        lines.Add(Write($"{prefix}.type", layer.Type.ToString()));
        lines.Add(Write($"{prefix}.name", layer.Name));
        lines.Add(Write($"{prefix}.uniformThickness", layer.UniformThickness));
        lines.Add(Write($"{prefix}.thickness", layer.Thickness));
        lines.Add(Write($"{prefix}.innerThickness", layer.InnerThickness));
        lines.Add(Write($"{prefix}.outerThickness", layer.OuterThickness));
        lines.Add(Write($"{prefix}.innerWidening", layer.InnerWidening));
        lines.Add(Write($"{prefix}.outerWidening", layer.OuterWidening));
        lines.Add(Write($"{prefix}.innerSlope", layer.InnerSlope));
        lines.Add(Write($"{prefix}.outerSlope", layer.OuterSlope));
        lines.Add(Write($"{prefix}.colorR", ClampColor(layer.ColorR)));
        lines.Add(Write($"{prefix}.colorG", ClampColor(layer.ColorG)));
        lines.Add(Write($"{prefix}.colorB", ClampColor(layer.ColorB)));
        lines.Add(Write($"{prefix}.hatchPattern", PavementLayerTemplateLabels.NormalizeHatchPattern(layer.HatchPattern)));
        lines.Add(Write($"{prefix}.hatchAngle", PavementLayerTemplateLabels.NormalizeHatchAngle(layer.HatchAngle)));
        lines.Add(Write($"{prefix}.hatchScale", PavementLayerTemplateLabels.NormalizeHatchScale(layer.HatchScale)));
    }

    private static void ReadStationValues(Dictionary<string, string> values, string prefix, List<SubgradeStationValueDto> rows)
    {
        var count = Math.Max(0, GetInt(values, $"{prefix}Count"));
        for (var i = 0; i < count; i++)
        {
            rows.Add(new()
            {
                Station = GetDouble(values, $"{prefix}.{i}.station"),
                Value = GetDouble(values, $"{prefix}.{i}.value"),
            });
        }
    }

    private static void WriteStationValues(List<string> lines, string prefix, List<SubgradeStationValueDto> rows)
    {
        lines.Add(Write($"{prefix}Count", rows.Count));
        for (var i = 0; i < rows.Count; i++)
        {
            lines.Add(Write($"{prefix}.{i}.station", rows[i].Station));
            lines.Add(Write($"{prefix}.{i}.value", rows[i].Value));
        }
    }

    private static FullRoadPavementTemplateDialogAction ParseAction(string action)
        => action.Equals("pickReferenceSubgradeTemplate", StringComparison.OrdinalIgnoreCase)
            ? FullRoadPavementTemplateDialogAction.PickReferenceSubgradeTemplate
            : FullRoadPavementTemplateDialogAction.None;

    private static string ActionCode(FullRoadPavementTemplateDialogAction action)
        => action == FullRoadPavementTemplateDialogAction.PickReferenceSubgradeTemplate
            ? "pickReferenceSubgradeTemplate"
            : "none";

    private static Dictionary<string, string> ReadValues(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line.Substring(0, separator).TrimStart('\uFEFF');
            values[key] = Unescape(line.Substring(separator + 1));
        }
        return values;
    }

    private static string Get(Dictionary<string, string> values, string key, string fallback = "")
        => values.TryGetValue(key, out var value) ? value : fallback;

    private static int GetInt(Dictionary<string, string> values, string key, int fallback = 0)
        => int.TryParse(Get(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static double GetDouble(Dictionary<string, string> values, string key, double fallback = 0.0)
        => double.TryParse(Get(values, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static bool GetBool(Dictionary<string, string> values, string key, bool fallback = false)
    {
        var value = Get(values, key, fallback ? "1" : "0");
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static T ParseEnum<T>(string value, T fallback)
        where T : struct
        => Enum.TryParse<T>(value, true, out var result) ? result : fallback;

    private static List<T> ParseEnumList<T>(string value)
        where T : struct
    {
        var result = new List<T>();
        foreach (var raw in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<T>(raw, ignoreCase: true, out var parsed)
                && Enum.IsDefined(typeof(T), parsed)
                && !result.Contains(parsed))
            {
                result.Add(parsed);
            }
        }
        return result;
    }

    private static string JoinEnumList<T>(IEnumerable<T> values)
        where T : struct
        => string.Join(";", values.Select(value => value.ToString()));

    private static string Write(string key, string value)
        => $"{key}={Escape(value)}";

    private static string Write(string key, bool value)
        => $"{key}={(value ? 1 : 0)}";

    private static string Write(string key, int value)
        => $"{key}={value.ToString(CultureInfo.InvariantCulture)}";

    private static string Write(string key, double value)
        => $"{key}={value.ToString("R", CultureInfo.InvariantCulture)}";

    private static int ClampColor(int value)
        => Math.Max(0, Math.Min(255, value));

    private static string Escape(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value ?? string.Empty)
        {
            if (ch is '%' or '\r' or '\n')
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
}
