using CteLogLife.Bsoft;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Tests;

/// <summary>Stands in for the operator's mouse and keyboard: the test says what was "done".</summary>
public sealed class FakeRecorder : IInputRecorder
{
    public bool Started { get; private set; }
    public Recording Result { get; set; } = Recording.Empty;

    public void Start() => Started = true;
    public Recording Stop() => Result;
}

/// <summary>Records what the bot repeats instead of sending it to the desktop.</summary>
public sealed class FakeRecoveryInput : IRecoveryInput
{
    public List<string> Actions { get; } = [];
    public Action? OnAction { get; set; }

    public void Click(int x, int y, RecordedKind kind)
    {
        Actions.Add($"{kind} {x},{y}");
        OnAction?.Invoke();
    }

    public void Key(int key, bool alt, bool shift)
    {
        Actions.Add($"Key {(alt ? "Alt+" : "")}{(shift ? "Shift+" : "")}0x{key:X2}");
        OnAction?.Invoke();
    }

    public void Wait(int ms)
    {
    }
}

public class UnknownScreenTests : IDisposable
{
    private const string Control = "confirmCNPJ";
    private const int ControlX = 300, ControlY = 200;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cte-unknown-" + Guid.NewGuid().ToString("N"));
    private readonly TemplateStore _templates = new(Path.Combine(AppContext.BaseDirectory, "Templates"));
    private readonly FakeScreen _screen = new();
    private readonly RunControl _control = new();
    private readonly ListLog _log = new();
    private readonly FakeRecorder _recorder = new();
    private readonly FakeRecoveryInput _input = new();
    private readonly LearnedScreenStore _learned;
    private DesktopBot _bot;

    public UnknownScreenTests()
    {
        _learned = new LearnedScreenStore(Path.Combine(_root, "learned"));
        _bot = NewBot(learn: true);
    }

    private string Shots => Path.Combine(_root, "shots");

    private DesktopBot NewBot(bool learn) => new(_templates, _screen, _control, _log,
        new UnknownScreens(_learned, _control, _log, Shots, () => _recorder, _input) { Learn = learn })
    {
        RecoverAfterMs = 60,
        RecoverEveryMs = 60,
    };

