using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Bsoft;

/// <summary>
/// Finds controls on screen by their screenshot and sends mouse and keyboard input.
///
/// The work is organised in steps: one screen element (the anchor) and what is done on it. A pause
/// never cuts a step in half: it takes effect before the next anchor is looked for, so no field is
/// left half typed. While paused the bot watches whether Bsoft is used; if it was, the last step is
/// done again from its anchor when it is one that overwrites its fields (see <see cref="Step"/>).
///
/// After each action the bot waits for the screen to answer and go quiet instead of always waiting
/// <see cref="AfterActionMs"/>; that time is now only the upper limit (see <see cref="Adaptive"/>).
/// </summary>
public sealed class DesktopBot : IDisposable
{
    /// <summary>The wait after an action in the Python bot. Now the most an action waits.</summary>
    public const int AfterActionMs = 300;

    private const int PollMs = 40;
    private const double DefaultMatching = 0.97;

    // Waiting for the screen to answer an action.
    private const int FloorMs = 60;       // never go on sooner than this
    private const int QuietMs = 60;       // the screen must have stopped changing for this long
    private const int SettlePollMs = 15;
    private const int LookMs = 25;        // about what one look at the screen costs
    private const int NoiseSamples = 120; // a blinking text cursor changes fewer colour values than this

    private const int RedoWaitMs = 3000;

    private readonly TemplateStore _templates;
    private readonly IScreenSource _screen;
    private readonly RunControl _control;
    private readonly IRunLog _log;
    private readonly UnknownScreens? _unknown;
    private readonly IDesktop _desktop;
    private readonly Func<IPauseWatcher>? _newWatcher;
    private readonly Dictionary<string, string> _preferred = new(StringComparer.OrdinalIgnoreCase);
    private ScreenBox? _last;
    private bool _inStep;
    private bool _redoing;
    private StepInfo? _lastStep;
    private uint _workProcess; // the program the bot is typing into (Bsoft)
    private ScreenBox? _watched; // where the answer to the current action is looked for

    private sealed record StepInfo(string Name, string? Anchor, int WaitMs, double Matching, Action Actions, bool Redo);

    /// <param name="unknown">
    /// What to do when a required control does not show up. Without it the run simply stops there.
    /// </param>
    /// <param name="desktop">Mouse, keyboard, clipboard and clock. The real ones when omitted.</param>
    /// <param name="pauseWatcher">
    /// Tells whether Bsoft was used during a pause. Without it nothing is redone after a pause.
    /// </param>
    public DesktopBot(TemplateStore templates, IScreenSource screen, RunControl control, IRunLog log,
        UnknownScreens? unknown = null, IDesktop? desktop = null, Func<IPauseWatcher>? pauseWatcher = null)
    {
        _templates = templates;
        _screen = screen;
        _control = control;
        _log = log;
        _unknown = unknown;
        _desktop = desktop ?? new RealDesktop();
        _newWatcher = pauseWatcher;
    }

    /// <summary>How long a required control may be missing before learned screens are looked for.</summary>
    public int RecoverAfterMs { get; init; } = 1500;

    /// <summary>How often learned screens are looked for after that.</summary>
    public int RecoverEveryMs { get; init; } = 1000;

    /// <summary>
    /// On: go on as soon as the screen answered the action, and stop waiting for an optional pop-up
    /// once the screen that comes after it is there. Off: the fixed waits the Python bot had.
    /// </summary>
    public bool Adaptive { get; init; } = true;

    /// <summary>
    /// How long an optional element may still show up after the screen that follows it is already
    /// there (<see cref="FindBefore"/>).
    /// </summary>
    public int OptionalGraceMs { get; init; } = 700;

    public void Dispose()
    {
        _screen.Dispose();
        (_desktop as IDisposable)?.Dispose();
    }

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
        NotInsideAStep(label);
        _last = null;
        var images = Images(label);

