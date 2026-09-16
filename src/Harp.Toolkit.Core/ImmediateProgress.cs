namespace Harp.Toolkit;

/// <summary>
/// Reports progress on the thread that calls <see cref="Report"/>.
/// </summary>
/// <typeparam name="T">The type of the progress report.</typeparam>
/// <remarks>
/// <see cref="Progress{T}"/> posts each report to the thread pool when the calling thread has
/// no <see cref="SynchronizationContext"/>. Reports then run at the same time and in the wrong
/// order. This class calls the handler at once, so the last report is available to a caller
/// that handles an exception.
/// </remarks>
public sealed class ImmediateProgress<T> : IProgress<T>
{
    readonly Action<T> handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateProgress{T}"/> class.
    /// </summary>
    /// <param name="handler">The handler that receives each progress report.</param>
    public ImmediateProgress(Action<T> handler)
    {
        this.handler = handler;
    }

    /// <summary>
    /// Reports a progress update to the handler.
    /// </summary>
    /// <param name="value">The value of the progress update.</param>
    public void Report(T value) => handler(value);
}