    public void Dispose()
    {
        _bot.Dispose();
        _templates.Dispose();
        _learned.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A warning dialog as Windows draws one: frame, title bar, a line of text and an OK button.</summary>
    private static Mat Dialog(string message = "Certificado digital expira em 10 dias")
    {
        var dialog = new Mat(160, 360, MatType.CV_8UC3, new Scalar(250, 250, 250));
        Cv2.Rectangle(dialog, new Rect(0, 0, 360, 30), new Scalar(140, 70, 20), -1);
        Cv2.Rectangle(dialog, new Rect(0, 0, 360, 160), new Scalar(90, 90, 90), 2);
        Cv2.PutText(dialog, message, new Point(18, 75), HersheyFonts.HersheySimplex, 0.5, new Scalar(20, 20, 20));
        Cv2.Rectangle(dialog, new Rect(140, 110, 80, 30), new Scalar(225, 225, 225), -1);
        Cv2.Rectangle(dialog, new Rect(140, 110, 80, 30), new Scalar(120, 120, 120));
        Cv2.PutText(dialog, "OK", new Point(168, 131), HersheyFonts.HersheySimplex, 0.5, new Scalar(20, 20, 20));
        return dialog;
    }

    /// <summary>Where the OK button of a dialog shown at (x, y) is.</summary>
    private static (int X, int Y) OkOf(int x, int y) => (x + 180, y + 125);

    private void ShowControl()
    {
        _screen.Blank();
        _screen.Show(_templates.Image(Control)!, ControlX, ControlY);
    }

    /// <summary>
    /// The bot gets stuck behind <paramref name="dialog"/>, the operator "does" <paramref name="done"/>
    /// (which closes it) and continues.
    /// </summary>
    private async Task StuckThenSolved(Mat dialog, int x, int y, Recording done)
    {
        _screen.Blank();
        _screen.Show(dialog, x, y);
        var require = Task.Run(() => _bot.Require(Control, waitMs: 200));
        await WaitUntil(() => _control.Attention is not null);
        _recorder.Result = done;
        ShowControl();
        _control.Resume();
        await require;
    }

    [Fact]
    public async Task An_unknown_screen_is_captured_taught_once_and_then_handled_alone()
    {
        using var dialog = Dialog();
        _screen.Show(dialog, 800, 400);

        var require = Task.Run(() => _bot.Require(Control, waitMs: 200));
        await WaitUntil(() => _control.Attention is not null);

        // The bot stopped, said why and saved what it was looking at.
        var attention = _control.Attention!;
        Assert.Equal("Tela desconhecida", attention.Title);
        Assert.Contains($"\"{Control}\"", attention.Message);
        Assert.Contains("fica gravado", attention.Message);
        Assert.True(File.Exists(attention.ScreenshotPath));
        Assert.True(_control.IsPaused);
        Assert.True(_recorder.Started);
        Assert.False(require.IsCompleted);

        // The operator clicks OK: the dialog closes, the control shows up, and they continue.
        var (okX, okY) = OkOf(800, 400);
        _recorder.Result = new Recording([new RecordedStep(RecordedKind.Click, okX, okY, DelayMs: 2500)]);
        ShowControl();
        _control.Resume();
        await require;

        Assert.Equal((ControlX, ControlY), (_bot.Last.Left, _bot.Last.Top));
        var (screen, picture) = Assert.Single(_learned.Snapshot());
        Assert.Equal(Control, screen.WaitingFor);
        Assert.InRange(picture.Width, 360, 372);  // the dialog and a few pixels around it
        Assert.InRange(picture.Height, 160, 172);
        Assert.Equal(RecordedKind.Click, Assert.Single(screen.Steps).Kind);
        Assert.Empty(_input.Actions);
        Assert.Contains(_log.Lines, line => line.Contains("Tela desconhecida") && line.Contains(attention.ScreenshotPath!));
        Assert.Contains(_log.Lines, line => line.Contains("Tela aprendida") && line.Contains("clique"));

        // Another day the same dialog shows up, somewhere else on screen: no question this time.
        _screen.Blank();
        _screen.Show(dialog, 620, 310);
        _input.OnAction = ShowControl;
        _bot.Require(Control, waitMs: 5000);

        Assert.Null(_control.Attention);
        var (againX, againY) = OkOf(620, 310);
        Assert.Equal([$"Click {againX},{againY}"], _input.Actions);
        Assert.Equal(1, _learned.Snapshot()[0].Screen.Uses);
        Assert.Single(Directory.GetFiles(Shots));
        Assert.Contains(_log.Lines, line => line.Contains("Tela já conhecida"));

        // The learned screen survives a restart of the app.
        using var reopened = new LearnedScreenStore(_learned.Folder);
        var (restored, restoredPicture) = Assert.Single(reopened.Snapshot());
        Assert.Equal(screen.Steps, restored.Steps);
        Assert.Equal(1, restored.Uses);
        Assert.Equal((picture.Width, picture.Height), (restoredPicture.Width, restoredPicture.Height));
    }

    [Fact]
    public async Task A_key_answer_is_learned_and_repeated_too()
    {
        using var dialog = Dialog();
        await StuckThenSolved(dialog, 800, 400,
            new Recording([new RecordedStep(RecordedKind.Key, Key: 0x09), new RecordedStep(RecordedKind.Key, Key: 0x0D)]));
        Assert.Equal(2, Assert.Single(_learned.Snapshot()).Screen.Steps.Count);

        _screen.Blank();
        _screen.Show(dialog, 800, 400);
        _input.OnAction = () =>
        {
            if (_input.Actions.Count == 2) ShowControl();
        };
        _bot.Require(Control, waitMs: 5000);

        Assert.Equal(["Key 0x09", "Key 0x0D"], _input.Actions);
    }

    [Fact]
    public async Task A_dialog_with_another_message_is_not_mistaken_for_a_learned_one()
    {
        using var known = Dialog();
        var (okX, okY) = OkOf(800, 400);
        await StuckThenSolved(known, 800, 400, new Recording([new RecordedStep(RecordedKind.Click, okX, okY)]));
        Assert.Single(_learned.Snapshot());

        using var other = Dialog("Deseja excluir este registro?");
        _screen.Blank();
        _screen.Show(other, 800, 400);
        var require = Task.Run(() => _bot.Require(Control, waitMs: 400));
        await WaitUntil(() => _control.Attention is not null);

        Assert.Empty(_input.Actions); // nothing was clicked: the operator is asked instead
        _control.RequestStop();
        await Assert.ThrowsAsync<RunStoppedException>(() => require);
    }

    [Fact]
    public async Task What_cannot_be_repeated_safely_is_only_written_to_the_log()
    {
        using var dialog = Dialog();
        var (okX, okY) = OkOf(800, 400);

        // Text was typed: never stored, never repeated.
        await StuckThenSolved(dialog, 800, 400, new Recording(
            [new RecordedStep(RecordedKind.Click, okX, okY)],
            "foi digitado texto ou usado um atalho que o robô não repete sozinho"));
        Assert.Empty(_learned.Snapshot());
        Assert.Contains(_log.Lines, line => line.Contains("a ação não foi gravada: foi digitado texto"));

        // The click was nowhere near what went away.
        await StuckThenSolved(dialog, 800, 400, new Recording([new RecordedStep(RecordedKind.Click, 60, 990)]));
        Assert.Empty(_learned.Snapshot());
        Assert.Contains(_log.Lines, line => line.Contains("não pôde ser gravada: o clique não foi sobre algo que saiu da tela"));

        // One click on the dialog and another far from it.
        await StuckThenSolved(dialog, 800, 400, new Recording(
            [new RecordedStep(RecordedKind.Click, okX, okY), new RecordedStep(RecordedKind.Click, 60, 990)]));
        Assert.Empty(_learned.Snapshot());
        Assert.Contains(_log.Lines, line => line.Contains("houve cliques longe da tela que apareceu"));

        // Nothing was done at all: the control was just late.
        await StuckThenSolved(dialog, 800, 400, Recording.Empty);
        Assert.Empty(_learned.Snapshot());
        Assert.Contains(_log.Lines, line => line.Contains("sem nenhuma ação do operador"));

        Assert.Equal(4, Directory.GetFiles(Shots).Length); // every stop left its capture
        Assert.Empty(_input.Actions);
    }

    [Fact]
    public async Task Teaching_a_screen_again_replaces_the_answer_that_did_not_work()
    {
        using var dialog = Dialog();
        var (okX, okY) = OkOf(800, 400);
        await StuckThenSolved(dialog, 800, 400, new Recording([new RecordedStep(RecordedKind.Key, Key: 0x1B)]));

        // The learned Esc does nothing this time, so the operator is asked again and clicks OK.
        _screen.Blank();
        _screen.Show(dialog, 800, 400);
        var require = Task.Run(() => _bot.Require(Control, waitMs: 500));
        await WaitUntil(() => _control.Attention is not null);
        Assert.Equal(["Key 0x1B", "Key 0x1B"], _input.Actions); // tried twice, then gave up on it
        _recorder.Result = new Recording([new RecordedStep(RecordedKind.Click, okX, okY)]);
        ShowControl();
        _control.Resume();
        await require;

        var (screen, _) = Assert.Single(_learned.Snapshot());
        Assert.Equal(RecordedKind.Click, Assert.Single(screen.Steps).Kind);
    }

    [Fact]
    public async Task Stopping_at_an_unknown_screen_ends_the_run_before_the_point_of_no_return()
    {
        var require = Task.Run(() => _bot.Require(Control, waitMs: 150));
        await WaitUntil(() => _control.Attention is not null);
        _control.RequestStop();
        await Assert.ThrowsAsync<RunStoppedException>(() => require);
        Assert.Empty(_learned.Snapshot());
    }

    [Fact]
    public async Task Stopping_after_the_point_of_no_return_reports_the_missing_control()
    {
        _control.BeginCritical();
        var require = Task.Run(() => _bot.Require("fifthPopUp", waitMs: 150));
        await WaitUntil(() => _control.Attention is not null); // the wait holds although a pause would not
        _control.RequestStop();
        var error = await Assert.ThrowsAsync<ElementNotFoundException>(() => require);
        Assert.Equal("fifthPopUp", error.Label);
    }

    [Fact]
    public async Task With_learning_off_the_bot_still_stops_and_captures_but_records_nothing()
    {
        _bot = NewBot(learn: false); // same screen, so the first bot is not disposed here
        using var dialog = Dialog();
        var (okX, okY) = OkOf(800, 400);
        _recorder.Result = new Recording([new RecordedStep(RecordedKind.Click, okX, okY)]);

        _screen.Show(dialog, 800, 400);
        var require = Task.Run(() => _bot.Require(Control, waitMs: 200));
        await WaitUntil(() => _control.Attention is not null);
        Assert.DoesNotContain("gravado", _control.Attention!.Message);
        ShowControl();
        _control.Resume();
        await require;

        Assert.False(_recorder.Started);
        Assert.Empty(_learned.Snapshot());
        Assert.Single(Directory.GetFiles(Shots));
    }

    [Fact]
    public void Forgetting_removes_the_pictures_from_disk()
    {
        using var picture = Dialog();
        _learned.Add(new LearnedScreen
        {
            Id = "one", Learned = DateTime.Now, Steps = [new LearnedStep(RecordedKind.Key, Key: 0x0D)],
        }, picture);
        Assert.Equal(1, _learned.Count);
        Assert.True(File.Exists(Path.Combine(_learned.Folder, "one.png")));

        var changes = 0;
        _learned.Changed += () => changes++;
        _learned.Clear();

        Assert.Equal(0, _learned.Count);
        Assert.False(File.Exists(Path.Combine(_learned.Folder, "one.png")));
        Assert.Equal(1, changes);
        using var reopened = new LearnedScreenStore(_learned.Folder);
        Assert.Equal(0, reopened.Count);
    }

    // ------------------------------------------------------------------ what changed on screen

    private static Mat Desktop() => new(1080, 1920, MatType.CV_8UC3, new Scalar(240, 240, 240));

    private static void Put(Mat screen, Mat piece, int x, int y)
    {
        using var region = new Mat(screen, new Rect(x, y, piece.Width, piece.Height));
        piece.CopyTo(region);
    }

    [Fact]
    public void The_changed_area_is_the_dialog_also_when_only_keys_were_used()
    {
        using var before = Desktop();
        using var after = Desktop();
        using var dialog = Dialog();
        Put(before, dialog, 500, 300);
        // A clock ticking in a corner must not matter.
        Cv2.PutText(after, "14:31", new Point(1840, 1065), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 0, 0));

        var (area, reason) = ScreenChange.Find(before, after, anchor: null);

        Assert.Null(reason);
        Assert.Equal(new Rect(496, 296, 368, 168), area);
    }

