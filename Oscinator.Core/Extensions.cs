using Microsoft.Extensions.Logging;

namespace Oscinator.Core;

public static class Extensions
{
    public static void NoAwait(this Task task, ILogger? logger, string? label)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted) 
                logger?.LogError(t.Exception, "Task {Label} faulted with an exception", label);
        });
    }
    
    public static void NoAwait<T>(this ValueTask<T> task, ILogger? logger, string? label)
    {
        if (task.IsCompletedSuccessfully) return;
        task.AsTask().NoAwait(logger, label);
    }
}