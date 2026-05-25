using System.Threading;
using System.Security.Principal;

namespace Hermes.Windows.Infrastructure;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private bool _ownsMutex;
    private CancellationTokenSource? _listenerCts;

    public SingleInstanceGuard()
    {
        var instanceName = GetInstanceName();
        _mutex = new Mutex(true, $@"Local\{instanceName}", out _ownsMutex);
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{instanceName}.Activate");
    }

    public bool IsFirstInstance => _ownsMutex;

    public void SignalExistingInstance()
    {
        _activationEvent.Set();
    }

    public void StartActivationListener(Action activationAction)
    {
        if (!_ownsMutex)
        {
            return;
        }

        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activationEvent.WaitOne(500))
                    {
                        activationAction();
                    }
                }
                catch
                {
                    return;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _activationEvent.Dispose();
        _mutex.Dispose();
    }

    private static string GetInstanceName()
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (!string.IsNullOrWhiteSpace(sid))
            {
                return $"Hermes.Windows.{sid}";
            }
        }
        catch
        {
            // Fall back to the legacy name if Windows identity lookup fails.
        }

        return "Hermes.Windows";
    }
}
