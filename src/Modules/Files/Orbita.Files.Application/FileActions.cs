using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Application;

public sealed class FileActions(IActionCatalog catalog, IEnumerable<IFileActionHandler> handlers) : IFileActions
{
    public Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken = default)
    {
        var action = Enum.GetValues<ActionGroup>().SelectMany(group => catalog.For(request.File, group))
            .SingleOrDefault(candidate => candidate.Id == request.ActionId);
        if (action is null || !action.Available) throw new DomainException("ErrorActionUnavailable");
        var handler = handlers.SingleOrDefault(candidate => candidate.CanHandle(request.ActionId))
            ?? throw new DomainException("ErrorActionUnavailable");
        if (string.IsNullOrWhiteSpace(request.OutputDirectory)) throw new DomainException("ErrorInvalidPath");
        return handler.ExecuteAsync(request, cancellationToken);
    }
}
