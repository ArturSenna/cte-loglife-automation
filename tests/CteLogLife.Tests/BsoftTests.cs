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
        Assert.Equal(["valores_aba_icms", "valores_aba_icms-alt", "valores_aba_icms-alt2"], _store.Candidates("valores_aba_icms"));
        Assert.Equal(["valores_frete_valor", "valores_frete_valor-alt"], _store.Candidates("valores_frete_valor"));
        Assert.Equal(["transmissao_aviso_contingencia"], _store.Candidates("transmissao_aviso_contingencia"));
        Assert.Equal(["cancelamento_sucesso", "cancelamento_sucesso-alt"], _store.Candidates("cancelamento_sucesso"));
        Assert.NotNull(_store.Image("valores_frete_valor"));
        Assert.NotNull(_store.Image("busca_cliente_campo_cnpj"));
        Assert.NotNull(_store.Image("busca_cliente_primeira_linha"));
        Assert.Null(_store.Image("does-not-exist"));
    }

    [Fact]
    public void Every_label_the_driver_uses_has_an_image()
    {
        string[] labels =
        [
            "emissao_incluir_f1", "remetente_codigo", "busca_cliente_campo_cnpj", "busca_cliente_qtde_1",
            "busca_cliente_confirmar", "destinatario_codigo", "tomador_resp_pagamento", "tomador_opcao_outros",
            "tomador_codigo", "carga_aba_parte3", "carga_incluir", "carga_modelo_nfe", "carga_campo_natureza",
            "carga_campo_peso", "carga_confirmar", "complementar_finalidade_emissao", "complementar_incluir_cte",
            "complementar_abrir_busca", "complementar_busca_periodo", "complementar_busca_numero",
            "complementar_busca_localizar", "complementar_busca_marcar_todos", "complementar_busca_confirmar",
            "valores_aba_parte4", "valores_frete_valor", "valores_aba_icms", "valores_icms_cst",
            "valores_aba_ibs_cbs", "valores_ibs_cbs_cst", "valores_aba_observacoes", "valores_campo_observacao",
            "valores_obs_contribuinte", "valores_obs_contribuinte_incluir", "valores_obs_contribuinte_confirmar",
            "transmissao_aviso_icms_zero", "transmissao_aviso_valores_alterados", "transmissao_numero_documento",
            "transmissao_aviso_valor_credito", "transmissao_pergunta_incluir_cte", "transmissao_botao_mdfe",
            "transmissao_pergunta_gerar_cte", "transmissao_aviso_contingencia", "transmissao_impressao_vias",
            "transmissao_numero_cte", "cancelamento_aba_consultas", "cancelamento_campo_numero_cte",
            "cancelamento_localizar", "cancelamento_status_autorizado", "cancelamento_menu_cte",
            "cancelamento_opcao_cancelar", "cancelamento_confirmar", "cancelamento_sucesso",
            "cancelamento_ja_cancelado", "guia_icms_incluir_f1", "guia_icms_documento", "guia_icms_data_emissao",
            "guia_icms_data_movimento", "guia_icms_codigo_fornecedor", "guia_icms_centro_custo",
            "guia_icms_pre_cadastrada", "guia_icms_local_cobranca", "guia_icms_codigo_barras",
            "guia_icms_observacao", "guia_icms_confirmar", "busca_cliente_primeira_linha",
        ];
        Assert.All(labels, label => Assert.True(_store.Image(label) is not null, $"sem imagem para '{label}'"));
    }

    [Fact]
    public void A_control_is_found_where_it_is_on_screen_and_only_there()
    {
        var template = _store.Image("busca_cliente_confirmar")!;
        _screen.Show(template, 700, 420);

        Assert.True(_bot.Find("busca_cliente_confirmar", 0.97, 10));
        Assert.Equal(new ScreenBox(700, 420, template.Width, template.Height), _bot.Last);
        Assert.Equal(700 + template.Width / 2.0, _bot.Last.CenterX);

        Assert.False(_bot.Find("remetente_codigo", 0.97, 10));
        Assert.Throws<InvalidOperationException>(() => _bot.Last);
    }

    [Fact]
    public void An_alternate_look_of_the_control_matches_under_the_same_label()
    {
        _screen.Show(_store.Image("valores_aba_icms-alt2")!, 300, 200);
        Assert.True(_bot.Find("valores_aba_icms", 0.97, 10));
        Assert.Equal((300, 200), (_bot.Last.Left, _bot.Last.Top));
    }

    [Fact]
    public void The_message_of_a_finished_cancellation_is_recognised_as_bsoft_shows_it_now()
    {
        // "Cancelamento de CT-e realizado com sucesso.", cut from a capture of 18/11/2026. The older
        // image of it scores 0.85 on that screen, below what counts as found: the bot reported an
        // error for a CT-e it had just cancelled and left the message open.
        var message = _store.Image("cancelamento_sucesso-alt")!;
        _screen.Show(message, 857, 522);

        Assert.True(_bot.Find("cancelamento_sucesso", 0.97, 10));
        Assert.Equal(new ScreenBox(857, 522, message.Width, message.Height), _bot.Last);
    }

    [Fact]
    public void Find_keeps_looking_until_the_control_shows_up()
    {
        var template = _store.Image("transmissao_impressao_vias")!;
        _screen.OnCapture = count =>
        {
            if (count == 3) _screen.Show(template, 900, 500);
        };

        Assert.True(_bot.Find("transmissao_impressao_vias", 0.97, 5000));
        Assert.Equal(3, _screen.Captures);
    }

    [Fact]
    public void A_required_control_that_never_appears_stops_with_its_name()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var error = Assert.Throws<ElementNotFoundException>(() => _bot.Require("valores_aba_parte4", waitMs: 600));
        Assert.Equal("valores_aba_parte4", error.Label);
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
        Assert.Throws<RunStoppedException>(() => _bot.Find("valores_aba_parte4", 0.97, 60000));
        Assert.Equal(2, _screen.Captures);
    }

    [Fact]
    public async Task Time_spent_paused_does_not_count_against_the_search()
    {
        var template = _store.Image("transmissao_aviso_icms_zero")!;
        // The first look of a test run is slow (images are read, OpenCV starts up): it is done here so
        // that it cannot use up the search's own time below.
        Assert.False(_bot.Find("transmissao_aviso_icms_zero", 0.97, 10));
        var looksBefore = _screen.Captures;
        _screen.OnCapture = count =>
        {
            if (count == looksBefore + 1) _control.RequestPause();
        };
        var search = Task.Run(() => _bot.Find("transmissao_aviso_icms_zero", 0.97, 1000));
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
