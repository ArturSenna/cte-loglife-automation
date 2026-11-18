using System.Globalization;
using System.Text.Json;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Journal;
using CteLogLife.Core.Tax;

namespace CteLogLife.Core.Flows;

/// <summary>
/// The emission flows. Each one fetches its services, writes the conference spreadsheet and hands a list
/// of jobs to the <see cref="EmissionRunner"/>. They differ only in where the data comes from.
/// </summary>
public sealed partial class EmissionFlows(FlowContext ctx)
{
    private const string CteColumn = "CTE LOGLIFE";

    private readonly EmissionRunner _runner = new(ctx);

    // ------------------------------------------------------------------ unified (symbolic + normal)

    /// <summary>
    /// Emits every CT-e the system lists for the period: symbolic first, then normal by road, then the
    /// other normal ones (the order comes from the API).
    /// </summary>
    public async Task<string> UnifiedAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var reportFolder = FlowContext.RequireFolder(ctx.Settings.NormalFolder, "CTe normal");
        var pdfFolder = FlowContext.RequireFolder(ctx.Settings.CtePdfFolder, "Pasta CTE");
        var tax = ctx.Tax();

        ctx.Log.Info($"Buscando serviços para emissão unificada de {start:dd/MM/yyyy} a {end:dd/MM/yyyy}...");
        var root = await ctx.Api.GetUnifiedEmissionAsync(start, end, ct);
        ctx.DumpDebug("ServicesAPI_Unified.json", root);

        var services = root.Items("services");
        var total = Count(root, "total", services.Count);
        var symbolicCount = Count(root, "symbolic_count", 0);
        var normalCount = Count(root, "normal_count", 0);
        ctx.Log.Info($"{total} serviço(s): {symbolicCount} simbólico(s), {normalCount} normal(is).");
        if (total == 0 || services.Count == 0)
            return "Nenhum serviço encontrado para emissão de CTE unificado no período selecionado.";

        var stamp = ctx.Stamp(start);
        var excelFile = Path.Combine(reportFolder, $"Lista CTE Unificado-{stamp}.xlsx");
        var associateCsv = Path.Combine(reportFolder, $"Associar Unificado-{stamp}.csv");

        var report = new ReportTable();
        var reportLock = new object(); // the bot's thread and an include request may both touch the report
        EmissionJob Build(JsonElement service)
        {
            var protocol = service.Str("protocol");
            var symbolic = service.Get("is_symbolic").Truthy();
            var label = service.Str("priority_label", "UNKNOWN");
            var source = service.Items("source_addresses");
            var destination = service.Items("destination_addresses");
            var customer = service.Get("customer");
            var price = service.Get("requested_service").Get("price").Or(service.Get("budget").Get("price"));

            var row = report.AddRow(
                ("PROTOCOLO", ServiceText.ProtocolCell(protocol)),
                ("TIPO", label),
                ("CLIENTE", customer.Str("trading_firstname")),
                ("ETAPA", ServiceText.StepLabel(service.Str("step"))),
                ("DATA COLETA", ServiceText.CollectDate(service.Get("requested_service").Get("collect_date"))),
                ("PREÇO TRANSPORTE", ServiceText.Cell(service.Get("requested_service").Get("price"))),
                ("NOME REMETENTE", ServiceText.Names(source)),
                ("CIDADE ORIGEM", service.Str("source_city")),
                ("ENDEREÇO REMETENTE", ServiceText.Meta(source)),
                ("CNPJ/CPF REMETENTE", ServiceText.CnpjsListed(source)),
                ("COLETADOR ORIGEM", service.Get("source_collector").Str("trading_name")),
                ("NOME DESTINATÁRIO", ServiceText.Names(destination)),
                ("CIDADE DESTINO", service.Str("destination_city")),
                ("ENDEREÇO DESTINATÁRIO", ServiceText.Meta(destination)),
                ("CNPJ/CPF DESTINATÁRIO", ServiceText.CnpjsListed(destination)),
                ("COLETADOR DESTINO", service.Get("destination_collector").Str("trading_name")));

            PartiesInput parties;
            CargoInput cargo;
            string value;
            if (symbolic)
            {
                var data = service.Get("symbolic_emission_data");
                parties = new PartiesInput(
                    [data.Str("source_collector_cnpj")],
                    [data.Str("destination_collector_cnpj")],
                    PartiesInput.ParsePayer(data.Str("tomador", "Remetente")));
                value = data.Str("fixed_value", "5,00");
                cargo = new CargoInput(data.Str("volumes", "1"), InstanceOf(data));
            }
            else
            {
                parties = NormalParties(source, destination, customer.Str("cnpj_cpf"));
                value = price.Truthy() ? PyFormat.Json(price!.Value) : "0";
                cargo = new CargoInput();
            }

            var icms = IcmsCalculator.Compute(tax, FirstState(source), FirstState(destination),
                $"Protocolo {protocol}", PyFormat.Money(value));

            return new EmissionJob
            {
                Protocol = protocol,
                Title = $"{protocol} · {customer.Str("trading_firstname")}",
                Subtitle = $"{label} · {service.Str("source_city")} → {service.Str("destination_city")}",
                Parties = parties,
                Cargo = cargo,
                Tax = new TaxInput(value, icms.TaxText, icms.TpInfo, icms.OriginUf, icms.Observation),
                Plan = new PostPlan
                {
                    Kind = CteKind.LogLife,
                    Symbolic = symbolic,
                    AssociateCsvPath = associateCsv,
                    PdfFolder = pdfFolder,
                    UploadType = LogLifeApi.UploadTypeLogLife,
                    CsvProtocol = protocol,
                },
                Row = row,
            };
        }

