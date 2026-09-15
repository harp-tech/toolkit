namespace Harp.Toolkit;

/// <summary>
/// Reports progress on the thread that calls <see cref="Report"/>, unlike
/// <see cref="Progress{T}"/>, which posts each report to the thread pool.
/// </summary>
internal sealed class ImmediateProgress<T> : IProgress<T>
{
    readonly Action<T> handler;

    public ImmediateProgress(Action<T> handler)
    {
        this.handler = handler;
    }

    public void Report(T value) => handler(value);
}
