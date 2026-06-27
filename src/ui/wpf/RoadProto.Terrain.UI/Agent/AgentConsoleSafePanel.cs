using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RoadProto.Terrain.UI.Agent;

public sealed class AgentConsoleSafePanel : UserControl
{
    private readonly AgentConsoleViewModel _viewModel = new();
    private readonly TextBlock _statusText;
    private readonly TextBlock _traceText;
    private readonly ComboBox _providerBox;
    private TextBox _baseUrlBox = null!;
    private TextBox _modelBox = null!;
    private PasswordBox _apiKeyBox = null!;
    private CheckBox _enabledBox = null!;
    private readonly TextBox _messagesBox;
    private readonly TextBox _inputBox;
    private readonly Border _confirmPanel;
    private readonly Border _pickTargetPanel;
    private readonly TextBox _logBox;
    private bool _isSyncing;
    private bool _isRestoringInputFocus;

    public AgentConsoleSafePanel()
    {
        ProbeLifecycle("Before Build");
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var root = new Grid
        {
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var providerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        DockPanel.SetDock(providerPanel, Dock.Right);
        providerPanel.Children.Add(new TextBlock
        {
            Text = "Provider",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        _providerBox = new ComboBox
        {
            Width = 110,
            Height = 28,
            ItemsSource = _viewModel.Providers,
        };
        _providerBox.SelectionChanged += (_, _) =>
        {
            if (!_isSyncing && _providerBox.SelectedItem is string provider)
            {
                _viewModel.SelectedProvider = provider;
                RefreshFromViewModel();
            }
        };
        providerPanel.Children.Add(_providerBox);
        header.Children.Add(providerPanel);

        var titlePanel = new StackPanel();
        titlePanel.Children.Add(new TextBlock
        {
            Text = "Agent 控制台",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
        });
        _statusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
            FontSize = 12,
        };
        _traceText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
            FontSize = 12,
        };
        titlePanel.Children.Add(_statusText);
        titlePanel.Children.Add(_traceText);
        header.Children.Add(titlePanel);

        var settingsPanel = BuildSettingsPanel();
        Grid.SetRow(settingsPanel, 1);
        root.Children.Add(settingsPanel);

        var messagePanel = new Grid();
        messagePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        messagePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(messagePanel, 2);
        root.Children.Add(messagePanel);

        _messagesBox = new TextBox
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 225, 236)),
            Background = Brushes.White,
            Padding = new Thickness(8),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(_messagesBox, 0);
        messagePanel.Children.Add(_messagesBox);

        _confirmPanel = BuildConfirmPanel();
        Grid.SetRow(_confirmPanel, 1);
        messagePanel.Children.Add(_confirmPanel);

        _pickTargetPanel = BuildPickTargetPanel();
        Grid.SetRow(_pickTargetPanel, 1);
        messagePanel.Children.Add(_pickTargetPanel);

        var inputPanel = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(inputPanel, 3);
        root.Children.Add(inputPanel);

        _inputBox = new TextBox
        {
            MinHeight = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
        };
        _inputBox.PreviewKeyDown += OnInputPreviewKeyDown;
        _inputBox.PreviewMouseDown += OnInputPreviewMouseDown;
        inputPanel.Children.Add(_inputBox);

        var sendButton = new Button
        {
            Width = 68,
            Margin = new Thickness(8, 0, 0, 0),
            Content = "发送",
        };
        sendButton.Click += async (_, _) => await SendCurrentInputAsync();
        Grid.SetColumn(sendButton, 1);
        inputPanel.Children.Add(sendButton);

