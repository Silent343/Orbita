using Orbita.Files.Application;
using Orbita.Files.Domain;

namespace Orbita.Files.Infrastructure;

public sealed class TextActionHandler : IFileActionHandler
{
    public bool CanHandle(string actionId) => actionId is "text.txt" or "text.md";

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        using var output = OutputFile.Create(request.OutputDirectory,
            Path.GetFileNameWithoutExtension(request.File.Name), request.ActionId == "text.md" ? ".md" : ".txt");
        await using var source = File.OpenRead(request.File.Path);
        await source.CopyToAsync(output.Stream, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var length = output.Stream.Length;
        output.Commit();
        return new ActionResult([output.Path], length);
    }
}