        var repeats = recover && _unknown is not null ? new Dictionary<string, int>() : null;
        long nextRecover = RecoverAfterMs;
        var started = _desktop.Now;
        long paused = 0;
        while (true)
        {
            paused += Gate();
            var waited = _desktop.Now - started - paused;
            using (var shot = _screen.Capture())
            {
                if (Match(shot, label, images, matching) is { } box)
                {
                    _last = box;
                    return true;
                }

                if (repeats is not null && waited >= nextRecover)
                {
                    nextRecover = waited + RecoverEveryMs;
                    if (_unknown!.TryRepeat(shot, label, repeats))
                    {
                        // The screen in the way was dealt with: the control gets its full wait again.
                        started = _desktop.Now;
                        paused = 0;
                        nextRecover = RecoverAfterMs;
                        continue;
                    }
                }
            }
            if (_desktop.Now - started - paused >= waitMs) return false;
            _desktop.Sleep(PollMs);
        }
    }

    /// <summary>
    /// Looks for an element that only sometimes comes before <paramref name="next"/> (a pop-up that
    /// some CT-es show and others do not). True when it shows up. False as soon as
    /// <paramref name="next"/> is on screen and the optional element did not join it within
    /// <see cref="OptionalGraceMs"/>, so its absence does not cost the whole wait; whichever of the two
    /// comes, it is caught for as long as <paramref name="totalWaitMs"/>.
    /// With <see cref="Adaptive"/> off this is the old plain wait of <paramref name="optionalWaitMs"/>.
    /// </summary>
    public bool FindBefore(string optional, string next, int optionalWaitMs, int totalWaitMs,
        double matching = DefaultMatching)
    {
        if (!Adaptive) return Find(optional, matching, optionalWaitMs);

        NotInsideAStep(optional);
        _last = null;
        var optionalImages = Images(optional);
        var nextImages = Images(next);
        var started = _desktop.Now;
        long paused = 0;
        long nextSince = -1;
        while (true)
        {
            paused += Gate();
            using (var shot = _screen.Capture())
            {
                if (Match(shot, optional, optionalImages, matching) is { } box)
                {
                    _last = box;
                    return true;
                }
                if (nextSince < 0 && Match(shot, next, nextImages, matching) is not null) nextSince = _desktop.Now;
            }
            if (nextSince >= 0 && _desktop.Now - nextSince >= OptionalGraceMs) return false;
            if (_desktop.Now - started - paused >= totalWaitMs) return false;
            _desktop.Sleep(PollMs);
        }
    }

    /// <summary>
    /// Like <see cref="Find"/>, for a control the next step cannot do without. When it does not show up
    /// the screen is one the bot does not know: it is captured and the operator is asked to solve it
    /// (see <see cref="UnknownScreens"/>), then the control is looked for again. The run only stops
    /// here when the operator says so, or when no unknown-screen handling was given.
    /// </summary>
    public void Require(string label, int waitMs = 10000, double matching = DefaultMatching)
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

    private List<(string Name, Mat Image)> Images(string label)
    {
        var images = new List<(string Name, Mat Image)>();
        foreach (var candidate in Ordered(label))
            if (_templates.Image(candidate) is { } image) images.Add((candidate, image));
        if (images.Count == 0)
            throw new ElementNotFoundException(label, "A imagem de referência não existe na pasta Templates.");
        return images;
    }

    private ScreenBox? Match(Mat shot, string label, List<(string Name, Mat Image)> images, double matching)
    {
        foreach (var (name, image) in images)
        {
            if (TemplateMatcher.Find(shot, image, matching) is not { } box) continue;
            _preferred[label] = name;
            return box;
        }
        return null;
    }

    private IEnumerable<string> Ordered(string label)
    {
        var candidates = _templates.Candidates(label);
        if (!_preferred.TryGetValue(label, out var first)) return candidates;
        return candidates.Where(c => c == first).Concat(candidates.Where(c => c != first));
    }

    private void NotInsideAStep(string label)
    {
        if (_inStep)
            throw new InvalidOperationException(
                $"O elemento '{label}' foi procurado no meio de um passo. Um passo só age; a busca vem antes dele.");
    }

    // ------------------------------------------------------------------ steps

    /// <summary>
    /// One step of the work: waits for <paramref name="anchor"/> (like <see cref="Require"/>) and runs
    /// <paramref name="actions"/> on it. A pause asked for while the actions run starts when they
    /// end; a stop still takes effect at once.
    /// </summary>
    /// <param name="name">What the operator reads when the bot talks about this step.</param>
    /// <param name="redo">
    /// True for a step that can simply be done again from its anchor: it clicks into its field and
    /// replaces what is there (select all, then type). When Bsoft was used during a pause, the last
    /// step is redone on continuing only if it is one of these.
    /// </param>
    public void Step(string name, string anchor, Action actions, bool redo = false, int waitMs = 10000,
        double matching = DefaultMatching)
    {
        Require(anchor, waitMs, matching);
        Run(new StepInfo(name, anchor, waitMs, matching, actions, redo));
    }

    /// <summary>
    /// A group of actions on the element found last (by <see cref="Find"/>), kept whole across a pause
    /// like a <see cref="Step"/>. It has no anchor of its own, so it is never redone.
    /// </summary>
    public void Act(string name, Action actions) => Run(new StepInfo(name, null, 0, 0, actions, Redo: false));

    private void Run(StepInfo step)
    {
        if (_inStep) throw new InvalidOperationException($"O passo \"{step.Name}\" começou dentro de outro passo.");
        if (Foreign(_desktop.ProcessOf(_desktop.Foreground())) is var working and not 0) _workProcess = working;
        _inStep = true;
        try
        {
            step.Actions();
        }
        finally
        {
            _inStep = false;
        }
        _lastStep = step;
    }

    /// <summary>
    /// A place where a pause may start even though no element is looked for. The driver calls it right
    /// before the point of no return, which is the last chance to stop before the CT-e is sent.
    /// </summary>
    public void Checkpoint() => Gate();

    // ------------------------------------------------------------------ mouse

    /// <summary>Clicks the centre of the last element found.</summary>
    public void Click() => Mouse(Last.CenterX, Last.CenterY, clicks: 1);

    public void DoubleClick() => Mouse(Last.CenterX, Last.CenterY, clicks: 2);

    public void RightClick() => Mouse(Last.CenterX, Last.CenterY, clicks: 1, right: true);

    /// <summary>Clicks at an offset from the top-left corner of the last element found.</summary>
    public void ClickRelative(double dx, double dy) => Mouse(Last.Left + dx, Last.Top + dy, clicks: 1);

    public void DoubleClickRelative(double dx, double dy) => Mouse(Last.Left + dx, Last.Top + dy, clicks: 2);

    /// <summary>Clicks an absolute screen position. No wait afterwards.</summary>
    public void ClickAt(double x, double y)
    {
        Gate();
        MouseAt(x, y, clicks: 1, right: false, out var before);
        before?.Dispose();
    }

    private void Mouse(double x, double y, int clicks, bool right = false)
    {
        Gate();
        MouseAt(x, y, clicks, right, out var before);
        After(before, AfterActionMs);
    }

    /// <param name="before">
    /// The screen right before the click, with the pointer already in place: what the pointer lights
    /// up by hovering is in it, so only the click's own effect counts as the screen's answer.
    /// </param>
    private void MouseAt(double x, double y, int clicks, bool right, out Mat? before)
    {
        _desktop.MoveTo(x, y);
        _desktop.Sleep(100);
        before = Before();
        for (var i = 0; i < clicks; i++) _desktop.MouseClick(right);
    }

    // ------------------------------------------------------------------ keyboard

    /// <param name="waitMs">
    /// Left out: the bot goes on when the screen answered. Given: that fixed time is waited, for the
    /// places where Bsoft needs longer than it shows.
    /// </param>
    public void Press(Key key, int? waitMs = null)
    {
        Gate();
        var before = waitMs is null ? Before() : null;
        _desktop.Tap(key);
        After(before, waitMs ?? AfterActionMs);
    }

    public void Tab(int? waitMs = null) => Press(Key.Tab, waitMs);
    public void Enter() => Press(Key.Enter);

    /// <summary>Ctrl plus a letter (Ctrl+A, Ctrl+C, Ctrl+V).</summary>
    public void Control(char letter)
    {
        Gate();
        var before = Before();
        Chord(letter);
        After(before, AfterActionMs);
    }

    private void Chord(char letter)
    {
        _desktop.KeyDown(Key.Control);
        try
        {
            _desktop.TapChar(letter);
        }
        finally
        {
            _desktop.KeyUp(Key.Control);
        }
    }

    public void SelectAll() => Control('a');

    /// <summary>Types text with no pause between keys.</summary>
    public void TypeKey(string text)
    {
        Gate();
        var before = Before();
        foreach (var ch in text) _desktop.TapChar(ch);
        After(before, AfterActionMs);
    }

    /// <summary>Types text slowly, 200 ms per key, for the fields that drop fast input.</summary>
    public void TypeKeys(string text)
    {
        Gate();
        foreach (var ch in text)
        {
            _desktop.TapChar(ch);
            _desktop.Sleep(200);
        }
    }

    /// <summary>Types text the way a person does (Shift for capitals), pausing between keys.</summary>
    public void TypeLikeAPerson(string text, int intervalMs)
    {
        Gate();
        foreach (var ch in text)
        {
            _desktop.TypeLikeAPerson(ch);
            _desktop.Sleep(intervalMs);
        }
        _desktop.Sleep(100);
    }

    /// <summary>
    /// Puts text on the clipboard and pastes it with Ctrl+V. The clipboard is read back first: pasting
    /// only happens once it holds the text, and never when another program put something else there.
    /// </summary>
    public void PasteText(string text)
    {
        Gate();
        _desktop.SetClipboard(text);
        if (!ClipboardHolds(text))
        {
            // Another program wrote to the clipboard in between: once more, then give up.
            _desktop.SetClipboard(text);
            if (!ClipboardHolds(text))
                throw new InvalidOperationException(
                    "Outro programa alterou a área de transferência antes de o robô colar o texto. Nada foi colado.");
        }
        var before = Before();
        Chord('v');
        After(before, 2 * AfterActionMs);
    }

    /// <summary>
    /// Waits (at most <see cref="AfterActionMs"/>) until the clipboard can be read and holds the text.
    /// False only when it holds a different text; a clipboard that stays busy counts as fine, as before.
    /// </summary>
    private bool ClipboardHolds(string text)
    {
        static string Plain(string value) => value.Replace("\r", "").TrimEnd('\0');

        var wanted = Plain(text);
        var started = _desktop.Now;
        if (!Adaptive) _desktop.Sleep(AfterActionMs);
        while (true)
        {
            var held = _desktop.TryGetClipboard();
            if (held is not null && Plain(held) == wanted) return true;
            if (_desktop.Now - started >= AfterActionMs) return held is null;
            _desktop.Sleep(20);
        }
    }

    public void Wait(int ms)
    {
        Gate();
        _desktop.Sleep(ms);
    }

    // ------------------------------------------------------------------ clipboard and clock for the driver

    public void ClearClipboard() => _desktop.ClearClipboard();

    /// <summary>The clipboard text, or null when another program has the clipboard right now.</summary>
    public string? ClipboardNow() => _desktop.TryGetClipboard();

    /// <summary>A plain wait that is not a pause point.</summary>
    public void Sleep(int ms) => _desktop.Sleep(ms);

    public long Now => _desktop.Now;

    /// <summary>The text of the control that has the keyboard focus, when Windows can read it.</summary>
    public string? FocusedText() => _desktop.FocusedText();

    // ------------------------------------------------------------------ waiting for the screen to answer

    /// <summary>
    /// The screen right before an action. The answer is then looked for only inside the window that
    /// has the keyboard (Bsoft): a clock or an animation somewhere else on the screen is neither an
    /// answer nor a reason to keep waiting.
    /// </summary>
    private Mat? Before()
    {
        if (!Adaptive) return null;
        _watched = _desktop.ForegroundArea();
        return _screen.Capture();
    }

    /// <summary>
    /// Waits for Bsoft to answer the action just sent: until the screen changed and then stayed the
    /// same for <see cref="QuietMs"/>. Never longer than <paramref name="maxMs"/>, the fixed wait this
    /// replaces; when nothing changes at all that whole time is waited, exactly as before.
    /// </summary>
    private void After(Mat? before, int maxMs)
    {
        if (before is null)
        {
            _desktop.Sleep(maxMs);
            return;
        }

        var previous = before;
        try
        {
            var started = _desktop.Now;
            long changedAt = -1;
            _desktop.Sleep(Math.Min(FloorMs, maxMs));
            while (true)
            {
                // Not enough time left for another look to matter: wait it out, like before.
                var remaining = maxMs - (_desktop.Now - started);
                if (remaining <= LookMs)
                {
                    if (remaining > 0) _desktop.Sleep((int)remaining);
                    return;
                }

                var shot = _screen.Capture();
                var changed = Differs(previous, shot, _watched);
                previous.Dispose();
                previous = shot;

                var now = _desktop.Now;
                if (changed) changedAt = now;
                else if (changedAt >= 0 && now - changedAt >= QuietMs) return;

                var left = maxMs - (now - started);
                if (left > 0) _desktop.Sleep((int)Math.Min(SettlePollMs, left));
            }
        }
        finally
        {
            previous.Dispose();
        }
    }

    /// <summary>
    /// Whether two screenshots differ by more than a blinking text cursor, inside <paramref name="area"/>
    /// (the whole screen when null or off screen).
    /// </summary>
    private static bool Differs(Mat a, Mat b, ScreenBox? area)
    {
        if (a.Rows != b.Rows || a.Cols != b.Cols) return true;
        var whole = new Rect(0, 0, a.Cols, a.Rows);
        var part = area is { } box ? whole & new Rect(box.Left, box.Top, box.Width, box.Height) : whole;
        if (part.Width < 8 || part.Height < 8) part = whole;

        using var first = new Mat(a, part);
        using var second = new Mat(b, part);
        using var difference = new Mat();
        Cv2.Absdiff(first, second, difference);
        // One value per colour channel (the result of Absdiff is always one block of memory).
        using var values = difference.Reshape(1);
        return Cv2.CountNonZero(values) > NoiseSamples;
    }

    // ------------------------------------------------------------------ pause gate

    /// <summary>
    /// The pause/stop checkpoint; returns the milliseconds spent parked here. Inside a step only a
    /// stop is honoured. Outside one, a pause parks the bot; meanwhile Bsoft is watched, and when the
    /// pause ends the keyboard goes back to the window that had it and the last step is redone if
    /// Bsoft was used.
    /// </summary>
    private long Gate()
    {
        if (_inStep)
        {
            _control.ThrowIfStopped();
            return 0;
        }
        if (!_control.IsPauseRequested || _control.InCritical)
        {
            _control.Checkpoint();
            return 0;
        }

        var started = _desktop.Now;
        var foreground = _desktop.Foreground();
        var watcher = StartWatching(foreground);
        TimeSpan paused;
        bool? used;
        try
        {
            paused = _control.Checkpoint();
        }
        finally
        {
            used = watcher is null ? false : watcher.Stop();
            _control.SetPauseNote(null);
        }
        if (paused <= TimeSpan.Zero) return 0;

        if (foreground != IntPtr.Zero && _desktop.Foreground() != foreground)
        {
            if (!_desktop.GiveForeground(foreground))
                _log.Warn("Não foi possível devolver o foco ao Bsoft depois da pausa.");
            _desktop.Sleep(400);
        }
        AfterPause(used);
        _last = null;
        return _desktop.Now - started;
    }

    private IPauseWatcher? StartWatching(IntPtr foreground)
    {
        if (_newWatcher is null || _redoing) return null;
        // Normally Bsoft has the keyboard when the pause starts. When the operator paused from this
        // program's own window, it is the program the bot was last working in that is watched.
        var process = Foreign(_desktop.ProcessOf(foreground));
        if (process == 0) process = _workProcess;
        if (process == 0) return null;

        var watcher = _newWatcher();
        var step = _lastStep;
        watcher.Start(process, () => _control.SetPauseNote(step switch
        {
            null => "O Bsoft foi usado durante a pausa. Deixe a tela como estava antes de continuar.",
            { Redo: true } => $"O Bsoft foi usado durante a pausa. Ao continuar, o robô refaz o passo \"{step.Name}\".",
            _ => $"O Bsoft foi usado durante a pausa. O robô não refaz sozinho o passo \"{step.Name}\": confira-o antes de continuar.",
        }));
        return watcher;
    }

    /// <summary>The program id, or 0 when it is unknown or this program itself.</summary>
    private static uint Foreign(uint process) => process == (uint)Environment.ProcessId ? 0 : process;

    /// <param name="used">True: Bsoft got keys or clicks during the pause. Null: that could not be watched.</param>
    private void AfterPause(bool? used)
    {
        if (used == false || _redoing || _lastStep is not { } step) return;

        if (!step.Redo || step.Anchor is null)
        {
            if (used == true)
                _log.Warn($"O Bsoft foi usado durante a pausa. O passo \"{step.Name}\" não é refeito automaticamente.");
            return;
        }

        _log.Warn(used == true
            ? $"O Bsoft foi usado durante a pausa: refazendo o passo \"{step.Name}\"."
            : $"Não foi possível observar o Bsoft durante a pausa: refazendo o passo \"{step.Name}\" por segurança.");
        _redoing = true;
        try
        {
            if (!Find(step.Anchor, step.Matching, RedoWaitMs, recover: false))
            {
                _log.Warn($"O campo do passo \"{step.Name}\" não está mais na tela; o robô segue sem refazê-lo.");
                return;
            }
            Run(step);
        }
        finally
        {
            _redoing = false;
        }
    }
}

/// <summary>Watches, during a pause, whether the program the bot was working in gets keys or clicks.</summary>
public interface IPauseWatcher
{
    /// <param name="process">The program to watch.</param>
    /// <param name="used">Called (on another thread) when it is used; may be called more than once.</param>
    void Start(uint process, Action used);

    /// <summary>Stops watching. True when the program was used, null when that could not be watched.</summary>
    bool? Stop();
}

/// <summary>The real watcher: the same keyboard and mouse hooks that record an unknown screen.</summary>
public sealed class PauseWatcher : IPauseWatcher
{
    private InputRecorder? _recorder;

    public void Start(uint process, Action used)
    {
        _recorder = new InputRecorder { OnlyProcess = process, Seen = used };
        _recorder.Start();
    }

    public bool? Stop()
    {
        if (_recorder is not { } recorder) return false;
        _recorder = null;
        var seen = recorder.Stop();
        if (!recorder.Installed) return null;
        return seen.Steps.Count > 0 || seen.Unrepeatable is not null;
    }
}
