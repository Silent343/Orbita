using Orbita.Files.Application;
using Orbita.Files.Domain;

namespace Orbita.Files.Infrastructure;

public sealed class ExternalConversionHandler : IFileActionHandler
{
    public bool CanHandle(string id) => id == "word.pdf" || id.StartsWith("video.", StringComparison.Ordinal)
        || id.StartsWith("sheet.", StringComparison.Ordinal) || id.StartsWith("markup.", StringComparison.Ordinal)
        || id.StartsWith("book.", StringComparison.Ordinal);

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var work = new ConversionWork();
        var results = request.ActionId.Split('.')[0] switch
        {
            "video" => await MediaConversions.ConvertAsync(request, work, cancellationToken),
            "sheet" or "word" => await OfficeConversions.ConvertAsync(request, work, cancellationToken),
            _ => await BookConversions.ConvertAsync(request, work, cancellationToken)
        };
        return await work.PublishAsync(request, results, cancellationToken);
    }
}
