using System.Net;
using System.Text.Json;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Flows;
using CteLogLife.Core.Journal;

namespace CteLogLife.Tests;

public class UnifiedEmissionTests
{
    private static readonly DateOnly Start = new(2026, 11, 10);
    private static readonly DateOnly End = new(2026, 11, 12);

    [Fact]
    public async Task Emits_every_service_and_records_number_pdf_and_report()
    {
        using var rig = new TestRig();
        rig.ServeUnified();

        var message = await new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None);

        Assert.StartsWith("Emissões de CTE unificado realizadas com sucesso!", message);
        Assert.Contains("Total: 2\nSimbólicos: 1\nNormais: 1", message);

        // Bsoft got the symbolic one first, exactly as the API ordered them.
        Assert.Equal(["parties", "cargo", "taxes", "parties", "cargo", "taxes"], rig.Driver.Steps);

        var symbolic = rig.Driver.Parties[0];
        Assert.Equal(["33.333.333/0001-33"], symbolic.SenderCnpjs);
        Assert.Equal(["44.444.444/0001-44"], symbolic.ReceiverCnpjs);
        Assert.Equal(Payer.Sender, symbolic.Payer);
        Assert.Equal(new CargoInput("3", 1), rig.Driver.Cargos[0]);

        // MG origin: the rate is typed, the observation keeps the MG text.
        var symbolicTax = rig.Driver.Taxes[0];
        Assert.Equal("5,00", symbolicTax.Price);
        Assert.Equal("12", symbolicTax.Tax);
        Assert.Equal("0.48", symbolicTax.TpInfo);
        Assert.Equal("MG", symbolicTax.Uf);
        Assert.StartsWith("Protocolo 101 - ICMS devido a MG.", symbolicTax.IcmsText);

        // The customer is one of the receivers: it pays and is the only receiver offered to Bsoft.
        var normal = rig.Driver.Parties[1];
        Assert.Equal(["55.555.555/0001-55"], normal.SenderCnpjs);
        Assert.Equal(["66.666.666/0001-66"], normal.ReceiverCnpjs);
        Assert.Equal(Payer.Receiver, normal.Payer);
        Assert.Equal(new CargoInput(), rig.Driver.Cargos[1]);

        // Other origins: rate 0 and the ICMS amount written into the observation.
        var normalTax = rig.Driver.Taxes[1];
        Assert.Equal("200", normalTax.Price);
        Assert.Equal("0", normalTax.Tax);
        Assert.Equal("19.20", normalTax.TpInfo);
        Assert.Equal("SP", normalTax.Uf);
        Assert.StartsWith("Protocolo 102 - ICMS de R$19.20, pago com crédito presumido", normalTax.IcmsText);

        // The numbers went to the system, with the symbolic flag only where it applies.
        var posts = rig.Http.To("/api/uploads/cte-loglife/json").ToList();
        Assert.Equal(2, posts.Count);
        using (var first = JsonDocument.Parse(posts[0].Body))
        {
            var item = first.RootElement.GetProperty("data")[0];
            Assert.Equal("101", item.GetProperty("protocol").GetString());
            Assert.Equal(1001, item.GetProperty("cte_loglife").GetInt64());
            Assert.Equal("17/11/2026", item.GetProperty("cte_loglife_emission_date").GetString());
            Assert.True(item.GetProperty("symbolic").GetBoolean());
        }
        using (var second = JsonDocument.Parse(posts[1].Body))
        {
            var item = second.RootElement.GetProperty("data")[0];
            Assert.Equal(1002, item.GetProperty("cte_loglife").GetInt64());
            Assert.False(item.TryGetProperty("symbolic", out _));
        }
        Assert.All(posts, post => Assert.Equal("SESSION", post.Authorization));

        // Each PDF was uploaded and linked to its protocol.
        var uploads = rig.Http.Calls.Where(c => c.Path == "/api/pdf").ToList();
        Assert.Equal(2, uploads.Count);
        Assert.Contains("filename=00001001.pdf", uploads[0].Body.Replace("\"", ""));
        Assert.Contains("CTE LOGLIFE", uploads[0].Body);
        var links = rig.Http.Calls.Where(c => c.Path == "/api/pdf/associate").ToList();
        Assert.Equal(2, links.Count);
        Assert.Contains("Protocolo,Arquivo PDF\r\n101,00001001.pdf", links[0].Body);
        Assert.Empty(Directory.GetFiles(rig.ReportFolder, "*.csv"));

