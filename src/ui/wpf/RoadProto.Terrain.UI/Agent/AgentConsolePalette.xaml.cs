using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RoadProto.Terrain.UI.Agent;

public partial class AgentConsolePalette : UserControl
{
    private readonly AgentConsoleViewModel _viewModel = new();
    private bool _isRestoringInputFocus;

    public AgentConsolePalette()
    {
        ProbeLifecycle("Before InitializeComponent");
        InitializeComponent();
        ProbeLifecycle("After InitializeComponent");
        DataContext = _viewModel;
        ProbeLifecycle("After DataContext");
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProbeLifecycle("Loaded");
        Loaded -= OnLoaded;
        BeginInitializeAsync();
    }

    private void BeginInitializeAsync()
    {
        ProbeLifecycle("Before BeginInitializeDispatch");
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            ProbeLifecycle("Before InitializeAfterHost");
            await InitializeAfterHostAsync();
            ProbeLifecycle("After InitializeAfterHost");
        }), DispatcherPriority.ContextIdle);
    }

    private async Task InitializeAfterHostAsync()
    {
        try
        {
            ProbeLifecycle("Before ViewModelInitialize");
            await _viewModel.InitializeAsync();
            ProbeLifecycle("After ViewModelInitialize");
        }
        catch (Exception ex)
        {
            ProbeLifecycle("ViewModelInitializeException " + ex);
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        await SendCurrentInputAsync();
    }

    private async Task SendCurrentInputAsync()
    {
        try
        {
            var text = InputBox.Text;
            await _viewModel.SendAsync(text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                InputBox.Clear();
            }

            RestoreInputFocusAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ConfirmAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async void OnCancelClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.CancelAsync();
            RestoreInputFocusAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async void OnSaveProviderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.SaveProviderAsync(ApiKeyBox.Password);
            ApiKeyBox.Clear();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private void OnOpenRoadProtoLogsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_viewModel.RoadProtoLogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _viewModel.RoadProtoLogDirectory,
                UseShellExecute = true,
            });
            _viewModel.Log("OpenRoadProtoLogs", _viewModel.RoadProtoLogDirectory);
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await SendCurrentInputAsync();
        }
    }

    private void OnInputPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!InputBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            RestoreInputFocusAsync();
        }
    }

    private void RestoreInputFocusAsync()
    {
        if (_isRestoringInputFocus)
        {
            return;
        }

        _isRestoringInputFocus = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                InputBox.Focus();
                Keyboard.Focus(InputBox);
            }
            finally
            {
                _isRestoringInputFocus = false;
            }
        }), DispatcherPriority.Input);
    }

    private static void ProbeLifecycle(string message)
    {
        try
        {
            var directory = @"F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto";
            Directory.CreateDirectory(directory);
            var line = $"{DateTimeOffset.Now:O} AgentConsolePalette {message}{Environment.NewLine}";
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
