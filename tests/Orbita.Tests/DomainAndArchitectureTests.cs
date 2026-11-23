using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.Preferences.Domain;
using Orbita.SharedKernel;
using Xunit;

namespace Orbita.Tests;

public sealed class DomainAndArchitectureTests
{
    [Theory]
    [InlineData("sample.PDF", FileCategory.Pdf)]
    [InlineData("photo.jpeg", FileCategory.Image)]
    [InlineData("README.md", FileCategory.Text)]
    [InlineData("archive.bin", FileCategory.Other)]
    public void File_type_is_classified_without_case_sensitivity(string name, FileCategory expected)
    {
        var asset = FileAsset.Create(Path.Combine(Path.GetTempPath(), name), 128, DateTimeOffset.UtcNow);
        Assert.Equal(expected, asset.Category);
    }

    [Fact]
    public void Invalid_file_paths_and_sizes_are_rejected()
    {
        Assert.Throws<DomainException>(() => FileAsset.Create("relative.pdf", 1, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => FileAsset.Create(Path.Combine(Path.GetTempPath(), "file.pdf"), -1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Unavailable_and_incompatible_actions_never_reach_a_handler()
    {
        var handler = new SpyHandler();
        var service = new FileActions(new ActionCatalog(), [handler]);
        var asset = FileAsset.Create(Path.Combine(Path.GetTempPath(), "file.pdf"), 10, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DomainException>(() => service.ExecuteAsync(new ActionRequest(asset, "unknown", Path.GetTempPath())));
        await Assert.ThrowsAsync<DomainException>(() => service.ExecuteAsync(new ActionRequest(asset, "image.crop", Path.GetTempPath())));
        Assert.False(handler.WasCalled);
    }

    [Theory]
    [InlineData("es", "dark", 350)]
    [InlineData("en", "system", 200)]
    public void Valid_preferences_are_accepted(string language, string theme, int delay) =>
        new UserPreferences { Language = language, Theme = theme, ShiftHoldMilliseconds = delay }.Validate();

    [Fact]
    public void Invalid_preferences_are_rejected()
    {
        Assert.Throws<DomainException>(() => new UserPreferences { Language = "xx" }.Validate());
        Assert.Throws<DomainException>(() => new UserPreferences { Theme = "neon" }.Validate());
        Assert.Throws<DomainException>(() => new UserPreferences { ShiftHoldMilliseconds = 10 }.Validate());
    }

    [Fact]
    public void Domain_and_application_assemblies_do_not_depend_on_infrastructure_or_presentation()
    {
        var assemblies = new[] { typeof(FileAsset).Assembly, typeof(FileActions).Assembly,
            typeof(UserPreferences).Assembly, typeof(Orbita.Preferences.Application.PreferencesService).Assembly,
            typeof(Orbita.Activity.Domain.OperationRecord).Assembly, typeof(Orbita.Activity.Application.ActivityService).Assembly };
        foreach (var assembly in assemblies)
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            Assert.DoesNotContain("Infrastructure", reference.Name!);
            Assert.DoesNotContain("Desktop", reference.Name!);
            Assert.DoesNotContain("Microsoft.UI", reference.Name!);
        }
    }

    private sealed class SpyHandler : IFileActionHandler
    {
        public bool WasCalled { get; private set; }
        public bool CanHandle(string actionId) => true;
        public Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new ActionResult([], 0));
        }
    }
}