        // The spreadsheet has both rows and the emitted numbers.
        var report = SheetTable.Read(Directory.GetFiles(rig.ReportFolder, "Lista CTE Unificado-10-11-2026-14-30.xlsx").Single());
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal("09/11/2026", SheetTable.Text(report.Rows[0][report.IndexOf("DATA COLETA")]));
        Assert.Equal("Rua Das Flores, 10 - Centro, Belo Horizonte - MINAS GERAIS, 30000-000",
            SheetTable.Text(report.Rows[0][report.IndexOf("ENDEREÇO REMETENTE")]));
        Assert.Equal("1. B - 2\n2. C - 3", SheetTable.Text(report.Rows[1][report.IndexOf("NOME DESTINATÁRIO")]));
        Assert.Equal("1001", SheetTable.Text(report.Rows[0][report.IndexOf("CTE LOGLIFE")]));
        Assert.Equal("1002", SheetTable.Text(report.Rows[1][report.IndexOf("CTE LOGLIFE")]));

        Assert.Empty(rig.Journal.Open());
        Assert.All(rig.Queue.Snapshot(), item => Assert.Equal(QueueItemState.Done, item.State));
    }

    [Fact]
    public async Task Missing_screen_element_stops_the_run_before_anything_is_sent()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Driver.BeforeTransmit = tax =>
        {
            if (tax.Price == "200") throw new ElementNotFoundException("valueShipping22");
        };

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));

        Assert.Contains("valueShipping22", error.Message);
        Assert.Contains("Emitidos antes do erro: 1 de 2", error.Message);
        Assert.Single(rig.Http.To("/api/uploads/cte-loglife/json"));
        Assert.Empty(rig.Journal.Open()); // nothing was transmitted for 102, so it can be emitted again
        Assert.Equal(QueueItemState.Failed, rig.Queue.Snapshot()[1].State);
    }

    [Fact]
    public async Task Unreadable_number_is_asked_and_a_cancelled_answer_blocks_the_protocol()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Driver.AfterTransmit = number => number == 1001 ? null : number;
        rig.Driver.LastNumberHint = "4321";
        rig.Operator.Answers.Enqueue("abc");
        rig.Operator.Answers.Enqueue(null);

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));

        Assert.Contains("o número não foi copiado nem informado", error.Message);
        Assert.Equal(2, rig.Operator.Questions.Count);
        Assert.Equal("4321", rig.Operator.Questions[0].Prefill);
        Assert.StartsWith("\"abc\" não é um número válido", rig.Operator.Questions[1].Message);
        Assert.Empty(rig.Http.To("/api/uploads/cte-loglife/json"));

        var open = Assert.Single(rig.Journal.Open());
        Assert.Equal(EmissionStage.Uncertain, open.Stage);
        Assert.Equal("101", open.Protocol);

        // A second run leaves that protocol alone and emits the other one.
        rig.Driver.AfterTransmit = null;
        var message = await new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None);
        Assert.Contains("1 protocolo(s) não emitido(s) por terem pendências", message);
        Assert.Contains(rig.Operator.Notices, notice => notice.StartsWith("Protocolo 101 não foi emitido"));
        var post = Assert.Single(rig.Http.To("/api/uploads/cte-loglife/json"));
        Assert.Contains("\"protocol\":\"102\"", post.Body);
    }

    [Fact]
    public async Task Number_typed_by_the_operator_is_recorded()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Driver.AfterTransmit = number =>
        {
            if (number != 1001) return number;
            File.WriteAllBytes(Path.Combine(rig.PdfFolder, "00000777.pdf"), [1, 2, 3]);
            return null;
        };
        rig.Operator.Answers.Enqueue(" 777 ");

        await new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None);

        Assert.Contains("\"cte_loglife\":777", rig.Http.To("/api/uploads/cte-loglife/json").First().Body);
        Assert.Empty(rig.Journal.Open());
    }

    [Fact]
    public async Task Failure_after_transmit_is_reported_as_uncertain()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Driver.AfterTransmit = _ => throw new TransmitUncertainException("O elemento 'fifthPopUp' não apareceu depois do F4.");

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));

        Assert.Contains("ficou sem confirmação", error.Message);
        Assert.Contains("fifthPopUp", Assert.Single(rig.Operator.Questions).Message);
        Assert.Equal(EmissionStage.Uncertain, Assert.Single(rig.Journal.Open()).Stage);
        Assert.False(rig.Control.InCritical);
    }

    [Fact]
    public async Task Api_refusal_keeps_the_emission_pending_and_it_can_be_finished_later()
    {
        using var rig = new TestRig();
        var apiDown = true;
        rig.ServeUnified(otherwise: call =>
            apiDown && call.Path == "/api/uploads/cte-loglife/json"
                ? FakeApi.Json("""{"message":"Serviço indisponível"}""", HttpStatusCode.BadRequest)
                : null);

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));

        Assert.Contains("CT-e 1001 emitido para o protocolo 101", error.Message);
        Assert.Contains("Serviço indisponível", error.Message);
        Assert.Single(rig.Driver.Taxes); // the run stopped: 102 was not emitted while the system was refusing
        var pending = Assert.Single(rig.Journal.Open());
        Assert.Equal(EmissionStage.NumberCaptured, pending.Stage);
        Assert.Equal(1001, pending.CteNumber);

        // The journal survives a restart.
        var reloaded = new EmissionJournal(Path.Combine(rig.Root, "journal", "emissions.jsonl"));
        Assert.Equal(1001, Assert.Single(reloaded.Open()).CteNumber);

        apiDown = false;
        var finished = await new PostEmission(rig.Ctx).RunAsync(pending, null, CancellationToken.None);
        Assert.Equal(EmissionStage.Completed, finished.Stage);
        Assert.Empty(rig.Journal.Open());
        Assert.Single(rig.Http.Calls, c => c.Path == "/api/pdf/associate");
    }

    [Fact]
    public async Task Pdf_that_never_appears_parks_the_emission()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Driver.PdfFolder = null; // Bsoft never writes the file
        rig.Settings.PdfWaitSeconds = 1;

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));

        Assert.Contains("00001001.pdf não apareceu", error.Message);
        Assert.Equal(EmissionStage.Reported, Assert.Single(rig.Journal.Open()).Stage);
    }

    [Fact]
    public async Task Queue_reordered_while_paused_changes_what_is_emitted_next()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Control.RequestPause();

        var run = Task.Run(() => new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));
        await WaitUntil(() => rig.Control.IsPaused);

        var pending = rig.Queue.Snapshot();
        Assert.Equal(2, pending.Count);
        Assert.StartsWith("101", pending[0].Title);
        Assert.True(rig.Queue.MoveToTop(pending[1].Id));
        rig.Control.Resume();
        await run;

        Assert.Equal("200", rig.Driver.Taxes[0].Price); // protocol 102 went first
        Assert.Equal("5,00", rig.Driver.Taxes[1].Price);
        var posts = rig.Http.To("/api/uploads/cte-loglife/json").ToList();
        Assert.Contains("\"protocol\":\"102\"", posts[0].Body);
        // The spreadsheet rows keep the API order; each number lands on its own protocol.
        var report = SheetTable.Read(Directory.GetFiles(rig.ReportFolder, "*.xlsx").Single());
        Assert.Equal("1002", SheetTable.Text(report.Rows[0][report.IndexOf("CTE LOGLIFE")]));
        Assert.Equal("1001", SheetTable.Text(report.Rows[1][report.IndexOf("CTE LOGLIFE")]));
    }

    [Fact]
    public async Task Stop_between_emissions_ends_the_run_cleanly()
    {
        using var rig = new TestRig();
        rig.ServeUnified();
        rig.Driver.AfterTransmit = number =>
        {
            rig.Control.RequestStop(); // asked in the middle of a transmit: it waits for this CT-e to finish
            return number;
        };

        var message = await new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None);

        Assert.StartsWith("Execução interrompida pelo operador.", message);
        Assert.Contains("Emitidos: 1 de 2", message);
        Assert.Single(rig.Http.Calls, c => c.Path == "/api/pdf/associate");
        Assert.Empty(rig.Journal.Open());
        Assert.Equal(QueueItemState.Skipped, rig.Queue.Snapshot()[1].State);
    }

    [Fact]
    public async Task Job_with_incomplete_data_is_left_out_without_touching_bsoft()
    {
        using var rig = new TestRig();
        rig.ServeUnified(TestRig.UnifiedJson.Replace("\"33.333.333/0001-33\"", "\"\""));

        var message = await new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None);

        Assert.Contains("1 protocolo(s) não emitido(s) por dados incompletos", message);
        Assert.Contains(rig.Operator.Notices, n => n.Contains("CNPJ do remetente ausente"));
        Assert.Single(rig.Driver.Taxes);
    }

    [Fact]
    public async Task Nothing_to_emit_and_missing_folders_are_reported()
    {
        using var rig = new TestRig();
        rig.ServeUnified("""{"total":0,"services":[]}""");
        Assert.Equal("Nenhum serviço encontrado para emissão de CTE unificado no período selecionado.",
            await new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));

        rig.Settings.CtePdfFolder = "Pasta onde os arquivos CTe estão.";
        var error = await Assert.ThrowsAsync<FlowException>(() =>
            new EmissionFlows(rig.Ctx).UnifiedAsync(Start, End, CancellationToken.None));
        Assert.Contains("Pasta CTE", error.Message);
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

