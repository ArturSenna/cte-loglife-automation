using System.Diagnostics;

namespace CteLogLife.Core.Automation;

/// <summary>Thrown at a checkpoint when the operator asked the run to stop.</summary>
public sealed class RunStoppedException() : OperationCanceledException("Execução interrompida pelo operador.");

/// <summary>Why the bot is parked waiting for the person at the screen, as the overlay shows it.</summary>
public sealed record OperatorAttention(string Title, string Message, string? ScreenshotPath = null);

/// <summary>
/// Pause and stop for a running batch. The bot calls <see cref="Checkpoint"/> before every step; while a
/// CT-e is past the point of no return (<see cref="BeginCritical"/>) requests are held back until it is
/// fully emitted and recorded.
/// </summary>
public sealed class RunControl
{
    private readonly object _gate = new();
    private bool _pauseRequested;
    private bool _stopRequested;
    private bool _paused;
    private int _critical;
    private OperatorAttention? _attention;

    /// <summary>Set while the bot waits in <see cref="WaitForOperator"/>.</summary>
    public OperatorAttention? Attention { get { lock (_gate) return _attention; } }

    /// <summary>Raised (on the calling thread) whenever pause, stop or critical state changes.</summary>
    public event Action? Changed;

    public bool IsPauseRequested { get { lock (_gate) return _pauseRequested; } }
    public bool IsStopRequested { get { lock (_gate) return _stopRequested; } }

    /// <summary>True while the bot is actually parked at a checkpoint.</summary>
    public bool IsPaused { get { lock (_gate) return _paused; } }

    public bool InCritical { get { lock (_gate) return _critical > 0; } }

    public void RequestPause() => Update(() => _pauseRequested = true);

    public void Resume() => Update(() => _pauseRequested = false);

    public void TogglePause() => Update(() => _pauseRequested = !_pauseRequested);

    public void RequestStop() => Update(() => _stopRequested = true);

    /// <summary>Clears every request. Called before a run starts.</summary>
    public void Reset() => Update(() =>
    {
        _pauseRequested = false;
        _stopRequested = false;
        _paused = false;
        _critical = 0;
        _attention = null;
    });

    public void BeginCritical() => Update(() => _critical++);

    public void EndCritical() => Update(() => _critical = 0);

    private void Update(Action change)
    {
        lock (_gate)
        {
            change();
            Monitor.PulseAll(_gate);
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Blocks while paused and throws <see cref="RunStoppedException"/> when a stop was requested.
    /// Does nothing inside a critical section. Returns how long the call stayed paused.
    /// </summary>
    public TimeSpan Checkpoint()
    {
        lock (_gate)
        {
            if (_critical > 0) return TimeSpan.Zero;
            if (_stopRequested) throw new RunStoppedException();
            if (!_pauseRequested) return TimeSpan.Zero;
            _paused = true;
        }
        Changed?.Invoke();

        var watch = Stopwatch.StartNew();
        bool stopped;
        lock (_gate)
        {
            while (_pauseRequested && !_stopRequested)
                Monitor.Wait(_gate, 250);
            _paused = false;
            stopped = _stopRequested;
        }
        Changed?.Invoke();

        if (stopped) throw new RunStoppedException();
        return watch.Elapsed;
    }

    /// <summary>
    /// Parks the bot because it cannot go on by itself (a screen it does not know). Returns true when
    /// the operator continues (the pause shortcuts and buttons do it) and false when a stop is asked.
    /// Unlike a pause this also holds inside a critical section: there is no next step to take.
    /// </summary>
    public bool WaitForOperator(OperatorAttention attention)
    {
        lock (_gate)
        {
            if (_stopRequested) return false;
            _attention = attention;
            _pauseRequested = true;
            _paused = true;
        }
        Changed?.Invoke();

        bool resumed;
        lock (_gate)
        {
            while (_pauseRequested && !_stopRequested)
                Monitor.Wait(_gate, 250);
            resumed = !_stopRequested;
            _attention = null;
            _paused = false;
            _pauseRequested = false;
        }
        Changed?.Invoke();
        return resumed;
    }
}
