using System.Text.Json;
using Orbita.Activity.Domain;
using Orbita.Activity.Infrastructure;
using Orbita.Localization;
using Orbita.Preferences.Application;
using Orbita.Preferences.Domain;
using Orbita.Preferences.Infrastructure;
using Xunit;

namespace Orbita.Tests;

public sealed class LocalizationAndPersistenceTests
{
    [Fact]
    public void Language_changes_notify_existing_bindings_and_translate_content()
    {
        var localizer = new JsonLocalizer();
        var notifications = new List<string?>();
        localizer.PropertyChanged += (_, arguments) => notifications.Add(arguments.PropertyName);
        var spanish = localizer["WorkspaceTitle"];
        var observableTitle = localizer.Strings["WorkspaceTitle"];
        var valueUpdated = false;
        observableTitle.PropertyChanged += (_, arguments) => valueUpdated |= arguments.PropertyName == "Value";
        localizer.ChangeLanguage("en");
        Assert.NotEqual(spanish, localizer["WorkspaceTitle"]);
        Assert.Contains(string.Empty, notifications);
        Assert.Equal("Preferences", localizer["NavSettings"]);
        Assert.True(valueUpdated);
        Assert.Same(observableTitle, localizer.Strings["WorkspaceTitle"]);
        Assert.Equal(localizer["WorkspaceTitle"], observableTitle.Value);
    }

    [Fact]
    public void Both_languages_have_exactly_the_same_translation_keys()
    {
        var assembly = typeof(JsonLocalizer).Assembly;
        Dictionary<string, string> Read(string language)
        {
            using var stream = assembly.GetManifestResourceStream($"Orbita.Localization.Resources.{language}.json")!;
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
        var spanish = Read("es");
        var english = Read("en");
        Assert.Equal(spanish.Keys.Order(), english.Keys.Order());
        Assert.All(spanish.Values.Concat(english.Values), text => Assert.False(string.IsNullOrWhiteSpace(text)));
    }

    [Fact]
    public async Task Preferences_round_trip_and_invalid_updates_do_not_overwrite_saved_data()
    {
        using var directory = new TemporaryWorkspace();
        var service = new PreferencesService(new JsonPreferencesRepository(directory.FilePath("preferences.json")));
        await service.SaveAsync(new UserPreferences { Language = "en", Theme = "light" });
        await Assert.ThrowsAsync<Orbita.SharedKernel.DomainException>(() => service.SaveAsync(new UserPreferences { Language = "xx" }));
        Assert.Equal("en", (await service.LoadAsync()).Language);
        Assert.Equal("light", (await service.LoadAsync()).Theme);
    }

    [Fact]
    public async Task Concurrent_history_writes_preserve_all_entries()
    {
        using var directory = new TemporaryWorkspace();
        var repository = new JsonActivityRepository(directory.FilePath("activity.json"));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(index => repository.AppendAsync(
            OperationRecord.Completed($"file-{index}.pdf", "ActionPng", ["result.png"], index))));
        var items = await repository.GetRecentAsync();
        Assert.Equal(20, items.Count);
        Assert.Equal(20, items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task History_retention_is_bounded()
    {
        using var directory = new TemporaryWorkspace();
        var repository = new JsonActivityRepository(directory.FilePath("activity.json"));
        for (var index = 0; index < 205; index++)
            await repository.AppendAsync(OperationRecord.Completed($"file-{index}", "ActionZip", ["out.zip"], 10));
        var items = await repository.GetRecentAsync();
        Assert.Equal(200, items.Count);
        Assert.DoesNotContain(items, item => item.InputName == "file-0");
    }
}
