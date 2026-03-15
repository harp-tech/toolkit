namespace Harp.Toolkit;

static class TaskExtensions
{
    internal static async Task<T> WithTimeout<T>(this Task<T> task, int? millisecondsDelay)
    {
        if (!millisecondsDelay.HasValue)
            return await task;

        if (await Task.WhenAny(task, Task.Delay(millisecondsDelay.GetValueOrDefault())) == task)
        {
            return task.Result;
        }
        else throw new TimeoutException("There was a timeout while awaiting the device response.");
    }
}