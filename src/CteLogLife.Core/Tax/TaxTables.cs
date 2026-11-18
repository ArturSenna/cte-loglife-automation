using CteLogLife.Core.Excel;

namespace CteLogLife.Core.Tax;

/// <summary>An ICMS rate: its value and the exact text the bot types into Bsoft.</summary>
public readonly record struct Aliquot(decimal Value, string Text);

/// <summary>
/// The two spreadsheets that drive the ICMS fields: <c>Complementares.xlsx</c> (state, UF and the
/// legal text for the observation field) and <c>Alíquota.xlsx</c> (rate by origin row and destination column).
/// </summary>
public sealed class TaxTables
{
    public const string StatesFile = "Complementares.xlsx";
    public const string StatesSheet = "Plan1";
    public const string AliquotFile = "Alíquota.xlsx";
    public const string AliquotSheet = "Planilha1";

    private readonly List<(string Estado, string Uf, string Info)> _states = [];
    private readonly Dictionary<string, Dictionary<string, Aliquot>> _aliquots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _originRowCount = new(StringComparer.Ordinal);

    public static TaxTables Load(string dataDirectory)
    {
        var tables = new TaxTables();
        tables.LoadStates(SheetTable.Read(Path.Combine(dataDirectory, StatesFile), StatesSheet));
        tables.LoadAliquots(SheetTable.Read(Path.Combine(dataDirectory, AliquotFile), AliquotSheet));
        return tables;
    }

    private void LoadStates(SheetTable sheet)
    {
        int estado = Required(sheet, "Estado", StatesFile), uf = Required(sheet, "UF", StatesFile),
            info = Required(sheet, "Info", StatesFile);
        foreach (var row in sheet.Rows)
            _states.Add((SheetTable.Text(row[estado]), SheetTable.Text(row[uf]), SheetTable.Text(row[info])));
    }

    private void LoadAliquots(SheetTable sheet)
    {
        var ufColumn = Required(sheet, "UF", AliquotFile);
        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            if (c == ufColumn) continue;
            var destination = sheet.Columns[c];
            if (string.IsNullOrWhiteSpace(destination)) continue;

            // pandas reads a column as float as soon as one cell has decimals, and the rate is then
            // typed as "12.0" instead of "12". Keep that exact text.
            var isFloatColumn = sheet.Rows.Any(row => SheetTable.Number(row[c]) is { } v && v != Math.Truncate(v));
            foreach (var row in sheet.Rows)
            {
                var origin = SheetTable.Text(row[ufColumn]);
                if (origin.Length == 0 || SheetTable.Number(row[c]) is not { } value) continue;
                var text = isFloatColumn ? PyFormat.Float(value) : ((long)value).ToString();
                if (!_aliquots.TryGetValue(origin, out var byDestination))
                    _aliquots[origin] = byDestination = new Dictionary<string, Aliquot>(StringComparer.Ordinal);
                byDestination[destination] = new Aliquot(value, text);
            }
        }
        foreach (var row in sheet.Rows)
        {
            var origin = SheetTable.Text(row[ufColumn]);
            if (origin.Length > 0) _originRowCount[origin] = _originRowCount.GetValueOrDefault(origin) + 1;
        }
    }

    private static int Required(SheetTable sheet, string column, string file)
    {
        var index = sheet.IndexOf(column);
        if (index < 0) throw new InvalidDataException($"Coluna '{column}' não encontrada em {file}.");
        return index;
    }

    /// <summary>UF for a state as written in <c>Estado</c> (full name or code); null unless exactly one row matches.</summary>
    public string? UfOf(string estado) => Single(estado)?.Uf;

    /// <summary>The legal text for a state; null unless exactly one row matches.</summary>
    public string? InfoOf(string estado) => Single(estado)?.Info;

    private (string Estado, string Uf, string Info)? Single(string estado)
    {
        (string, string, string)? found = null;
        foreach (var row in _states)
        {
            if (row.Estado != estado) continue;
            if (found is not null) return null;
            found = row;
        }
        return found;
    }

    /// <summary>The rate from <paramref name="originUf"/> to <paramref name="destinationUf"/>; null when not on the table.</summary>
    public Aliquot? AliquotFor(string originUf, string destinationUf)
    {
        if (_originRowCount.GetValueOrDefault(originUf) != 1) return null;
        return _aliquots.TryGetValue(originUf, out var byDestination) &&
               byDestination.TryGetValue(destinationUf, out var aliquot)
            ? aliquot
            : null;
    }
}

/// <summary>The ICMS values of one emission, ready to be typed.</summary>
/// <param name="OriginUf">UF of the sender.</param>
/// <param name="TaxText">Rate typed in the ICMS field ("0" outside MG).</param>
/// <param name="TpInfo">ICMS amount as text, used for the GNRE note and the observation.</param>
/// <param name="Observation">Text for the observation field.</param>
public sealed record IcmsValues(string OriginUf, string DestinationUf, string TaxText, string TpInfo, string Observation);

public static class IcmsCalculator
{
    /// <summary>
    /// Computes the ICMS fields for a CT-e.
    /// </summary>
    /// <param name="originState">State of the first sender address, as the API gives it.</param>
    /// <param name="observationPrefix">Start of the observation, e.g. "Protocolo 123".</param>
    /// <param name="value">Value the ICMS amount is computed on.</param>
    /// <param name="strict">
    /// True for the flows that never tolerated a state missing from the tables (complementary, by UF):
    /// they stop with an error. Otherwise the raw state is used and the rate falls back to 0.
    /// </param>
    public static IcmsValues Compute(TaxTables tables, string originState, string destinationState,
        string observationPrefix, decimal value, bool strict = false)
    {
        var originUf = tables.UfOf(originState);
        var destinationUf = tables.UfOf(destinationState);
        if (strict && (originUf is null || destinationUf is null))
            throw new InvalidDataException(
                $"Estado não encontrado em {TaxTables.StatesFile}: \"{(originUf is null ? originState : destinationState)}\".");
        originUf ??= originState;
        destinationUf ??= destinationState;

        var info = tables.InfoOf(originUf);
        var aliquot = tables.AliquotFor(originUf, destinationUf);
        if (strict && (info is null || aliquot is null))
            throw new InvalidDataException(
                $"Alíquota ou texto de ICMS não encontrado para {originUf} → {destinationUf}.");

        var rate = aliquot ?? new Aliquot(0m, "0");
        var amount = PyFormat.Fixed2(rate.Value * value * 0.008m);
        var observation = $"{observationPrefix} - {info ?? ""}";
        var taxText = rate.Text;
        if (originUf != "MG")
        {
            observation = observation.Replace("#", amount);
            taxText = "0";
        }
        return new IcmsValues(originUf, destinationUf, taxText, amount, observation);
    }
}