public class OtherFlowTests
{
    [Fact]
    public async Task Complementary_emission_links_the_original_cte_and_uses_its_own_endpoint()
    {
        using var rig = new TestRig();
        rig.Http.Responder = call => call.Path.StartsWith("/api/public/service/cte-emission-complementary")
            ? FakeApi.Json("""
              { "services": [ { "protocol": 300, "cte_loglife": "5555", "extra_value": "42.5",
                  "collect_date": "2026-11-10T15:00:00.000Z",
                  "customer": { "trading_firstname": "LAB C" },
                  "billing": { "step": "FINALIZADO", "budgetPrice": 100, "totalAdditionalCollects": "42.5" },
                  "source_addresses": [ { "state": "MINAS GERAIS" } ],
                  "destination_addresses": [ { "state": "RJ" } ] } ] }
              """)
            : null;

        var message = await new EmissionFlows(rig.Ctx).ComplementaryAsync(new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30), "300; 301", CancellationToken.None);

        Assert.Equal("Emissões de CTe complementar finalizadas!\n\nProtocolos não elegíveis: 301", message);
        Assert.Contains("protocols=300%2C301", rig.Http.Calls.First(c => c.Path.Contains("complementary?")).Path);
        Assert.Equal(["original:5555", "taxes"], rig.Driver.Steps);

