using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Orbita.Desktop.Presentation.Controls;
using Orbita.Desktop.Services;
using Orbita.Files.Domain;
using Windows.ApplicationModel.DataTransfer;

namespace Orbita.Desktop.Presentation.Views;

public sealed class WorkspaceView : UserControl
{
    private readonly WorkspaceViewModel _viewModel;
    private readonly RadialWheel _wheel;
    private readonly ProgressRing _progress = new() { Width = 20, Height = 20, IsActive = false };
    private readonly Button _pick;
    private readonly Button _cancel;
    private readonly Button _result;
    private readonly Button _convert;
    private readonly Button _tools;
    private readonly TextBlock _notice;
    public event EventHandler<FileAction>? ActionRequested;

    public WorkspaceView(WorkspaceViewModel viewModel, FilePickerService picker, Action<Exception> error)
    {
        _viewModel = viewModel;
        var page = new StackPanel { Spacing = 22, MaxWidth = 1040, HorizontalAlignment = HorizontalAlignment.Stretch };
        var eyebrow = Ui.Text(viewModel.L, "WorkspaceEyebrow", 11, .7);
        eyebrow.CharacterSpacing = 160;
        eyebrow.Foreground = Ui.ResourceBrush("OrbitAccentBrush");
        page.Children.Add(eyebrow);
        page.Children.Add(Ui.Heading(viewModel.L, "WorkspaceTitle", "WorkspaceDescription"));

        var surface = new Grid { RowSpacing = 6 };
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var drop = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        drop.Children.Add(Ui.Text(viewModel.L, "DropFile", 15));
        drop.Children.Add(Ui.Text(viewModel.L, "DropHint", 12, .6));
        top.Children.Add(drop);
        _pick = Ui.Button(viewModel.L, "SelectFile", "\uE8E5");
        _pick.Click += async (_, _) =>
        {
            try { var path = await picker.PickFileAsync(); if (path is not null) await viewModel.SelectAsync(path); }
            catch (Exception exception) { error(exception); }
        };
        Grid.SetColumn(_pick, 1);
        top.Children.Add(_pick);
        surface.Children.Add(top);

        _wheel = new RadialWheel(viewModel);
        _wheel.ActionRequested += (_, action) => ActionRequested?.Invoke(this, action);
        Grid.SetRow(_wheel, 1);
        surface.Children.Add(_wheel);

        var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        _convert = Ui.Button(viewModel.L, "Conversions");
        _tools = Ui.Button(viewModel.L, "Tools");
        _convert.Click += (_, _) => viewModel.SwitchGroup(ActionGroup.Convert);
        _tools.Click += (_, _) => viewModel.SwitchGroup(ActionGroup.Tools);
        modes.Children.Add(_convert);
        modes.Children.Add(_tools);
        Grid.SetRow(modes, 2);
        surface.Children.Add(modes);
        var hint = Ui.Text(viewModel.L, "WheelHint", 12, .55);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(hint, 3);
        surface.Children.Add(hint);

        var card = Ui.Card(surface);
        card.AllowDrop = true;
        card.DragOver += (_, arguments) =>
        {
            arguments.AcceptedOperation = !_viewModel.IsBusy && arguments.DataView.Contains(StandardDataFormats.StorageItems)
                ? DataPackageOperation.Copy : DataPackageOperation.None;
        };
        card.Drop += async (_, arguments) =>
        {
            try
            {
                var items = await arguments.DataView.GetStorageItemsAsync();
                if (items.Count == 1 && items[0] is Windows.Storage.StorageFile file) await viewModel.SelectAsync(file.Path);
                else throw new Orbita.SharedKernel.DomainException("ErrorOneFile");
            }
            catch (Exception exception) { error(exception); }
        };
        page.Children.Add(card);

        _notice = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = .65, FontSize = 12 };
        page.Children.Add(_notice);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        footer.Children.Add(_progress);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 500 };
        Ui.Bind(status, TextBlock.TextProperty, viewModel, nameof(viewModel.Status));
        footer.Children.Add(status);
        _cancel = Ui.Button(viewModel.L, "Cancel");
        _cancel.Click += (_, _) => viewModel.Cancel();
        footer.Children.Add(_cancel);
        _result = Ui.Button(viewModel.L, "OpenOutput", "\uE8A7");
        _result.Click += async (_, _) =>
        {
            try { if (viewModel.LastOutputs.Count > 0) await Windows.System.Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(viewModel.LastOutputs[0])!); }
            catch (Exception exception) { error(exception); }
        };
        footer.Children.Add(_result);
        page.Children.Add(footer);
        var privacy = new StackPanel { Spacing = 7, Margin = new Thickness(0, 6, 0, 8) };
        privacy.Children.Add(Ui.Text(viewModel.L, "LocalDescription", 12, .55));
        privacy.Children.Add(Ui.Text(viewModel.L, "ShortcutHint", 12, .55));
        page.Children.Add(privacy);
        Content = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Loaded += (_, _) => { viewModel.PropertyChanged += OnChanged; UpdateState(); };
        Unloaded += (_, _) => viewModel.PropertyChanged -= OnChanged;
        UpdateState();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs arguments) => UpdateState();

    private void UpdateState()
    {
        _progress.IsActive = _viewModel.IsBusy;
        _progress.Visibility = _viewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        _cancel.Visibility = _viewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        _pick.IsEnabled = !_viewModel.IsBusy;
        _result.Visibility = !_viewModel.IsBusy && _viewModel.LastOutputs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _convert.IsEnabled = !_viewModel.IsBusy && _viewModel.Group != ActionGroup.Convert;
        _tools.IsEnabled = !_viewModel.IsBusy && _viewModel.Group != ActionGroup.Tools;
        _notice.Text = _viewModel.File?.Category switch
        {
            FileCategory.Pdf => _viewModel.L["PdfTextNotice"] + " " + _viewModel.L["RequiresDocumentEngine"],
            FileCategory.Image => _viewModel.L["JpegNotice"],
            _ => ""
        };
        _notice.Visibility = _notice.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
