using System.Globalization;
using System.Text.Json;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Journal;
using CteLogLife.Core.Tax;

namespace CteLogLife.Core.Flows;

public sealed partial class EmissionFlows
{
    // ------------------------------------------------------------------ complementary

    /// <summary>
    /// Emits complementary CT-es for the extras of services whose normal CT-e already exists.
    /// With <paramref name="protocolsText"/> ("Emitir Avulso") only those protocols are emitted, even
    /// when they already have a complementary CT-e.
    /// </summary>
    public async Task<string> ComplementaryAsync(DateOnly start, DateOnly end, string? protocolsText,
        CancellationToken ct)
    {
        List<long>? protocols = null;
        if (protocolsText is not null)
        {
            protocols = [];
            foreach (var entry in protocolsText.Split(';').Where(e => e.Trim().Length > 0))
            {
                if (!long.TryParse(entry.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                    return "Protocolos inválidos. Separe os protocolos com ';'.";
                protocols.Add(number);
            }
            if (protocols.Count == 0) return "Informe ao menos um protocolo para a emissão avulsa.";
        }

        var reportFolder = FlowContext.RequireFolder(ctx.Settings.ComplementaryFolder, "CTe complementar");
        var pdfFolder = FlowContext.RequireFolder(ctx.Settings.CtePdfFolder, "Pasta CTE");
        var tax = ctx.Tax();

        ctx.Log.Info("Requisitando serviços elegíveis para CTe complementar");
        IReadOnlyList<JsonElement> services;
        try
        {
            services = await ctx.Api.GetComplementaryEmissionAsync(start, end, protocols, ct);
        }
        catch (ApiException ex)
        {
            return $"Erro ao buscar serviços para CTe complementar:\n{ex.Message}";
        }

        var returned = services.Select(s => s.Str("protocol")).ToHashSet();
        var notEligible = (protocols ?? []).Select(p => p.ToString(CultureInfo.InvariantCulture))
            .Where(p => !returned.Contains(p)).ToList();
        var notEligibleText = notEligible.Count > 0
            ? "\n\nProtocolos não elegíveis: " + string.Join(", ", notEligible)
            : "";
        ctx.Log.Info($"{services.Count} serviço(s) para CTe complementar.");
        if (services.Count == 0)
            return "Nenhum serviço encontrado para emissão de CTe complementar no período selecionado."
                   + notEligibleText;

        var stamp = ctx.Stamp(start);
        var excelFile = Path.Combine(reportFolder, $"CTE complementar-{stamp}.xlsx");
        var associateCsv = Path.Combine(reportFolder, $"Associar complementar-{stamp}.csv");

        var report = new ReportTable();
        var reportLock = new object();
        EmissionJob Build(JsonElement service)
        {
            var protocol = service.Str("protocol");
            var billing = service.Get("billing");
            var customer = service.Get("customer");
            var extra = service.Get("extra_value").Number()
                        ?? throw new FlowException($"Protocolo {protocol}: valor do extra ausente na resposta da API.");

            var row = report.AddRow(
                ("PROTOCOLO", ServiceText.ProtocolCell(protocol)),
                ("CLIENTE", customer.Str("trading_firstname")),
                ("ETAPA", ServiceText.Cell(billing.Get("step"))),
                ("TIPO DE SERVIÇO", ServiceText.Cell(billing.Get("serviceType"))),
                ("DATA COLETA", ServiceText.CollectDate(service.Get("collect_date"))),
                ("CIDADE ORIGEM", ServiceText.Cell(billing.Get("sourceCity"))),
                ("CIDADE DESTINO", ServiceText.Cell(billing.Get("destinationCity"))),
                ("CTE LOGLIFE", ServiceText.Cell(service.Get("cte_loglife"))),
                ("VALOR DO ORÇAMENTO", ServiceText.Cell(billing.Get("budgetPrice"))),
                ("FRANQUIA", ServiceText.Cell(billing.Get("franchising"))),
                ("PESO TAXADO NO SERVIÇO", ServiceText.Cell(billing.Get("taxedWeight"))),
                ("VOLUME EMBARQUE", ServiceText.Cell(billing.Get("boardVolume"))),
                ("VALOR KG EXTRA", ServiceText.Cell(billing.Get("extraKGPrice"))),
                ("VALOR TOTAL KG EXTRA", ServiceText.Cell(billing.Get("totalExtraKGPrice"))),
                ("QTD. END. ORIGEM NO SERVIÇO", ServiceText.Cell(billing.Get("serviceSourceAddressQty"))),
                ("QTD. END. ORIGEM NO ORÇAMENTO", ServiceText.Cell(billing.Get("budgetSourceAddressQty"))),
                ("VALOR COLETA ADICIONAL", ServiceText.Cell(billing.Get("addCollectPrice"))),
                ("VALOR TOTAL COLETAS ADICIONAIS", billing.Get("totalAdditionalCollects").Number() ?? 0m),
                ("VALOR COLETAS ADICIONAIS AJUSTADO", ServiceText.Cell(service.Get("extra_breakdown").Get("additional_collects"))),
                ("QTD. END. DESTINO NO SERVIÇO", ServiceText.Cell(billing.Get("serviceDestinationAddressQty"))),
                ("QTD. END. DESTINO NO ORÇAMENTO", ServiceText.Cell(billing.Get("budgetDestinationAddressQty"))),
                ("VALOR ENTREGA ADICIONAL", ServiceText.Cell(billing.Get("addDeliveryPrice"))),
                ("VALOR TOTAL ENTREGAS ADICIONAIS", billing.Get("totalAdditionalDeliveries").Number() ?? 0m),
                ("NOME REMETENTE", ServiceText.Cell(billing.Get("sourceAddress"))),
                ("NOME DESTINATÁRIO", ServiceText.Cell(billing.Get("destinationAddress"))),
                ("MATERIAL EXTRA (CLIENTE)", ServiceText.Cell(billing.Get("infoExtraMaterials"))),
                ("CUSTO TOTAL MATERIAL EXTRA (CLIENTE)", ServiceText.Cell(billing.Get("totalCostExtraMaterials"))),
                ("PREÇO COLETA SEM SUCESSO", ServiceText.Cell(billing.Get("unsuccessPrice"))),
                ("VALOR TOTAL DO EXTRA", extra),
                ("OBSERVAÇÕES", ServiceText.Cell(billing.Get("observation"))));

            var source = service.Items("source_addresses");
            var destination = service.Items("destination_addresses");
            var icms = IcmsCalculator.Compute(tax,
                source.Count > 0 ? source[0].Str("state") : "",
                destination.Count > 0 ? destination[0].Str("state") : "",
                $"Protocolo {protocol}", extra, strict: true);

            var original = service.Str("cte_loglife");
            if (original.Length == 0)
                throw new FlowException($"Protocolo {protocol}: sem CT-e normal para complementar.");

            return new EmissionJob
            {
                Protocol = protocol,
                Title = $"{protocol} · {customer.Str("trading_firstname")}",
                Subtitle = $"COMPLEMENTAR do CT-e {original} · R$ {PyFormat.Fixed2(extra)}",
                OriginalCte = original,
                Tax = new TaxInput(PyFormat.Float(extra), icms.TaxText, icms.TpInfo, icms.OriginUf,
                    icms.Observation, Complementary: true),
                Plan = new PostPlan
                {
                    Kind = CteKind.Complementary,
                    AssociateCsvPath = associateCsv,
                    PdfFolder = pdfFolder,
                    UploadType = LogLifeApi.UploadTypeComplementary,
                    CsvProtocol = protocol,
                },
                Row = row,
            };
        }

        var jobs = services.Select(Build).ToList();
        report.Save(excelFile);
        ctx.Log.Info($"Relatório exportado: {excelFile}");

        var outcome = await _runner.RunAsync("CT-e complementar", jobs, (job, number) =>
        {
            lock (reportLock)
            {
                report.Set(job.Row, "CTE Complementar", number);
                ctx.SaveReport(report, excelFile);
            }
        }, ct, include: async (protocol, token) =>
        {
            // Asked by protocol, as "Emitir avulso" does: the system says whether it is eligible.
            if (!long.TryParse(protocol, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new FlowException($"\"{protocol}\" não é um protocolo válido.");
            var found = await ctx.Api.GetComplementaryEmissionAsync(start, end, [number], token);
            var service = FindService(found, protocol) ?? throw new FlowException(
                $"O protocolo {protocol} não é elegível para CT-e complementar de {start:dd/MM/yyyy} a {end:dd/MM/yyyy}.");
            lock (reportLock)
            {
                var job = Build(service);
                ctx.SaveReport(report, excelFile);
                return job;
            }
        });

        return outcome.Message("Emissões de CTe complementar finalizadas!" + notEligibleText);
    }

    // ------------------------------------------------------------------ grouped by UF ("CTe por UF")

    private static readonly string[] GroupedCustomers =
    [
        "OC PRECISION MEDICINE",
        "IRA INSTITUTO ROBERTO ALVARENGA LTDA",
        "MICROIMAGEM LAB DE ANATOMIA PAT E CITOPATOLOGIA SC LTDA",
    ];

    private sealed record GroupedService(JsonElement Service, string Protocol, decimal Price, decimal Additional,
        List<JsonElement> Source, List<JsonElement> Destination, string OriginUf, string DestinationUf,
        bool CapitalRoute);

    /// <summary>
    /// One CT-e per origin/destination state pair for the customers billed that way: the services of the
    /// period without a CT-e are summed (budget price plus additional charges) and emitted together.
    /// </summary>
    public async Task<string> GroupedByUfAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var reportFolder = FlowContext.RequireFolder(ctx.Settings.NormalFolder, "CTe normal");
        var pdfFolder = FlowContext.RequireFolder(ctx.Settings.CtePdfFolder, "Pasta CTE");
        var tax = ctx.Tax();

        ctx.Log.Info("Buscando endereços, coletadores, serviços e faturamento...");
        var queryStart = start.AddDays(-5);
        var queryEnd = end.AddDays(5);
        var addresses = (await ctx.Api.GetAddressesAsync(ct)).Items()
            .GroupBy(a => a.Str("id")).ToDictionary(g => g.Key, g => g.First());
        var collectors = (await ctx.Api.GetCollectorsAsync(ct)).Items()
            .GroupBy(c => c.Str("id")).ToDictionary(g => g.Key, g => g.First());
        var ongoing = (await ctx.Api.GetOngoingServicesAsync(ct)).Items();
        var finalized = (await ctx.Api.GetFinalizedServicesAsync(queryStart, queryEnd, ct)).Items();
        var billing = (await ctx.Api.GetBillingReportAsync(queryStart, queryEnd, ct)).Items();

        List<JsonElement> Resolve(JsonElement? ids) => ids.Items().Select(id =>
            addresses.TryGetValue(PyFormat.Json(id), out var address)
                ? address
                : throw new FlowException($"Endereço {PyFormat.Json(id)} não encontrado no cadastro.")).ToList();

        decimal AdditionalOf(string protocol)
        {
            var matches = billing.Where(b => b.Str("protocol") == protocol).ToList();
            if (matches.Count != 1) return 0m;
            var text = matches[0].Str("additionalChargesTotalValue").TrimStart('R', '$', ' ');
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0m, value)
                : 0m;
        }

        var windowStart = start.ToDateTime(TimeOnly.MinValue);
        var windowEnd = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var candidates = new List<GroupedService>();
        foreach (var service in ongoing.Concat(finalized))
        {
            if (service.Get("is_business") is not { ValueKind: JsonValueKind.False }) continue;
            if (!GroupedCustomers.Contains(service.Get("customerIDService").Str("trading_firstname"))) continue;
            if (service.Str("step") == "toValidateCancelRequest") continue;

            var requested = service.Get("serviceIDRequested");
            if (ServiceText.CollectMoment(requested.Get("collect_date")) is { } collect &&
                (collect < windowStart || collect > windowEnd)) continue;

            var cte = service.Get("cte_loglife");
            if (cte is not null && PyFormat.Json(cte.Value) is not ("nan" or "-")) continue;

            var price = requested.Get("budgetIDService").Get("price").Number() ?? 0m;
            if (price == 0m) continue;

            var source = Resolve(requested.Get("source_address_id"));
            var destination = Resolve(requested.Get("destination_address_id"));
            if (source.Count == 0 || destination.Count == 0) continue;
            var originState = source[0].Str("state");
            var destinationState = destination[0].Str("state");
            if (originState == destinationState) continue;

            var protocol = service.Str("protocol");
            var capitalRoute = InCapital(originState, Cities(source)) && InCapital(destinationState, Cities(destination));
            candidates.Add(new GroupedService(service, protocol, price, AdditionalOf(protocol), source, destination,
                originState, destinationState, capitalRoute));
        }

        var groups = candidates
            .OrderByDescending(c => c.CapitalRoute).ThenBy(c => ProtocolOrder(c.Protocol))
            .GroupBy(c => (c.OriginUf, c.DestinationUf))
            .OrderBy(g => g.Key.OriginUf, StringComparer.Ordinal).ThenBy(g => g.Key.DestinationUf, StringComparer.Ordinal)
            .ToList();
        if (groups.Count == 0)
            return "Nenhum serviço encontrado para emissão de CTe por UF no período selecionado.";

        var stamp = ctx.Stamp(start);
        var excelFile = Path.Combine(reportFolder, $"Lista CTE-{stamp}.xlsx");
        var uploadCsv = Path.Combine(reportFolder, $"Upload-{stamp}.csv");
        var associateCsv = Path.Combine(reportFolder, $"Associar-{stamp}.csv");

        var report = new ReportTable();
        var jobs = new List<EmissionJob>();
        foreach (var group in groups)
        {
            var first = group.First();
            var requested = first.Service.Get("serviceIDRequested");
            var customer = first.Service.Get("customerIDService");
            var protocols = string.Join(" ,", group.Select(g => g.Protocol));
            var totalPrice = group.Sum(g => g.Price);
            var totalAdditional = group.Sum(g => g.Additional);
            var total = totalPrice + totalAdditional;

            string CollectorName(string field) =>
                requested.Get(field) is { } id && collectors.TryGetValue(PyFormat.Json(id), out var collector)
                    ? collector.Str("trading_name")
                    : "";

            var row = report.AddRow(
                ("PROTOCOLO", protocols),
                ("CLIENTE", customer.Str("trading_firstname")),
                ("VALOR ORÇAMENTO", totalPrice),
                ("VALORES ADICIONAIS", totalAdditional),
                ("VALOR TOTAL", total),
                ("NOME REMETENTE", ServiceText.Names(first.Source)),
                ("CIDADE ORIGEM", Cities(first.Source)),
                ("ENDEREÇO REMETENTE", ServiceText.Meta(first.Source, "cityIDAddress.name", "state")),
                ("CNPJ/CPF REMETENTE", ServiceText.CnpjsListed(first.Source)),
                ("COLETADOR ORIGEM", CollectorName("source_collector_id")),
                ("NOME DESTINATÁRIO", ServiceText.Names(first.Destination)),
                ("CIDADE DESTINO", Cities(first.Destination)),
                ("ENDEREÇO DESTINATÁRIO", ServiceText.Meta(first.Destination, "cityIDAddress.name", "state")),
                ("CNPJ/CPF DESTINATÁRIO", ServiceText.CnpjsListed(first.Destination)),
                ("COLETADOR DESTINO", CollectorName("destination_collector_id")));

            var customerCnpj = customer.Str("cnpj_cpf");
            var senders = ServiceText.Cnpjs(first.Source);
            var receivers = ServiceText.Cnpjs(first.Destination);
            var payer = senders.Contains(customerCnpj) ? Payer.Sender
                : receivers.Contains(customerCnpj) ? Payer.Receiver
                : Payer.Other;

            var icms = IcmsCalculator.Compute(tax, first.OriginUf, first.DestinationUf, $"Protocolos {protocols}",
                total, strict: true);

            jobs.Add(new EmissionJob
            {
                Protocol = protocols,
                Title = $"{icms.OriginUf} → {icms.DestinationUf} · {customer.Str("trading_firstname")}",
                Subtitle = $"{group.Count()} protocolo(s) · R$ {PyFormat.Fixed2(total)}",
                // Every address is offered to Bsoft here, as this flow always did.
                Parties = new PartiesInput(senders, receivers, payer, customerCnpj),
                Tax = new TaxInput(PyFormat.Float(total), icms.TaxText, icms.TpInfo, icms.OriginUf,
                    // The protocol list has always been written twice in this flow's observation.
                    $"Protocolos {protocols} - {icms.Observation}"),
                Plan = new PostPlan
                {
                    Kind = CteKind.LogLife,
                    PostJson = false,
                    UploadCsvPath = uploadCsv,
                    UploadCsvRequired = true,
                    KeepUploadCsv = true,
                    AssociateCsvPath = associateCsv,
                    PdfFolder = pdfFolder,
                    UploadType = LogLifeApi.UploadTypeLogLife,
                    CsvProtocol = protocols,
                },
                Row = row,
            });
        }

        report.Save(excelFile);
        ctx.Log.Info($"Relatório exportado: {excelFile}");

        var outcome = await _runner.RunAsync("CT-e por UF", jobs, (job, number) =>
        {
            report.Set(job.Row, CteColumn, number);
            ctx.SaveReport(report, excelFile);
        }, ct);

        return outcome.Message("Emissões realizadas com sucesso!");
    }

    /// <summary>The distinct cities of the addresses, sorted, as "A, B".</summary>
    private static string Cities(IEnumerable<JsonElement> addresses) => string.Join(", ",
        addresses.Select(a => a.Get("cityIDAddress").Str("name")).Distinct().OrderBy(c => c, StringComparer.Ordinal));

    private static bool InCapital(string state, string cities) =>
        cities.Trim().ToLowerInvariant().Contains(BrazilStates.Capital(state).Trim().ToLowerInvariant());
}
