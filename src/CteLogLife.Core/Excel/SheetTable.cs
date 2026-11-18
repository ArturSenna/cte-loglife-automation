using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace CteLogLife.Core.Excel;

/// <summary>A worksheet read into memory: first row is the header. Reads both .xlsx and .xls.</summary>
public sealed class SheetTable
{
    static SheetTable() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public SheetTable(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<object?[]> Rows { get; }

    public static SheetTable Read(string path, string? sheetName = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            if (sheetName is not null && !string.Equals(reader.Name, sheetName, StringComparison.OrdinalIgnoreCase))
                continue;

            var columns = new List<string>();
            var rows = new List<object?[]>();
            if (reader.Read())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                    columns.Add(Text(reader.GetValue(i)));
            }
            // Blank rows are kept so that row N here is always spreadsheet line N + 2.
            while (reader.Read())
            {
                var row = new object?[columns.Count];
                for (var i = 0; i < columns.Count && i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[i] = value is DBNull ? null : value;
                }
                rows.Add(row);
            }
            return new SheetTable(columns, rows);
        } while (reader.NextResult());

        throw new InvalidDataException($"Planilha '{sheetName}' não encontrada em {Path.GetFileName(path)}.");
    }

    /// <summary>Index of the column with exactly this header, or -1.</summary>
    public int IndexOf(string column)
    {
        for (var i = 0; i < Columns.Count; i++)
            if (Columns[i] == column) return i;
        return -1;
    }

    /// <summary>Index of the column matching this header ignoring case and outer spaces, or -1.</summary>
    public int Find(string column)
    {
        var wanted = column.Trim();
        for (var i = 0; i < Columns.Count; i++)
            if (string.Equals(Columns[i].Trim(), wanted, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    public int FindFirst(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var index = Find(candidate);
            if (index >= 0) return index;
        }
        return -1;
    }

    public static bool IsBlank(object? value) => value is null or DBNull || value is double d && double.IsNaN(d);

    /// <summary>A cell as text: whole numbers without decimals, dates as dd/MM/yyyy.</summary>
    public static string Text(object? value) => value switch
    {
        null or DBNull => "",
        string s => s.Trim(),
        double d when Math.Abs(d % 1) < double.Epsilon && Math.Abs(d) < 1e15 =>
            ((long)d).ToString(CultureInfo.InvariantCulture),
        double d => PyFormat.Float(d),
        DateTime dt => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>A cell as a number: numeric cells as they are, text with comma or dot decimals parsed.</summary>
    public static decimal? Number(object? value)
    {
        switch (value)
        {
            case null or DBNull: return null;
            case double d: return double.IsNaN(d) || double.IsInfinity(d) ? null : (decimal)d;
            case int i: return i;
            case long l: return l;
            case decimal m: return m;
            case string s:
                return decimal.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            default: return null;
        }
    }
}
