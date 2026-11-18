using System.Text;
using ClosedXML.Excel;

namespace CteLogLife.Core.Excel;

/// <summary>A table built in memory and saved as .xlsx (the conference spreadsheets the operators use).</summary>
public sealed class ReportTable
{
    private readonly List<string> _columns = [];
    private readonly List<Dictionary<string, object?>> _rows = [];

    public ReportTable(params string[] columns) => _columns.AddRange(columns);

    public IReadOnlyList<string> Columns => _columns;
    public int RowCount => _rows.Count;

    public int AddRow(params (string Column, object? Value)[] cells)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (column, value) in cells)
        {
            EnsureColumn(column);
            row[column] = value;
        }
        _rows.Add(row);
        return _rows.Count - 1;
    }

    public void Set(int row, string column, object? value)
    {
        EnsureColumn(column);
        _rows[row][column] = value;
    }

    public object? Get(int row, string column) => _rows[row].GetValueOrDefault(column);

    private void EnsureColumn(string column)
    {
        if (!_columns.Contains(column)) _columns.Add(column);
    }

    public void Save(string path, string sheetName = "Sheet1") => SaveWorkbook(path, (sheetName, this));

    public static void SaveWorkbook(string path, params (string Sheet, ReportTable Table)[] sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var (sheetName, table) in sheets)
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            for (var c = 0; c < table._columns.Count; c++)
            {
                var header = sheet.Cell(1, c + 1);
                header.Value = table._columns[c];
                header.Style.Font.Bold = true;
            }
            for (var r = 0; r < table._rows.Count; r++)
            {
                for (var c = 0; c < table._columns.Count; c++)
                {
                    if (!table._rows[r].TryGetValue(table._columns[c], out var value) || value is null) continue;
                    sheet.Cell(r + 2, c + 1).Value = ToCell(value);
                }
            }
        }
        workbook.SaveAs(path);
    }

    private static XLCellValue ToCell(object value) => value switch
    {
        string s => s,
        bool b => b,
        int i => i,
        long l => l,
        double d => d,
        decimal m => m,
        DateTime dt => dt,
        _ => value.ToString() ?? "",
    };
}

/// <summary>CSV files in the shape pandas wrote them: UTF-8 without BOM, CRLF, minimal quoting.</summary>
public static class CsvFile
{
    public static void Write(string path, IReadOnlyList<string> headers, params IReadOnlyList<string>[] rows)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', headers.Select(Escape))).Append("\r\n");
        foreach (var row in rows)
            builder.Append(string.Join(',', row.Select(Escape))).Append("\r\n");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0) return field;
        return '"' + field.Replace("\"", "\"\"") + '"';
    }
}
