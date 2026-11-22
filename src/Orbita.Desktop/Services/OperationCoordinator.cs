using Orbita.Activity.Application;
using Orbita.Activity.Domain;
using Orbita.Files.Application;
using Orbita.Files.Domain;

namespace Orbita.Desktop.Services;

public sealed record CompletedOperation(ActionResult Result, bool HistorySaved);

/// <summary>Coordinates bounded contexts through their application contracts.</summary>
public sealed class OperationCoordinator(IFileActions actions, ActivityService activity,
    PreferencesState preferences, Diagnostics diagnostics)
{
    public async Task<CompletedOperation> ExecuteAsync(FileAsset file, FileAction action,
        ImageEditOptions? options, CancellationToken cancellationToken)
    {
        var output = preferences.Current.OutputDirectory
            ?? Path.Combine(Path.GetDirectoryName(file.Path)!, "Orbita");
        var result = await actions.ExecuteAsync(new ActionRequest(file, action.Id, output, options), cancellationToken);
        try
        {
            // Once output is committed, cancellation must not suppress the history entry.
            await activity.RecordAsync(OperationRecord.Completed(file.Name, action.LabelKey, result.OutputPaths, result.OutputBytes));
            return new CompletedOperation(result, true);
        }
        catch (Exception exception)
        {
            diagnostics.Log(exception);
            return new CompletedOperation(result, false);
        }
    }
}
