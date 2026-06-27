using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.Runtime;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RoadProto.Terrain.UI.Agent;

public sealed class AgentConsoleCommands
{
    private static PaletteSet? _palette;
    private static Grid? _hostGrid;
    private static AgentConsoleSafePanel? _safePanel;
    private static bool _isAttachQueued;

    [CommandMethod("RD_AGENT_CONSOLE_UI", CommandFlags.Session)]
    public void ShowAgentConsole()
    {
        try
        {
            Probe("Enter ShowAgentConsole");
            if (_palette == null)
            {
                Probe("Before PaletteSet");
                _palette = new PaletteSet("Agent 控制台")
                {
                    Size = new System.Drawing.Size(560, 720),
                    MinimumSize = new System.Drawing.Size(480, 560),
                    DockEnabled = DockSides.Left | DockSides.Right,
                    Style = PaletteSetStyles.ShowAutoHideButton
                        | PaletteSetStyles.ShowCloseButton
                        | PaletteSetStyles.ShowPropertiesMenu
                        | PaletteSetStyles.Snappable
                        | PaletteSetStyles.SingleColDock,
                };
                _palette.SizeChanged += OnPaletteSizeChanged;
                Probe("After PaletteSet");

                Probe("Before SafePaletteHost");
                _hostGrid = CreateStartupContent();
                Probe("After SafePaletteHost");

                Probe("Before AddVisualHost");
                _palette.AddVisual("Agent", _hostGrid, false);
                ApplyPaletteSizeToHost();
                Probe("After AddVisualHost");
            }

            Probe("Before Visible");
            _palette.Visible = true;
            Probe("After Visible");
            Probe("Before Dock");
            _palette.DockEnabled = DockSides.Left | DockSides.Right;
            _palette.Dock = DockSides.Right;
            ApplyPaletteSizeToHost();
            Probe("After Dock");
            Probe("Before Activate");
            _palette.Activate(0);
            ApplyPaletteSizeToHost();
            Probe("After Activate");
            QueueAttachAgentConsole(_hostGrid);
        }
        catch (System.Exception ex)
        {
            Probe("Exception " + ex);
            AcadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nAgent 控制台打开失败：{ex.Message}\n{ex}");
            _isAttachQueued = false;
            _palette = null;
            _hostGrid = null;
            _safePanel = null;
        }
    }

    private static Grid CreateStartupContent()
    {
        Probe("Before SafeWindowContent");
        var root = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)),
            Margin = new System.Windows.Thickness(16),
        };

        root.RowDefinitions.Add(new RowDefinition { Height = System.Windows.GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = System.Windows.GridLength.Auto });

        var title = new TextBlock
        {
            Text = "Agent 控制台",
            FontSize = 16,
            FontWeight = System.Windows.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var status = new TextBlock
        {
            Text = "正在初始化...",
            Margin = new System.Windows.Thickness(0, 8, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
            TextWrapping = System.Windows.TextWrapping.Wrap,
        };
        Grid.SetRow(status, 1);
        root.Children.Add(status);

        Probe("After SafeWindowContent");
        return root;
    }

    private static void QueueAttachAgentConsole(Grid? hostGrid)
    {
        if (hostGrid == null || _isAttachQueued || hostGrid.Children.OfType<AgentConsoleSafePanel>().Any())
        {
            return;
        }

        _isAttachQueued = true;
        Probe("Before AttachAgentConsoleDispatch");
        hostGrid.Dispatcher.BeginInvoke(new Action(async () =>
        {
            await AttachAgentConsoleAsync(hostGrid);
        }), DispatcherPriority.ContextIdle);
        Probe("After AttachAgentConsoleDispatch");
    }

    private static async Task AttachAgentConsoleAsync(Grid hostGrid)
    {
        try
        {
            Probe("Before AttachAgentConsole");
            await Task.Yield();
            if (!ReferenceEquals(_hostGrid, hostGrid))
            {
                Probe("Skip AttachAgentConsole StaleHost");
                return;
            }

            Probe("Before AgentConsoleSafePanel");
            hostGrid.RowDefinitions.Clear();
            hostGrid.ColumnDefinitions.Clear();
            hostGrid.Margin = new System.Windows.Thickness(0);
            hostGrid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            hostGrid.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            var panel = new AgentConsoleSafePanel();
            panel.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            panel.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            _safePanel = panel;
            hostGrid.Children.Clear();
            hostGrid.Children.Add(panel);
            ApplyPaletteSizeToHost();
            Probe("After AgentConsoleSafePanelContent");
        }
        catch (System.Exception ex)
        {
            Probe("AttachAgentConsoleException " + ex);
        }
        finally
        {
            _isAttachQueued = false;
        }
    }

    private static void OnPaletteSizeChanged(object sender, PaletteSetSizeEventArgs e)
    {
        ApplyPaletteSizeToHost();
    }

    private static void ApplyPaletteSizeToHost()
    {
        var palette = _palette;
        var hostGrid = _hostGrid;
        if (palette == null || hostGrid == null)
        {
            return;
        }

        var width = palette.PaletteSize.Width;
        var height = palette.PaletteSize.Height;
        if (!IsUsableSize(width) || !IsUsableSize(height))
        {
            width = palette.Size.Width;
            height = palette.Size.Height;
        }

        width = Math.Max(1.0, width);
        height = Math.Max(1.0, height);

        hostGrid.Width = width;
        hostGrid.Height = height;
        hostGrid.MinWidth = 1.0;
        hostGrid.MinHeight = 1.0;

        var panel = _safePanel;
        if (panel != null)
        {
            panel.Width = width;
            panel.Height = height;
            panel.MinWidth = 1.0;
            panel.MinHeight = 1.0;
        }
    }

    private static bool IsUsableSize(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 1.0;
    }

    private static void Probe(string message)
    {
        try
        {
            var directory = @"F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto";
            Directory.CreateDirectory(directory);
            var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
            File.AppendAllText(
                Path.Combine(directory, "agent_palette_startup_probe.log"),
                line,
                Encoding.UTF8);
        }
        catch
        {
        }
    }
}
