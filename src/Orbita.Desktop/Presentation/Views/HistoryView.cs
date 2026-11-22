using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orbita.Desktop.Presentation.Views;

public sealed class HistoryView : UserControl
{
    private readonly HistoryViewModel _viewModel;
    private readonly StackPanel _items = new() { Spacing = 12 };
    private readonly Action<Exception> _error;

    public HistoryView(HistoryViewModel viewModel, Action<Exception> error)
    {
        _viewModel = viewModel;
        _error = error;
        var page = new StackPanel { Spacing = 24, MaxWidth = 1040, HorizontalAlignment = HorizontalAlignment.Stretch };
        page.Children.Add(Ui.Heading(viewModel.L, "HistoryTitle", "HistoryDescription"));
        var refresh = Ui.Button(viewModel.L, "Refresh", "\uE72C");
        refresh.Click += async (_, _) => await LoadAsync();
        page.Children.Add(refresh);
        page.Children.Add(_items);
        Content = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Loaded += async (_, _) => { viewModel.PropertyChanged += OnChanged; await LoadAsync(); };
        Unloaded += (_, _) => viewModel.PropertyChanged -= OnChanged;
    }

    private async Task LoadAsync()
    {
        try { await _viewModel.LoadAsync(); }
        catch (Exception exception) { _error(exception); }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs arguments) => Render();

    private void Render()
    {
        _items.Children.Clear();
        if (_viewModel.Operations.Count == 0) { _items.Children.Add(Ui.Card(Ui.Text(_viewModel.L, "HistoryEmpty", 15, .65))); return; }
        foreach (var operation in _viewModel.Operations)
        {
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(new TextBlock { Text = operation.InputName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = $"{_viewModel.L[operation.ActionLabelKey]} · {FileSizeFormatter.Format(operation.OutputBytes, _viewModel.L.Culture)} · {operation.CompletedAt.ToLocalTime().ToString("g", _viewModel.L.Culture)}", Opacity = .6 });
            var button = Ui.Button(_viewModel.L, "OpenOutput", "\uE8A7");
            button.Click += async (_, _) =>
            {
                try
                {
                    var directory = Path.GetDirectoryName(operation.Outputs.FirstOrDefault());
                    if (directory is null || !Directory.Exists(directory)) throw new DirectoryNotFoundException();
                    await Windows.System.Launcher.LaunchFolderPathAsync(directory);
                }
                catch (Exception exception) { _error(exception); }
            };
            content.Children.Add(button);
            _items.Children.Add(Ui.Card(content));
        }
    }
}
