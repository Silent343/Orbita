using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Orbita.Desktop.Composition;
using Orbita.Desktop.Services;

namespace Orbita.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, arguments) => new Diagnostics(new AppPaths()).Log(arguments.Exception);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs arguments)
    {
        var launchArguments = Environment.GetCommandLineArgs();
        var verificationIndex = Array.IndexOf(launchArguments, "--verify-ui");
        try
        {
            _services = ServiceRegistration.Build();
            var failedPreferences = false;
            try { await _services.GetRequiredService<PreferencesState>().LoadAsync(); }
            catch (Exception exception) { _services.GetRequiredService<Diagnostics>().Log(exception); failedPreferences = true; }
            _window = new MainWindow(_services);
            _window.Activate();
            if (failedPreferences) _window.ShowNotice("PreferencesLoadWarning", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning);
            if (verificationIndex >= 0 && verificationIndex + 1 < launchArguments.Length)
                await Verification.UiVerification.RunAsync(_window, _services, launchArguments[verificationIndex + 1]);
        }
        catch (Exception exception)
        {
            new Diagnostics(new AppPaths()).Log(exception);
            if (verificationIndex >= 0 && verificationIndex + 1 < launchArguments.Length)
            {
                var directory = launchArguments[verificationIndex + 1];
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "ui-verification.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { success = false, startupError = exception.ToString() }));
            }
            Exit();
        }
    }
}
