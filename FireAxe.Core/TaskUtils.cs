using System;

namespace FireAxe;

public static class TaskUtils
{
    public static async Task WhenAllIgnoreCanceled(IEnumerable<Task> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        Task[] taskArray = [.. tasks];
        var exceptions = new List<Exception>();
        foreach (var task in taskArray)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }
        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}