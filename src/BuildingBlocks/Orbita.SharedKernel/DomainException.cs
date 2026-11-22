namespace Orbita.SharedKernel;

public sealed class DomainException(string resourceKey) : Exception(resourceKey)
{
    public string ResourceKey { get; } = resourceKey;
}