        var jobs = services.Select(Build).ToList();
        report.Save(excelFile);
        ctx.Log.Info($"Relatório exportado: {excelFile}");

        var outcome = await _runner.RunAsync("Emissão unificada", jobs, (job, number) =>
        {
            lock (reportLock)
            {
                report.Set(job.Row, CteColumn, number);
                ctx.SaveReport(report, excelFile);
            }
        }, ct, include: async (protocol, token) =>
        {
            // The list is asked again, so the new protocol comes with the same data as the others.
            var fresh = await ctx.Api.GetUnifiedEmissionAsync(start, end, token);
            var service = FindService(fresh.Items("services"), protocol) ?? throw new FlowException(
                $"O protocolo {protocol} não está entre os serviços a emitir de {start:dd/MM/yyyy} a {end:dd/MM/yyyy}. "
                + "Se for de outro período, emita-o pela emissão avulsa.");
            lock (reportLock)
            {
                var job = Build(service);
                ctx.SaveReport(report, excelFile);
                return job;
            }
        });

        return outcome.Message(
            "Emissões de CTE unificado realizadas com sucesso!\n\n"
            + $"Total: {total}\nSimbólicos: {symbolicCount}\nNormais: {normalCount}");
    }

    // ------------------------------------------------------------------ symbolic only

    public async Task<string> SymbolicAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var reportFolder = FlowContext.RequireFolder(ctx.Settings.NormalFolder, "CTe normal");
        var pdfFolder = FlowContext.RequireFolder(ctx.Settings.CtePdfFolder, "Pasta CTE");
        var tax = ctx.Tax();

        ctx.Log.Info($"Buscando serviços para CT-e simbólico de {start:dd/MM/yyyy} a {end:dd/MM/yyyy}...");
        var root = await ctx.Api.GetSymbolicEmissionAsync(start, end, ct);
        ctx.DumpDebug("ServicesAPI_Symbolic.json", root);

        var services = root.Items("services").OrderBy(s => ProtocolOrder(s.Str("protocol"))).ToList();
        var total = Count(root, "total", services.Count);
        if (total == 0 || services.Count == 0)
            return "Nenhum serviço encontrado para emissão de CTE simbólico no período selecionado.";

        var stamp = ctx.Stamp(start);
        var excelFile = Path.Combine(reportFolder, $"Lista CTE Simbólico-{stamp}.xlsx");
        var associateCsv = Path.Combine(reportFolder, $"Associar Simbólicos-{stamp}.csv");

        var report = new ReportTable();
        var reportLock = new object();
        EmissionJob Build(JsonElement service)
        {
            var protocol = service.Str("protocol");
            var customer = service.Get("customer");
            var source = service.Items("source_addresses");
            var destination = service.Items("destination_addresses");
            var data = service.Get("symbolic_emission_data");

            var row = report.AddRow(
                ("PROTOCOLO", ServiceText.ProtocolCell(protocol)),
                ("CLIENTE", customer.Str("trading_firstname")),
                ("ETAPA", ServiceText.StepLabel(service.Str("step"))),
                ("DATA COLETA", ServiceText.CollectDate(service.Get("requested_service").Get("collect_date"))),
                ("CIDADE ORIGEM", service.Str("source_city")),
                ("COLETADOR ORIGEM", service.Get("source_collector").Str("trading_name")),
                ("CIDADE DESTINO", service.Str("destination_city")),
                ("COLETADOR DESTINO", service.Get("destination_collector").Str("trading_name")),
                ("TRANSPORTADORA", service.Get("source_branch").Str("shipping_company")));

            // The ICMS amount of this flow is computed on the budget price, although 5,00 is emitted.
            var budget = service.Get("requested_service").Get("price").Or(service.Get("budget").Get("price"));
            var icms = IcmsCalculator.Compute(tax, FirstState(source), FirstState(destination),
                $"Protocolo {protocol}", budget.Number() ?? 0m);
            var value = data.Str("fixed_value", "5,00");

            return new EmissionJob
            {
                Protocol = protocol,
                Title = $"{protocol} · {customer.Str("trading_firstname")}",
                Subtitle = $"SIMBÓLICO · {service.Str("source_city")} → {service.Str("destination_city")}",
                Parties = new PartiesInput(
                    [data.Str("source_collector_cnpj")],
                    [data.Str("destination_collector_cnpj")],
                    PartiesInput.ParsePayer(data.Str("tomador", "Remetente"))),
                Cargo = new CargoInput(data.Str("volumes", "1"), InstanceOf(data)),
                Tax = new TaxInput(value, icms.TaxText, icms.TpInfo, icms.OriginUf, icms.Observation),
                Plan = new PostPlan
                {
                    Kind = CteKind.LogLife,
                    Symbolic = true,
                    AssociateCsvPath = associateCsv,
                    PdfFolder = pdfFolder,
                    UploadType = LogLifeApi.UploadTypeLogLife,
                    CsvProtocol = protocol,
                },
                Row = row,
            };
        }

        var jobs = services.Select(Build).ToList();
        report.Save(excelFile);
        ctx.Log.Info($"Relatório exportado: {excelFile}");

        var outcome = await _runner.RunAsync("CT-e simbólico", jobs, (job, number) =>
        {
            lock (reportLock)
            {
                report.Set(job.Row, CteColumn, number);
                ctx.SaveReport(report, excelFile);
            }
        }, ct, include: async (protocol, token) =>
        {
            var fresh = await ctx.Api.GetSymbolicEmissionAsync(start, end, token);
            var service = FindService(fresh.Items("services"), protocol) ?? throw new FlowException(
                $"O protocolo {protocol} não está entre os CT-es simbólicos a emitir de {start:dd/MM/yyyy} a {end:dd/MM/yyyy}.");
            lock (reportLock)
            {
                var job = Build(service);
                ctx.SaveReport(report, excelFile);
                return job;
            }
        });

        return outcome.Message("Emissões de CTE simbólico realizadas com sucesso!");
    }

    // ------------------------------------------------------------------ single protocols ("CTe avulso")

    /// <summary>Emits the protocols typed by the operator (separated by ';'), as normal or symbolic CT-es.</summary>
    public async Task<string> SingleAsync(DateOnly date, bool symbolic, string protocolsText, string volumes,
        CancellationToken ct)
    {
        var reportFolder = FlowContext.RequireFolder(ctx.Settings.NormalFolder, "CTe normal");
        var pdfFolder = FlowContext.RequireFolder(ctx.Settings.CtePdfFolder, "Pasta CTE");
        var tax = ctx.Tax();

        var protocols = protocolsText.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        if (protocols.Count == 0) return "Informe ao menos um protocolo para a emissão avulsa.";
        if (!int.TryParse(volumes, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return "Informe a quantidade de volumes.";

        var services = new List<JsonElement>();
        foreach (var protocol in protocols)
        {
            ctx.Log.Info($"Buscando serviço do protocolo {protocol}...");
            JsonElement? service;
            try
            {
                service = await ctx.Api.GetEmissionServiceAsync(protocol, ct);
            }
            catch (ApiException ex)
            {
                return $"Erro ao buscar serviço {protocol}: {ex.Message}";
            }
            if (service is { } found) services.Add(found);
            else ctx.Operator.Notice($"Serviço com protocolo {protocol} não encontrado!");
        }
        if (services.Count == 0) return "Nenhum serviço encontrado para os protocolos informados.";

        var stamp = ctx.Stamp(date);
        var excelFile = Path.Combine(reportFolder,
            symbolic ? $"CTe Simbólico-{stamp}.xlsx" : $"Lista CTE-{stamp}.xlsx");
        var uploadCsv = Path.Combine(reportFolder, $"Upload-{stamp}.csv");
        var associateCsv = Path.Combine(reportFolder, $"Associar-{stamp}.csv");

        var report = new ReportTable();
        var reportLock = new object();
        var jobs = new List<EmissionJob>();

        // A customer invoiced by NF never gets a normal CT-e.
        bool InvoiceOnly(JsonElement service) =>
            !symbolic && service.Get("customer").Str("emission_type") == "NF";

        int AddRow(JsonElement service)
        {
            var source = service.Items("source_addresses");
            var destination = service.Items("destination_addresses");
            return report.AddRow(
                ("PROTOCOLO", ServiceText.ProtocolCell(service.Str("protocol"))),
                ("CLIENTE", service.Get("customer").Str("trading_firstname")),
                ("ETAPA", ServiceText.StepLabel(service.Str("step"))),
                ("DATA COLETA", ServiceText.CollectDate(service.Get("requested_service").Get("collect_date"))),
                ("PREÇO TRANSPORTE", ServiceText.Cell(service.Get("requested_service").Get("price"))),
                ("PREÇO KG EXTRA", ServiceText.Cell(service.Get("requested_service").Get("price_kg_extra"))),
                ("NOME REMETENTE", ServiceText.Names(source)),
                ("CIDADE ORIGEM", service.Str("source_city")),
                ("ENDEREÇO REMETENTE", ServiceText.Meta(source)),
                ("CNPJ/CPF REMETENTE", ServiceText.CnpjsListed(source)),
                ("COLETADOR ORIGEM", service.Get("source_collector").Str("trading_name")),
                ("NOME DESTINATÁRIO", ServiceText.Names(destination)),
                ("CIDADE DESTINO", service.Str("destination_city")),
                ("ENDEREÇO DESTINATÁRIO", ServiceText.Meta(destination)),
                ("CNPJ/CPF DESTINATÁRIO", ServiceText.CnpjsListed(destination)),
                ("COLETADOR DESTINO", service.Get("destination_collector").Str("trading_name")));
        }

        EmissionJob Build(JsonElement service, int row)
        {
            var protocol = service.Str("protocol");
            var customer = service.Get("customer");
            var customerName = customer.Str("trading_firstname");
            var customerCnpj = customer.Str("cnpj_cpf");
            var source = service.Items("source_addresses");
            var destination = service.Items("destination_addresses");
            var price = service.Get("requested_service").Get("price").Or(service.Get("budget").Get("price"));

            PartiesInput parties;
            string value;
            decimal icmsBase;
            if (symbolic)
            {
                value = "5,00";
                icmsBase = 5m;
                var senders = CollectorCnpj(service.Get("source_collector"));
                var receivers = customerName == "METHODOS LABORATORIO"
                    ? ["30296133000118"]
                    : CollectorCnpj(service.Get("destination_collector"));
                // These collectors never pay the freight: the receiver does.
                var receiverPays = senders.Count > 0 && senders[0] is
                    "17.062.517/0001-08" or "17.062.517/0002-99" or "50.699.404/0001-93";
                parties = new PartiesInput(senders, receivers, receiverPays ? Payer.Receiver : Payer.Sender,
                    customerCnpj);
            }
            else
            {
                value = price.Truthy() ? PyFormat.Json(price!.Value) : "0";
                icmsBase = PyFormat.Money(value);
                parties = NormalParties(source, destination, customerCnpj);
            }

            var icms = IcmsCalculator.Compute(tax,
                source.Count > 0 ? BrazilStates.AbbreviationOrSame(source[0].Str("city_state", source[0].Str("state"))) : "",
                destination.Count > 0 ? BrazilStates.AbbreviationOrSame(destination[0].Str("city_state", destination[0].Str("state"))) : "",
                $"Protocolo {protocol}", icmsBase);

            return new EmissionJob
            {
                Protocol = protocol,
                Title = $"{protocol} · {customerName}",
                Subtitle = $"{(symbolic ? "SIMBÓLICO" : "NORMAL")} · {service.Str("source_city")} → {service.Str("destination_city")}",
                Parties = parties,
                Cargo = new CargoInput(volumes, symbolic ? 1 : null),
                Tax = new TaxInput(value, icms.TaxText, icms.TpInfo, icms.OriginUf, icms.Observation),
                Plan = new PostPlan
                {
                    Kind = CteKind.LogLife,
                    Symbolic = symbolic,
                    UploadCsvPath = symbolic ? null : uploadCsv,
                    AssociateCsvPath = associateCsv,
                    PdfFolder = pdfFolder,
                    UploadType = LogLifeApi.UploadTypeLogLife,
                    CsvProtocol = protocol,
                },
                Row = row,
            };
        }

        var blocked = false;
        foreach (var service in services)
        {
            var row = AddRow(service);
            if (blocked) continue;
            if (InvoiceOnly(service))
            {
                // As before: this protocol and the ones after it are left out.
                ctx.Operator.Notice($"Protocolo {service.Str("protocol")} não pode ser emitido como CTe normal!");
                blocked = true;
                continue;
            }
            jobs.Add(Build(service, row));
        }

        report.Save(excelFile);
        if (jobs.Count == 0)
        {
            DeleteQuietly(excelFile, symbolic);
            return "Nenhum CT-e emitido.";
        }

        var outcome = await _runner.RunAsync(symbolic ? "CT-e simbólico avulso" : "CT-e avulso", jobs,
            (job, number) =>
            {
                lock (reportLock)
                {
                    report.Set(job.Row, CteColumn, number);
                    ctx.SaveReport(report, excelFile);
                }
            }, ct, include: async (protocol, token) =>
            {
                // Same rules as the protocols typed at the start: same kind of CT-e, same volumes.
                var service = await ctx.Api.GetEmissionServiceAsync(protocol, token)
                              ?? throw new FlowException($"Serviço com protocolo {protocol} não encontrado!");
                if (InvoiceOnly(service))
                    throw new FlowException($"Protocolo {protocol} não pode ser emitido como CTe normal!");
                lock (reportLock)
                {
                    var job = Build(service, AddRow(service));
                    ctx.SaveReport(report, excelFile);
                    return job;
                }
            });

        // The symbolic single emission never kept its spreadsheet.
        DeleteQuietly(excelFile, symbolic);
        return outcome.Message("Emissões realizadas com sucesso!");
    }

    // ------------------------------------------------------------------ helpers

    private static PartiesInput NormalParties(IReadOnlyList<JsonElement> source,
        IReadOnlyList<JsonElement> destination, string customerCnpj)
    {
        IReadOnlyList<string> senders = ServiceText.Cnpjs(source);
        IReadOnlyList<string> receivers = ServiceText.Cnpjs(destination);
        var payer = Payer.Other;
        if (senders.Contains(customerCnpj))
        {
            payer = Payer.Sender;
            senders = [customerCnpj];
        }
        else if (receivers.Contains(customerCnpj))
        {
            payer = Payer.Receiver;
            receivers = [customerCnpj];
        }
        return new PartiesInput(senders, receivers, payer, customerCnpj);
    }

    private static IReadOnlyList<string> CollectorCnpj(JsonElement? collector) =>
        collector is { ValueKind: JsonValueKind.Object } ? [collector.Str("cnpj")] : [];

    private static string FirstState(IReadOnlyList<JsonElement> addresses) =>
        addresses.Count > 0
            ? BrazilStates.Abbreviation(addresses[0].Str("city_state", addresses[0].Str("state")))
            : "";

    private static int? InstanceOf(JsonElement? symbolicData) =>
        symbolicData.Get("fixed_cte_instance") is { ValueKind: JsonValueKind.Number } instance
            ? instance.GetInt32()
            : 1;

    /// <summary>The service of a protocol in an API listing (protocols come as numbers or as text).</summary>
    private static JsonElement? FindService(IReadOnlyList<JsonElement> services, string protocol)
    {
        foreach (var service in services)
            if (service.Str("protocol") == protocol) return service;
        return null;
    }

    private static int Count(JsonElement root, string name, int fallback) =>
        root.Get(name) is { ValueKind: JsonValueKind.Number } value ? value.GetInt32() : fallback;

    private static (int, long, string) ProtocolOrder(string protocol) =>
        long.TryParse(protocol, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (0, number, "")
            : (1, 0L, protocol);

    private static void DeleteQuietly(string path, bool condition)
    {
        if (!condition) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The spreadsheet is a convenience copy.
        }
    }
}
