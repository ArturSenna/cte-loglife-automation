using System.Globalization;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Bsoft;

/// <summary>
/// What the operator did on one unknown screen, kept until the bot sees whether it solved the problem.
/// </summary>
public sealed class Lesson(Mat before, Recording recording, string label, string? screenshotPath) : IDisposable
{
    /// <summary>The screen as it was when the bot got stuck.</summary>
    public Mat Before { get; } = before;

    public Recording Recording { get; } = recording;
    public string Label { get; } = label;
    public string? ScreenshotPath { get; } = screenshotPath;

    public void Dispose() => Before.Dispose();
}

/// <summary>
/// What the bot does when a control it needs does not show up: capture the screen, stop and wait for
/// the operator, record what the operator does, and the next time the same screen appears repeat it
/// without asking. Whatever cannot be learned safely is written to the log with the capture.
/// </summary>
public sealed class UnknownScreens
{
    /// <summary>How alike the screen must be to a learned one before its action is repeated.</summary>
    public const double Matching = 0.97;

    private const int MaxRepeatsPerWait = 2;
    private const int KeptScreenshots = 150;

    private readonly LearnedScreenStore _store;
    private readonly RunControl _control;
    private readonly IRunLog _log;
    private readonly string _screenshotFolder;
    private readonly Func<IInputRecorder> _newRecorder;
    private readonly IRecoveryInput _input;

    public UnknownScreens(LearnedScreenStore store, RunControl control, IRunLog log, string screenshotFolder,
        Func<IInputRecorder>? newRecorder = null, IRecoveryInput? input = null)
    {
        _store = store;
        _control = control;
        _log = log;
        _screenshotFolder = screenshotFolder;
        _newRecorder = newRecorder ?? (() => new InputRecorder());
        _input = input ?? new SendInputRecovery();
    }

    /// <summary>Off: the bot still stops, captures and waits, but records and repeats nothing.</summary>
    public bool Learn { get; init; } = true;

    public Func<DateTime> Now { get; init; } = () => DateTime.Now;

    // ------------------------------------------------------------------ a screen seen before

