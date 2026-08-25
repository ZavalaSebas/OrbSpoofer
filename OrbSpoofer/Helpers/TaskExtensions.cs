using System.Diagnostics;

namespace OrbSpoofer.Helpers;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task, string? context = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
                Debug.WriteLine($"FireAndForget{(context != null ? $" [{context}]" : "")} failed: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
