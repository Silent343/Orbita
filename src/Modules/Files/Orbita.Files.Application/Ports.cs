using Orbita.Files.Domain;

namespace Orbita.Files.Application;

public interface IFileInspector
{
    Task<FileAsset> InspectAsync(string path, CancellationToken cancellationToken = default);
}

public interface IActionCatalog
{
    IReadOnlyList<FileAction> For(FileAsset file, ActionGroup group);
}

public interface IFileActionHandler
{
    bool CanHandle(string actionId);
    Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken);
}

public interface IFileActions
{
    Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken = default);
}