        var tax = Assert.Single(rig.Driver.Taxes);
        Assert.True(tax.Complementary);
        Assert.Equal("42.5", tax.Price);
        Assert.Equal("12", tax.Tax); // MG to RJ
        Assert.Equal("4.08", tax.TpInfo);

        var post = Assert.Single(rig.Http.To("/api/uploads/cte-complementary/json"));
        Assert.Contains("\"cte_complementary\":1001", post.Body);
        Assert.Contains("CTE COMPLEMENTAR", rig.Http.Calls.Single(c => c.Path == "/api/pdf").Body);
    }

    [Fact]
    public async Task Complementary_rejects_bad_protocol_lists_before_calling_the_api()
    {
        using var rig = new TestRig();
        var flows = new EmissionFlows(rig.Ctx);
        var start = new DateOnly(2026, 11, 1);

        Assert.Equal("Protocolos inválidos. Separe os protocolos com ';'.",
            await flows.ComplementaryAsync(start, start, "12a;13", CancellationToken.None));
        Assert.Equal("Informe ao menos um protocolo para a emissão avulsa.",
            await flows.ComplementaryAsync(start, start, " ; ", CancellationToken.None));
        Assert.Empty(rig.Http.Calls);
    }

    [Fact]
    public async Task Single_normal_emission_refuses_invoice_customers_and_uploads_the_csv()
    {
        using var rig = new TestRig();
        rig.Http.Responder = call =>
        {
            if (call.Path.EndsWith("/cte-emission/900"))
                return FakeApi.Json("""
                  { "service": { "protocol": 900, "step": "toCollectService",
                      "customer": { "trading_firstname": "LAB D", "cnpj_cpf": "10.000.000/0001-00", "emission_type": "CTE" },
                      "requested_service": { "price": 80.0 }, "source_city": "BH", "destination_city": "RIO",
                      "source_addresses": [ { "city_state": "MINAS GERAIS", "cnpj_cpf": "10.000.000/0001-00" } ],
                      "destination_addresses": [ { "city_state": "RIO DE JANEIRO", "cnpj_cpf": "20.000.000/0001-00" } ] } }
                  """);
            if (call.Path.EndsWith("/cte-emission/901"))
                return FakeApi.Json("""
                  { "service": { "protocol": 901, "customer": { "emission_type": "NF" },
                      "source_addresses": [], "destination_addresses": [] } }
                  """);
            return call.Path.EndsWith("/cte-emission/902") ? FakeApi.Json("""{"service":null}""") : null;
        };

        var message = await new EmissionFlows(rig.Ctx).SingleAsync(new DateOnly(2026, 11, 17), symbolic: false,
            "900;901; 902", "2", CancellationToken.None);

        Assert.Equal("Emissões realizadas com sucesso!", message);
        Assert.Contains("Serviço com protocolo 902 não encontrado!", rig.Operator.Notices);
        Assert.Contains("Protocolo 901 não pode ser emitido como CTe normal!", rig.Operator.Notices);

        Assert.Equal(new CargoInput("2"), Assert.Single(rig.Driver.Cargos));
        var tax = Assert.Single(rig.Driver.Taxes);
        Assert.Equal("80.0", tax.Price); // typed exactly as the Python app typed it
        Assert.Equal("12", tax.Tax);
        Assert.Equal(Payer.Sender, rig.Driver.Parties[0].Payer);

        var csv = Assert.Single(rig.Http.Calls, c => c.Path == "/api/uploads/cte-loglife");
        Assert.Contains("Protocolo,CTE Loglife,Data Emissão CTE\r\n900,1001,17/11/2026", csv.Body);
        Assert.Single(Directory.GetFiles(rig.ReportFolder, "Lista CTE-17-11-2026-14-30.xlsx"));
    }

    [Fact]
    public async Task Batch_cancellation_clears_each_protocol_and_survives_a_failed_cancel()
    {
        using var rig = new TestRig();
        rig.Http.Responder = call => call.Path.StartsWith("/api/public/service/cancelled-unsuccessful")
            ? FakeApi.Json("""
              { "services": [
                  { "protocol": 1, "cte_loglife": "10", "cte_complementary": "-" },
                  { "protocol": 2, "cte_loglife": 20, "cte_complementary": "21" },
                  { "protocol": 3, "cte_loglife": null } ] }
              """)
            : null;
        rig.Driver.OnCancel = number =>
        {
            if (number == "20") throw new ElementNotFoundException("8confirmar");
        };

        var message = await new MaintenanceFlows(rig.Ctx).CancelBatchAsync(new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 2), CancellationToken.None);

        Assert.Equal(["10", "21"], rig.Driver.Cancelled);
        Assert.Contains("CT-es Loglife:\n  Cancelados: 1\n  Erros: 1\n  API: 1 OK, 0 erro", message);
        Assert.Contains("CT-es Complementares:\n  Cancelados: 1\n  Erros: 0\n  API: 1 OK, 0 erro", message);
        Assert.EndsWith("Total: 2 cancelados, 1 erros", message);

        var cleared = Assert.Single(rig.Http.To("/api/uploads/cte-loglife/json"));
        Assert.Equal("""{"data":[{"protocol":"1","cte_loglife":null,"cte_loglife_emission_date":null}]}""", cleared.Body);
        Assert.Single(rig.Http.To("/api/uploads/cte-complementary/json"));
        Assert.False(rig.Control.InCritical);
    }

    [Fact]
    public async Task Gnre_comparison_writes_the_workbook_the_slip_entry_reads()
    {
        using var rig = new TestRig();
        var bsoft = new ReportTable();
        bsoft.AddRow(("Nº CT-e", 1234), ("Data", new DateTime(2026, 11, 3)), ("Remetente - UF", "SP"),
            ("Destinatário - UF", "MG"), ("Total", 1000.0));
        bsoft.AddRow(("Nº CT-e", 9999), ("Data", "2026-11-04"), ("Remetente - UF", "SP"),
            ("Destinatário - UF", "RJ"), ("Total", "50,00"));
        rig.Settings.BsoftReportPath = Path.Combine(rig.Root, "bsoft.xlsx");
        bsoft.Save(rig.Settings.BsoftReportPath);

        var target = new ReportTable();
        target.AddRow(("Chave Documento", "3526111234567800019957001" + "000001234" + "1000000010"),
            ("Valor do GNRE", 90.0), ("UF Favorecida", "MG"), ("Linha Digitável", "85800000000 0"));
        rig.Settings.TargetReportPath = Path.Combine(rig.Root, "target.xlsx");
        target.Save(rig.Settings.TargetReportPath);

        var flows = new MaintenanceFlows(rig.Ctx);
        var message = await flows.CompareGnreAsync(CancellationToken.None);

        // SP to MG is 12%; with the 20% presumed credit the slip should be 9.6% of 1000 = 96.00.
        Assert.Contains("CT-es processados: 1", message);
        Assert.Contains("Diferença total: R$ 6.00", message);
        var output = Path.Combine(rig.ReportFolder, "comparar-GNRE-17-11-2026-14-30.xlsx");
        var detail = SheetTable.Read(output, "Detalhado");
        Assert.Equal("03/11/2026", SheetTable.Text(detail.Rows[0][detail.IndexOf("Data")]));
        Assert.Equal(96m, SheetTable.Number(detail.Rows[0][detail.IndexOf("GNRE Calculado")]));
        var summary = SheetTable.Read(output, "Resumo");
        Assert.Equal("MG", SheetTable.Text(summary.Rows[0][0]));

        // The slip entry reads that workbook and asks the system for the cost center.
        rig.Settings.TargetReportPath = output;
        rig.Http.Responder = call => call.Path.StartsWith("/api/public/service/by-cte")
            ? FakeApi.Json("""{"cost_center":"BH MATRIZ","cnpj_cpf":"1"}""")
            : null;
        var slipMessage = await flows.EnterIcmsSlipsAsync(CancellationToken.None);

        Assert.Equal("Processamento concluído!\n\nGNREs processadas: 1\nErros: 0", slipMessage);
        Assert.Equal(new IcmsSlip("1234", "90.0", "80", "BH MATRIZ", "85800000000 0", "03/11/2026"),
            Assert.Single(rig.Driver.Slips));
        // Public routes carry the token built into the app and need no login.
        var lookup = rig.Http.To("/api/public/service/by-cte").Single();
        Assert.Equal(Core.Api.LogLifeApi.PublicToken, lookup.XToken);
        Assert.Null(lookup.Authorization);
    }

    [Fact]
    public async Task Payer_conference_flags_documents_that_differ_and_tolerates_lost_leading_zeros()
    {
        using var rig = new TestRig();
        var sheet = new ReportTable();
        sheet.AddRow(("Número", 1), ("Pagador do Frete - CNPJ/CPF", 1234567000190.0), ("Pagador do Frete - Nome", "ZERO"), ("Total", 100));
        sheet.AddRow(("Número", 2), ("Pagador do Frete - CNPJ/CPF", "11.222.333/0001-44"), ("Pagador do Frete - Nome", "OUTRO"), ("Total", 100));
        sheet.AddRow(("Número", 3), ("Pagador do Frete - CNPJ/CPF", "x"), ("Pagador do Frete - Nome", "SIMB"), ("Total", "R$ 5,00"));
        sheet.AddRow(("Número", 4), ("Pagador do Frete - CNPJ/CPF", "y"), ("Pagador do Frete - Nome", "SUMIU"), ("Total", 10));
        var path = Path.Combine(rig.Root, "conf.xlsx");
        sheet.Save(path);
        rig.Http.Responder = call =>
        {
            if (call.Path.EndsWith("cteNumber=1")) return FakeApi.Json("""{"cnpj_cpf":"01.234.567/0001-90"}""");
            if (call.Path.EndsWith("cteNumber=2")) return FakeApi.Json("""{"cnpj_cpf":"99.999.999/0001-99"}""");
            return FakeApi.Json("""{"message":"not found"}""", HttpStatusCode.NotFound);
        };

        var result = await new MaintenanceFlows(rig.Ctx).ConferPayersAsync(path, "pagador do frete - cnpj/cpf ",
            CancellationToken.None);

        Assert.Equal((3, 0, 1, 1), (result.CheckedRows, result.SkippedRows, result.NotFoundRows, result.ValueFiveRows));
        var issue = Assert.Single(result.Inconsistencies);
        Assert.Equal(new PayerInconsistency("2", 3, "OUTRO", "11.222.333/0001-44", "99.999.999/0001-99", "CNPJ/CPF divergente"), issue);
    }
}