    /// <summary>
    /// Looks for a learned screen in <paramref name="shot"/> and repeats what the operator did on it.
    /// <paramref name="repeats"/> counts the repetitions of one wait, so an action that does not clear
    /// its screen is not repeated for ever. Returns true when something was done.
    /// </summary>
    public bool TryRepeat(Mat shot, string label, Dictionary<string, int> repeats)
    {
        if (!Learn) return false;
        foreach (var (screen, picture) in _store.Snapshot())
        {
            if (repeats.GetValueOrDefault(screen.Id) >= MaxRepeatsPerWait) continue;
            if (TemplateMatcher.Find(shot, picture, Matching) is not { } found) continue;

            repeats[screen.Id] = repeats.GetValueOrDefault(screen.Id) + 1;
            _log.Warn($"Tela já conhecida apareceu enquanto o robô esperava '{label}'. Repetindo o que foi feito nela em "
                      + $"{screen.Learned:dd/MM/yyyy HH:mm} ({Describe(screen.Steps)}).");
            foreach (var step in screen.Steps)
            {
                if (_control.IsStopRequested) return true;
                _input.Wait(Math.Clamp(step.DelayMs, 300, 1500));
                if (step.Kind == RecordedKind.Key) _input.Key(step.Key, step.Alt, step.Shift);
                else _input.Click(found.Left + step.Dx, found.Top + step.Dy, step.Kind);
            }
            _input.Wait(600);
            _store.MarkUsed(screen.Id, Now());
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ a screen never seen

    /// <summary>
    /// Saves the capture, shows the operator what happened and waits. While waiting, what the operator
    /// does is recorded. Returns null when the operator asked to stop instead of continuing.
    /// </summary>
    public Lesson? AskOperator(Mat shot, string label)
    {
        var path = SaveScreenshot(shot, label);
        _log.Warn($"Tela desconhecida: o elemento '{label}' não apareceu. "
                  + (path is null ? "A captura não pôde ser salva." : $"Captura: {path}.")
                  + " Aguardando o operador.");

        var message = $"O robô esperava o elemento \"{label}\" e ele não apareceu.\n"
                      + "Resolva a tela no Bsoft e depois clique em Continuar."
                      + (Learn ? "\nO que você fizer nela fica gravado e é repetido quando ela aparecer de novo." : "");

        var recorder = Learn ? _newRecorder() : null;
        using var focus = new ForegroundTracker();
        Recording recording;
        bool resumed;
        recorder?.Start();
        try
        {
            resumed = _control.WaitForOperator(new OperatorAttention("Tela desconhecida", message, path));
        }
        finally
        {
            recording = recorder?.Stop() ?? Recording.Empty;
        }

        if (!resumed)
        {
            _log.Warn($"O operador interrompeu a execução na tela desconhecida (esperando '{label}').");
            return null;
        }
        if (!focus.GiveBack()) _log.Warn("Não foi possível devolver o foco ao Bsoft depois da tela desconhecida.");
        return new Lesson(shot.Clone(), recording, label, path);
    }

    /// <summary>
    /// Called once the control the bot wanted is on screen again: turns the operator's action into a
    /// learned screen, or writes to the log why it could not be learned.
    /// </summary>
    public void Finish(Lesson lesson, Mat after)
    {
        if (!Learn) return;
        var recording = lesson.Recording;
        var where = lesson.ScreenshotPath is null ? "" : $" Captura: {lesson.ScreenshotPath}.";

        if (recording.Unrepeatable is { } why)
        {
            _log.Warn($"A tela desconhecida (esperando '{lesson.Label}') foi resolvida, mas a ação não foi gravada: {why}.{where}");
            return;
        }
        if (recording.Steps.Count == 0)
        {
            _log.Info($"O elemento '{lesson.Label}' apareceu sem nenhuma ação do operador (o Bsoft estava lento?). Nada foi gravado.{where}");
            return;
        }

        var clicks = recording.Steps.Where(step => step.Kind != RecordedKind.Key).ToList();
        Point? anchor = clicks.Count > 0 ? new Point(clicks[0].X, clicks[0].Y) : null;
        var (area, reason) = ScreenChange.Find(lesson.Before, after, anchor);
        if (area is not { } region)
        {
            _log.Warn($"A tela desconhecida (esperando '{lesson.Label}') foi resolvida, mas não pôde ser gravada: {reason}.{where}");
            return;
        }
        var reach = new Rect(region.X - ScreenChange.ClickMargin, region.Y - ScreenChange.ClickMargin,
            region.Width + 2 * ScreenChange.ClickMargin, region.Height + 2 * ScreenChange.ClickMargin);
        if (clicks.Any(click => !reach.Contains(new Point(click.X, click.Y))))
        {
            _log.Warn($"A tela desconhecida (esperando '{lesson.Label}') foi resolvida, mas não foi gravada: "
                      + $"houve cliques longe da tela que apareceu.{where}");
            return;
        }

        using var picture = new Mat(lesson.Before, region).Clone();

        // Taught again: the newest answer replaces the one that did not solve it.
        foreach (var (known, knownPicture) in _store.Snapshot())
            if (TemplateMatcher.Find(picture, knownPicture, Matching) is not null ||
                TemplateMatcher.Find(knownPicture, picture, Matching) is not null)
                _store.Remove(known.Id);

        var now = Now();
        var screen = _store.Add(new LearnedScreen
        {
            Id = $"{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            Learned = now,
            WaitingFor = lesson.Label,
            Width = region.Width,
            Height = region.Height,
            Steps = recording.Steps.Select(step => step.Kind == RecordedKind.Key
                ? new LearnedStep(step.Kind, Key: step.Key, Alt: step.Alt, Shift: step.Shift, DelayMs: step.DelayMs)
                : new LearnedStep(step.Kind, step.X - region.X, step.Y - region.Y, DelayMs: step.DelayMs)).ToList(),
        }, picture);
        _log.Warn($"Tela aprendida (esperando '{lesson.Label}'): {Describe(screen.Steps)}. Quando ela aparecer de novo, o robô "
                  + $"repete sozinho. Imagem: {Path.Combine(_store.Folder, screen.Id + ".png")}");
    }

    // ------------------------------------------------------------------ helpers

    private static string Describe(IReadOnlyList<LearnedStep> steps) => string.Join(", ", steps.Select(step => step.Kind switch
    {
        RecordedKind.Click => "clique",
        RecordedKind.DoubleClick => "clique duplo",
        RecordedKind.RightClick => "clique direito",
        _ => "tecla " + KeyName(step),
    }));

    private static string KeyName(LearnedStep step)
    {
        var name = step.Key switch
        {
            0x0D => "Enter",
            0x1B => "Esc",
            0x09 => "Tab",
            0x20 => "Espaço",
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            >= 0x30 and <= 0x5A => ((char)step.Key).ToString(),
            _ => "0x" + step.Key.ToString("X2", CultureInfo.InvariantCulture),
        };
        return (step.Alt ? "Alt+" : "") + (step.Shift ? "Shift+" : "") + name;
    }

    private string? SaveScreenshot(Mat shot, string label)
    {
        try
        {
            Directory.CreateDirectory(_screenshotFolder);
            var safe = new string(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray());
            var path = Path.Combine(_screenshotFolder, $"{Now():yyyy-MM-dd_HH-mm-ss-fff}_{safe}.png");
            Cv2.ImEncode(".png", shot, out var png);
            File.WriteAllBytes(path, png);

            // The captures show customer data: only the most recent ones are kept.
            foreach (var old in Directory.GetFiles(_screenshotFolder, "*.png").OrderByDescending(file => file).Skip(KeptScreenshots))
                File.Delete(old);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OpenCVException)
        {
            return null;
        }
    }
}

/// <summary>
/// Remembers the last window in front that is not one of ours. The operator may end a wait by clicking
/// this program's panel, and the bot's next keystroke has to go to Bsoft, not to the panel.
/// </summary>
internal sealed class ForegroundTracker : IDisposable
{
    private readonly uint _ownProcess = (uint)Environment.ProcessId;
    private readonly Timer _timer;
    private IntPtr _lastForeign;

    public ForegroundTracker()
    {
        Sample();
        _timer = new Timer(_ => Sample(), null, 150, 150);
    }

    private void Sample()
    {
        var window = Native.GetForegroundWindow();
        if (window != IntPtr.Zero && !IsOwn(window)) _lastForeign = window;
    }

    private bool IsOwn(IntPtr window)
    {
        Native.GetWindowThreadProcessId(window, out var process);
        return process == _ownProcess;
    }

    /// <summary>Puts that window back in front when one of ours took its place. False when Windows refused.</summary>
    public bool GiveBack()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        var current = Native.GetForegroundWindow();
        if (current == IntPtr.Zero || !IsOwn(current)) return true;
        if (_lastForeign == IntPtr.Zero || !Native.IsWindow(_lastForeign)) return true;
        var given = Native.SetForegroundWindow(_lastForeign);
        Thread.Sleep(400);
        return given;
    }

    public void Dispose() => _timer.Dispose();
}
