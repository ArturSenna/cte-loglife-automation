using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CteLogLife.Core;

/// <summary>
/// Text forms that match what the Python app produced. They matter because these
/// strings are typed into Bsoft key by key (prices, tax rates, slip values).
/// </summary>
public static class PyFormat
{
    /// <summary>Python's <c>str(float)</c>: shortest round-trip, always with a decimal part ("12.0", "20.5").</summary>
    public static string Float(double value)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsPositiveInfinity(value)) return "inf";
        if (double.IsNegativeInfinity(value)) return "-inf";

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        if (text.Contains('E'))
            return text.Replace("E+", "e+").Replace("E-", "e-");
        return text.Contains('.') ? text : text + ".0";
    }

    public static string Float(decimal value) => Float((double)value);

    /// <summary>Python's <c>str()</c> of a decoded JSON value.</summary>
    public static string Json(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Number => JsonNumber(element),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Null or JsonValueKind.Undefined => "None",
        _ => element.GetRawText(),
    };

    private static string JsonNumber(JsonElement element)
    {
        var raw = element.GetRawText();
        var isInteger = raw.IndexOfAny(['.', 'e', 'E']) < 0;
        return isInteger ? raw : Float(element.GetDouble());
    }

    /// <summary>Python's <c>str.title()</c>: first letter of every run of letters upper, the rest lower.</summary>
    public static string Title(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Length);
        var previousIsLetter = false;
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(previousIsLetter ? char.ToLowerInvariant(ch) : char.ToUpperInvariant(ch));
                previousIsLetter = true;
            }
            else
            {
                builder.Append(ch);
                previousIsLetter = false;
            }
        }
        return builder.ToString();
    }

    /// <summary>Parses "5,00", "150.5" or "150" as a decimal (comma or dot as the decimal separator).</summary>
    public static decimal Money(string text)
    {
        var normalized = (text ?? "").Trim().Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;
        throw new FormatException($"Valor numérico inválido: \"{text}\"");
    }

    /// <summary>Python's <c>"{:0.2f}"</c>, rounding half away from zero (currency rounding).</summary>
    public static string Fixed2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    public static string OnlyDigits(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
            if (ch is >= '0' and <= '9') builder.Append(ch);
        return builder.ToString();
    }
}

/// <summary>Null-tolerant access to loosely typed API responses.</summary>
public static class JsonX
{
    /// <summary>The property value, or null when missing, JSON null, or the element is not an object.</summary>
    public static JsonElement? Get(this JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value;
    }

    public static JsonElement? Get(this JsonElement? element, string name) => element?.Get(name);

    public static string Str(this JsonElement element, string name, string fallback = "") =>
        element.Get(name) is { } value ? PyFormat.Json(value) : fallback;

    public static string Str(this JsonElement? element, string name, string fallback = "") =>
        element is { } value ? value.Str(name, fallback) : fallback;

    public static IReadOnlyList<JsonElement> Items(this JsonElement element, string name) =>
        element.Get(name).Items();

    public static IReadOnlyList<JsonElement> Items(this JsonElement element) => ((JsonElement?)element).Items();

    public static IReadOnlyList<JsonElement> Items(this JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array) return [];
        var list = new List<JsonElement>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray()) list.Add(item);
        return list;
    }

    /// <summary>Python truthiness of a decoded JSON value.</summary>
    public static bool Truthy(this JsonElement? element)
    {
        if (element is not { } value) return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDouble() != 0,
            JsonValueKind.String => value.GetString()!.Length > 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => false,
        };
    }

    /// <summary>A number, or a string holding one; null otherwise.</summary>
    public static decimal? Number(this JsonElement? element)
    {
        if (element is not { } value) return null;
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetDecimal(out var number) ? number : (decimal)value.GetDouble();
        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString()!.Trim().Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    /// <summary>Python's <c>a or b</c> over JSON values: the first truthy one.</summary>
    public static JsonElement? Or(this JsonElement? first, JsonElement? second) => first.Truthy() ? first : second;
}
