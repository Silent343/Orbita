namespace Orbita.Activity.Domain;

public sealed record OperationRecord(Guid Id, string InputName, string ActionLabelKey,
    DateTimeOffset CompletedAt, IReadOnlyList<string> Outputs, long OutputBytes)
{
    public static OperationRecord Completed(string inputName, string actionLabelKey,
        IReadOnlyList<string> outputs, long outputBytes) =>
        new(Guid.NewGuid(), inputName, actionLabelKey, DateTimeOffset.UtcNow, outputs, outputBytes);
}
