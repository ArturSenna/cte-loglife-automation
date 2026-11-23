using CteLogLife.Bsoft;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Tests;

/// <summary>
/// Steps, pauses and waits of the bot, on a fake screen and a fake clock: a pause never cuts a step in
/// half, a step is redone when Bsoft was used during the pause, and the bot goes on as soon as the
/// screen answered instead of always waiting the same time.
/// </summary>
public class StepTests : IDisposable
{
    private const string FieldA = "carga_campo_natureza", FieldB = "carga_campo_peso";

    private readonly TemplateStore _templates = new(Path.Combine(AppContext.BaseDirectory, "Templates"));
    private readonly FakeScreen _screen = new(1400, 900); // smaller than a real screen: the searches run faster
    private readonly FakeDesktop _desktop = new();
    private readonly FakePauseWatcher _watcher = new();
    private readonly RunControl _control = new();
    private readonly ListLog _log = new();
    private readonly DesktopBot _bot;

    public StepTests()
    {
        _bot = NewBot(adaptive: true);
        _screen.Show(_templates.Image(FieldA)!, 50, 50);
        _screen.Show(_templates.Image(FieldB)!, 50, 200);
    }

    private DesktopBot NewBot(bool adaptive) =>
        new(_templates, _screen, _control, _log, desktop: _desktop, pauseWatcher: () => _watcher) { Adaptive = adaptive };

    public void Dispose()
    {
        _bot.Dispose();
        _templates.Dispose();
    }

    private void TypeIntoA() => _bot.Step("Natureza", FieldA, () =>
    {
        _bot.Click();
        _bot.SelectAll();
        _bot.TypeKey("AB");
    }, redo: true);

    private void ClickB() => _bot.Step("Pesos", FieldB, _bot.Click);

    private static readonly string[] TypedIntoA =
        ["click", "down Control", "char a", "up Control", "char A", "char B"];

    /// <summary>The events without the mouse positions.</summary>
    private List<string> Sent() => _desktop.Events.Where(e => !e.StartsWith("move ")).ToList();

    private async Task WaitUntilPaused()
    {
        for (var i = 0; i < 2000 && !_control.IsPaused; i++) await Task.Delay(5);
        Assert.True(_control.IsPaused, "o robô não chegou a pausar");
    }

    // ------------------------------------------------------------------ a pause and a step

    [Fact]
    public async Task A_pause_asked_in_the_middle_of_a_step_starts_when_the_step_ends()
    {
        _desktop.OnEvent = e =>
        {
            if (e == "click" && _desktop.Events.Count(x => x == "click") == 1) _control.RequestPause();
        };
        var work = Task.Run(() =>
        {
            TypeIntoA();
            ClickB();
        });

        await WaitUntilPaused();
        Assert.Equal(TypedIntoA, Sent()); // the whole first step, nothing of the second

        _control.Resume();
        await work;
        Assert.Equal([.. TypedIntoA, "click"], Sent());
        Assert.DoesNotContain(_log.Lines, line => line.Contains("refazendo"));
    }

    [Fact]
    public void A_stop_asked_in_the_middle_of_a_step_takes_effect_at_once()
    {
        _desktop.OnEvent = e =>
        {
            if (e == "click") _control.RequestStop();
        };
        Assert.Throws<RunStoppedException>(TypeIntoA);
        Assert.Equal(["click"], Sent());
    }

