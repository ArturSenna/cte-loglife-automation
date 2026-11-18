using System.Globalization;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Journal;

namespace CteLogLife.Core.Flows;

/// <summary>
/// Emits a list of CT-es one at a time: fills Bsoft, transmits, records the number in the LogLife system
/// and uploads the PDF. Every stage is journaled first, pause and reordering take effect between
/// CT-es, and nothing is typed blind: a missing screen element stops the run.
/// </summary>
public sealed class EmissionRunner(FlowContext ctx)
{
    private readonly PostEmission _post = new(ctx);

    /// <param name="onReported">Called once a CT-e number is recorded, to update the conference spreadsheet.</param>
    /// <param name="include">
    /// Fetches and builds the job of one more protocol, asked for by the operator during a pause. It
    /// throws a <see cref="FlowException"/> with the reason when the protocol cannot be emitted here.
    /// </param>
    public async Task<RunOutcome> RunAsync(string title, IReadOnlyList<EmissionJob> jobs,
        Action<EmissionJob, long>? onReported, CancellationToken ct,
        Func<string, CancellationToken, Task<EmissionJob>>? include = null)
    {
        // The job table is shared with the include requests, which arrive on another thread.
        var sync = new object();
        var byId = jobs.ToDictionary(job => job.Id);
        var closed = false;
        var included = 0;
        int Total()
        {
            lock (sync) return byId.Count;
        }

        ctx.Queue.Load(title, jobs.Select(job => new QueueItem(job.Id, job.Title, job.Subtitle)));
        if (include is not null)
            ctx.Queue.AllowInclude("protocolo", async (text, token) =>
            {
                var protocol = text.Trim();
                if (protocol.Length == 0) return new IncludeResult(false, "Digite o protocolo.");
                if (AlreadyQueued(protocol) is { } known) return known;

                EmissionJob job;
                try
                {
                    job = await include(protocol, token);
                }
                catch (Exception ex) when (ex is FlowException or ApiException)
                {
                    return new IncludeResult(false, ex.Message);
                }

                lock (sync)
                {
                    if (closed)
                        return new IncludeResult(false,
                            $"A execução terminou antes de o protocolo {protocol} entrar na fila. Emita-o pela emissão avulsa.");
                    if (AlreadyQueued(protocol) is { } raced) return raced;
                    byId[job.Id] = job;
                    included++;
                    ctx.Queue.Add(new QueueItem(job.Id, job.Title, job.Subtitle), next: true);
                }
                ctx.Log.Info($"Protocolo {job.Protocol} incluído na fila pelo operador.");
                return new IncludeResult(true, $"Protocolo {job.Protocol} incluído: é o próximo da fila.");
            });

        // A protocol that is already part of this run is not fetched again: when it is still waiting it
        // just becomes the next one.
        IncludeResult? AlreadyQueued(string protocol)
        {
            lock (sync)
            {
                if (closed) return new IncludeResult(false, "A execução já terminou.");
                var existing = byId.Values.FirstOrDefault(job => job.Protocol == protocol);
                if (existing is null) return null;
                var item = ctx.Queue.Snapshot().FirstOrDefault(i => i.Id == existing.Id);
                switch (item?.State)
                {
                    case QueueItemState.Pending:
                        ctx.Queue.MoveToTop(existing.Id);
                        return new IncludeResult(true, $"O protocolo {protocol} já estava na fila: agora é o próximo.");
                    case QueueItemState.Running:
                        return new IncludeResult(false, $"O protocolo {protocol} está sendo emitido agora.");
                    default:
                        return new IncludeResult(false,
                            $"O protocolo {protocol} já passou por esta execução ({item?.Result ?? "sem resultado"}).");
                }
            }
        }

        IBsoftDriver? driver = null;
        int emitted = 0, skipped = 0, invalid = 0;
        try
        {
            while (true)
            {
                ctx.Control.Checkpoint();
                ct.ThrowIfCancellationRequested();
                QueueItem? item;
                EmissionJob job;
                lock (sync)
                {
                    // Taking the next item and closing the run are one step, so an include can never
                    // land in a queue nobody is reading any more.
                    item = ctx.Queue.TakeNext();
                    if (item is null)
                    {
                        closed = true;
                        break;
                    }
                    job = byId[item.Id];
                }

                if (ctx.Journal.FindOpen(job.Protocol, job.Plan.Kind) is { } open)
                {
                    skipped++;
                    var reason = open.CteNumber is { } pending
                        ? $"o CT-e {pending} já foi emitido e ainda não está registrado no sistema"
                        : "uma emissão anterior pode ter sido autorizada e não foi confirmada";
                    ctx.Operator.Notice($"Protocolo {job.Protocol} não foi emitido: {reason}. Resolva em Pendências.");
                    ctx.Queue.Finish(item.Id, QueueItemState.Skipped, "pendência");
                    continue;
                }

                if (Validate(job) is { } problem)
                {
                    invalid++;
                    ctx.Operator.Notice($"Protocolo {job.Protocol} não foi emitido: {problem}.");
                    ctx.Queue.Finish(item.Id, QueueItemState.Failed, problem);
                    continue;
                }

                driver ??= ctx.CreateDriver();
                try
                {
                    ctx.Log.Info($"Emitindo {job.Title}");
                    var number = await EmitAsync(driver, job, onReported, ct);
                    emitted++;
                    ctx.Queue.Finish(item.Id, QueueItemState.Done, $"CT-e {number}");
                    ctx.Log.Info($"Protocolo {job.Protocol}: CT-e {number} emitido e registrado.");
                }
                catch (RunStoppedException)
                {
                    ctx.Queue.Finish(item.Id, QueueItemState.Skipped, "interrompido");
                    throw;
                }
                catch (Exception ex)
                {
                    ctx.Queue.Finish(item.Id, QueueItemState.Failed, ex.Message);
                    throw;
                }
            }
            return new RunOutcome(emitted, Total(), skipped, invalid, Stopped: false, included);
        }
        catch (RunStoppedException)
        {
            Close();
            ctx.Queue.SkipPending("interrompido");
            ctx.Log.Warn("Execução interrompida pelo operador.");
            return new RunOutcome(emitted, Total(), skipped, invalid, Stopped: true, included);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Close();
            ctx.Queue.SkipPending("execução encerrada");
            ctx.Log.Error(ex.Message);
            throw new FlowException($"{ex.Message}\n\nEmitidos antes do erro: {emitted} de {Total()}.", ex);
        }
        finally
        {
            Close();
            ctx.Control.EndCritical();
        }

        void Close()
        {
            lock (sync) closed = true;
            ctx.Queue.DisallowInclude();
        }
    }