        _logBox = new TextBox
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 225, 236)),
            Background = new SolidColorBrush(Color.FromRgb(251, 252, 254)),
            Padding = new Thickness(6),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(_logBox, 4);
        root.Children.Add(_logBox);

        Content = root;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        RefreshFromViewModel();
        ProbeLifecycle("After Build");
    }

    private Grid BuildSettingsPanel()
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddLabel(panel, "BaseUrl", 0);
        _baseUrlBox = AddTextBox(panel, 0);
        _baseUrlBox.TextChanged += (_, _) =>
        {
            if (!_isSyncing)
            {
                _viewModel.ProviderBaseUrl = _baseUrlBox.Text;
            }
        };

        AddLabel(panel, "Model", 1);
        _modelBox = AddTextBox(panel, 1);
        _modelBox.TextChanged += (_, _) =>
        {
            if (!_isSyncing)
            {
                _viewModel.ProviderModel = _modelBox.Text;
            }
        };

        AddLabel(panel, "API Key", 2);
        _apiKeyBox = new PasswordBox { Margin = new Thickness(0, 4, 0, 0) };
        Grid.SetRow(_apiKeyBox, 2);
        Grid.SetColumn(_apiKeyBox, 1);
        Grid.SetColumnSpan(_apiKeyBox, 2);
        panel.Children.Add(_apiKeyBox);

        _enabledBox = new CheckBox
        {
            Content = "启用",
            Margin = new Thickness(0, 6, 0, 0),
        };
        _enabledBox.Checked += (_, _) =>
        {
            if (!_isSyncing)
            {
                _viewModel.ProviderEnabled = true;
            }
        };
        _enabledBox.Unchecked += (_, _) =>
        {
            if (!_isSyncing)
            {
                _viewModel.ProviderEnabled = false;
            }
        };
        Grid.SetRow(_enabledBox, 3);
        Grid.SetColumn(_enabledBox, 1);
        panel.Children.Add(_enabledBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 4, 0, 0),
        };
        var logButton = new Button { Width = 74, Content = "日志目录" };
        logButton.Click += OnOpenRoadProtoLogsClick;
        buttonPanel.Children.Add(logButton);
        var saveButton = new Button
        {
            Width = 68,
            Margin = new Thickness(6, 0, 0, 0),
            Content = "保存",
        };
        saveButton.Click += async (_, _) => await SaveProviderAsync();
        buttonPanel.Children.Add(saveButton);
        Grid.SetRow(buttonPanel, 3);
        Grid.SetColumn(buttonPanel, 2);
        panel.Children.Add(buttonPanel);

        return panel;
    }

    private Border BuildConfirmPanel()
    {
        var border = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(238, 244, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(191, 210, 248)),
            BorderThickness = new Thickness(1),
        };

        var dock = new DockPanel { LastChildFill = true };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(10, 0, 0, 0),
        };
        DockPanel.SetDock(buttonPanel, Dock.Right);

        var confirmButton = new Button { Width = 76, Content = "确认执行" };
        confirmButton.Click += async (_, _) => await ConfirmAsync();
        buttonPanel.Children.Add(confirmButton);
        var cancelButton = new Button
        {
            Width = 64,
            Margin = new Thickness(6, 0, 0, 0),
            Content = "取消",
        };
        cancelButton.Click += async (_, _) => await CancelAsync();
        buttonPanel.Children.Add(cancelButton);
        dock.Children.Add(buttonPanel);
        dock.Children.Add(new TextBlock
        {
            Text = "本次操作需要确认",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
        });
        border.Child = dock;
        return border;
    }

    private Border BuildPickTargetPanel()
    {
        var border = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(238, 244, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(191, 210, 248)),
            BorderThickness = new Thickness(1),
        };

        var dock = new DockPanel { LastChildFill = true };
        var pickButton = new Button
        {
            Width = 64,
            Content = "点选",
            Margin = new Thickness(10, 0, 0, 0),
        };
        pickButton.Click += async (_, _) => await PickTargetAsync();
        DockPanel.SetDock(pickButton, Dock.Right);
        dock.Children.Add(pickButton);
        dock.Children.Add(new TextBlock
        {
            Text = "请选择要操作的路基模板，或在输入框里输入模板名称。",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
        });
        border.Child = dock;
        return border;
    }

    private static void AddLabel(Grid panel, string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = row == 0 ? new Thickness(0) : new Thickness(0, 4, 0, 0),
        };
        Grid.SetRow(label, row);
        panel.Children.Add(label);
    }

    private static TextBox AddTextBox(Grid panel, int row)
    {
        var box = new TextBox { Margin = row == 0 ? new Thickness(0) : new Thickness(0, 4, 0, 0) };
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        Grid.SetColumnSpan(box, 2);
        panel.Children.Add(box);
        return box;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProbeLifecycle("Loaded");
        Loaded -= OnLoaded;
        _ = Dispatcher.BeginInvoke(new Action(async () =>
        {
            ProbeLifecycle("Before ViewModelInitialize");
            await InitializeAfterHostAsync();
            ProbeLifecycle("After ViewModelInitialize");
        }), DispatcherPriority.ContextIdle);
    }

    private async Task InitializeAfterHostAsync()
    {
        try
        {
            await _viewModel.InitializeAsync();
            RefreshFromViewModel();
        }
        catch (Exception ex)
        {
            ProbeLifecycle("ViewModelInitializeException " + ex);
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(RefreshFromViewModel), DispatcherPriority.Background);
    }

    private void RefreshFromViewModel()
    {
        _isSyncing = true;
        try
        {
            _statusText.Text = _viewModel.BackendStatus;
            _traceText.Text = "TraceId: " + _viewModel.TraceId;
            _providerBox.SelectedItem = _viewModel.SelectedProvider;
            _baseUrlBox.Text = _viewModel.ProviderBaseUrl;
            _modelBox.Text = _viewModel.ProviderModel;
            _enabledBox.IsChecked = _viewModel.ProviderEnabled;
            _messagesBox.Text = _viewModel.MessagesText;
            _logBox.Text = _viewModel.LogText;
            _confirmPanel.Visibility = _viewModel.CanConfirm ? Visibility.Visible : Visibility.Collapsed;
            _pickTargetPanel.Visibility = _viewModel.CanPickTarget ? Visibility.Visible : Visibility.Collapsed;
            _messagesBox.ScrollToEnd();
            _logBox.ScrollToEnd();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SendCurrentInputAsync()
    {
        try
        {
            var text = _inputBox.Text;
            await _viewModel.SendAsync(text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                _inputBox.Clear();
            }

            RefreshFromViewModel();
            RestoreInputFocusAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async Task ConfirmAsync()
    {
        try
        {
            await _viewModel.ConfirmAsync();
            RefreshFromViewModel();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async Task CancelAsync()
    {
        try
        {
            await _viewModel.CancelAsync();
            RefreshFromViewModel();
            RestoreInputFocusAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async Task PickTargetAsync()
    {
        try
        {
            await _viewModel.PickTargetAsync();
            RefreshFromViewModel();
            RestoreInputFocusAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Messages.Add("Agent: " + ex.Message);
        }
    }

    private async Task SaveProviderAsync()
    {
        try
        {
            await _viewModel.SaveProviderAsync(_apiKeyBox.Password);
            _apiKeyBox.Clear();
            RefreshFromViewModel();
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
        if (!_inputBox.IsKeyboardFocusWithin)
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
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _inputBox.Focus();
                Keyboard.Focus(_inputBox);
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
            var line = $"{DateTimeOffset.Now:O} AgentConsoleSafePanel {message}{Environment.NewLine}";
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
