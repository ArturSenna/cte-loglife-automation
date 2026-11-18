using System.Globalization;
using System.Text.RegularExpressions;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;

namespace CteLogLife.Core.Flows;

/// <summary>A CT-e whose payer in the spreadsheet differs from the customer registered in the system.</summary>
public sealed record PayerInconsistency(string CteNumber, int RowNumber, string PayerName, string FileValue,
    string ApiValue, string Reason);

public sealed record PayerConferenceResult(IReadOnlyList<PayerInconsistency> Inconsistencies, int CheckedRows,
    int SkippedRows, int NotFoundRows, int ValueFiveRows, int TotalRows);

public sealed partial class MaintenanceFlows
{
    // ------------------------------------------------------------------ GNRE comparison

    /// <summary>
    /// Compares the GNRE each CT-e of the Bsoft report should have with what the Target report charged,
    /// and writes a workbook with a summary by state and the detail by CT-e.
    /// </summary>
    public Task<string> CompareGnreAsync(CancellationToken ct)
    {
        var bsoftPath = ctx.Settings.BsoftReportPath;
        var targetPath = ctx.Settings.TargetReportPath;
        if (string.IsNullOrWhiteSpace(bsoftPath) || !File.Exists(bsoftPath))
            return Task.FromResult("❌ Por favor, selecione o arquivo Relatório B-soft.");
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
            return Task.FromResult("❌ Por favor, selecione o arquivo Relatório Target.");

        var tax = ctx.Tax();
        var bsoft = SheetTable.Read(bsoftPath);
        var target = SheetTable.Read(targetPath);

        var cteColumn = bsoft.IndexOf("Nº CT-e");
        if (cteColumn < 0) cteColumn = bsoft.IndexOf("Número");
        if (cteColumn < 0)
            return Task.FromResult("❌ Coluna 'Nº CT-e' ou 'Número' não encontrada no B-Soft.\n"
                                   + $"Colunas disponíveis: {string.Join(", ", bsoft.Columns)}");

        string[] bsoftRequired = ["Data", "Remetente - UF", "Destinatário - UF", "Total"];
        var missingBsoft = bsoftRequired.Where(c => bsoft.IndexOf(c) < 0).ToList();
        if (missingBsoft.Count > 0)
            return Task.FromResult($"❌ Colunas faltando no B-Soft: {string.Join(", ", missingBsoft)}");

        string[] targetRequired = ["Chave Documento", "Valor do GNRE", "UF Favorecida", "Linha Digitável"];
        var missingTarget = targetRequired.Where(c => target.IndexOf(c) < 0).ToList();
        if (missingTarget.Count > 0)
            return Task.FromResult($"❌ Colunas faltando no Target: {string.Join(", ", missingTarget)}");

        int dateColumn = bsoft.IndexOf("Data"), originColumn = bsoft.IndexOf("Remetente - UF"),
            destinationColumn = bsoft.IndexOf("Destinatário - UF"), totalColumn = bsoft.IndexOf("Total");
        int keyColumn = target.IndexOf("Chave Documento"), gnreColumn = target.IndexOf("Valor do GNRE"),
            favoredColumn = target.IndexOf("UF Favorecida"), barcodeColumn = target.IndexOf("Linha Digitável");

        // The CT-e number sits in characters 26-34 of the 44-digit access key.
        var targetByCte = target.Rows
            .Select(row =>
            {
                var key = SheetTable.Text(row[keyColumn]);
                var number = key.Length > 25 ? key[25..Math.Min(34, key.Length)].Trim() : "";
                if (number.Length > 0 && number.All(char.IsAsciiDigit))
                    number = long.Parse(number, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                return (Number: number, Row: row);
            })
            .ToLookup(pair => pair.Number, pair => pair.Row);

        var detail = new ReportTable();
        var byState = new SortedDictionary<string, (int Count, decimal Calculated, decimal Target, decimal Difference)>(
            StringComparer.Ordinal);
        var totalDifference = 0m;
        foreach (var row in bsoft.Rows)
        {
            ct.ThrowIfCancellationRequested();
            if (SheetTable.Number(row[cteColumn]) is not { } rawNumber) continue;
            var cteNumber = ((long)rawNumber).ToString(CultureInfo.InvariantCulture);

            foreach (var targetRow in targetByCte[cteNumber])
            {
                if (SheetTable.Number(row[totalColumn]) is not { } total ||
                    SheetTable.Number(targetRow[gnreColumn]) is not { } targetGnre)
                {
                    ctx.Log.Warn($"CT-e {cteNumber}: valores não numéricos, linha ignorada.");
                    continue;
                }

                var origin = SheetTable.Text(row[originColumn]);
                var destination = SheetTable.Text(row[destinationColumn]);
                var rate = 0m;
                if (tax.AliquotFor(origin, destination) is { } aliquot) rate = aliquot.Value * 0.8m;
                else ctx.Log.Warn($"Alíquota não encontrada para {origin} -> {destination} (CT-e {cteNumber})");

                var calculated = Round2(rate * total / 100m);
                var charged = Round2(targetGnre);
                var difference = Round2(rate * total / 100m - targetGnre);
                var favored = SheetTable.Text(targetRow[favoredColumn]);

                detail.AddRow(
                    ("CT-e", cteNumber),
                    ("Data", DateText(row[dateColumn])),
                    ("UF Origem", origin),
                    ("UF Destino", destination),
                    ("UF Favorecida", favored),
                    ("Total B-Soft", total),
                    ("Alíquota (%)", rate),
                    ("GNRE Calculado", calculated),
                    ("GNRE Target", charged),
                    ("Diferença", difference),
                    ("Linha Digitável", SheetTable.Text(targetRow[barcodeColumn])));

                totalDifference += difference;
                if (favored.Length > 0)
                {
                    var sum = byState.GetValueOrDefault(favored);
                    byState[favored] = (sum.Count + 1, sum.Calculated + calculated, sum.Target + charged,
                        sum.Difference + difference);
                }
            }
        }

        if (detail.RowCount == 0)
            return Task.FromResult("⚠️ Nenhum CT-e correspondente encontrado entre os relatórios.");

        var summary = new ReportTable();
        foreach (var (state, sum) in byState)
            summary.AddRow(
                ("UF Favorecida", state),
                ("Qtd CT-es", sum.Count),
                ("Total GNRE Calculado", Round2(sum.Calculated)),
                ("Total GNRE Target", Round2(sum.Target)),
                ("Total Diferença", Round2(sum.Difference)));

        var folder = Directory.Exists(ctx.Settings.NormalFolder)
            ? ctx.Settings.NormalFolder
            : Path.GetDirectoryName(bsoftPath) ?? ".";
        var outputFile = Path.Combine(folder,
            $"comparar-GNRE-{ctx.Now().ToString("dd-MM-yyyy-HH-mm", CultureInfo.InvariantCulture)}.xlsx");
        ReportTable.SaveWorkbook(outputFile, ("Resumo", summary), ("Detalhado", detail));
        ctx.Log.Info($"Relatório gerado: {outputFile}");

        return Task.FromResult(
            "Comparação GNRE concluída!\n\n"
            + $"CT-es processados: {detail.RowCount}\n"
            + $"Diferença total: R$ {totalDifference.ToString("N2", CultureInfo.InvariantCulture)}\n\n"
            + $"Relatório salvo em:\n{outputFile}");
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string DateText(object? cell)
    {
        if (cell is DateTime date) return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var text = SheetTable.Text(cell);
        return DateTime.TryParseExact(text, ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : text;
    }

    // ------------------------------------------------------------------ ICMS slips ("Lançar guias de ICMS")

    private sealed record SlipRow(int Line, string CteNumber, decimal Value, string Date, string Favored, string Barcode);

    /// <summary>
    /// Enters in Bsoft the ICMS slips listed on the "Detalhado" sheet of the comparison workbook
    /// (the file selected as "Relatório Target").
    /// </summary>
    public async Task<string> EnterIcmsSlipsAsync(CancellationToken ct)
    {
        var path = ctx.Settings.TargetReportPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "❌ Por favor, selecione o arquivo Relatório Target.";

        ctx.Log.Info($"Carregando relatório Target: {path}");
        SheetTable sheet;
        try
        {
            sheet = SheetTable.Read(path, "Detalhado");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return $"❌ Erro ao processar GNREs: {ex.Message}";
        }

        string[] required = ["GNRE Target", "CT-e", "Data", "UF Favorecida", "Linha Digitável"];
        var missing = required.Where(c => sheet.IndexOf(c) < 0).ToList();
        if (missing.Count > 0) return $"❌ Erro ao processar GNREs: colunas faltando: {string.Join(", ", missing)}";
        int valueColumn = sheet.IndexOf("GNRE Target"), cteColumn = sheet.IndexOf("CT-e"),
            dateColumn = sheet.IndexOf("Data"), favoredColumn = sheet.IndexOf("UF Favorecida"),
            barcodeColumn = sheet.IndexOf("Linha Digitável");

        var slips = new List<SlipRow>();
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            var row = sheet.Rows[i];
            if (SheetTable.Number(row[valueColumn]) is not { } value || value <= 0) continue;
            slips.Add(new SlipRow(i + 2, SheetTable.Text(row[cteColumn]), value, DateText(row[dateColumn]),
                SheetTable.Text(row[favoredColumn]).ToUpperInvariant(), SheetTable.Text(row[barcodeColumn])));
        }
        if (slips.Count == 0) return "⚠️ Nenhuma GNRE encontrada para processar.";
        ctx.Log.Info($"{slips.Count} GNREs para processar");

        var byId = slips.ToDictionary(_ => Guid.NewGuid().ToString("N"));
        ctx.Queue.Load("Guias de ICMS", byId.Select(pair => new QueueItem(pair.Key,
            $"CT-e {pair.Value.CteNumber}",
            $"{pair.Value.Favored} · R$ {PyFormat.Fixed2(pair.Value.Value)} · {pair.Value.Date}")));

        int processed = 0, errors = 0;
        var stopped = false;
        IBsoftDriver? driver = null;
        while (true)
        {
            QueueItem? item = null;
            try
            {
                ctx.Control.Checkpoint();
                ct.ThrowIfCancellationRequested();
                item = ctx.Queue.TakeNext();
                if (item is null) break;
                var slip = byId[item.Id];

                var supplier = BrazilStates.SupplierCode(slip.Favored)
                               ?? throw new FlowException($"UF Favorecida não encontrada: {slip.Favored}");

                ctx.Queue.Step = "Consultando centro de custo";
                var service = await ctx.Api.GetServiceByCteAsync(slip.CteNumber, ct)
                              ?? throw new FlowException($"Nenhum serviço encontrado para CT-e {slip.CteNumber}");
                var costCenter = service.Str("cost_center");
                if (costCenter.Length == 0)
                    throw new FlowException($"Cost center não encontrado para CT-e {slip.CteNumber}");

                driver ??= ctx.CreateDriver();
                ctx.Queue.Step = "Lançando no Bsoft";
                ctx.Log.Info($"Lançando guia do CT-e {slip.CteNumber} - Valor: R$ {PyFormat.Fixed2(slip.Value)}");
                driver.EnterIcmsSlip(new IcmsSlip(slip.CteNumber, PyFormat.Float(slip.Value), supplier, costCenter,
                    slip.Barcode, slip.Date));

                processed++;
                ctx.Queue.Finish(item.Id, QueueItemState.Done, "lançada");
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
                errors++;
                ctx.Log.Error($"Erro ao processar linha {byId[item.Id].Line}: {ex.Message}");
                ctx.Queue.Finish(item.Id, QueueItemState.Failed, ex.Message);
            }
        }

        return (stopped ? "Processamento interrompido pelo operador.\n\n" : "Processamento concluído!\n\n")
               + $"GNREs processadas: {processed}\nErros: {errors}";
    }

    // ------------------------------------------------------------------ payer conference

    /// <summary>
    /// Checks, for every CT-e of a Bsoft spreadsheet, that the freight payer is the customer the system
    /// has for that CT-e. Rows with a value of 5 (symbolic) are left out.
    /// </summary>
    public async Task<PayerConferenceResult> ConferPayersAsync(string filePath, string payerColumnName,
        CancellationToken ct)
    {
        var sheet = SheetTable.Read(filePath);
        var available = string.Join(", ", sheet.Columns);

        var cteColumn = sheet.FindFirst("Número", "Nº CT-e");
        if (cteColumn < 0)
            throw new FlowException($"Coluna 'Número' ou 'Nº CT-e' não encontrada. Colunas disponíveis: {available}");
        var payerColumn = sheet.Find(payerColumnName);
        if (payerColumn < 0)
            throw new FlowException($"Coluna '{payerColumnName}' não encontrada. Colunas disponíveis: {available}");
        var nameColumn = sheet.FindFirst("Pagador do Frete - Nome", "Pagador do Frete - Nome Fantasia");
        var valueColumns = new[] { sheet.Find("Total"), sheet.Find("Valor do Frete") }.Where(i => i >= 0).ToArray();

        var inconsistencies = new List<PayerInconsistency>();
        int checkedRows = 0, skipped = 0, notFound = 0, valueFive = 0;
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = sheet.Rows[i];
            var digits = PyFormat.OnlyDigits(SheetTable.Text(row[cteColumn]));
            if (digits.Length == 0)
            {
                skipped++;
                continue;
            }
            var cteNumber = digits.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";

            if (valueColumns.Any(c => IsValueFive(row[c])))
            {
                valueFive++;
                continue;
            }

            checkedRows++;
            var service = await ctx.Api.GetServiceByCteAsync(cteNumber, ct);
            if (service is null)
            {
                notFound++;
                continue;
            }

            var fileRaw = SheetTable.Text(row[payerColumn]);
            var apiRaw = service.Str("cnpj_cpf");
            var expectedLength = ExpectedDocumentLength(fileRaw, apiRaw);
            var fileDocument = NormalizeDocument(fileRaw, expectedLength);
            var apiDocument = NormalizeDocument(apiRaw, expectedLength);
            if (fileDocument.Length == 0 || apiDocument.Length == 0 || fileDocument != apiDocument)
            {
                inconsistencies.Add(new PayerInconsistency(cteNumber, i + 2,
                    nameColumn >= 0 ? SheetTable.Text(row[nameColumn]) : "",
                    FormatDocument(fileRaw, expectedLength), FormatDocument(apiRaw, expectedLength),
                    "CNPJ/CPF divergente"));
            }
        }

        return new PayerConferenceResult(inconsistencies, checkedRows, skipped, notFound, valueFive, sheet.Rows.Count);
    }

    internal static bool IsValueFive(object? cell) =>
        ParseCurrency(cell) is { } value && Math.Abs(value - 5m) < 0.0001m;

    /// <summary>A currency cell in Brazilian or plain notation ("R$ 1.234,56", "1234.56", 5).</summary>
    internal static decimal? ParseCurrency(object? cell)
    {
        if (cell is not string) return SheetTable.Number(cell);
        var text = Regex.Replace(((string)cell).Trim(), @"[^\d,.\-]", "");
        if (text.Length == 0 || text is "-" or "," or ".") return null;
        if (text.Contains(',') && text.Contains('.'))
            text = text.LastIndexOf(',') > text.LastIndexOf('.')
                ? text.Replace(".", "").Replace(',', '.')
                : text.Replace(",", "");
        else if (text.Contains(','))
            text = text.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>14 when either value looks like a CNPJ, 11 when like a CPF (leading zeros may be missing).</summary>
    internal static int? ExpectedDocumentLength(params string[] values)
    {
        var lengths = values.Select(v => PyFormat.OnlyDigits(v).Length).Where(length => length > 0).ToList();
        if (lengths.Any(length => length is 12 or 13 or 14)) return 14;
        if (lengths.Any(length => length is 10 or 11)) return 11;
        return null;
    }

    internal static string NormalizeDocument(string value, int? expectedLength)
    {
        var digits = PyFormat.OnlyDigits(value);
        return expectedLength is { } length && digits.Length > 0 && digits.Length < length
            ? digits.PadLeft(length, '0')
            : digits;
    }

    internal static string FormatDocument(string value, int? expectedLength)
    {
        var digits = PyFormat.OnlyDigits(value);
        if (expectedLength is { } length && digits.Length > 0 && digits.Length < length)
            digits = digits.PadLeft(length, '0');
        else if (digits.Length == 13) digits = digits.PadLeft(14, '0');
        else if (digits.Length == 10) digits = digits.PadLeft(11, '0');

        return digits.Length switch
        {
            14 => $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}",
            11 => $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}",
            _ => value.Trim(),
        };
    }
}
