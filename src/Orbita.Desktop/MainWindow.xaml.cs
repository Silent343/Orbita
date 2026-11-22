using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Orbita.Desktop.Presentation;
using Orbita.Desktop.Presentation.Views;
using Orbita.Desktop.Services;
using Orbita.Desktop.WindowsIntegration;
using Orbita.Files.Domain;
using Orbita.Localization;
using Windows.Graphics;

namespace Orbita.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly WorkspaceViewModel _workspace;
    private readonly PreferencesState _preferences;
    private readonly Diagnostics _diagnostics;
    private readonly FilePickerService _picker;
    private readonly ExplorerShortcutService _shortcut;
    private readonly OverlayWindow _overlay;
    private string _activePage = "workspace";
    private string? _noticeKey;
    private bool _closing;
    private bool _actionDialogOpen;
    private string? _theme;
    public ILocalizer L { get; }

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        L = services.GetRequiredService<ILocalizer>();
        _workspace = services.GetRequiredService<WorkspaceViewModel>();
        _preferences = services.GetRequiredService<PreferencesState>();
        _diagnostics = services.GetRequiredService<Diagnostics>();
        _shortcut = services.GetRequiredService<ExplorerShortcutService>();
        _picker = new FilePickerService(this);
        InitializeComponent();
        Root.DataContext = this;
        Title = L["AppName"];
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.Resize(new SizeInt32(1280, 1000));
        _overlay = new OverlayWindow(_workspace, L);
        ApplyPreferences();

        _overlay.ActionRequested += async (_, action) => await ExecuteActionAsync(action);
        _overlay.OpenApplicationRequested += (_, _) => { Activate(); ShowPage("workspace"); };
        _workspace.Warning += (_, key) => ShowNotice(key, InfoBarSeverity.Warning);
        _preferences.Changed += PreferencesChanged;
        L.LanguageChanged += LanguageChanged;
        _shortcut.FileRequested += ShortcutFileRequested;
        _shortcut.Released += (_, _) => _overlay.Hide();
        _shortcut.Start(DispatcherQueue);
        Navigation.SelectedItem = WorkspaceItem;
        AppWindow.Closing += (_, arguments) =>
        {
            if (!_workspace.IsBusy) return;
            arguments.Cancel = true;
            _closing = true;
            _workspace.Cancel();
        };
        _workspace.PropertyChanged += WorkspacePropertyChanged;
        Closed += OnClosed;
    }

    private void WorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs arguments)
    {
        if (_closing && !_workspace.IsBusy && arguments.PropertyName == nameof(_workspace.IsBusy))
            DispatcherQueue.TryEnqueue(Close);
    }

    private void LanguageChanged(object? sender, EventArgs arguments)
    {
        Title = L["AppName"];
        if (_noticeKey is not null) NoticeBar.Message = L[_noticeKey];
    }

    private void PreferencesChanged(object? sender, EventArgs arguments) => ApplyPreferences();

    private void ApplyPreferences()
    {
        var theme = _preferences.Current.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        Root.RequestedTheme = theme;
        Ui.Theme = theme == ElementTheme.Light || (theme == ElementTheme.Default
            && Application.Current.RequestedTheme == ApplicationTheme.Light) ? "Light" : "Dark";
        _overlay.ApplyTheme(theme);
        if (_theme != _preferences.Current.Theme)
        {
            _theme = _preferences.Current.Theme;
            ShowPage(_activePage);
        }
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs arguments)
    {
        if (arguments.SelectedItem is NavigationViewItem { Tag: string page }) ShowPage(page);
    }

    internal void ShowPage(string page)
    {
        _activePage = page;
        var selected = Navigation.MenuItems.Concat(Navigation.FooterMenuItems).OfType<NavigationViewItem>()
            .FirstOrDefault(item => (string)item.Tag == page);
        if (selected is not null && !ReferenceEquals(Navigation.SelectedItem, selected))
        {
            Navigation.SelectedItem = selected;
            return;
        }
        PageHost.Content = page switch
        {
            "history" => new HistoryView(_services.GetRequiredService<HistoryViewModel>(), ShowError),
            "settings" => new SettingsView(_services.GetRequiredService<SettingsViewModel>(), _picker,
                _services.GetRequiredService<AppPaths>(), ShowError),
            _ => CreateWorkspace()
        };
    }

    private WorkspaceView CreateWorkspace()
    {
        var view = new WorkspaceView(_workspace, _picker, ShowError);
        view.ActionRequested += async (_, action) => await ExecuteActionAsync(action);
        return view;
    }

    private async void ShortcutFileRequested(object? sender, string path)
    {
        try
        {
            if (_workspace.IsBusy || _actionDialogOpen) return;
            await _workspace.SelectAsync(path);
            if (_workspace.File?.Path == path && !_closing) _overlay.ShowAtCursor();
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async Task ExecuteActionAsync(FileAction action)
    {
        if (_workspace.IsBusy || _actionDialogOpen) return;
        try
        {
            if (!action.Available) { ShowNotice(action.UnavailableReasonKey ?? "ErrorActionUnavailable", InfoBarSeverity.Warning); return; }
            ImageEditOptions? options = null;
            if (action.Id is "image.crop" or "image.resize" && _workspace.File is not null)
            {
                _actionDialogOpen = true;
                Activate();
                options = await ImageOptionsDialog.ShowAsync(Root.XamlRoot, _workspace.File, action.Id == "image.crop", L);
                _actionDialogOpen = false;
                if (options is null) return;
            }
            await _workspace.ExecuteAsync(action, options);
            Activate();
        }
        catch (Exception exception) { _actionDialogOpen = false; ShowError(exception); }
    }

    public void ShowNotice(string key, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        _noticeKey = key;
        NoticeBar.Message = L[key];
        NoticeBar.Severity = severity;
        NoticeBar.IsOpen = true;
    }

    private void ShowError(Exception exception)
    {
        _diagnostics.Log(exception);
        ShowNotice(Diagnostics.ResourceKey(exception), InfoBarSeverity.Error);
    }

    private void OnClosed(object sender, WindowEventArgs arguments)
    {
        _closing = true;
        _workspace.Cancel();
        _shortcut.Dispose();
        _overlay.Close();
        _preferences.Changed -= PreferencesChanged;
        L.LanguageChanged -= LanguageChanged;
        _shortcut.FileRequested -= ShortcutFileRequested;
        _workspace.PropertyChanged -= WorkspacePropertyChanged;
    }

    internal FrameworkElement VerificationRoot => Root;
    internal OverlayWindow VerificationOverlay => _overlay;
}
