using Orbita.Activity.Application;
using Orbita.Activity.Domain;
using Orbita.Persistence;

namespace Orbita.Activity.Infrastructure;

public sealed class JsonActivityRepository(string path) : IActivityRepository
{
    private readonly JsonFileStore<List<OperationRecord>> _store = new(path, () => []);

    public async Task<IReadOnlyList<OperationRecord>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        (await _store.ReadAsync(cancellationToken)).OrderByDescending(item => item.CompletedAt).ToArray();

    public Task AppendAsync(OperationRecord operation, CancellationToken cancellationToken = default) =>
        _store.UpdateAsync(items => items.Append(operation).TakeLast(200).ToList(), cancellationToken);
}
