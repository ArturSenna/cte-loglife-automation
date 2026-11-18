using System.Globalization;
using System.Text.Json;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Journal;

namespace CteLogLife.Core.Flows;

/// <summary>A CT-e to cancel in Bsoft and, when the protocol is known, to clear in the LogLife system.</summary>
public sealed record CancelTarget(string Number, string? Protocol = null, CteKind? Kind = null);

public sealed record CancelResult(CancelTarget Target, bool Cancelled, string? Error, bool? ApiUpdated);

/// <summary>Cancellation, clean-up and the ICMS slip (GNRE) utilities.</summary>
public sealed partial class MaintenanceFlows(FlowContext ctx)
{
    // ------------------------------------------------------------------ cancellation

    /// <summary>
    /// Cancels the CT-es one by one. A failed cancellation is recorded and the list goes on; a stop
    /// request ends it after the CT-e in progress.
    /// </summary>
    public async Task<(List<CancelResult> Results, bool Stopped)> CancelManyAsync(string title,
        IReadOnlyList<CancelTarget> targets, CancellationToken ct)
    {
        var byId = new Dictionary<string, CancelTarget>();
        var items = new List<QueueItem>();
        foreach (var target in targets)
        {
            var id = Guid.NewGuid().ToString("N");
            byId[id] = target;
            var kind = target.Kind switch
            {
                CteKind.LogLife => "Loglife",
                CteKind.Complementary => "Complementar",
                _ => "",
            };
            items.Add(new QueueItem(id, $"CT-e {target.Number}",
                string.Join(" · ", new[] { kind, target.Protocol is null ? "" : $"protocolo {target.Protocol}" }
                    .Where(part => part.Length > 0))));
        }
        ctx.Queue.Load(title, items);

        var results = new List<CancelResult>();
        IBsoftDriver? driver = null;
        var stopped = false;
        while (true)
        {
            QueueItem? item = null;
            try
            {
                ctx.Control.Checkpoint();
                ct.ThrowIfCancellationRequested();
                item = ctx.Queue.TakeNext();
                if (item is null) break;
                var target = byId[item.Id];

                driver ??= ctx.CreateDriver();
                ctx.Log.Info($"Cancelando CT-e {target.Number}");
                ctx.Queue.Step = "Cancelando no Bsoft";
                var cancelledNow = driver.CancelCte(target.Number, ctx.Control.BeginCritical);
                ctx.Log.Info(cancelledNow
                    ? $"CT-e {target.Number} cancelado."
                    : $"CT-e {target.Number} já estava cancelado.");

                bool? apiUpdated = null;
                if (target is { Protocol: { } protocol, Kind: { } kind })
                {
                    ctx.Queue.Step = "Atualizando o sistema";
                    try
                    {
                        if (kind == CteKind.Complementary)
                            await ctx.Api.PostCteComplementaryAsync(protocol, null, null, ct);
                        else
                            await ctx.Api.PostCteLoglifeAsync(protocol, null, null, symbolic: false, ct);
                        apiUpdated = true;
                    }
                    catch (ApiException ex)
                    {
                        apiUpdated = false;
                        ctx.Log.Error($"Protocolo {protocol}: CT-e cancelado, mas o sistema não foi atualizado: {ex.Message}");
                    }
                }
                results.Add(new CancelResult(target, true, null, apiUpdated));
                ctx.Queue.Finish(item.Id, QueueItemState.Done, apiUpdated == false ? "sistema não atualizado" : "cancelado");
            }
            catch (RunStoppedException)
            {
                if (item is not null) ctx.Queue.Finish(item.Id, QueueItemState.Skipped, "interrompido");
                ctx.Queue.SkipPending("interrompido");
                stopped = true;
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && item is not null)
            {
                ctx.Log.Error($"Erro ao cancelar CT-e {byId[item.Id].Number}: {ex.Message}");
                results.Add(new CancelResult(byId[item.Id], false, ex.Message, null));
                ctx.Queue.Finish(item.Id, QueueItemState.Failed, ex.Message);
            }
            finally
            {
                ctx.Control.EndCritical();
            }
        }
        return (results, stopped);
    }

    /// <summary>Cancels every CT-e of the services cancelled or unsuccessful in the period.</summary>
    public async Task<string> CancelBatchAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        ctx.Log.Info($"Consultando cancelamentos entre {start:yyyy-MM-dd} e {end:yyyy-MM-dd}");
        JsonElement root;
        try
        {
            root = await ctx.Api.GetCancelledUnsuccessfulAsync(start, end, ct);
        }
        catch (ApiException ex)
        {
            return $"❌ Erro ao consultar API: {ex.Message}";
        }
        if (root.Get("services") is not { ValueKind: JsonValueKind.Array } list)
            return "⚠️ Estrutura de retorno da API inesperada. Nenhum CT-e extraído.";

