using Orbita.Desktop.Services;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.Localization;

namespace Orbita.Desktop.Presentation;

public sealed class WorkspaceViewModel(ILocalizer localizer, IFileInspector inspector,
    IActionCatalog catalog, OperationCoordinator operations, Diagnostics diagnostics) : LocalizedViewModel(localizer)
{
    private FileAsset? _file;
    private ActionGroup _group;
    private bool _isBusy;
    private string _statusKey = "Ready";
    private object[] _statusArguments = [];
    private CancellationTokenSource? _cancellation;

    public FileAsset? File { get => _file; private set => SetProperty(ref _file, value); }
    public ActionGroup Group { get => _group; private set => SetProperty(ref _group, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public IReadOnlyList<FileAction> Actions => File is null ? [] : catalog.For(File, Group);
    public string FileSize => File is null ? "—" : FileSizeFormatter.Format(File.Size, L.Culture);
    public string FileName => File?.Name ?? L["EmptyFile"];
    public string Status => L.Format(_statusKey, _statusArguments.Select(argument => argument is long bytes
        ? (object)FileSizeFormatter.Format(bytes, L.Culture) : argument).ToArray());
    public IReadOnlyList<string> LastOutputs { get; private set; } = [];
    public event EventHandler? FileChanged;
    public event EventHandler<string>? Warning;

    public async Task SelectAsync(string path)
    {
        if (IsBusy) return;
        try
        {
            File = await inspector.InspectAsync(path);
            Group = ActionGroup.Convert;
            if (catalog.For(File, Group).Count == 0) Group = ActionGroup.Tools;
            SetStatus("Ready");
            RaisePropertyChanged(string.Empty);
            FileChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) { HandleError(exception); }
    }

    public void SwitchGroup(ActionGroup group)
    {
        if (IsBusy) return;
        Group = group;
        RaisePropertyChanged(nameof(Actions));
    }

    public async Task ExecuteAsync(FileAction action, ImageEditOptions? options = null, IReadOnlyList<string>? additionalImages = null)
    {
        if (IsBusy || File is null) return;
        if (!action.Available) { Warning?.Invoke(this, action.UnavailableReasonKey ?? "ErrorActionUnavailable"); return; }
        IsBusy = true;
        SetStatus("Processing");
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
            var completed = await operations.ExecuteAsync(File, action, options, cancellation.Token, additionalImages);
            LastOutputs = completed.Result.OutputPaths;
            SetStatus("ResultSaved", LastOutputs.Count, completed.Result.OutputBytes);
            if (!completed.HistorySaved) Warning?.Invoke(this, "HistorySaveWarning");
        }
        catch (OperationCanceledException) { SetStatus("Cancelled"); }
        catch (Exception exception) { HandleError(exception); }
        finally { _cancellation = null; IsBusy = false; RaisePropertyChanged(nameof(LastOutputs)); }
    }

    public void Cancel() => _cancellation?.Cancel();
    private void SetStatus(string key, params object[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        RaisePropertyChanged(nameof(Status));
    }

    private void HandleError(Exception exception)
    {
        diagnostics.Log(exception);
        var key = Diagnostics.ResourceKey(exception);
        SetStatus(key);
        Warning?.Invoke(this, key);
    }
}
