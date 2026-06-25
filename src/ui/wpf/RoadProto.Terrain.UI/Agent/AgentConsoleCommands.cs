using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using WpfControls = System.Windows.Controls;
using WpfMedia = System.Windows.Media;

namespace RoadProto.Terrain.UI.Agent;

public sealed class AgentConsoleCommands
{
    private static PaletteSet? _palette;
    private static WpfControls.Grid? _wpfHost;

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
                    Size = new Size(560, 720),
                    MinimumSize = new Size(480, 560),
                    DockEnabled = DockSides.Right,
                    Style = PaletteSetStyles.ShowAutoHideButton
                        | PaletteSetStyles.ShowCloseButton
                        | PaletteSetStyles.ShowPropertiesMenu
                        | PaletteSetStyles.Snappable
                        | PaletteSetStyles.SingleColDock,
                };
                Probe("After PaletteSet");
                Probe("Before WpfHost");
                _wpfHost = new WpfControls.Grid
                {
                    Background = WpfMedia.Brushes.White,
                };
                Probe("After WpfHost");
                Probe("Before AddVisualHost");
                _palette.AddVisual("Agent", _wpfHost, false);
                Probe("After AddVisualHost");
                Probe("Before AgentConsolePalette");
                var paletteContent = new AgentConsolePalette();
                Probe("After AgentConsolePalette");
                Probe("Before WpfHostChildrenAdd");
                _wpfHost.Children.Add(paletteContent);
                Probe("After WpfHostChildrenAdd");
            }

            Probe("Before Visible");
            _palette.Visible = true;
            Probe("After Visible");
            Probe("Before Dock");
            _palette.Dock = DockSides.Right;
            Probe("After Dock");
            Probe("Before Activate");
            _palette.Activate(0);
            Probe("After Activate");
        }
        catch (System.Exception ex)
        {
            Probe("Exception " + ex);
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nAgent 控制台打开失败：{ex.Message}\n{ex}");
            _palette = null;
        }
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
