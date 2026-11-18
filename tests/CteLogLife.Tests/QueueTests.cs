using System.Text.Json.Nodes;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Flows;

namespace CteLogLife.Tests;

public class QueueTests
{
    private static readonly DateOnly Start = new(2026, 11, 10);
    private static readonly DateOnly End = new(2026, 11, 12);

    /// <summary>The unified listing with a third service (protocol 103), as the system would answer later.</summary>
    private static string UnifiedWithThird()
    {
        var root = JsonNode.Parse(TestRig.UnifiedJson)!;
        var services = root["services"]!.AsArray();
        var third = JsonNode.Parse(services[1]!.ToJsonString())!;
        third["protocol"] = 103;
        third["customer"]!["trading_firstname"] = "LAB URGENTE";
        third["requested_service"]!["price"] = 321;
        services.Add(third);
        root["total"] = 3;
        root["normal_count"] = 2;
        return root.ToJsonString();
    }

    [Fact]
    public void Search_ignores_case_and_accents_and_needs_every_word()
    {
        var item = new QueueItem("1", "48213 · CLÍNICA SÃO LUCAS", "NORMAL RODOVIÁRIO · UBERLÂNDIA → GOIÂNIA");
        Assert.True(RunQueue.Matches(item, "48213"));
        Assert.True(RunQueue.Matches(item, "clinica sao"));
        Assert.True(RunQueue.Matches(item, "goiania 482"));
        Assert.True(RunQueue.Matches(item, "  "));
        Assert.False(RunQueue.Matches(item, "48214"));
        Assert.False(RunQueue.Matches(item, "clinica recife"));
    }

    [Fact]
    public void An_added_item_goes_next_or_last_among_the_pending_ones()
    {
        var queue = new RunQueue();
        queue.Load("x", [new QueueItem("a", "A", ""), new QueueItem("b", "B", ""), new QueueItem("c", "C", "")]);
        queue.Finish(queue.TakeNext()!.Id, QueueItemState.Done);
        queue.TakeNext(); // b is running

        queue.Add(new QueueItem("n", "N", ""), next: true);
        queue.Add(new QueueItem("z", "Z", ""), next: false);

        Assert.Equal(["a", "b", "n", "c", "z"], queue.Snapshot().Select(i => i.Id));
        Assert.Equal("n", queue.TakeNext()!.Id);
    }

    [Fact]
    public async Task A_queue_without_a_running_emission_refuses_includes()
    {
        var queue = new RunQueue();
        queue.Load("Cancelamento", [new QueueItem("a", "CT-e 1", "")]);
        Assert.False(queue.CanInclude);
        var result = await queue.IncludeAsync("123", CancellationToken.None);
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Protocol_included_during_a_pause_is_fetched_and_emitted_next()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Control.RequestPause();

        var run = Task.Run(() => new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));
        await WaitUntil(() => rig.Control.IsPaused);
        Assert.True(rig.Queue.CanInclude);
        Assert.Equal("protocolo", rig.Queue.IncludeLabel);

        // Not in the system's list for the period: refused with the reason, nothing added.
        var refused = await rig.Queue.IncludeAsync("999", CancellationToken.None);
        Assert.False(refused.Ok);
        Assert.Contains("999 não está entre os serviços a emitir de 10/11/2026 a 12/11/2026", refused.Message);
        Assert.Equal(2, rig.Queue.Total);

        // Already waiting in the queue: it only moves to the front.
        var moved = await rig.Queue.IncludeAsync(" 102 ", CancellationToken.None);
        Assert.True(moved.Ok);
        Assert.Contains("já estava na fila", moved.Message);
        Assert.StartsWith("102", rig.Queue.Snapshot()[0].Title);

        // New in the system since the run started: fetched again and put first.
        rig.ServeUnified(UnifiedWithThird());
        var added = await rig.Queue.IncludeAsync("103", CancellationToken.None);
        Assert.True(added.Ok, added.Message);
        var titles = rig.Queue.Snapshot().Select(i => i.Title).ToList();
        Assert.Equal(["103 · LAB URGENTE", "102 · LAB B", "101 · LAB A"], titles);

        // Asking twice does not queue it twice.
        Assert.Contains("já estava na fila", (await rig.Queue.IncludeAsync("103", CancellationToken.None)).Message);
        Assert.Equal(3, rig.Queue.Total);

        rig.Control.Resume();
        var message = await run;

        Assert.Contains("1 protocolo(s) incluído(s) durante a execução.", message);
        Assert.Equal(["321", "200", "5,00"], rig.Driver.Taxes.Select(t => t.Price));
        var posts = rig.Http.To("/api/uploads/cte-loglife/json").ToList();
        Assert.Equal(3, posts.Count);
        Assert.Contains("\"protocol\":\"103\"", posts[0].Body);

