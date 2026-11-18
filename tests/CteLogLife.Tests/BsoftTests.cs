using CteLogLife.Bsoft;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Tests;

/// <summary>A screen that shows whatever image the test puts on it.</summary>
public sealed class FakeScreen : IScreenSource
{
    private readonly object _lock = new();
    private readonly Mat _current;

    public FakeScreen(int width = 1920, int height = 1080) =>
        _current = new Mat(height, width, MatType.CV_8UC3, new Scalar(240, 240, 240));

    public int Captures { get; private set; }

    /// <summary>Called on every capture, before the image is handed out.</summary>
    public Action<int>? OnCapture { get; set; }

    public void Show(Mat template, int x, int y)
    {
        lock (_lock)
        {
            using var region = new Mat(_current, new Rect(x, y, template.Width, template.Height));
            template.CopyTo(region);
        }
    }

    public void Blank()
    {
        lock (_lock) _current.SetTo(new Scalar(240, 240, 240));
    }

    public Mat Capture()
    {
        Captures++;
        OnCapture?.Invoke(Captures);
        lock (_lock) return _current.Clone();
    }

    public void Dispose() => _current.Dispose();
}

public class TemplateTests : IDisposable
{
    private static readonly string TemplatesDir = Path.Combine(AppContext.BaseDirectory, "Templates");

    private readonly TemplateStore _store = new(TemplatesDir);
    private readonly FakeScreen _screen = new();
    private readonly RunControl _control = new();
    private readonly ListLog _log = new();
    private readonly DesktopBot _bot;

    public TemplateTests() => _bot = new DesktopBot(_store, _screen, _control, _log);

    public void Dispose()
    {
        _bot.Dispose();
        _store.Dispose();
    }

    [Fact]
    public void Labels_resolve_to_files_the_way_the_python_bot_resolved_them()
    {
        Assert.True(_store.Count > 100);
        Assert.Equal(["icms", "icms-alt", "icms-alt2"], _store.Candidates("icms"));
        Assert.Equal(["valueShipping22", "valueShipping22-alt"], _store.Candidates("valueShipping22"));
        Assert.Equal(["contingencia"], _store.Candidates("contingencia"));
        Assert.Equal(["9-sucesso", "9-sucesso-alt"], _store.Candidates("9-sucesso"));
        Assert.NotNull(_store.Image("valueShipping22")); // file is "valueShipping22.PNG.png"
        Assert.NotNull(_store.Image("CNPJ_input"));      // file is "CNPJ_input.PNG"
        Assert.NotNull(_store.Image("5.1ct-e"));
        Assert.Null(_store.Image("does-not-exist"));
    }

    [Fact]
    public void Every_label_the_driver_uses_has_an_image()
    {
        string[] labels =
        [
            "iniciarEmissao", "searchSender", "CNPJ_input", "Found1", "confirmCNPJ", "searchReceiver", "Tomador1",
            "Others", "searchPayer", "Part3", "IncludeNat", "NF-e", "Nat", "Weight1", "confirmNAT", "emissionComp",
            "includeCTe", "searchCTe", "clearDate", "inputCTE", "findCTe", "selectAll", "confirmCTE", "part4",
            "valueShipping22", "icms", "CST", "ibs-cbs", "ibs-cst", "Obs2", "Obs3", "payerObs", "include_tp_info",
            "confirm_tp_info", "zeroValue", "confirmValue", "finalizeEmission", "creditconfirmPopUp", "secondPopUp",
            "thirdPopUp", "fourthPopUp", "contingencia", "fifthPopUp", "clicarCte", "2consultas",
            "10ClicarBordaInferior", "4Localizar", "5duploClickStatus", "5.1ct-e", "5.2Cancelar_CTE", "8confirmar",
            "9-sucesso", "cancelled", "icms_start", "icms_document", "icms_emission_date", "icms_payment_date",
            "icms_state", "icms_cost_center", "icms_payment", "icms_bank", "icms_barcode", "icms_obs", "icms_confirm",
        ];
        Assert.All(labels, label => Assert.True(_store.Image(label) is not null, $"sem imagem para '{label}'"));
    }

    [Fact]
    public void A_control_is_found_where_it_is_on_screen_and_only_there()
    {
        var template = _store.Image("confirmCNPJ")!;
        _screen.Show(template, 700, 420);

        Assert.True(_bot.Find("confirmCNPJ", 0.97, 10));
        Assert.Equal(new ScreenBox(700, 420, template.Width, template.Height), _bot.Last);
        Assert.Equal(700 + template.Width / 2.0, _bot.Last.CenterX);

        Assert.False(_bot.Find("searchSender", 0.97, 10));
        Assert.Throws<InvalidOperationException>(() => _bot.Last);
    }

