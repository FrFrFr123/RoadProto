using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RoadProto.Terrain.UI.Bridge;

namespace RoadProto.Terrain.UI;

public partial class FullRoadPavementTemplateWindow : Window
{
    private const string NoLayerStatusText = "当前部件未配置结构层";

    private readonly FullRoadPavementTemplateDialogRequest _request;
    private readonly ObservableCollection<FullRoadPavementComponentDto> _components = new();
    private readonly List<ComboOption<PavementLayerType>> _layerTypeOptions;
    private readonly List<PreviewComponentHitTarget> _previewComponentHitTargets = new();
    private readonly List<PreviewLayerHitTarget> _previewLayerHitTargets = new();
    private bool _loading = true;
    private bool _applyingLayerInputs;
    private bool _previewPanning;
    private Point _lastPreviewPanPoint;
    private double _previewZoom = 1.0;
    private Vector _previewPan;
    private int _selectedComponentIndex;
    private int _selectedLayerIndex;

    public FullRoadPavementTemplateWindow(FullRoadPavementTemplateDialogRequest request)
    {
        InitializeComponent();
        _request = request;
        _layerTypeOptions = Enum.GetValues(typeof(PavementLayerType))
            .Cast<PavementLayerType>()
            .Select(value => new ComboOption<PavementLayerType>(PavementLayerTemplateLabels.LayerTypeLabel(value), value))
            .ToList();
        Response = null;
        LoadRequest();
    }

    public event EventHandler<FullRoadPavementTemplateDialogResponse>? ApplyRequested;

    public FullRoadPavementTemplateDialogResponse? Response { get; private set; }

    private bool HasReferenceSubgradeTemplate
        => !string.IsNullOrWhiteSpace(_request.ReferenceSubgradeTemplateHandle) && _components.Count > 0;

    private FullRoadPavementComponentDto? CurrentComponent
        => _selectedComponentIndex >= 0 && _selectedComponentIndex < _components.Count
            ? _components[_selectedComponentIndex]
            : null;

    private PavementLayerTemplateLayerDto? CurrentLayer
        => CurrentComponent is { } component
            && _selectedLayerIndex >= 0
            && _selectedLayerIndex < component.Pavement.Layers.Count
                ? component.Pavement.Layers[_selectedLayerIndex]
                : null;

    private void LoadRequest()
    {
        _loading = true;
        TemplateNameBox.Text = string.IsNullOrWhiteSpace(_request.TemplateName)
            ? "整幅路路面结构层模板"
            : _request.TemplateName;
        DisplayScaleBox.Text = Format(_request.DisplayScale <= 0 ? 10.0 : _request.DisplayScale);
        ReferenceSubgradeTemplateText.Text = string.IsNullOrWhiteSpace(_request.ReferenceSubgradeTemplateName)
            ? "未选择"
            : _request.ReferenceSubgradeTemplateName;
        LayerTypeBox.ItemsSource = _layerTypeOptions;
        LayerTypeBox.DisplayMemberPath = nameof(ComboOption<PavementLayerType>.Label);
        LayerNameBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(LayerInput_Changed));
        HatchPatternBox.ItemsSource = PavementLayerTemplateLabels.HatchPatternOptions;

        _components.Clear();
        foreach (var source in _request.Components)
        {
            var component = source.Clone();
            if (_request.ApplyDefaultPresets)
            {
                var preset = DefaultPresetFor(component);
                if (preset != null && component.Pavement.Layers.Count == 0)
                {
                    component.Pavement = preset;
                }
            }
            component.Pavement = PavementLayerTemplateLayerEditorHelper.NormalizeTemplateForComponent(
                component.Pavement,
                component.DisplayName,
                component.Width,
                ReadDisplayScale());
            _components.Add(component);
        }