    [Fact]
    public void Changes_that_do_not_identify_a_screen_are_refused_with_the_reason()
    {
        using var before = Desktop();
        using var dialog = Dialog();

        using (var same = Desktop())
            Assert.Equal("nada mudou na tela depois da ação", ScreenChange.Find(before, same, null).Reason);

        using (var small = Desktop())
        {
            Cv2.Rectangle(small, new Rect(900, 500, 30, 20), new Scalar(0, 0, 200), -1);
            Assert.Contains("pequena demais", ScreenChange.Find(before, small, new Point(910, 510)).Reason);
        }

        using (var everything = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(30, 30, 30)))
            Assert.Contains("tela inteira mudou", ScreenChange.Find(before, everything, new Point(900, 500)).Reason);

        using (var two = Desktop())
        {
            Put(two, dialog, 200, 200);
            Put(two, dialog, 1300, 700);
            Assert.Contains("mais de uma parte da tela mudou", ScreenChange.Find(before, two, null).Reason);
            // With a click there is no doubt which one it was.
            Assert.Equal(new Rect(1296, 696, 368, 168), ScreenChange.Find(before, two, new Point(1480, 825)).Area);
        }

        using (var resized = new Mat(768, 1366, MatType.CV_8UC3, new Scalar(240, 240, 240)))
            Assert.Contains("resolução", ScreenChange.Find(before, resized, null).Reason);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
    }
}