        var targets = new List<CancelTarget>();
        foreach (var kind in new[] { CteKind.LogLife, CteKind.Complementary })
        {
            var field = kind == CteKind.LogLife ? "cte_loglife" : "cte_complementary";
            foreach (var service in list.EnumerateArray())
            {
                if (!service.Get(field).Truthy()) continue;
                var number = service.Str(field).Trim();
                if (number.Length > 0 && number.All(char.IsAsciiDigit))
                    targets.Add(new CancelTarget(number, service.Str("protocol"), kind));
            }
        }
        if (targets.Count == 0) return "⚠️ Nenhum CT-e válido encontrado para cancelamento.";

        var (results, stopped) = await CancelManyAsync("Cancelamento em lote", targets, ct);

        string Section(string label, CteKind kind)
        {
            var mine = results.Where(r => r.Target.Kind == kind).ToList();
            return $"{label}:\n"
                   + $"  Cancelados: {mine.Count(r => r.Cancelled)}\n"
                   + $"  Erros: {mine.Count(r => !r.Cancelled)}\n"
                   + $"  API: {mine.Count(r => r.ApiUpdated == true)} OK, {mine.Count(r => r.ApiUpdated == false)} erro\n\n";
        }

        return (stopped ? "Cancelamento interrompido pelo operador.\n\n" : "Cancelamento concluído!\n\n")
               + Section("CT-es Loglife", CteKind.LogLife)
               + Section("CT-es Complementares", CteKind.Complementary)
               + $"Total: {results.Count(r => r.Cancelled)} cancelados, {results.Count(r => !r.Cancelled)} erros";
    }

    /// <summary>Cancels one CT-e and clears it from its protocol.</summary>
    public async Task<string> CancelSingleAsync(string cteNumber, string protocol, CancellationToken ct,
        CteKind kind = CteKind.LogLife)
    {
        if (string.IsNullOrWhiteSpace(cteNumber) || string.IsNullOrWhiteSpace(protocol))
            return "⚠️ CT-e e Protocolo são obrigatórios.";

        var (results, stopped) = await CancelManyAsync("Cancelamento avulso",
            [new CancelTarget(cteNumber.Trim(), protocol.Trim(), kind)], ct);
        if (stopped || results.Count == 0) return "Cancelamento interrompido pelo operador.";

        var result = results[0];
        var label = kind == CteKind.Complementary ? "Complementar" : "Loglife";
        if (!result.Cancelled) return $"❌ Erro no cancelamento: {result.Error}";
        return result.ApiUpdated == false
            ? $"❌ CT-e {label} {cteNumber} cancelado no Bsoft, mas o sistema LogLife não foi atualizado. Veja o registro de atividade."
            : $"✅ CT-e {label} {cteNumber} cancelado com sucesso!";
    }

    /// <summary>Cancels the CT-es picked in the payer conference.</summary>
    public async Task<string> CancelSelectedAsync(IReadOnlyList<string> cteNumbers, CancellationToken ct)
    {
        var (results, stopped) = await CancelManyAsync("Cancelamento da conferência",
            cteNumbers.Distinct().Select(n => new CancelTarget(n)).ToList(), ct);
        var errors = results.Where(r => !r.Cancelled).ToList();
        var message = (stopped ? "Cancelamento interrompido pelo operador.\n\n" : "Cancelamento concluído!\n\n")
                      + $"CT-es cancelados: {results.Count(r => r.Cancelled)}\nErros: {errors.Count}";
        if (errors.Count > 0)
            message += "\n\nPrimeiros erros:\n" +
                       string.Join("\n", errors.Take(5).Select(e => $"- {e.Target.Number}: {e.Error}"));
        return message;
    }

    /// <summary>
    /// Compares the CT-es of the Bsoft report with the ones the LogLife system knows (last 90 days) and
    /// cancels the ones that exist only in Bsoft.
    /// </summary>
    public async Task<string> ValidateAndCancelAsync(CancellationToken ct)
    {
        var reportPath = ctx.Settings.BsoftReportPath;
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
            return "❌ Por favor, selecione o arquivo Relatório B-soft.";

        ctx.Log.Info($"Lendo arquivo: {reportPath}");
        var sheet = SheetTable.Read(reportPath);
        var cteColumn = sheet.IndexOf("Nº CT-e");
        if (cteColumn < 0) cteColumn = sheet.IndexOf("Número");
        if (cteColumn < 0)
            return "❌ Coluna 'Nº CT-e' ou 'Número' não encontrada no relatório.\n"
                   + $"Colunas disponíveis: {string.Join(", ", sheet.Columns)}";
        var totalColumn = sheet.IndexOf("Total");

        var inBsoft = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in sheet.Rows)
        {
            // Symbolic CT-es (total of 5) are not checked.
            if (totalColumn >= 0 && SheetTable.Number(row[totalColumn]) == 5m) continue;
            if (SheetTable.IsBlank(row[cteColumn])) continue;
            if (SheetTable.Number(row[cteColumn]) is { } asNumber)
            {
                inBsoft.Add(((long)asNumber).ToString(CultureInfo.InvariantCulture));
                continue;
            }
            var text = SheetTable.Text(row[cteColumn]);
            if (text.Length > 0 && text.ToLowerInvariant() is not ("nan" or "-")) inBsoft.Add(text);
        }
        ctx.Log.Info($"{inBsoft.Count} CT-es encontrados no Relatório B-soft");

        var today = DateOnly.FromDateTime(ctx.Now());
        JsonElement root;
        try
        {
            root = await ctx.Api.GetServicesWithCteAsync(today.AddDays(-90), today, ct);
        }
        catch (ApiException ex)
        {
            return $"❌ Erro na validação: {ex.Message}";
        }
        if (root.Get("services") is not { ValueKind: JsonValueKind.Array } list)
            return "⚠️ Estrutura de retorno da API inesperada. Nenhum CT-e extraído.";

        var loglife = new HashSet<string>(StringComparer.Ordinal);
        var complementary = new HashSet<string>(StringComparer.Ordinal);
        foreach (var service in list.EnumerateArray())
        {
            Collect(service, "cte_loglife", loglife);
            Collect(service, "cte_complementary", complementary);
        }
        var inApi = loglife.Union(complementary).ToHashSet(StringComparer.Ordinal);

        var toCancel = inBsoft.Except(inApi).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (toCancel.Count == 0) return "✅ Todos os CT-es estão sincronizados!\nNenhum cancelamento necessário.";
        ctx.Log.Warn($"{toCancel.Count} CT-es do B-soft não estão no sistema: {string.Join(", ", toCancel)}");

        var numeric = toCancel.Where(n => n.All(char.IsAsciiDigit)).ToList();
        var (results, stopped) = await CancelManyAsync("Validar e cancelar (B-soft)",
            numeric.Select(n => new CancelTarget(n)).ToList(), ct);

        var cancelled = results.Count(r => r.Cancelled);
        var errors = results.Count(r => !r.Cancelled) + (toCancel.Count - numeric.Count);
        return (stopped ? "Validação interrompida pelo operador.\n\n" : "Validação concluída!\n\n")
               + $"CT-es no B-soft: {inBsoft.Count}\n"
               + "CT-es na API:\n"
               + $"  Loglife: {loglife.Count}\n"
               + $"  Complementares: {complementary.Count}\n"
               + $"  Total: {inApi.Count}\n\n"
               + $"Cancelados: {cancelled}\n"
               + $"Erros: {errors}";

        static void Collect(JsonElement service, string field, HashSet<string> into)
        {
            if (!service.Get(field).Truthy()) return;
            var number = service.Str(field).Trim();
            if (number is not ("-" or "" or "nan")) into.Add(number);
        }
    }

    // ------------------------------------------------------------------ symbolic clean-up

    /// <summary>
    /// Removes the CT-e number from the services of the period whose customer is not invoiced by CT-e
    /// (the symbolic ones), so they can be emitted again.
    /// </summary>
    public async Task<string> ClearSymbolicAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var folder = FlowContext.RequireFolder(ctx.Settings.NormalFolder, "CTe normal");
        var csvPath = Path.Combine(folder, "LimparCTE.csv");

        var ongoing = (await ctx.Api.GetOngoingServicesAsync(ct)).Items();
        var finalized = (await ctx.Api.GetFinalizedServicesAsync(start.AddDays(-5), end.AddDays(5), ct)).Items();

        var windowStart = start.ToDateTime(TimeOnly.MinValue);
        var windowEnd = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var protocols = new List<string>();
        foreach (var service in ongoing.Concat(finalized))
        {
            if (service.Get("is_business") is not { ValueKind: JsonValueKind.False }) continue;
            if (ServiceText.CollectMoment(service.Get("serviceIDRequested").Get("collect_date")) is { } collect &&
                (collect < windowStart || collect > windowEnd)) continue;
            if (service.Get("customerIDService").Str("emission_type") is "CTE" or "AMBOS") continue;
            if (service.Get("cte_loglife") is not { } cte || PyFormat.Json(cte) is "nan" or "-") continue;
            protocols.Add(service.Str("protocol"));
        }

        var failures = 0;
        foreach (var protocol in protocols.OrderBy(p => long.TryParse(p, out var n) ? n : long.MaxValue))
        {
            ct.ThrowIfCancellationRequested();
            ctx.Log.Info($"Limpando CT-e do protocolo {protocol}");
            CsvFile.Write(csvPath, ["Protocolo", "CTE Loglife", "Data Emissão CTE"], [protocol, "-", "-"]);
            try
            {
                await ctx.Api.UploadCteLoglifeCsvAsync(csvPath, ct);
            }
            catch (ApiException ex)
            {
                failures++;
                ctx.Log.Error($"Protocolo {protocol}: não foi possível limpar o CT-e: {ex.Message}");
            }
            finally
            {
                File.Delete(csvPath);
            }
        }

        return "Numeração dos CTes simbólicos removidas do sistema."
               + (failures > 0 ? $"\n\n{failures} de {protocols.Count} protocolo(s) falharam. Veja o registro de atividade." : "");
    }
}