    private async Task<long> EmitAsync(IBsoftDriver driver, EmissionJob job, Action<EmissionJob, long>? onReported,
        CancellationToken ct)
    {
        var entry = ctx.Journal.Append(new JournalEntry
        {
            EmissionId = job.Id,
            Stage = EmissionStage.Started,
            Protocol = job.Protocol,
            Title = job.Title,
            Plan = job.Plan,
            EmissionDate = ctx.Today(),
        });

        var transmitted = false;
        try
        {
            long? number;
            try
            {
                if (job.OriginalCte is { } original)
                {
                    ctx.Queue.Step = "CT-e original";
                    driver.SelectOriginalCte(original);
                }
                else
                {
                    ctx.Queue.Step = "Remetente, destinatário e tomador";
                    driver.FillParties(job.Parties ?? throw new InvalidOperationException("Emissão sem remetente."));
                    ctx.Queue.Step = "Carga";
                    driver.FillCargo(job.Cargo);
                }

                ctx.Queue.Step = "Valores e impostos";
                number = driver.FillTaxesAndTransmit(job.Tax, () =>
                {
                    ctx.Control.BeginCritical();
                    transmitted = true;
                    entry = ctx.Journal.Append(entry with { Stage = EmissionStage.Transmitting });
                    ctx.Queue.Step = "Transmitindo";
                });
            }
            catch (Exception ex) when (!transmitted)
            {
                // Nothing was sent: the protocol can simply be emitted again later.
                ctx.Journal.Append(entry with { Stage = EmissionStage.Aborted, Note = ex.Message });
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Log.Error($"Protocolo {job.Protocol}: falha após o envio. {ex.Message}");
                number = AskNumber(driver.LastNumberHint,
                    $"A emissão do protocolo {job.Protocol} não pôde ser acompanhada até o fim:\n{ex.Message}\n\n"
                    + "Confira no Bsoft. Se o CT-e foi autorizado, digite o número do campo \"Nº CT-e\" para "
                    + "continuar. Se não foi, cancele esta janela.");
                if (number is null)
                {
                    ctx.Journal.Append(entry with { Stage = EmissionStage.Uncertain, Note = ex.Message });
                    throw new FlowException(
                        $"Protocolo {job.Protocol}: a emissão ficou sem confirmação e foi registrada em Pendências. "
                        + "Confira no Bsoft antes de emitir novamente.", ex);
                }
            }

            if (number is null)
            {
                ctx.Log.Warn($"Número do CT-e não foi lido do Bsoft (protocolo {job.Protocol}). Solicitando ao operador.");
                number = AskNumber(driver.LastNumberHint,
                    $"Não foi possível copiar o número do CT-e que acabou de ser emitido (protocolo {job.Protocol}).\n\n"
                    + "O CT-e já foi autorizado. Digite o número que aparece no campo \"Nº CT-e\" do Bsoft "
                    + "para continuar a emissão:");
                if (number is null)
                {
                    ctx.Journal.Append(entry with
                    {
                        Stage = EmissionStage.Uncertain,
                        Note = "CT-e autorizado, número não informado.",
                    });
                    throw new FlowException(
                        $"CT-e emitido para o protocolo {job.Protocol}, mas o número não foi copiado nem informado. "
                        + "Informe o número em Pendências antes de emitir novamente.");
                }
            }

            entry = ctx.Journal.Append(entry with { Stage = EmissionStage.NumberCaptured, CteNumber = number });
            try
            {
                await _post.RunAsync(entry, () => onReported?.Invoke(job, number.Value), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new FlowException(
                    $"CT-e {number} emitido para o protocolo {job.Protocol}, mas o registro no sistema não terminou: "
                    + $"{ex.Message}\nEle está em Pendências para ser reenviado.", ex);
            }
            return number.Value;
        }
        finally
        {
            ctx.Control.EndCritical();
        }
    }

    /// <summary>What is missing for this job to be typed into Bsoft, or null when it is complete.</summary>
    private static string? Validate(EmissionJob job)
    {
        if (job.OriginalCte is not null) return null;
        if (job.Parties is not { } parties) return "sem remetente e destinatário";
        if (parties.SenderCnpjs.Count == 0 || parties.SenderCnpjs.Any(string.IsNullOrWhiteSpace))
            return "CNPJ do remetente ausente";
        if (parties.ReceiverCnpjs.Count == 0 || parties.ReceiverCnpjs.Any(string.IsNullOrWhiteSpace))
            return "CNPJ do destinatário ausente";
        if (parties.Payer == Payer.Other && string.IsNullOrWhiteSpace(parties.PayerCnpj))
            return "CNPJ do tomador ausente";
        if (!int.TryParse(job.Cargo.Volumes, NumberStyles.None, CultureInfo.InvariantCulture, out var volumes) ||
            volumes <= 0)
            return "quantidade de volumes inválida";
        return null;
    }

    private long? AskNumber(string? hint, string message)
    {
        var prompt = message;
        while (true)
        {
            var typed = ctx.Operator.AskText("Número do CT-e", prompt, hint);
            if (typed is null) return null;
            var digits = typed.Trim();
            if (digits.Length > 0 && digits.All(c => c is >= '0' and <= '9') &&
                long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return number;
            prompt = $"\"{typed}\" não é um número válido. Digite apenas os dígitos.\n\n{message}";
        }
    }
}

/// <summary>
/// The steps after a CT-e is authorized: record the number, upload the PDF, link it to the protocol.
/// Each one is journaled, so a failed run can be finished later from where it stopped.
/// </summary>
public sealed class PostEmission(FlowContext ctx)
{
    /// <param name="entry">An emission with a known number at <see cref="EmissionStage.NumberCaptured"/> or later.</param>
    public async Task<JournalEntry> RunAsync(JournalEntry entry, Action? afterReported, CancellationToken ct)
    {
        var plan = entry.Plan;
        var number = entry.CteNumber ?? throw new InvalidOperationException("Emissão sem número de CT-e.");
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        var pdfName = numberText.PadLeft(8, '0') + ".pdf";

        if (entry.Stage is EmissionStage.NumberCaptured or EmissionStage.Transmitting or EmissionStage.Uncertain)
        {
            ctx.Queue.Step = "Registrando no sistema";
            if (plan.PostJson)
            {
                if (plan.Kind == CteKind.Complementary)
                    await ctx.Api.PostCteComplementaryAsync(entry.Protocol, number, entry.EmissionDate, ct);
                else
                    await ctx.Api.PostCteLoglifeAsync(entry.Protocol, number, entry.EmissionDate, plan.Symbolic, ct);
            }

            if (plan.UploadCsvPath is { } uploadCsv)
            {
                CsvFile.Write(uploadCsv, ["Protocolo", "CTE Loglife", "Data Emissão CTE"],
                    [plan.CsvProtocol, numberText, entry.EmissionDate ?? ""]);
                try
                {
                    await ctx.Api.UploadCteLoglifeCsvAsync(uploadCsv, ct);
                }
                catch (ApiException ex) when (!plan.UploadCsvRequired)
                {
                    ctx.Log.Warn($"Upload do CSV do protocolo {entry.Protocol} recusado ({ex.Message}); o número já foi registrado.");
                }
                if (!plan.KeepUploadCsv) TryDelete(uploadCsv);
            }

            entry = ctx.Journal.Append(entry with { Stage = EmissionStage.Reported });
            afterReported?.Invoke();
        }

        if (entry.Stage == EmissionStage.Reported)
        {
            var pdfPath = Path.Combine(plan.PdfFolder, pdfName);
            await WaitForFileAsync(pdfPath, ct);
            ctx.Queue.Step = "Enviando PDF";
            await ctx.Api.UploadPdfAsync(pdfPath, plan.UploadType, ct);
            entry = ctx.Journal.Append(entry with { Stage = EmissionStage.PdfUploaded });
        }

        if (entry.Stage == EmissionStage.PdfUploaded)
        {
            ctx.Queue.Step = "Associando PDF";
            CsvFile.Write(plan.AssociateCsvPath, ["Protocolo", "Arquivo PDF"], [plan.CsvProtocol, pdfName]);
            await ctx.Api.AssociatePdfAsync(plan.AssociateCsvPath, plan.UploadType, ct);
            TryDelete(plan.AssociateCsvPath);
            entry = ctx.Journal.Append(entry with { Stage = EmissionStage.Completed });
        }

        return entry;
    }

    /// <summary>Waits until Bsoft has finished writing the PDF: present, not growing and readable.</summary>
    private async Task WaitForFileAsync(string path, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, ctx.Settings.PdfWaitSeconds));
        long lastSize = -1;
        ctx.Queue.Step = $"Aguardando {Path.GetFileName(path)}";
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                try
                {
                    long size;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        size = stream.Length;
                    if (size > 0 && size == lastSize) return;
                    lastSize = size;
                }
                catch (IOException)
                {
                    // Still being written.
                }
            }
            // A stop request only cuts the wait short while the file has not shown up at all.
            if (lastSize < 0 && ctx.Control.IsStopRequested)
                throw new FlowException($"Interrompido enquanto aguardava o arquivo {Path.GetFileName(path)}.");
            if (DateTime.UtcNow >= deadline)
                throw new FlowException(
                    $"O arquivo {Path.GetFileName(path)} não apareceu em {Path.GetDirectoryName(path)} "
                    + $"após {ctx.Settings.PdfWaitSeconds} s.");
            await Task.Delay(ctx.PdfPollInterval, ct);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ctx.Log.Warn($"Não foi possível remover {Path.GetFileName(path)}: {ex.Message}");
        }
    }
}