        // The included protocol got its own row in the conference spreadsheet, with its number.
        var report = SheetTable.Read(Directory.GetFiles(rig.ReportFolder, "*.xlsx").Single());
        Assert.Equal(3, report.Rows.Count);
        Assert.Equal("103", SheetTable.Text(report.Rows[2][report.IndexOf("PROTOCOLO")]));
        Assert.Equal("1001", SheetTable.Text(report.Rows[2][report.IndexOf("CTE LOGLIFE")]));

        Assert.Contains(rig.Log.Lines, line => line.Contains("Protocolo 103 incluído na fila pelo operador"));
        Assert.False(rig.Queue.CanInclude); // the run is over
        Assert.False((await rig.Queue.IncludeAsync("104", CancellationToken.None)).Ok);
    }

    [Fact]
    public async Task Single_emission_includes_by_protocol_with_the_rules_of_the_run()
    {
        using var rig = new TestRig();
        rig.Http.Responder = call =>
        {
            string Service(int protocol, string type) => $$"""
                { "service": { "protocol": {{protocol}}, "step": "toCollectService",
                    "customer": { "trading_firstname": "LAB {{protocol}}", "cnpj_cpf": "10.000.000/0001-00", "emission_type": "{{type}}" },
                    "requested_service": { "price": 80.0 }, "source_city": "BH", "destination_city": "RIO",
                    "source_addresses": [ { "city_state": "MINAS GERAIS", "cnpj_cpf": "10.000.000/0001-00" } ],
                    "destination_addresses": [ { "city_state": "RIO DE JANEIRO", "cnpj_cpf": "20.000.000/0001-00" } ] } }
                """;
            if (call.Path.EndsWith("/cte-emission/900")) return FakeApi.Json(Service(900, "CTE"));
            if (call.Path.EndsWith("/cte-emission/905")) return FakeApi.Json(Service(905, "CTE"));
            if (call.Path.EndsWith("/cte-emission/906")) return FakeApi.Json(Service(906, "NF"));
            return call.Path.Contains("/cte-emission/") ? FakeApi.Json("""{"service":null}""") : null;
        };
        rig.Control.RequestPause();

        var run = Task.Run(() => new EmissionFlows(rig.Ctx).SingleAsync(new DateOnly(2026, 11, 17), symbolic: false,
            "900", "4", CancellationToken.None));
        await WaitUntil(() => rig.Control.IsPaused);

        Assert.Equal("Protocolo 906 não pode ser emitido como CTe normal!",
            (await rig.Queue.IncludeAsync("906", CancellationToken.None)).Message);
        Assert.Equal("Serviço com protocolo 907 não encontrado!",
            (await rig.Queue.IncludeAsync("907", CancellationToken.None)).Message);
        Assert.True((await rig.Queue.IncludeAsync("905", CancellationToken.None)).Ok);

        rig.Control.Resume();
        await run;

        // 905 went first, with the volumes typed for the run.
        Assert.Equal([new CargoInput("4"), new CargoInput("4")], rig.Driver.Cargos);
        var uploads = rig.Http.Calls.Where(c => c.Path == "/api/uploads/cte-loglife").ToList();
        Assert.Contains("905,1001", uploads[0].Body);
        Assert.Contains("900,1002", uploads[1].Body);
    }

    [Fact]
    public async Task Waiting_for_the_operator_holds_even_past_the_point_of_no_return()
    {
        var control = new RunControl();
        control.BeginCritical();
        Assert.Equal(TimeSpan.Zero, control.Checkpoint()); // a plain pause never blocks here
        var changes = 0;
        control.Changed += () => Interlocked.Increment(ref changes);

        var wait = Task.Run(() => control.WaitForOperator(new OperatorAttention("Tela desconhecida", "…", "shot.png")));
        await WaitUntil(() => control.Attention is not null);
        Assert.True(control.IsPaused);
        Assert.Equal("shot.png", control.Attention!.ScreenshotPath);
        Assert.False(wait.IsCompleted);

        control.TogglePause(); // the pause shortcut is how the operator continues
        Assert.True(await wait);
        Assert.Null(control.Attention);
        Assert.False(control.IsPaused);
        Assert.False(control.IsPauseRequested);
        Assert.True(changes >= 2);

        // A stop ends the wait with "do not go on", and later waits return at once.
        var second = Task.Run(() => control.WaitForOperator(new OperatorAttention("t", "m")));
        await WaitUntil(() => control.Attention is not null);
        control.RequestStop();
        Assert.False(await second);
        Assert.False(control.WaitForOperator(new OperatorAttention("t", "m")));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
    }
}
