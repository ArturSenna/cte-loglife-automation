using System.Diagnostics;
using System.Runtime.InteropServices;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Bsoft;

/// <summary>
/// Finds controls on screen by their screenshot and sends mouse and keyboard input, with the same
/// timing the Python bot had (300 ms after each action). Every action first passes the run's
/// pause/stop gate.
/// </summary>
public sealed class DesktopBot : IDisposable
{
    public const int AfterActionMs = 300;
    private const int PollMs = 40;

    private readonly TemplateStore _templates;
    private readonly IScreenSource _screen;
    private readonly RunControl _control;
    private readonly IRunLog _log;
    private readonly UnknownScreens? _unknown;
    private readonly Dictionary<string, string> _preferred = new(StringComparer.OrdinalIgnoreCase);
    private ScreenBox? _last;

    /// <param name="unknown">
    /// What to do when a required control does not show up. Without it the run simply stops there.
    /// </param>
    public DesktopBot(TemplateStore templates, IScreenSource screen, RunControl control, IRunLog log,
        UnknownScreens? unknown = null)
    {
        _templates = templates;
        _screen = screen;
        _control = control;
        _log = log;
        _unknown = unknown;
    }

    /// <summary>How long a required control may be missing before learned screens are looked for.</summary>
    public int RecoverAfterMs { get; init; } = 1500;

    /// <summary>How often learned screens are looked for after that.</summary>
    public int RecoverEveryMs { get; init; } = 1000;

    public void Dispose() => _screen.Dispose();

    /// <summary>The element located by the last successful <see cref="Find"/>.</summary>
    public ScreenBox Last =>
        _last ?? throw new InvalidOperationException("Nenhum elemento localizado para esta ação.");

    // ------------------------------------------------------------------ finding

    /// <summary>
    /// Looks for a control until it shows up or <paramref name="waitMs"/> runs out (at least one look).
    /// Alternates of the label are tried on the same screenshot, the one that matched last time first.
    /// Time spent paused does not count.
    /// </summary>
    public bool Find(string label, double matching = 0.9, int waitMs = 10000) =>
        Find(label, matching, waitMs, recover: false);

    /// <param name="recover">
    /// While the control is missing, also look for screens the operator already taught and repeat
    /// their action (each repetition restarts the wait).
    /// </param>
    private bool Find(string label, double matching, int waitMs, bool recover)
    {
        _last = null;
        var images = new List<(string Name, Mat Image)>();
        foreach (var candidate in Ordered(label))
            if (_templates.Image(candidate) is { } image) images.Add((candidate, image));
        if (images.Count == 0)
            throw new ElementNotFoundException(label, "A imagem de referência não existe na pasta Templates.");

        var repeats = recover && _unknown is not null ? new Dictionary<string, int>() : null;
        double nextRecover = RecoverAfterMs;
        var watch = Stopwatch.StartNew();
        var paused = TimeSpan.Zero;
        while (true)
        {
            paused += Gate();
            var waited = (watch.Elapsed - paused).TotalMilliseconds;
            using (var shot = _screen.Capture())
            {
                foreach (var (name, image) in images)
                {
                    if (TemplateMatcher.Find(shot, image, matching) is not { } box) continue;
                    _last = box;
                    _preferred[label] = name;
                    return true;
                }

                if (repeats is not null && waited >= nextRecover)
                {
                    nextRecover = waited + RecoverEveryMs;
                    if (_unknown!.TryRepeat(shot, label, repeats))
                    {
                        // The screen in the way was dealt with: the control gets its full wait again.
                        watch.Restart();
                        paused = TimeSpan.Zero;
                        nextRecover = RecoverAfterMs;
                        continue;
                    }
                }
            }
            if ((watch.Elapsed - paused).TotalMilliseconds >= waitMs) return false;
            Thread.Sleep(PollMs);
        }
    }

    /// <summary>
    /// Like <see cref="Find"/>, for a control the next step cannot do without. When it does not show up
    /// the screen is one the bot does not know: it is captured and the operator is asked to solve it
    /// (see <see cref="UnknownScreens"/>), then the control is looked for again. The run only stops
    /// here when the operator says so, or when no unknown-screen handling was given.
    /// </summary>
    public void Require(string label, int waitMs = 10000, double matching = 0.97)
    {
        Lesson? lesson = null;
        try
        {
            while (true)
            {
                if (Find(label, matching, waitMs, recover: true))
                {
                    if (lesson is null) return;
                    var found = Last;
                    using (var after = _screen.Capture()) _unknown!.Finish(lesson, after);
                    _last = found;
                    return;
                }

                // What the operator did last time did not bring the control back: nothing to learn from it.
                lesson?.Dispose();
                lesson = null;
                if (_unknown is null) throw new ElementNotFoundException(label);

                using var shot = _screen.Capture();
                lesson = _unknown.AskOperator(shot, label);
                if (lesson is not null) continue;
                if (_control.InCritical)
                    throw new ElementNotFoundException(label, "A execução foi interrompida pelo operador nesta tela.");
                throw new RunStoppedException();
            }
        }
        finally
        {
            lesson?.Dispose();
        }
    }

    private IEnumerable<string> Ordered(string label)
    {
        var candidates = _templates.Candidates(label);
        if (!_preferred.TryGetValue(label, out var first)) return candidates;
        return candidates.Where(c => c == first).Concat(candidates.Where(c => c != first));
    }

    // ------------------------------------------------------------------ mouse

