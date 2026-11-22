using Orbita.Activity.Domain;

namespace Orbita.Activity.Application;

public interface IActivityRepository
{
    Task<IReadOnlyList<OperationRecord>> GetRecentAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(OperationRecord operation, CancellationToken cancellationToken = default);
}

public sealed class ActivityService(IActivityRepository repository)
{
    public Task<IReadOnlyList<OperationRecord>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        repository.GetRecentAsync(cancellationToken);

    public Task RecordAsync(OperationRecord operation, CancellationToken cancellationToken = default) =>
        repository.AppendAsync(operation, cancellationToken);
}
