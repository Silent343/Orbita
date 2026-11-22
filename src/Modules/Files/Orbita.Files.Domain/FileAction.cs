namespace Orbita.Files.Domain;

public sealed record FileAction(
    string Id, string LabelKey, string Glyph, ActionGroup Group,
    bool Available = true, string? UnavailableReasonKey = null);

public sealed record ImageEditOptions(uint Width = 0, uint Height = 0,
    uint Left = 0, uint Top = 0, uint CropWidth = 0, uint CropHeight = 0);

public sealed record ActionRequest(FileAsset File, string ActionId, string OutputDirectory,
    ImageEditOptions? ImageOptions = null);

public sealed record ActionResult(IReadOnlyList<string> OutputPaths, long OutputBytes);