    [Fact]
    public void An_element_cannot_be_looked_for_in_the_middle_of_a_step()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            _bot.Step("Natureza", FieldA, () => _bot.Find(FieldB, 0.97, 10)));
        Assert.Contains("no meio de um passo", error.Message);
    }

    [Fact]
    public async Task The_checkpoint_before_sending_is_a_place_to_pause()
    {
        TypeIntoA();
        _control.RequestPause();
        var work = Task.Run(_bot.Checkpoint);
        await WaitUntilPaused();
        _control.Resume();
        await work;
        Assert.Equal(TypedIntoA, Sent());
    }

    // ------------------------------------------------------------------ what happened during the pause

    [Fact]
    public async Task When_bsoft_was_used_during_the_pause_the_last_step_is_done_again()
    {
        TypeIntoA();
        _control.RequestPause();
        var work = Task.Run(ClickB);
        await WaitUntilPaused();

        Assert.True(_watcher.Started);
        Assert.Equal(_desktop.ForegroundProcess, _watcher.Process);
        Assert.Null(_control.PauseNote);

        _watcher.UseBsoft(); // the operator types or clicks in Bsoft
        Assert.Contains("refaz o passo \"Natureza\"", _control.PauseNote);

        _control.Resume();
        await work;
        Assert.Equal([.. TypedIntoA, .. TypedIntoA, "click"], Sent());
        Assert.Contains(_log.Lines, line => line.Contains("refazendo o passo \"Natureza\""));
        Assert.Null(_control.PauseNote);
    }

    [Fact]
    public async Task When_nobody_touched_bsoft_the_bot_just_goes_on()
    {
        TypeIntoA();
        _control.RequestPause();
        var work = Task.Run(ClickB);
        await WaitUntilPaused();
        _control.Resume();
        await work;

        Assert.Equal([.. TypedIntoA, "click"], Sent());
        Assert.DoesNotContain(_log.Lines, line => line.StartsWith("WARN"));
    }

    [Fact]
    public async Task A_step_that_cannot_be_redone_is_pointed_out_instead()
    {
        ClickB(); // a plain click: doing it again could do something else
        _control.RequestPause();
        var work = Task.Run(TypeIntoA);
        await WaitUntilPaused();

        _watcher.UseBsoft();
        Assert.Contains("não refaz sozinho o passo \"Pesos\"", _control.PauseNote);

        _control.Resume();
        await work;
        Assert.Equal(["click", .. TypedIntoA], Sent());
        Assert.Contains(_log.Lines, line => line.Contains("\"Pesos\" não é refeito"));
    }

    [Fact]
    public async Task When_the_field_of_the_step_is_gone_nothing_is_redone()
    {
        TypeIntoA();
        _control.RequestPause();
        var work = Task.Run(ClickB);
        await WaitUntilPaused();

        _watcher.UseBsoft();
        _screen.Blank(); // the operator moved on to another screen
        _screen.Show(_templates.Image(FieldB)!, 50, 200);
        _control.Resume();
        await work;

        Assert.Equal([.. TypedIntoA, "click"], Sent());
        Assert.Contains(_log.Lines, line => line.Contains("não está mais na tela"));
    }

    [Fact]
    public async Task When_the_pause_could_not_be_watched_the_step_is_redone_to_be_safe()
    {
        _watcher.Used = null; // Windows refused the hooks
        TypeIntoA();
        _control.RequestPause();
        var work = Task.Run(ClickB);
        await WaitUntilPaused();
        _control.Resume();
        await work;

        Assert.Equal([.. TypedIntoA, .. TypedIntoA, "click"], Sent());
        Assert.Contains(_log.Lines, line => line.Contains("por segurança"));
    }

    [Fact]
    public async Task Bsoft_is_watched_also_when_the_pause_was_asked_from_this_programs_window()
    {
        TypeIntoA(); // typed into "Bsoft", which had the keyboard
        var bsoft = _desktop.ForegroundProcess;
        _desktop.ForegroundWindow = new IntPtr(0x0E11A); // the operator clicks Pausar in this program
        _desktop.ForegroundProcess = (uint)Environment.ProcessId;
        _control.RequestPause();
        var work = Task.Run(ClickB);
        await WaitUntilPaused();

        Assert.True(_watcher.Started);
        Assert.Equal(bsoft, _watcher.Process);
        _control.Resume();
        await work;
    }

    [Fact]
    public async Task After_a_pause_the_keyboard_goes_back_to_the_window_that_had_it()
    {
        var bsoft = _desktop.ForegroundWindow;
        _control.RequestPause();
        var work = Task.Run(ClickB);
        await WaitUntilPaused();

        _desktop.ForegroundWindow = new IntPtr(0x0E11A); // the operator clicked the panel's search box
        _control.Resume();
        await work;
        Assert.Equal([bsoft], _desktop.GivenForeground);
    }

    // ------------------------------------------------------------------ going on when the screen answered

    private Mat Patch(int width, int height) => new(height, width, MatType.CV_8UC3, new Scalar(30, 60, 200));

    private long Timed(Action action)
    {
        var started = _desktop.Now;
        action();
        return _desktop.Now - started;
    }

    [Fact]
    public void The_bot_goes_on_as_soon_as_the_screen_answered_and_went_quiet()
    {
        _bot.Find(FieldA, 0.97, 10);
        using var answer = Patch(120, 20);
        _desktop.OnEvent = e =>
        {
            if (e == "tap Tab") _desktop.After(30, () => _screen.Show(answer, 50, 700));
        };

        var took = Timed(() => _bot.Tab());
        Assert.InRange(took, 100, 200); // answered at 30 ms, then quiet: well before the 300 of before
    }

    [Fact]
    public void Only_what_happens_inside_the_window_of_bsoft_counts_as_an_answer()
    {
        _bot.Find(FieldA, 0.97, 10);
        _desktop.Area = new ScreenBox(0, 0, 700, 600); // the window of Bsoft; the rest of the screen is something else
        using var answer = Patch(120, 20);

        // Something changes outside the window (a clock, another program): not an answer.
        _desktop.OnEvent = e =>
        {
            if (e == "tap Tab") _desktop.After(30, () => _screen.Show(answer, 900, 700));
        };
        Assert.Equal(DesktopBot.AfterActionMs, Timed(() => _bot.Tab()));

        // An animation outside goes on forever while Bsoft answers inside: the bot still goes on early.
        var flip = false;
        using var other = new Mat(20, 120, MatType.CV_8UC3, new Scalar(200, 60, 30));
        void Animate()
        {
            _screen.Show((flip = !flip) ? answer : other, 900, 700);
            _desktop.After(20, Animate);
        }
        Animate();
        _desktop.OnEvent = e =>
        {
            if (e == "tap Tab") _desktop.After(30, () => _screen.Show(answer, 50, 500));
        };
        Assert.InRange(Timed(() => _bot.Tab()), 100, 200);
    }

    [Fact]
    public void Without_an_answer_the_wait_is_the_one_of_before()
    {
        _bot.Find(FieldA, 0.97, 10);
        Assert.Equal(DesktopBot.AfterActionMs, Timed(() => _bot.Tab()));
        Assert.Equal(DesktopBot.AfterActionMs + 100, Timed(_bot.Click)); // 100 ms between moving and clicking
    }

    [Fact]
    public void A_screen_that_keeps_changing_never_makes_the_wait_longer_than_before()
    {
        _bot.Find(FieldA, 0.97, 10);
        using var one = Patch(120, 20);
        using var other = new Mat(20, 120, MatType.CV_8UC3, new Scalar(200, 60, 30));
        var flip = false;
        void Animate()
        {
            _screen.Show((flip = !flip) ? one : other, 50, 700);
            _desktop.After(20, Animate);
        }
        _desktop.OnEvent = e =>
        {
            if (e == "tap Tab") _desktop.After(10, Animate);
        };

        Assert.InRange(Timed(() => _bot.Tab()), DesktopBot.AfterActionMs, DesktopBot.AfterActionMs + 30);
    }

    [Fact]
    public void A_blinking_text_cursor_does_not_count_as_an_answer()
    {
        _bot.Find(FieldA, 0.97, 10);
        using var cursor = new Mat(16, 2, MatType.CV_8UC3, new Scalar(0, 0, 0));
        _desktop.OnEvent = e =>
        {
            if (e == "tap Tab") _desktop.After(30, () => _screen.Show(cursor, 50, 700));
        };
        Assert.Equal(DesktopBot.AfterActionMs, Timed(() => _bot.Tab()));
    }

    [Fact]
    public void A_wait_written_in_the_step_is_kept_as_it_is()
    {
        _bot.Find(FieldA, 0.97, 10);
        using var answer = Patch(120, 20);
        _desktop.OnEvent = _ => _desktop.After(30, () => _screen.Show(answer, 50, 700));

        Assert.Equal(500, Timed(() => _bot.Tab(500)));
        Assert.Equal(200, Timed(() => _bot.Wait(200)));
        Assert.Equal(3 * 200, Timed(() => _bot.TypeKeys("123")));
    }

    [Fact]
    public void With_the_setting_off_every_wait_is_the_fixed_one_and_the_screen_is_not_watched()
    {
        using var bot = NewBot(adaptive: false);
        bot.Find(FieldA, 0.97, 10);
        using var answer = Patch(120, 20);
        _desktop.OnEvent = _ => _desktop.After(30, () => _screen.Show(answer, 50, 700));

        var looks = _screen.Captures;
        Assert.Equal(300, Timed(() => bot.Tab()));
        Assert.Equal(400, Timed(bot.Click));
        Assert.Equal(900, Timed(() => bot.PasteText("X"))); // 300 for the clipboard, 600 after Ctrl+V
        Assert.Equal(looks, _screen.Captures);
    }

    // ------------------------------------------------------------------ an optional pop-up and the screen after it

    [Fact]
    public void An_optional_popup_that_shows_up_is_found()
    {
        _desktop.After(400, () => _screen.Show(_templates.Image("transmissao_aviso_contingencia")!, 500, 450));
        Assert.True(_bot.FindBefore("transmissao_aviso_contingencia", "transmissao_impressao_vias", 5000, 65000));
        Assert.Equal((500, 450), (_bot.Last.Left, _bot.Last.Top));
    }

    [Fact]
    public void When_the_next_screen_is_there_instead_the_bot_does_not_wait_for_the_popup()
    {
        _desktop.After(400, () => _screen.Show(_templates.Image("transmissao_impressao_vias")!, 500, 50));
        var took = Timed(() => Assert.False(_bot.FindBefore("transmissao_aviso_contingencia", "transmissao_impressao_vias", 5000, 65000)));
        Assert.InRange(took, 400 + 700, 400 + 700 + 150); // the grace time, not the 5 seconds of before
    }

    [Fact]
    public void A_popup_that_comes_a_moment_after_the_next_screen_is_still_caught()
    {
        _desktop.After(400, () => _screen.Show(_templates.Image("transmissao_impressao_vias")!, 500, 50));
        _desktop.After(900, () => _screen.Show(_templates.Image("transmissao_aviso_contingencia")!, 500, 450));
        Assert.True(_bot.FindBefore("transmissao_aviso_contingencia", "transmissao_impressao_vias", 5000, 65000));
    }

    [Fact]
    public void A_popup_that_takes_longer_than_its_old_wait_is_caught_too()
    {
        // Before, "transmissao_aviso_contingencia" was only looked for during 5 s; later than that the run got stuck.
        _desktop.After(8000, () => _screen.Show(_templates.Image("transmissao_aviso_contingencia")!, 500, 450));
        Assert.True(_bot.FindBefore("transmissao_aviso_contingencia", "transmissao_impressao_vias", 5000, 65000));
    }

    [Fact]
    public void With_the_setting_off_the_popup_gets_its_old_plain_wait()
    {
        using var bot = NewBot(adaptive: false);
        _screen.Show(_templates.Image("transmissao_impressao_vias")!, 500, 50);
        var took = Timed(() => Assert.False(bot.FindBefore("transmissao_aviso_contingencia", "transmissao_impressao_vias", 5000, 65000)));
        Assert.InRange(took, 5000, 5100);
    }

    // ------------------------------------------------------------------ pasting

    [Fact]
    public void Text_is_pasted_as_soon_as_the_clipboard_holds_it()
    {
        var took = Timed(() => _bot.PasteText("MATERIAL BIOLÓGICO"));
        Assert.Equal(["clipboard MATERIAL BIOLÓGICO", "down Control", "char v", "up Control"], _desktop.Events);
        Assert.Equal(2 * DesktopBot.AfterActionMs, took); // nothing answered on the fake screen: the wait after Ctrl+V only
    }

    [Fact]
    public void Nothing_is_pasted_when_another_program_changed_the_clipboard()
    {
        _desktop.ClipboardThief = "texto de outro programa";
        var error = Assert.Throws<InvalidOperationException>(() => _bot.PasteText("12345678000199"));
        Assert.Contains("área de transferência", error.Message);
        Assert.Equal(["clipboard 12345678000199", "clipboard 12345678000199"], _desktop.Events); // tried twice, no Ctrl+V
    }

    [Fact]
    public void A_clipboard_that_cannot_be_read_is_given_the_old_wait()
    {
        _desktop.ClipboardBusy = true;
        var took = Timed(() => _bot.PasteText("X"));
        Assert.Contains("char v", _desktop.Events);
        Assert.Equal(DesktopBot.AfterActionMs + 2 * DesktopBot.AfterActionMs, took);
    }
}