        _selectedComponentIndex = NormalizeComponentIndex(_request.CurrentComponentIndex);
        _selectedLayerIndex = 0;
        _loading = false;
        RefreshAll();
    }

    private static PavementLayerTemplateDto? DefaultPresetFor(FullRoadPavementComponentDto component)
    {
        if (component.Type == SubgradeComponentType.TravelLane)
        {
            var template = PavementLayerTemplatePresetFactory.Create(
                PavementSurfaceType.Asphalt,
                PavementLayerTemplateRoadSegmentType.MainlineLane);
            template.TemplateName = $"{component.DisplayName}结构层";
            template.PreviewWidth = Math.Max(0.1, component.Width);
            return template;
        }
        if (component.Type == SubgradeComponentType.HardShoulder)
        {
            var template = PavementLayerTemplatePresetFactory.Create(
                PavementSurfaceType.Asphalt,
                PavementLayerTemplateRoadSegmentType.MainlineShoulder);
            template.TemplateName = $"{component.DisplayName}结构层";
            template.PreviewWidth = Math.Max(0.1, component.Width);
            return template;
        }
        return null;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
        => DrawPreview();

    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        => DrawPreview();

    private void GlobalInput_Changed(object sender, EventArgs e)
    {
        if (!_loading)
        {
            foreach (var component in _components)
            {
                component.Pavement.DisplayScale = ReadDisplayScale();
            }
            RefreshAll();
        }
    }

    private void ReferenceSubgradeTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyLayerInputs();
        Response = CreateResponse(
            accepted: false,
            FullRoadPavementTemplateDialogAction.PickReferenceSubgradeTemplate);
        DialogResult = true;
    }

    private void PreviousComponentButton_Click(object sender, RoutedEventArgs e)
        => SelectAdjacentComponent(-1);

    private void NextComponentButton_Click(object sender, RoutedEventArgs e)
        => SelectAdjacentComponent(1);

    private void PreviousLayerButton_Click(object sender, RoutedEventArgs e)
        => SelectLayer(_selectedLayerIndex - 1);

    private void NextLayerButton_Click(object sender, RoutedEventArgs e)
        => SelectLayer(_selectedLayerIndex + 1);

    private void LayerCountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || CurrentComponent == null)
        {
            return;
        }
        if (!int.TryParse(LayerCountBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            return;
        }
        ApplyLayerInputs();
        count = Math.Max(0, Math.Min(100, count));
        var layers = CurrentComponent.Pavement.Layers;
        while (layers.Count < count)
        {
            layers.Add(PavementLayerTemplateLayerEditorHelper.CreateDefaultLayer(layers.Count));
        }
        while (layers.Count > count)
        {
            layers.RemoveAt(layers.Count - 1);
        }
        _selectedLayerIndex = layers.Count == 0 ? 0 : Math.Min(_selectedLayerIndex, layers.Count - 1);
        RefreshAll();
    }

    private void CurrentLayerBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || CurrentComponent == null)
        {
            return;
        }
        if (int.TryParse(CurrentLayerBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var layerNumber))
        {
            SelectLayer(layerNumber - 1);
        }
    }

    private void AddLayerButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentComponent == null)
        {
            return;
        }
        ApplyLayerInputs();
        var layers = CurrentComponent.Pavement.Layers;
        var insertIndex = layers.Count == 0 ? 0 : Math.Min(_selectedLayerIndex + 1, layers.Count);
        layers.Insert(insertIndex, PavementLayerTemplateLayerEditorHelper.CreateDefaultLayer(insertIndex));
        _selectedLayerIndex = insertIndex;
        RefreshAll();
    }

    private void DeleteLayerButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentComponent == null || CurrentComponent.Pavement.Layers.Count == 0)
        {
            return;
        }
        ApplyLayerInputs();
        CurrentComponent.Pavement.Layers.RemoveAt(Math.Max(0, Math.Min(_selectedLayerIndex, CurrentComponent.Pavement.Layers.Count - 1)));
        _selectedLayerIndex = CurrentComponent.Pavement.Layers.Count == 0
            ? 0
            : Math.Min(_selectedLayerIndex, CurrentComponent.Pavement.Layers.Count - 1);
        RefreshAll();
    }

    private void LayerInput_Changed(object sender, EventArgs e)
    {
        if (_loading || _applyingLayerInputs)
        {
            return;
        }
        ApplyLayerInputs();
        if (ReferenceEquals(sender, LayerTypeBox) && CurrentLayer != null)
        {
            LayerNameBox.ItemsSource = PavementLayerTemplateLabels.MaterialOptionsForLayerType(CurrentLayer.Type);
        }
        RefreshLayerFieldVisibility();
        DrawPreview();
    }

    private void PreviewCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyLayerInputs();
        var transform = CreatePreviewTransform(BuildComponentTopProfile(), _previewZoom, _previewPan);
        var mouse = e.GetPosition(PreviewCanvas);
        var newZoom = Math.Max(0.25, Math.Min(8.0, _previewZoom * (e.Delta > 0 ? 1.12 : 1.0 / 1.12)));
        if (transform != null)
        {
            var worldPoint = transform.ScreenToWorld(mouse);
            var nextTransform = CreatePreviewTransform(BuildComponentTopProfile(), newZoom, _previewPan);
            if (nextTransform != null)
            {
                _previewPan += mouse - nextTransform.WorldToScreen(worldPoint);
            }
        }

        _previewZoom = newZoom;
        DrawPreview();
        e.Handled = true;
    }

    private void PreviewCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            _previewPanning = true;
            _lastPreviewPanPoint = e.GetPosition(PreviewCanvas);
            PreviewCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var point = e.GetPosition(PreviewCanvas);
        for (var i = _previewLayerHitTargets.Count - 1; i >= 0; --i)
        {
            var hit = _previewLayerHitTargets[i];
            if (hit.Contains(point))
            {
                SelectComponentAndLayer(hit.ComponentIndex, hit.LayerIndex);
                e.Handled = true;
                return;
            }
        }

        var componentHit = _previewComponentHitTargets.FirstOrDefault(target => target.Bounds.Contains(point));
        if (componentHit != null)
        {
            SelectComponent(componentHit.ComponentIndex);
            e.Handled = true;
        }
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_previewPanning)
        {
            return;
        }

        var point = e.GetPosition(PreviewCanvas);
        _previewPan += point - _lastPreviewPanPoint;
        _lastPreviewPanPoint = point;
        DrawPreview();
    }

    private void PreviewCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _previewPanning = false;
        PreviewCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ApplyLayerInputs();
        Response = CreateResponse(accepted: true);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Response = CreateResponse(accepted: false);
        DialogResult = false;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        ApplyLayerInputs();
        ApplyRequested?.Invoke(this, CreateResponse(accepted: true));
    }

    private void SelectComponent(int index)
    {
        if (_components.Count == 0)
        {
            _selectedComponentIndex = -1;
            RefreshAll();
            return;
        }
        ApplyLayerInputs();
        _selectedComponentIndex = Math.Max(0, Math.Min(index, _components.Count - 1));
        _selectedLayerIndex = 0;
        RefreshAll();
    }

    private void SelectLayer(int index)
    {
        if (CurrentComponent == null)
        {
            return;
        }
        ApplyLayerInputs();
        var layers = CurrentComponent.Pavement.Layers;
        _selectedLayerIndex = layers.Count == 0 ? 0 : Math.Max(0, Math.Min(index, layers.Count - 1));
        RefreshAll();
    }

    private void SelectComponentAndLayer(int componentIndex, int layerIndex)
    {
        if (_components.Count == 0)
        {
            return;
        }

        ApplyLayerInputs();
        _selectedComponentIndex = Math.Max(0, Math.Min(componentIndex, _components.Count - 1));
        var layers = CurrentComponent?.Pavement.Layers;
        _selectedLayerIndex = layers == null || layers.Count == 0
            ? 0
            : Math.Max(0, Math.Min(layerIndex, layers.Count - 1));
        RefreshAll();
    }

    private void RefreshAll()
    {
        _loading = true;
        try
        {
            ReferenceSubgradeTemplateText.Text = string.IsNullOrWhiteSpace(_request.ReferenceSubgradeTemplateName)
                ? "未选择"
                : _request.ReferenceSubgradeTemplateName;
            CurrentComponentBox.Text = CurrentComponent?.DisplayName ?? string.Empty;
            var layerCount = CurrentComponent?.Pavement.Layers.Count ?? 0;
            LayerCountBox.Text = layerCount.ToString(CultureInfo.InvariantCulture);
            CurrentLayerBox.Text = layerCount == 0 ? "0" : (_selectedLayerIndex + 1).ToString(CultureInfo.InvariantCulture);
            var componentOrderPosition = CurrentDisplayOrderPosition();
            OkButton.IsEnabled = HasReferenceSubgradeTemplate;
            ApplyButton.IsEnabled = HasReferenceSubgradeTemplate && !string.IsNullOrWhiteSpace(_request.Handle);
            PreviousComponentButton.IsEnabled = componentOrderPosition > 0;
            NextComponentButton.IsEnabled = componentOrderPosition >= 0 && componentOrderPosition < _components.Count - 1;
            AddLayerButton.IsEnabled = HasReferenceSubgradeTemplate;
            DeleteLayerButton.IsEnabled = layerCount > 0;
            PreviousLayerButton.IsEnabled = layerCount > 1 && _selectedLayerIndex > 0;
            NextLayerButton.IsEnabled = layerCount > 1 && _selectedLayerIndex < layerCount - 1;
        }
        finally
        {
            _loading = false;
        }
        RefreshLayerEditor();
        DrawPreview();
    }

    private void RefreshLayerEditor()
    {
        var hasLayer = CurrentLayer != null;
        NoLayerText.Text = NoLayerStatusText;
        NoLayerText.Visibility = HasReferenceSubgradeTemplate && !hasLayer ? Visibility.Visible : Visibility.Collapsed;
        LayerEditorPanel.IsEnabled = hasLayer;
        LayerEditorPanel.Visibility = hasLayer ? Visibility.Visible : Visibility.Collapsed;
        if (!hasLayer)
        {
            return;
        }

        var layer = CurrentLayer!;
        _applyingLayerInputs = true;
        try
        {
            SelectComboValue(LayerTypeBox, layer.Type);
            LayerNameBox.ItemsSource = PavementLayerTemplateLabels.MaterialOptionsForLayerType(layer.Type);
            LayerNameBox.Text = string.IsNullOrWhiteSpace(layer.Name)
                ? PavementLayerTemplateLabels.LayerTypeLabel(layer.Type)
                : layer.Name;
            ColorRBox.Text = ClampColor(layer.ColorR).ToString(CultureInfo.InvariantCulture);
            ColorGBox.Text = ClampColor(layer.ColorG).ToString(CultureInfo.InvariantCulture);
            ColorBBox.Text = ClampColor(layer.ColorB).ToString(CultureInfo.InvariantCulture);
            ColorPreview.Background = new SolidColorBrush(LayerColor(layer, _selectedLayerIndex));
            HatchPatternBox.SelectedItem = PavementLayerTemplateLabels.NormalizeHatchPattern(layer.HatchPattern);
            HatchAngleBox.Text = Format(PavementLayerTemplateLabels.NormalizeHatchAngle(layer.HatchAngle));
            HatchScaleBox.Text = Format(PavementLayerTemplateLabels.NormalizeHatchScale(layer.HatchScale));
            UniformThicknessBox.IsChecked = layer.UniformThickness;
            ThicknessBox.Text = Format(layer.Thickness);
            InnerThicknessBox.Text = Format(layer.InnerThickness);
            OuterThicknessBox.Text = Format(layer.OuterThickness);
            UniformWideningBox.IsChecked = NearlyEqual(layer.InnerWidening, layer.OuterWidening);
            WideningBox.Text = Format(layer.InnerWidening);
            InnerWideningBox.Text = Format(layer.InnerWidening);
            OuterWideningBox.Text = Format(layer.OuterWidening);
            UniformSlopeBox.IsChecked = NearlyEqual(layer.InnerSlope, layer.OuterSlope);
            SlopeBox.Text = Format(layer.InnerSlope);
            InnerSlopeBox.Text = Format(layer.InnerSlope);
            OuterSlopeBox.Text = Format(layer.OuterSlope);
            RefreshLayerFieldVisibility();
        }
        finally
        {
            _applyingLayerInputs = false;
        }
    }

    private void RefreshLayerFieldVisibility()
    {
        var uniformThickness = UniformThicknessBox.IsChecked == true;
        ThicknessRow.Visibility = uniformThickness ? Visibility.Visible : Visibility.Collapsed;
        InnerThicknessRow.Visibility = uniformThickness ? Visibility.Collapsed : Visibility.Visible;
        OuterThicknessRow.Visibility = uniformThickness ? Visibility.Collapsed : Visibility.Visible;

        var uniformWidening = UniformWideningBox.IsChecked == true;
        WideningRow.Visibility = uniformWidening ? Visibility.Visible : Visibility.Collapsed;
        InnerWideningRow.Visibility = uniformWidening ? Visibility.Collapsed : Visibility.Visible;
        OuterWideningRow.Visibility = uniformWidening ? Visibility.Collapsed : Visibility.Visible;

        var uniformSlope = UniformSlopeBox.IsChecked == true;
        SlopeRow.Visibility = uniformSlope ? Visibility.Visible : Visibility.Collapsed;
        InnerSlopeRow.Visibility = uniformSlope ? Visibility.Collapsed : Visibility.Visible;
        OuterSlopeRow.Visibility = uniformSlope ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyLayerInputs()
    {
        if (_applyingLayerInputs || CurrentLayer == null)
        {
            return;
        }
        var layer = CurrentLayer;
        layer.Type = SelectedValue(LayerTypeBox, layer.Type);
        layer.Name = string.IsNullOrWhiteSpace(LayerNameBox.Text)
            ? PavementLayerTemplateLabels.LayerTypeLabel(layer.Type)
            : LayerNameBox.Text.Trim();
        layer.ColorR = ReadColor(ColorRBox.Text, layer.ColorR);
        layer.ColorG = ReadColor(ColorGBox.Text, layer.ColorG);
        layer.ColorB = ReadColor(ColorBBox.Text, layer.ColorB);
        layer.HatchPattern = PavementLayerTemplateLabels.NormalizeHatchPattern(HatchPatternBox.SelectedItem as string ?? "SOLID");
        layer.HatchAngle = PavementLayerTemplateLabels.NormalizeHatchAngle(ReadDouble(HatchAngleBox.Text, layer.HatchAngle));
        layer.HatchScale = PavementLayerTemplateLabels.NormalizeHatchScale(ReadDouble(HatchScaleBox.Text, layer.HatchScale));
        layer.UniformThickness = UniformThicknessBox.IsChecked == true;
        layer.Thickness = Math.Max(0.001, ReadDouble(ThicknessBox.Text, layer.Thickness));
        layer.InnerThickness = Math.Max(0.001, ReadDouble(InnerThicknessBox.Text, layer.InnerThickness));
        layer.OuterThickness = Math.Max(0.001, ReadDouble(OuterThicknessBox.Text, layer.OuterThickness));
        if (layer.UniformThickness)
        {
            layer.InnerThickness = layer.Thickness;
            layer.OuterThickness = layer.Thickness;
        }
        if (UniformWideningBox.IsChecked == true)
        {
            layer.InnerWidening = ReadDouble(WideningBox.Text, layer.InnerWidening);
            layer.OuterWidening = layer.InnerWidening;
        }
        else
        {
            layer.InnerWidening = ReadDouble(InnerWideningBox.Text, layer.InnerWidening);
            layer.OuterWidening = ReadDouble(OuterWideningBox.Text, layer.OuterWidening);
        }
        if (UniformSlopeBox.IsChecked == true)
        {
            layer.InnerSlope = ReadSlope(SlopeBox.Text, layer.InnerSlope);
            layer.OuterSlope = layer.InnerSlope;
        }
        else
        {
            layer.InnerSlope = ReadSlope(InnerSlopeBox.Text, layer.InnerSlope);
            layer.OuterSlope = ReadSlope(OuterSlopeBox.Text, layer.OuterSlope);
        }
    }

    private FullRoadPavementTemplateDialogResponse CreateResponse(
        bool accepted,
        FullRoadPavementTemplateDialogAction action = FullRoadPavementTemplateDialogAction.None)
        => new()
        {
            Accepted = accepted,
            Action = action,
            Handle = _request.Handle,
            InsertionX = _request.InsertionX,
            InsertionY = _request.InsertionY,
            InsertionZ = _request.InsertionZ,
            TemplateName = string.IsNullOrWhiteSpace(TemplateNameBox.Text) ? "整幅路路面结构层模板" : TemplateNameBox.Text.Trim(),
            DisplayScale = ReadDisplayScale(),
            ReferenceSubgradeTemplateHandle = _request.ReferenceSubgradeTemplateHandle,
            ReferenceSubgradeTemplateName = _request.ReferenceSubgradeTemplateName,
            ReferenceRoadGrade = _request.ReferenceRoadGrade,
            CurrentComponentIndex = _selectedComponentIndex,
            Components = _components.Select(component =>
            {
                var copy = component.Clone();
                copy.Pavement.DisplayScale = ReadDisplayScale();
                copy.Pavement.PreviewWidth = Math.Max(0.1, copy.Width);
                copy.Pavement.Layers = PavementLayerTemplateLayerEditorHelper.CloneNormalizedLayers(copy.Pavement.Layers);
                return copy;
            }).ToList(),
        };

    private void DrawPreview()
    {
        PreviewCanvas.Children.Clear();
        _previewComponentHitTargets.Clear();
        _previewLayerHitTargets.Clear();
        var hasReference = HasReferenceSubgradeTemplate;
        EmptyPreviewText.Visibility = hasReference ? Visibility.Collapsed : Visibility.Visible;
        if (!hasReference || PreviewCanvas.ActualWidth <= 1 || PreviewCanvas.ActualHeight <= 1)
        {
            return;
        }

        var components = BuildComponentTopProfile();
        var transform = CreatePreviewTransform(components, _previewZoom, _previewPan);
        if (transform == null)
        {
            return;
        }

        DrawGrid();
        DrawCenterLine(components, transform);

        foreach (var geometry in components)
        {
            var component = _components[geometry.ComponentIndex];
            var componentPoints = new List<Point> { transform.WorldToScreen(geometry.Start), transform.WorldToScreen(geometry.End) };
            var topStart = geometry.Start;
            var topEnd = geometry.End;
            for (var layerIndex = 0; layerIndex < component.Pavement.Layers.Count; ++layerIndex)
            {
                var layer = component.Pavement.Layers[layerIndex];
                var polygon = CreateLayerPolygon(geometry, layer, topStart, topEnd);
                var screenPoints = polygon.Points.Select(transform.WorldToScreen).ToList();
                DrawLayerPolygon(screenPoints, layer, layerIndex, geometry.ComponentIndex == _selectedComponentIndex && layerIndex == _selectedLayerIndex);
                DrawHatchPattern(screenPoints, layer, layerIndex);
                _previewLayerHitTargets.Add(new PreviewLayerHitTarget(geometry.ComponentIndex, layerIndex, screenPoints));
                componentPoints.AddRange(screenPoints);
                topStart = polygon.BottomStart;
                topEnd = polygon.BottomEnd;
            }

            DrawComponentTopLine(geometry, transform);
            DrawCurbs(geometry, transform);
            DrawComponentLabel(geometry, transform);
            _previewComponentHitTargets.Add(new PreviewComponentHitTarget(geometry.ComponentIndex, BoundsOf(componentPoints, 8.0)));
        }
    }

    private List<PreviewComponentGeometry> BuildComponentTopProfile()
    {
        var left = new List<PreviewComponentGeometry>();
        var right = new List<PreviewComponentGeometry>();
        var leftX = 0.0;
        var leftBaseY = 0.0;
        for (var i = 0; i < _components.Count; ++i)
        {
            var component = _components[i];
            if (component.Side != SubgradeSide.Left)
            {
                continue;
            }

            var width = Math.Max(0.1, component.Width);
            var topStartY = leftBaseY + InnerCurbHeightDelta(component);
            var outerBaseY = topStartY + DisplaySlope(component) * width * -1.0;
            if (IsFlatMedianComponent(component))
            {
                outerBaseY = topStartY;
            }
            var outer = new Point(leftX - width, outerBaseY);
            var inner = new Point(leftX, topStartY);
            left.Add(new PreviewComponentGeometry(i, outer, inner));
            leftX -= width;
            leftBaseY = outerBaseY + OuterCurbHeightDelta(component);
        }

        var rightX = 0.0;
        var rightBaseY = 0.0;
        for (var i = 0; i < _components.Count; ++i)
        {
            var component = _components[i];
            if (component.Side != SubgradeSide.Right)
            {
                continue;
            }

            var width = Math.Max(0.1, component.Width);
            var topStartY = rightBaseY + InnerCurbHeightDelta(component);
            var outerBaseY = topStartY + DisplaySlope(component) * width;
            if (IsFlatMedianComponent(component))
            {
                outerBaseY = topStartY;
            }
            var inner = new Point(rightX, topStartY);
            var outer = new Point(rightX + width, outerBaseY);
            right.Add(new PreviewComponentGeometry(i, inner, outer));
            rightX += width;
            rightBaseY = outerBaseY + OuterCurbHeightDelta(component);
        }

        left.Reverse();
        left.AddRange(right);
        return left;
    }

    private PreviewTransform? CreatePreviewTransform(IReadOnlyList<PreviewComponentGeometry> components, double zoom, Vector pan)
    {
        if (PreviewCanvas.ActualWidth <= 1 || PreviewCanvas.ActualHeight <= 1 || components.Count == 0)
        {
            return null;
        }

        var minX = components.Min(item => Math.Min(item.Start.X, item.End.X));
        var maxX = components.Max(item => Math.Max(item.Start.X, item.End.X));
        var minY = components.Min(item => Math.Min(item.Start.Y, item.End.Y) - ComponentDepth(_components[item.ComponentIndex]) - 0.35);
        var maxY = components.Max(item => Math.Max(item.Start.Y, item.End.Y) + 0.55);
        minX -= 0.45;
        maxX += 0.45;
        var width = Math.Max(0.1, maxX - minX);
        var height = Math.Max(0.1, maxY - minY);
        const double pad = 46.0;
        var usableWidth = Math.Max(1.0, PreviewCanvas.ActualWidth - pad * 2.0);
        var usableHeight = Math.Max(1.0, PreviewCanvas.ActualHeight - pad * 2.0);
        var baseScale = Math.Max(4.0, Math.Min(usableWidth / width, usableHeight / height));
        return new PreviewTransform(
            PreviewCanvas.ActualWidth,
            PreviewCanvas.ActualHeight,
            minX,
            maxY,
            width,
            height,
            baseScale * zoom,
            pan);
    }

    private LayerPreviewPolygon CreateLayerPolygon(
        PreviewComponentGeometry componentGeometry,
        PavementLayerTemplateLayerDto layer,
        Point inheritedTopStart,
        Point inheritedTopEnd)
    {
        var component = _components[componentGeometry.ComponentIndex];
        var grade = TopGrade(inheritedTopStart, inheritedTopEnd);
        var isLeft = component.Side == SubgradeSide.Left;
        var startOffset = isLeft ? -layer.OuterWidening : -layer.InnerWidening;
        var endOffset = isLeft ? layer.InnerWidening : layer.OuterWidening;
        var topStart = ShiftAlongGrade(inheritedTopStart, grade, startOffset);
        var topEnd = ShiftAlongGrade(inheritedTopEnd, grade, endOffset);
        var startThickness = isLeft ? LayerOuterThickness(layer) : LayerInnerThickness(layer);
        var endThickness = isLeft ? LayerInnerThickness(layer) : LayerOuterThickness(layer);
        var startSlope = isLeft ? layer.OuterSlope : layer.InnerSlope;
        var endSlope = isLeft ? layer.InnerSlope : layer.OuterSlope;
        var bottomStart = new Point(topStart.X - startThickness * startSlope, topStart.Y - startThickness);
        var bottomEnd = new Point(topEnd.X + endThickness * endSlope, topEnd.Y - endThickness);
        return new LayerPreviewPolygon(topStart, topEnd, bottomEnd, bottomStart);
    }

    private void DrawGrid()
    {
        var pen = new SolidColorBrush(Color.FromRgb(45, 52, 60));
        for (var x = 0.0; x < PreviewCanvas.ActualWidth; x += 60.0)
        {
            PreviewCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = PreviewCanvas.ActualHeight, Stroke = pen, StrokeThickness = 1, IsHitTestVisible = false });
        }
        for (var y = 0.0; y < PreviewCanvas.ActualHeight; y += 60.0)
        {
            PreviewCanvas.Children.Add(new Line { X1 = 0, X2 = PreviewCanvas.ActualWidth, Y1 = y, Y2 = y, Stroke = pen, StrokeThickness = 1, IsHitTestVisible = false });
        }
    }

    private void DrawCenterLine(IReadOnlyList<PreviewComponentGeometry> components, PreviewTransform transform)
    {
        var minY = components.Min(item => Math.Min(item.Start.Y, item.End.Y) - ComponentDepth(_components[item.ComponentIndex]) - 0.28);
        var maxY = components.Max(item => Math.Max(item.Start.Y, item.End.Y) + 0.28);
        var top = transform.WorldToScreen(new Point(0.0, maxY));
        var bottom = transform.WorldToScreen(new Point(0.0, minY));
        DrawLine(top.X, top.Y, bottom.X, bottom.Y, Brushes.White, 1.0, new DoubleCollection { 5, 4 });
        DrawText("中线", top.X + 6.0, top.Y + 2.0, Brushes.White, 11.0);
    }

    private void DrawComponentTopLine(PreviewComponentGeometry geometry, PreviewTransform transform)
    {
        var component = _components[geometry.ComponentIndex];
        var start = transform.WorldToScreen(geometry.Start);
        var end = transform.WorldToScreen(geometry.End);
        var stroke = new SolidColorBrush(Color.FromRgb(ToByte(component.ColorR), ToByte(component.ColorG), ToByte(component.ColorB)));
        if (geometry.ComponentIndex == _selectedComponentIndex)
        {
            DrawLine(start.X, start.Y, end.X, end.Y, Brushes.White, 4.0);
        }
        DrawLine(start.X, start.Y, end.X, end.Y, stroke, geometry.ComponentIndex == _selectedComponentIndex ? 2.6 : 2.1);
    }

    private void DrawCurbs(PreviewComponentGeometry geometry, PreviewTransform transform)
    {
        DrawCurb(geometry, innerSide: true, transform);
        DrawCurb(geometry, innerSide: false, transform);
    }

    private void DrawCurb(PreviewComponentGeometry geometry, bool innerSide, PreviewTransform transform)
    {
        var points = CreateCurbPolygon(geometry, innerSide);
        if (points == null)
        {
            return;
        }

        var component = _components[geometry.ComponentIndex];
        var screenPoints = points.Select(transform.WorldToScreen).ToList();
        var fill = new SolidColorBrush(Color.FromArgb(180, ToByte(component.ColorR), ToByte(component.ColorG), ToByte(component.ColorB)));
        PreviewCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection(screenPoints),
            Fill = fill,
            Stroke = Brushes.WhiteSmoke,
            StrokeThickness = geometry.ComponentIndex == _selectedComponentIndex ? 1.6 : 1.0,
            IsHitTestVisible = false,
        });
        DrawCurbSizeLabel(geometry, innerSide, points, transform);
    }

    private IReadOnlyList<Point>? CreateCurbPolygon(PreviewComponentGeometry geometry, bool innerSide)
    {
        var component = _components[geometry.ComponentIndex];
        var hasCurb = innerSide ? component.HasInnerCurb : component.HasOuterCurb;
        if (!hasCurb)
        {
            return null;
        }

        var width = Math.Max(0.0, innerSide ? component.InnerCurbWidth : component.OuterCurbWidth);
        var height = Math.Max(0.0, innerSide ? component.InnerCurbHeight : component.OuterCurbHeight);
        var embedDepth = Math.Max(0.0, innerSide ? component.InnerCurbEmbedDepth : component.OuterCurbEmbedDepth);
        var direction = geometry.End - geometry.Start;
        if (direction.Length <= 1.0e-9)
        {
            return null;
        }

        if (width <= 1.0e-9 || (height <= 1.0e-9 && embedDepth <= 1.0e-9))
        {
            return null;
        }

        var componentLength = direction.Length;
        direction.Normalize();
        var edge = CurbEdgePoint(geometry, innerSide);
        var curbAtStart = CurbEdgeIsStart(geometry, innerSide);
        var inward = curbAtStart ? direction : -direction;
        var innerEdge = edge + inward * Math.Min(width, Math.Max(0.0, componentLength));
        var ratio = componentLength > 1.0e-9
            ? Math.Max(0.0, Math.Min(1.0, Vector.Multiply(innerEdge - geometry.Start, geometry.End - geometry.Start) / (componentLength * componentLength)))
            : 0.0;
        var insideSurfaceY = geometry.Start.Y + (geometry.End.Y - geometry.Start.Y) * ratio;
        var curbTopStartY = edge.Y;
        var curbTopInsideY = insideSurfaceY;
        return new[]
        {
            new Point(edge.X, curbTopStartY),
            new Point(innerEdge.X, curbTopInsideY),
            new Point(innerEdge.X, curbTopInsideY - height - embedDepth),
            new Point(edge.X, curbTopStartY - height - embedDepth),
        };
    }

    private Point CurbEdgePoint(PreviewComponentGeometry geometry, bool innerSide)
    {
        var component = _components[geometry.ComponentIndex];
        if (component.Side == SubgradeSide.Left)
        {
            return innerSide ? geometry.End : geometry.Start;
        }

        return innerSide ? geometry.Start : geometry.End;
    }

    private bool CurbEdgeIsStart(PreviewComponentGeometry geometry, bool innerSide)
    {
        var component = _components[geometry.ComponentIndex];
        return component.Side == SubgradeSide.Left ? !innerSide : innerSide;
    }

    private void DrawCurbSizeLabel(
        PreviewComponentGeometry geometry,
        bool innerSide,
        IReadOnlyList<Point> points,
        PreviewTransform transform)
    {
        var component = _components[geometry.ComponentIndex];
        var width = innerSide ? component.InnerCurbWidth : component.OuterCurbWidth;
        var height = innerSide ? component.InnerCurbHeight : component.OuterCurbHeight;
        var embedDepth = innerSide ? component.InnerCurbEmbedDepth : component.OuterCurbEmbedDepth;
        var label = $"缘石 {Format(width)}/{Format(height)}/{Format(embedDepth)}";
        var topMid = new Point((points[0].X + points[1].X) * 0.5, (points[0].Y + points[1].Y) * 0.5);
        var screen = transform.WorldToScreen(new Point(topMid.X, topMid.Y + 0.08));
        DrawText(label, screen.X - 34.0, screen.Y - 14.0, Brushes.LightGray, 8.5);
    }

    private void DrawComponentLabel(PreviewComponentGeometry geometry, PreviewTransform transform)
    {
        var component = _components[geometry.ComponentIndex];
        var start = transform.WorldToScreen(geometry.Start);
        var end = transform.WorldToScreen(geometry.End);
        var midpoint = new Point((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5 - 22.0);
        DrawText(component.DisplayName, midpoint.X - 38.0, midpoint.Y, Brushes.Gainsboro, 11.0);
        DrawText(WidthText(component), midpoint.X - 28.0, midpoint.Y + 14.0, Brushes.Silver, 9.0);
        DrawText(SlopeText(component), midpoint.X - 28.0, midpoint.Y + 26.0, Brushes.Silver, 9.0);
    }

    private void DrawLayerPolygon(IReadOnlyList<Point> points, PavementLayerTemplateLayerDto layer, int index, bool selected)
    {
        var color = LayerColor(layer, index);
        PreviewCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection(points),
            Fill = new SolidColorBrush(Color.FromArgb(132, color.R, color.G, color.B)),
            Stroke = selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            StrokeThickness = selected ? 2.4 : 1.0,
            IsHitTestVisible = false,
        });
    }

    private void DrawHatchPattern(IReadOnlyList<Point> points, PavementLayerTemplateLayerDto layer, int index)
    {
        var pattern = PavementLayerTemplateLabels.NormalizeHatchPattern(layer.HatchPattern);
        if (pattern == "SOLID")
        {
            return;
        }

        var color = LayerColor(layer, index);
        var stroke = new SolidColorBrush(Color.FromArgb(190, color.R, color.G, color.B));
        var hatchScale = PavementLayerTemplateLabels.NormalizeHatchScale(layer.HatchScale);
        var spacing = Math.Max(3.0, (pattern == "DOTS" ? 12.0 : 10.0) * hatchScale);
        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var hatchCanvas = new Canvas
        {
            Width = PreviewCanvas.ActualWidth,
            Height = PreviewCanvas.ActualHeight,
            Clip = CreatePolygonClip(points),
            IsHitTestVisible = false,
        };
        PreviewCanvas.Children.Add(hatchCanvas);

        if (pattern == "DOTS")
        {
            for (var x = minX; x <= maxX; x += spacing)
            {
                for (var y = minY; y <= maxY; y += spacing)
                {
                    if (PointInPolygon(new Point(x, y), points))
                    {
                        hatchCanvas.Children.Add(new Ellipse { Width = 2.0, Height = 2.0, Fill = stroke }.At(x - 1.0, y - 1.0));
                    }
                }
            }
            return;
        }

        var angleRadians = PavementLayerTemplateLabels.NormalizeHatchAngle(layer.HatchAngle) * Math.PI / 180.0;
        var primaryDirection = new Vector(Math.Cos(angleRadians), -Math.Sin(angleRadians));
        AddHatchFamily(hatchCanvas, points, primaryDirection, spacing, stroke);
        if (pattern is "CROSS" or "ANSI32" or "ANSI37" or "ANSI38")
        {
            AddHatchFamily(hatchCanvas, points, new Vector(-primaryDirection.Y, primaryDirection.X), spacing, stroke);
        }
        if (pattern is "GRAVEL" or "EARTH" or "AR-CONC")
        {
            var secondaryRadians = angleRadians + Math.PI / 4.0;
            AddHatchFamily(hatchCanvas, points, new Vector(Math.Cos(secondaryRadians), -Math.Sin(secondaryRadians)), spacing * 1.35, stroke);
        }
    }

    private IEnumerable<int> DisplayOrder()
    {
        for (var i = _components.Count - 1; i >= 0; --i)
        {
            if (_components[i].Side == SubgradeSide.Left)
            {
                yield return i;
            }
        }
        for (var i = 0; i < _components.Count; ++i)
        {
            if (_components[i].Side == SubgradeSide.Right)
            {
                yield return i;
            }
        }
    }

    private void SelectAdjacentComponent(int delta)
    {
        var order = DisplayOrder().ToList();
        var position = order.IndexOf(_selectedComponentIndex);
        if (position < 0)
        {
            position = 0;
        }
        var nextPosition = Math.Max(0, Math.Min(position + delta, order.Count - 1));
        if (nextPosition >= 0 && nextPosition < order.Count)
        {
            SelectComponent(order[nextPosition]);
        }
    }

    private int CurrentDisplayOrderPosition()
        => DisplayOrder().ToList().IndexOf(_selectedComponentIndex);

    private double ComponentDepth(FullRoadPavementComponentDto component)
    {
        var layerDepth = component.Pavement.Layers.Sum(layer => Math.Max(0.02, (LayerInnerThickness(layer) + LayerOuterThickness(layer)) / 2.0));
        var curbDepth = Math.Max(CurbHeightOffset(component, innerSide: true), CurbHeightOffset(component, innerSide: false));
        return Math.Max(0.08, Math.Max(layerDepth, curbDepth));
    }

    private static Geometry CreatePolygonClip(IReadOnlyList<Point> points)
    {
        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = true,
            IsFilled = true,
        };
        for (var i = 1; i < points.Count; ++i)
        {
            figure.Segments.Add(new LineSegment(points[i], true));
        }
        return new PathGeometry(new[] { figure });
    }

    private static void AddHatchFamily(Canvas canvas, IReadOnlyList<Point> points, Vector direction, double spacing, Brush stroke)
    {
        if (points.Count == 0 || spacing <= 0.0)
        {
            return;
        }

        if (direction.Length <= 1.0e-9)
        {
            direction = new Vector(1.0, 0.0);
        }
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var minProjection = points.Min(point => point.X * normal.X + point.Y * normal.Y);
        var maxProjection = points.Max(point => point.X * normal.X + point.Y * normal.Y);
        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var diagonal = Math.Sqrt(Math.Pow(maxX - minX, 2.0) + Math.Pow(maxY - minY, 2.0)) + spacing * 2.0;

        for (var projection = minProjection - spacing; projection <= maxProjection + spacing; projection += spacing)
        {
            var center = new Point(normal.X * projection, normal.Y * projection);
            var start = center - direction * diagonal;
            var end = center + direction * diagonal;
            canvas.Children.Add(new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = stroke,
                StrokeThickness = 0.8,
                IsHitTestVisible = false,
            });
        }
    }

    private static Rect BoundsOf(IReadOnlyList<Point> points, double inflate)
    {
        if (points.Count == 0)
        {
            return Rect.Empty;
        }

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var rect = new Rect(new Point(minX, minY), new Point(maxX, maxY));
        rect.Inflate(inflate, inflate);
        return rect;
    }

    private static bool PointInPolygon(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var current = polygon[i];
            var previous = polygon[j];
            if (((current.Y > point.Y) != (previous.Y > point.Y)) &&
                point.X < (previous.X - current.X) * (point.Y - current.Y) / (previous.Y - current.Y) + current.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static Point ShiftAlongGrade(Point point, double grade, double offset)
        => new(point.X + offset, point.Y + offset * grade);

    private static double TopGrade(Point start, Point end)
    {
        var width = end.X - start.X;
        return Math.Abs(width) <= 1.0e-9 ? 0.0 : (end.Y - start.Y) / width;
    }

    private static double LayerInnerThickness(PavementLayerTemplateLayerDto layer)
        => Math.Max(0.001, layer.UniformThickness ? layer.Thickness : layer.InnerThickness);

    private static double LayerOuterThickness(PavementLayerTemplateLayerDto layer)
        => Math.Max(0.001, layer.UniformThickness ? layer.Thickness : layer.OuterThickness);

    private static double SafeSlope(double value)
        => IsFinite(value) ? value : 0.0;

    private static double DisplaySlope(FullRoadPavementComponentDto component)
    {
        if (component.SlopeMode == SubgradeSlopeMode.Fixed)
        {
            return SafeSlope(component.FixedSlope);
        }

        return component.VariableSlopeTable.Count > 0
            ? SafeSlope(component.VariableSlopeTable[0].Value)
            : 0.0;
    }

    private static string WidthText(FullRoadPavementComponentDto component)
        => $"宽 {Format(component.Width)}";

    private static string SlopeText(FullRoadPavementComponentDto component)
        => $"坡 {Format(DisplaySlope(component))}";

    private static bool IsFlatMedianComponent(FullRoadPavementComponentDto component)
        => component.Type is SubgradeComponentType.Median or SubgradeComponentType.SideMedian;

    private static double InnerCurbHeightDelta(FullRoadPavementComponentDto component)
        => component.HasInnerCurb ? Math.Max(0.0, component.InnerCurbHeight) : 0.0;

    private static double OuterCurbHeightDelta(FullRoadPavementComponentDto component)
        => component.HasOuterCurb ? -Math.Max(0.0, component.OuterCurbHeight) : 0.0;

    private static double CurbHeightOffset(FullRoadPavementComponentDto component, bool innerSide)
    {
        var hasCurb = innerSide ? component.HasInnerCurb : component.HasOuterCurb;
        if (!hasCurb)
        {
            return 0.0;
        }

        var height = innerSide ? component.InnerCurbHeight : component.OuterCurbHeight;
        var embedDepth = innerSide ? component.InnerCurbEmbedDepth : component.OuterCurbEmbedDepth;
        return Math.Max(0.0, height) + Math.Max(0.0, embedDepth);
    }

    private void DrawRectangle(Rect rect, Brush fill, Brush stroke, double strokeThickness)
    {
        PreviewCanvas.Children.Add(new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            IsHitTestVisible = false,
        }.At(rect.X, rect.Y));
    }

    private void DrawLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness, DoubleCollection? dash = null)
    {
        PreviewCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeDashArray = dash,
            IsHitTestVisible = false,
        });
    }

    private void DrawText(string text, double x, double y, Brush brush, double fontSize)
    {
        PreviewCanvas.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = fontSize,
            IsHitTestVisible = false,
        }.At(x, y));
    }

    private int NormalizeComponentIndex(int index)
    {
        if (_components.Count == 0)
        {
            return -1;
        }
        return Math.Max(0, Math.Min(index, _components.Count - 1));
    }

    private double ReadDisplayScale()
        => ReadDouble(DisplayScaleBox.Text, _request.DisplayScale <= 0 ? 10.0 : _request.DisplayScale);

    private static double ReadDouble(string text, double fallback)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            ? value
            : fallback;

    private static double ReadSlope(string text, double fallback)
    {
        var trimmed = text.Trim();
        var ratio = trimmed.Replace('：', ':').Split(':');
        if (ratio.Length == 2 &&
            double.TryParse(ratio[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(ratio[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            !NearlyEqual(denominator, 0.0))
        {
            return numerator / denominator;
        }

        return ReadDouble(trimmed, fallback);
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static int ReadColor(string text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? ClampColor(value)
            : ClampColor(fallback);

    private static int ClampColor(int value)
        => Math.Max(0, Math.Min(255, value));

    private static byte ToByte(int value)
        => (byte)ClampColor(value);

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool NearlyEqual(double first, double second)
        => Math.Abs(first - second) < 1.0e-9;

    private static Color LayerColor(PavementLayerTemplateLayerDto layer, int index)
    {
        if (layer.ColorR < 0 || layer.ColorG < 0 || layer.ColorB < 0)
        {
            var color = PavementLayerTemplateLabels.DefaultColorForLayerIndex(index);
            return Color.FromRgb((byte)color.R, (byte)color.G, (byte)color.B);
        }
        return Color.FromRgb(ToByte(layer.ColorR), ToByte(layer.ColorG), ToByte(layer.ColorB));
    }

    private static void SelectComboValue<T>(ComboBox comboBox, T value)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is ComboOption<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private static T SelectedValue<T>(ComboBox comboBox, T fallback)
        => comboBox.SelectedItem is ComboOption<T> option ? option.Value : fallback;

    private sealed class ComboOption<T>
    {
        public ComboOption(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public T Value { get; }
    }

    private sealed class PreviewComponentHitTarget
    {
        public PreviewComponentHitTarget(int componentIndex, Rect bounds)
        {
            ComponentIndex = componentIndex;
            Bounds = bounds;
        }

        public int ComponentIndex { get; }
        public Rect Bounds { get; }
    }

    private sealed class PreviewLayerHitTarget
    {
        private readonly IReadOnlyList<Point> _points;

        public PreviewLayerHitTarget(int componentIndex, int layerIndex, IReadOnlyList<Point> points)
        {
            ComponentIndex = componentIndex;
            LayerIndex = layerIndex;
            _points = points.ToList();
        }

        public int ComponentIndex { get; }
        public int LayerIndex { get; }

        public bool Contains(Point point)
            => BoundsOf(_points, 0.0).Contains(point) && PointInPolygon(point, _points);
    }

    private sealed class PreviewComponentGeometry
    {
        public PreviewComponentGeometry(int componentIndex, Point start, Point end)
        {
            ComponentIndex = componentIndex;
            Start = start;
            End = end;
        }

        public int ComponentIndex { get; }
        public Point Start { get; }
        public Point End { get; }
    }

    private sealed class LayerPreviewPolygon
    {
        public LayerPreviewPolygon(Point topStart, Point topEnd, Point bottomEnd, Point bottomStart)
        {
            TopStart = topStart;
            TopEnd = topEnd;
            BottomEnd = bottomEnd;
            BottomStart = bottomStart;
            Points = new[] { topStart, topEnd, bottomEnd, bottomStart };
        }

        public Point TopStart { get; }
        public Point TopEnd { get; }
        public Point BottomEnd { get; }
        public Point BottomStart { get; }
        public IReadOnlyList<Point> Points { get; }
    }

    private sealed class PreviewTransform
    {
        private readonly double _canvasWidth;
        private readonly double _canvasHeight;
        private readonly double _minX;
        private readonly double _maxY;
        private readonly double _contentWidth;
        private readonly double _contentHeight;
        private readonly double _scale;
        private readonly Vector _pan;

        public PreviewTransform(
            double canvasWidth,
            double canvasHeight,
            double minX,
            double maxY,
            double worldWidth,
            double worldHeight,
            double scale,
            Vector pan)
        {
            _canvasWidth = canvasWidth;
            _canvasHeight = canvasHeight;
            _minX = minX;
            _maxY = maxY;
            _contentWidth = worldWidth * scale;
            _contentHeight = worldHeight * scale;
            _scale = scale;
            _pan = pan;
        }

        public Point WorldToScreen(Point point)
            => new(
                (_canvasWidth - _contentWidth) / 2.0 + (point.X - _minX) * _scale + _pan.X,
                (_canvasHeight - _contentHeight) / 2.0 + (_maxY - point.Y) * _scale + _pan.Y);

        public Point ScreenToWorld(Point point)
            => new(
                _minX + (point.X - _pan.X - (_canvasWidth - _contentWidth) / 2.0) / _scale,
                _maxY - (point.Y - _pan.Y - (_canvasHeight - _contentHeight) / 2.0) / _scale);
    }
}

internal static class FullRoadPavementCanvasExtensions
{
    public static T At<T>(this T element, double x, double y)
        where T : UIElement
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }
}
