namespace Harp.Toolkit;

/// <summary>
/// Provides extension methods for asynchronous operations.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Awaits the specified task for no more than the specified time.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="millisecondsDelay">
    /// The time to wait, in milliseconds, for the task to complete.
    /// </param>
    /// <returns>
    /// The task object representing the asynchronous operation. The
    /// <see cref="Task{TResult}.Result"/> property contains the result of the specified task.
    /// </returns>
    /// <exception cref="TimeoutException">
    /// The task did not complete in the specified time. The task continues to run, because this
    /// method does not cancel it.
    /// </exception>
    public static async Task<T> WithTimeout<T>(this Task<T> task, int millisecondsDelay)
    {
        if (await Task.WhenAny(task, Task.Delay(millisecondsDelay)) == task)
        {
            return await task;
        }
        else throw new TimeoutException("There was a timeout while awaiting the device response.");
    }
}
