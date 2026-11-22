using Microsoft.Extensions.DependencyInjection;
using Orbita.Activity.Application;
using Orbita.Activity.Infrastructure;
using Orbita.Desktop.Presentation;
using Orbita.Desktop.Services;
using Orbita.Desktop.WindowsIntegration;
using Orbita.Files.Application;
using Orbita.Files.Infrastructure;
using Orbita.Localization;
using Orbita.Preferences.Application;
using Orbita.Preferences.Infrastructure;

namespace Orbita.Desktop.Composition;

public static class ServiceRegistration
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AppPaths>();
        services.AddSingleton<Diagnostics>();
        services.AddSingleton<ILocalizer, JsonLocalizer>();

        services.AddSingleton<IPreferencesRepository>(provider =>
            new JsonPreferencesRepository(provider.GetRequiredService<AppPaths>().Preferences));
        services.AddSingleton<PreferencesService>();
        services.AddSingleton<PreferencesState>();

        services.AddSingleton<IActivityRepository>(provider =>
            new JsonActivityRepository(provider.GetRequiredService<AppPaths>().Activity));
        services.AddSingleton<ActivityService>();

        services.AddSingleton<IFileInspector, LocalFileInspector>();
        services.AddSingleton<IActionCatalog, ActionCatalog>();
        services.AddSingleton<IFileActionHandler, ArchiveActionHandler>();
        services.AddSingleton<IFileActionHandler, TextActionHandler>();
        services.AddSingleton<IFileActionHandler, ImageActionHandler>();
        services.AddSingleton<IFileActionHandler, PdfActionHandler>();
        services.AddSingleton<IFileActions, FileActions>();
        services.AddSingleton<OperationCoordinator>();

        services.AddSingleton<WorkspaceViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<IExplorerSelectionReader, ExplorerSelectionReader>();
        services.AddSingleton<ExplorerShortcutService>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
