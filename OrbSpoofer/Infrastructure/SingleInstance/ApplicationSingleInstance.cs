namespace OrbSpoofer.Infrastructure.SingleInstance;

/// <summary>
/// Ensures only one process instance owns app resources at a time.
/// A second launch can signal the running instance to show its main window.
/// </summary>
public static class ApplicationSingleInstance
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _showWindowEvent;
    private static CancellationTokenSource? _listenerCancellation;

    public static bool TryBecomeOwner(string mutexName, string showWindowEventName)
    {
        _mutex = new Mutex(true, mutexName, out var createdNew);
        if (createdNew)
        {
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showWindowEventName);
            return true;
        }

        SignalExistingInstance(showWindowEventName);
        return false;
    }

    public static void ListenForShowWindowRequests(Action showMainWindow)
    {
        if (_showWindowEvent is null)
            return;

        _listenerCancellation?.Cancel();
        _listenerCancellation = new CancellationTokenSource();
        var token = _listenerCancellation.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_showWindowEvent.WaitOne(TimeSpan.FromSeconds(1)))
                        continue;
                }
                catch (AbandonedMutexException)
                {
                    continue;
                }

                if (token.IsCancellationRequested)
                    break;

                System.Windows.Application.Current.Dispatcher.Invoke(showMainWindow);
            }
        }, token);
    }

    public static void Dispose()
    {
        _listenerCancellation?.Cancel();
        _listenerCancellation = null;
        _showWindowEvent?.Dispose();
        _showWindowEvent = null;

        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // Already released or never acquired.
        }

        _mutex.Dispose();
        _mutex = null;
    }

    private static void SignalExistingInstance(string showWindowEventName)
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(showWindowEventName);
            showEvent.Set();
        }
        catch
        {
            // Owner may still be starting.
        }
    }
}

