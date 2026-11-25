using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Orbita.Desktop.Presentation;
using Orbita.Desktop.Presentation.Controls;
using Orbita.Desktop.Presentation.Views;
using Orbita.Desktop.Services;
using Orbita.Desktop.WindowsIntegration;
using Orbita.Files.Domain;
using Orbita.Files.Application;
using Orbita.Localization;
using Windows.Graphics;
using Forms = System.Windows.Forms;

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
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ContextMenuStrip _trayMenu;
    private readonly Forms.ToolStripMenuItem _traySettings;
    private readonly Forms.ToolStripMenuItem _trayExit;
    private string _activePage = "workspace";
    private string? _noticeKey;
    private bool _closing;
    private bool _actionDialogOpen;
    private string? _theme;
    public ILocalizer L { get; }

    public MainWindow(IServiceProvider services, bool enableTray = true)
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
        _overlay = new OverlayWindow(services.GetRequiredService<IActionCatalog>(), L, DispatcherQueue, ShowError);
        _traySettings = new Forms.ToolStripMenuItem();
        _traySettings.Click += (_, _) => { Activate(); ShowPage("settings"); };
        _trayExit = new Forms.ToolStripMenuItem();
        _trayExit.Click += (_, _) => RequestExit();
        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.AddRange([_traySettings, _trayExit]);
        _tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = _trayMenu };
        _tray.DoubleClick += (_, _) => { Activate(); ShowPage("settings"); };
        UpdateTrayLanguage();
        _tray.Visible = enableTray;
        ApplyPreferences();

        _overlay.ActionRequested += async (_, action) => await ExecuteActionAsync(action, fromOverlay: true);
        _workspace.Warning += (_, key) => ShowNotice(key, InfoBarSeverity.Warning);
        _preferences.Changed += PreferencesChanged;
        L.LanguageChanged += LanguageChanged;
        _shortcut.FileRequested += ShortcutFileRequested;
        _shortcut.Released += (_, _) => _overlay.Hide();
        _shortcut.Start(DispatcherQueue);
        Navigation.SelectedItem = WorkspaceItem;
        AppWindow.Closing += (_, arguments) =>
        {
            if (_closing) return;
            arguments.Cancel = true;
            AppWindow.Hide();
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
        UpdateTrayLanguage();
        if (_noticeKey is not null) NoticeBar.Message = L[_noticeKey];
    }

    private void UpdateTrayLanguage()
    {
        _tray.Text = L["AppName"];
        _traySettings.Text = L["NavSettings"];
        _trayExit.Text = L["ExitApplication"];
    }

    private void RequestExit()
    {
        _closing = true;
        if (_workspace.IsBusy) _workspace.Cancel();
        else Close();
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
        _overlay.ApplyTheme(Ui.Theme == "Light");
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
            if (!_closing && _workspace.File is { } file && string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase))
                _overlay.ShowAtCursor(file);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async Task ExecuteActionAsync(FileAction action, bool fromOverlay = false)
    {
        if (_workspace.IsBusy || _actionDialogOpen) return;
        try
        {
            if (!action.Available) { ShowNotice(action.UnavailableReasonKey ?? "ErrorActionUnavailable", InfoBarSeverity.Warning); return; }
            ImageEditOptions? options = null;
            IReadOnlyList<string>? additionalImages = null;
            if (_workspace.File is { } current && action.Id is "image.crop" or "image.resize" or "image.pdf" or "video.photos")
            {
                _actionDialogOpen = true;
                var wasVisible = AppWindow.IsVisible;
                Activate();
                try
                {
                    if (Root.XamlRoot is null)
                    {
                        var loaded = new TaskCompletionSource();
                        RoutedEventHandler onLoaded = (_, _) => loaded.TrySetResult();
                        Root.Loaded += onLoaded;
                        try { if (Root.XamlRoot is null) await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
                        finally { Root.Loaded -= onLoaded; }
                    }
                    var dialogRoot = Root.XamlRoot ?? throw new InvalidOperationException("The dialog host is unavailable.");
                    if (action.Id is "image.pdf" or "video.photos")
                    {
                        var dialog = new ContentDialog
                        {
                            XamlRoot = dialogRoot,
                            Title = L[action.LabelKey],
                            Content = L.Format(action.Id == "video.photos" ? "PhotosVideoInstructions" : "PhotosPdfInstructions", current.Name),
                            PrimaryButtonText = L["PhotosPdfAdd"],
                            SecondaryButtonText = L["PhotosPdfSingle"],
                            CloseButtonText = L["Cancel"],
                            DefaultButton = ContentDialogButton.Primary
                        };
                        switch (await dialog.ShowAsync())
                        {
                            case ContentDialogResult.Primary:
                                additionalImages = await _picker.PickImagesAsync();
                                if (additionalImages.Count == 0) return;
                                break;
                            case ContentDialogResult.Secondary:
                                break;
                            default:
                                return;
                        }
                    }
                    else
                    {
                        options = await ImageOptionsDialog.ShowAsync(dialogRoot, current, action.Id == "image.crop", L);
                        if (options is null) return;
                    }
                }
                finally
                {
                    _actionDialogOpen = false;
                    if (fromOverlay && !wasVisible) AppWindow.Hide();
                }
                if (!ReferenceEquals(current, _workspace.File)) return;
            }
            await _workspace.ExecuteAsync(action, options, additionalImages);
            if (fromOverlay && _tray.Visible)
                _tray.ShowBalloonTip(3500, L["AppName"], _workspace.Status, Forms.ToolTipIcon.None);
            else if (!_closing) Activate();
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
        _overlay.Dispose();
        _tray.Dispose();
        _trayMenu.Dispose();
        _preferences.Changed -= PreferencesChanged;
        L.LanguageChanged -= LanguageChanged;
        _shortcut.FileRequested -= ShortcutFileRequested;
        _workspace.PropertyChanged -= WorkspacePropertyChanged;
    }

    internal FrameworkElement VerificationRoot => Root;
    internal OverlayWindow VerificationOverlay => _overlay;
}
