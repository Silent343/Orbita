using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Orbita.Desktop.Services;
using Orbita.Preferences.Domain;

namespace Orbita.Desktop.Presentation.Views;

public sealed class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;
    private readonly Action<Exception> _error;

    public SettingsView(SettingsViewModel viewModel, FilePickerService picker, AppPaths paths, Action<Exception> error)
    {
        _viewModel = viewModel;
        _error = error;
        var page = new StackPanel { Spacing = 22, MaxWidth = 1040, HorizontalAlignment = HorizontalAlignment.Stretch };
        page.Children.Add(Ui.Heading(viewModel.L, "SettingsTitle", "SettingsDescription"));

        var language = CreateChoice("Language", [("es", "LanguageSpanish"), ("en", "LanguageEnglish")], viewModel.Current.Language);
        language.SelectionChanged += async (_, _) =>
        {
            if (language.SelectedItem is ComboBoxItem { Tag: string value })
                await SaveAsync(current => current with { Language = value });
        };
        page.Children.Add(Setting("Language", "LanguageDescription", language));

        var theme = CreateChoice("Appearance", [("dark", "ThemeDark"), ("light", "ThemeLight"), ("system", "ThemeSystem")], viewModel.Current.Theme);
        theme.SelectionChanged += async (_, _) =>
        {
            if (theme.SelectedItem is ComboBoxItem { Tag: string value })
                await SaveAsync(current => current with { Theme = value });
        };
        page.Children.Add(Setting("Appearance", null, theme));

        var shortcut = new ToggleSwitch { IsOn = viewModel.Current.ExplorerShortcutEnabled };
        shortcut.OnContent = "";
        shortcut.OffContent = "";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(shortcut, viewModel.L["ExplorerShortcut"]);
        shortcut.Toggled += async (_, _) => await SaveAsync(current => current with { ExplorerShortcutEnabled = shortcut.IsOn });
        page.Children.Add(Setting("ExplorerShortcut", "ExplorerShortcutDescription", shortcut));

        var delay = new NumberBox { Value = viewModel.Current.ShiftHoldMilliseconds, Minimum = 200, Maximum = 1500,
            SmallChange = 50, LargeChange = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 190 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(delay, viewModel.L["ShortcutDelay"]);
        delay.LostFocus += async (_, _) =>
        {
            if (double.IsFinite(delay.Value)) await SaveAsync(current => current with { ShiftHoldMilliseconds = (int)delay.Value });
        };
        page.Children.Add(Setting("ShortcutDelay", null, delay));

        var folderSection = new StackPanel { Spacing = 12 };
        folderSection.Children.Add(Ui.Text(viewModel.L, "OutputFolder", 16));
        var outputPath = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = .65 };
        Ui.Bind(outputPath, TextBlock.TextProperty, viewModel, nameof(viewModel.OutputDescription));
        folderSection.Children.Add(outputPath);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var folder = Ui.Button(viewModel.L, "ChooseFolder", "\uE8B7");
        folder.Click += async (_, _) =>
        {
            try
            {
                var path = await picker.PickFolderAsync();
                if (path is not null) await SaveAsync(current => current with { OutputDirectory = path });
            }
            catch (Exception exception) { error(exception); }
        };
        var reset = Ui.Button(viewModel.L, "UseDefault");
        reset.Click += async (_, _) => await SaveAsync(current => current with { OutputDirectory = null });
        buttons.Children.Add(folder);
        buttons.Children.Add(reset);
        folderSection.Children.Add(buttons);
        page.Children.Add(Ui.Card(folderSection));

        var about = new StackPanel { Spacing = 8 };
        about.Children.Add(Ui.Text(viewModel.L, "About", 16));
        about.Children.Add(Ui.Text(viewModel.L, "AboutDescription", 12, .6));
        var log = Ui.Button(viewModel.L, "DiagnosticLog", "\uE946");
        log.Click += async (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(paths.DataDirectory);
                await Windows.System.Launcher.LaunchFolderPathAsync(paths.DataDirectory);
            }
            catch (Exception exception) { error(exception); }
        };
        about.Children.Add(log);
        page.Children.Add(Ui.Card(about));
        Content = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private async Task SaveAsync(Func<UserPreferences, UserPreferences> update)
    {
        try { await _viewModel.SaveAsync(update); }
        catch (Exception exception) { _error(exception); }
    }

    private ComboBox CreateChoice(string label, (string Value, string Label)[] options, string selected)
    {
        var combo = new ComboBox { MinWidth = 190 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, _viewModel.L[label]);
        foreach (var option in options)
        {
            var item = new ComboBoxItem { Tag = option.Value, Content = Ui.Text(_viewModel.L, option.Label) };
            combo.Items.Add(item);
            if (option.Value == selected) combo.SelectedItem = item;
        }
        return combo;
    }

    private Border Setting(string label, string? description, FrameworkElement control)
    {
        var grid = new Grid { ColumnSpacing = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.Text(_viewModel.L, label, 16));
        if (description is not null) text.Children.Add(Ui.Text(_viewModel.L, description, 12, .6));
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return Ui.Card(grid);
    }
}