    [Fact]
    public void An_alternate_look_of_the_control_matches_under_the_same_label()
    {
        _screen.Show(_store.Image("icms-alt2")!, 300, 200);
        Assert.True(_bot.Find("icms", 0.97, 10));
        Assert.Equal((300, 200), (_bot.Last.Left, _bot.Last.Top));
    }

    [Fact]
    public void The_message_of_a_finished_cancellation_is_recognised_as_bsoft_shows_it_now()
    {
        // "Cancelamento de CT-e realizado com sucesso.", cut from a capture of 18/11/2026. The older
        // image of it scores 0.85 on that screen, below what counts as found: the bot reported an
        // error for a CT-e it had just cancelled and left the message open.
        var message = _store.Image("9-sucesso-alt")!;
        _screen.Show(message, 857, 522);

        Assert.True(_bot.Find("9-sucesso", 0.97, 10));
        Assert.Equal(new ScreenBox(857, 522, message.Width, message.Height), _bot.Last);
    }

    [Fact]
    public void Find_keeps_looking_until_the_control_shows_up()
    {
        var template = _store.Image("fifthPopUp")!;
        _screen.OnCapture = count =>
        {
            if (count == 3) _screen.Show(template, 900, 500);
        };

        Assert.True(_bot.Find("fifthPopUp", 0.97, 5000));
        Assert.Equal(3, _screen.Captures);
    }

    [Fact]
    public void A_required_control_that_never_appears_stops_with_its_name()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var error = Assert.Throws<ElementNotFoundException>(() => _bot.Require("part4", waitMs: 600));
        Assert.Equal("part4", error.Label);
        Assert.True(_screen.Captures >= 2, $"only {_screen.Captures} look(s) in {watch.ElapsedMilliseconds} ms");

        var missing = Assert.Throws<ElementNotFoundException>(() => _bot.Find("no-such-label"));
        Assert.Contains("imagem de referência", missing.Message);
    }

    [Fact]
    public void A_stop_request_ends_a_search_immediately()
    {
        _screen.OnCapture = count =>
        {
            if (count == 2) _control.RequestStop();
        };
        Assert.Throws<RunStoppedException>(() => _bot.Find("part4", 0.97, 60000));
        Assert.Equal(2, _screen.Captures);
    }

    [Fact]
    public async Task Time_spent_paused_does_not_count_against_the_search()
    {
        var template = _store.Image("zeroValue")!;
        // The first look of a test run is slow (images are read, OpenCV starts up): it is done here so
        // that it cannot use up the search's own time below.
        Assert.False(_bot.Find("zeroValue", 0.97, 10));
        var looksBefore = _screen.Captures;
        _screen.OnCapture = count =>
        {
            if (count == looksBefore + 1) _control.RequestPause();
        };
        var search = Task.Run(() => _bot.Find("zeroValue", 0.97, 1000));
        for (var i = 0; i < 2000 && !_control.IsPaused; i++) await Task.Delay(5);
        Assert.True(_control.IsPaused, $"a busca não pausou (terminada: {search.IsCompleted}, olhadas: {_screen.Captures})");

        await Task.Delay(1500); // longer than the search's own timeout
        _screen.Show(template, 100, 100);
        _control.Resume();

        Assert.True(await search);
    }

    [Fact]
    public void The_real_screen_can_be_captured()
    {
        using var grabber = new ScreenGrabber();
        using var shot = grabber.Capture();
        Assert.True(shot.Width >= 800 && shot.Height >= 600);
        Assert.Equal(3, shot.Channels());
    }

    [Fact]
    public void Clipboard_round_trip_when_enabled()
    {
        // Touches the real clipboard, so it only runs on request: set CTE_TEST_CLIPBOARD=1.
        if (Environment.GetEnvironmentVariable("CTE_TEST_CLIPBOARD") != "1") return;

        var before = ClipboardText.Get();
        try
        {
            ClipboardText.Set("MATERIAL BIOLÓGICO 00012345");
            Assert.Equal("MATERIAL BIOLÓGICO 00012345", ClipboardText.Get());
            Assert.Equal("MATERIAL BIOLÓGICO 00012345", ClipboardText.TryGet());
            ClipboardText.Clear();
            Assert.Equal("", ClipboardText.Get());
        }
        finally
        {
            ClipboardText.Set(before);
        }
    }
}
