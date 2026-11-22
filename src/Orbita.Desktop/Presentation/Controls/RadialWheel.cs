using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Orbita.Files.Domain;

namespace Orbita.Desktop.Presentation.Controls;

public sealed class RadialWheel : UserControl
{
    private readonly WorkspaceViewModel _viewModel;
    private readonly Canvas _canvas = new() { Width = 640, Height = 590 };
    private long _lastWheel;
    public event EventHandler<FileAction>? ActionRequested;

    public RadialWheel(WorkspaceViewModel viewModel)
    {
        _viewModel = viewModel;
        Content = new Viewbox { Child = _canvas, Stretch = Stretch.Uniform, MaxHeight = 520 };
        Loaded += (_, _) => { _viewModel.PropertyChanged += OnChanged; Render(); };
        Unloaded += (_, _) => _viewModel.PropertyChanged -= OnChanged;
        PointerWheelChanged += OnWheelChanged;
        Render();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs arguments) => Render();

    public void Render()
    {
        _canvas.Children.Clear();
        var accent = Ui.ResourceBrush("OrbitAccentBrush");
        var line = Ui.ResourceBrush("GlassBorderBrush");
        var ring = new Ellipse { Width = 420, Height = 420, Stroke = line, StrokeThickness = 1 };
        Place(ring, 110, 85);
        var outer = new Ellipse { Width = 448, Height = 448, Stroke = line, StrokeThickness = .5,
            StrokeDashArray = new DoubleCollection { 2, 9 }, Opacity = .6 };
        Place(outer, 96, 71);

        var centerContent = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Width = 164 };
        var glyph = new FontIcon { Glyph = _viewModel.File?.Category == FileCategory.Image ? "\uEB9F" : "\uE8A5",
            FontSize = 32, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center };
        centerContent.Children.Add(glyph);
        centerContent.Children.Add(new TextBlock { Text = _viewModel.FileName, FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Opacity = .65 });
        centerContent.Children.Add(new TextBlock { Text = _viewModel.FileSize, FontSize = 29,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextAlignment = TextAlignment.Center });
        centerContent.Children.Add(new TextBlock { Text = _viewModel.File?.Extension.TrimStart('.').ToUpperInvariant() ?? "—",
            FontSize = 11, CharacterSpacing = 180, TextAlignment = TextAlignment.Center, Foreground = accent });
        var center = new Border { Width = 196, Height = 196, CornerRadius = new CornerRadius(98),
            Background = Ui.ResourceBrush("OrbitCenterBrush"), BorderBrush = line, BorderThickness = new Thickness(1),
            Child = centerContent };
        ToolTipService.SetToolTip(center, _viewModel.File?.Path ?? _viewModel.L["NoFile"]);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(center, $"{_viewModel.FileName}, {_viewModel.FileSize}");
        Place(center, 222, 197);

        var available = _viewModel.Actions;
        for (var index = 0; index < available.Count; index++)
        {
            var action = available[index];
            var angle = -Math.PI / 2 + index * 2 * Math.PI / Math.Max(available.Count, 1);
            var label = Ui.Text(_viewModel.L, action.LabelKey, 12);
            label.TextAlignment = TextAlignment.Center;
            var stack = new StackPanel { Spacing = 9, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new FontIcon { Glyph = action.Glyph, FontSize = 23 });
            stack.Children.Add(label);
            var button = new Button { Width = 112, Height = 80, Content = stack,
                Padding = new Thickness(7), CornerRadius = new CornerRadius(20),
                Background = Ui.ResourceBrush("OrbitCenterBrush"), BorderBrush = line,
                IsEnabled = action.Available && !_viewModel.IsBusy };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, _viewModel.L[action.LabelKey]);
            ToolTipService.SetToolTip(button, _viewModel.L[action.UnavailableReasonKey ?? action.LabelKey]);
            button.Click += (_, _) => ActionRequested?.Invoke(this, action);
            Place(button, 320 + Math.Cos(angle) * 210 - 56, 295 + Math.Sin(angle) * 210 - 40);
        }
        if (_viewModel.File is null)
        {
            var text = Ui.Text(_viewModel.L, "NoFile", 14, .6);
            text.TextAlignment = TextAlignment.Center;
            text.Width = 400;
            Place(text, 120, 520);
        }
    }

    private void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        _canvas.Children.Add(element);
    }

    private void OnWheelChanged(object sender, PointerRoutedEventArgs arguments)
    {
        var shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (!shift) return;
        arguments.Handled = true;
        if (Environment.TickCount64 - _lastWheel < 180) return;
        _lastWheel = Environment.TickCount64;
        var delta = arguments.GetCurrentPoint(this).Properties.MouseWheelDelta;
        _viewModel.SwitchGroup(delta < 0 ? ActionGroup.Tools : ActionGroup.Convert);
    }
}