    /// <summary>Clicks the centre of the last element found.</summary>
    public void Click()
    {
        Gate();
        MouseAt(Last.CenterX, Last.CenterY, clicks: 1);
        Thread.Sleep(AfterActionMs);
    }

    public void DoubleClick()
    {
        Gate();
        MouseAt(Last.CenterX, Last.CenterY, clicks: 2);
        Thread.Sleep(AfterActionMs);
    }

    public void RightClick()
    {
        Gate();
        MouseAt(Last.CenterX, Last.CenterY, clicks: 1, right: true);
        Thread.Sleep(AfterActionMs);
    }

    /// <summary>Clicks at an offset from the top-left corner of the last element found.</summary>
    public void ClickRelative(double dx, double dy)
    {
        Gate();
        MouseAt(Last.Left + dx, Last.Top + dy, clicks: 1);
        Thread.Sleep(AfterActionMs);
    }

    public void DoubleClickRelative(double dx, double dy)
    {
        Gate();
        MouseAt(Last.Left + dx, Last.Top + dy, clicks: 2);
        Thread.Sleep(AfterActionMs);
    }

    /// <summary>Clicks an absolute screen position. No pause afterwards.</summary>
    public void ClickAt(double x, double y)
    {
        Gate();
        MouseAt(x, y, clicks: 1);
    }

    private static void MouseAt(double x, double y, int clicks, bool right = false)
    {
        InputSender.MoveTo(x, y);
        Thread.Sleep(100);
        for (var i = 0; i < clicks; i++) InputSender.MouseClick(right);
    }

    // ------------------------------------------------------------------ keyboard

    public void Press(Key key, int waitMs = AfterActionMs)
    {
        Gate();
        InputSender.Tap(key);
        Thread.Sleep(waitMs);
    }

    public void Tab(int waitMs = AfterActionMs) => Press(Key.Tab, waitMs);
    public void Enter() => Press(Key.Enter);

    /// <summary>Ctrl plus a letter (Ctrl+A, Ctrl+C, Ctrl+V).</summary>
    public void Control(char letter)
    {
        Gate();
        InputSender.KeyDown(Key.Control);
        try
        {
            InputSender.TapChar(letter);
        }
        finally
        {
            InputSender.KeyUp(Key.Control);
        }
        Thread.Sleep(AfterActionMs);
    }

    public void SelectAll() => Control('a');

    /// <summary>Types text with no pause between keys.</summary>
    public void TypeKey(string text)
    {
        Gate();
        foreach (var ch in text) InputSender.TapChar(ch);
        Thread.Sleep(AfterActionMs);
    }

    /// <summary>Types text slowly, 200 ms per key, for the fields that drop fast input.</summary>
    public void TypeKeys(string text)
    {
        Gate();
        foreach (var ch in text)
        {
            InputSender.TapChar(ch);
            Thread.Sleep(200);
        }
    }

    /// <summary>Types text the way a person does (Shift for capitals), pausing between keys.</summary>
    public void TypeLikeAPerson(string text, int intervalMs)
    {
        Gate();
        foreach (var ch in text)
        {
            InputSender.TypeLikeAPerson(ch);
            Thread.Sleep(intervalMs);
        }
        Thread.Sleep(100);
    }

    /// <summary>Puts text on the clipboard and pastes it with Ctrl+V.</summary>
    public void PasteText(string text)
    {
        Gate();
        ClipboardText.Set(text);
        Thread.Sleep(AfterActionMs);
        Control('v');
        Thread.Sleep(AfterActionMs);
    }

    public void Wait(int ms)
    {
        Gate();
        Thread.Sleep(ms);
    }

    // ------------------------------------------------------------------ pause gate and focus

    /// <summary>
    /// The pause/stop checkpoint. After a pause the window that had the keyboard is brought back to
    /// the front, because the next step may be a keystroke.
    /// </summary>
    private TimeSpan Gate()
    {
        if (!_control.IsPauseRequested || _control.InCritical) return _control.Checkpoint();

        var foreground = Native.GetForegroundWindow();
        var paused = _control.Checkpoint();
        if (paused > TimeSpan.Zero && foreground != IntPtr.Zero && Native.IsWindow(foreground) &&
            Native.GetForegroundWindow() != foreground)
        {
            if (!Native.SetForegroundWindow(foreground))
                _log.Warn("Não foi possível devolver o foco ao Bsoft depois da pausa.");
            Thread.Sleep(400);
        }
        return paused;
    }

    /// <summary>The text of the control that has the keyboard focus, when Windows can read it.</summary>
    public static string? FocusedControlText()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return null;
        var thread = Native.GetWindowThreadProcessId(foreground, out _);
        var info = new Native.GUITHREADINFO { cbSize = Marshal.SizeOf<Native.GUITHREADINFO>() };
        if (!Native.GetGUIThreadInfo(thread, ref info) || info.hwndFocus == IntPtr.Zero) return null;

        if (Native.SendMessageTimeoutW(info.hwndFocus, Native.WmGetTextLength, IntPtr.Zero, IntPtr.Zero,
                Native.SmtoAbortIfHung, 500, out var length) == IntPtr.Zero) return null;
        var size = (int)length;
        if (size is <= 0 or > 64) return null;

        var buffer = new char[size + 1];
        if (Native.SendMessageTimeoutText(info.hwndFocus, Native.WmGetText, buffer.Length, buffer,
                Native.SmtoAbortIfHung, 500, out var copied) == IntPtr.Zero) return null;
        return new string(buffer, 0, Math.Min((int)copied, size)).Trim();
    }
}
