using System.Globalization;
using System.Text.Json;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Journal;
using CteLogLife.Core.Tax;

namespace CteLogLife.Core.Flows;

/// <summary>An error with a message written for the operator.</summary>
public sealed class FlowException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Everything a flow needs from the outside world.</summary>
public sealed class FlowContext
{
    public required LogLifeApi Api { get; init; }
    public required Func<TaxTables> Tax { get; init; }
    public required Func<IBsoftDriver> CreateDriver { get; init; }
    public required EmissionJournal Journal { get; init; }
    public required IOperator Operator { get; init; }
    public required IRunLog Log { get; init; }
    public required RunControl Control { get; init; }
    public required RunQueue Queue { get; init; }
    public required AppSettings Settings { get; init; }
    public Func<DateTime> Now { get; init; } = () => DateTime.Now;
    public string DebugFolder { get; init; } = AppPaths.DebugFolder;
    public TimeSpan PdfPollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    internal string Today() => Now().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    internal string Stamp(DateOnly date) =>
        $"{date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}-{Now().ToString("HH-mm", CultureInfo.InvariantCulture)}";

    /// <summary>A folder setting that must point to an existing folder before the bot starts.</summary>
    internal static string RequireFolder(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new FlowException($"Selecione a pasta \"{label}\" em Configurações antes de iniciar.");
        return path.Replace('/', '\\').TrimEnd('\\');
    }

    internal void SaveReport(ReportTable report, string path)
    {
        try
        {
            report.Save(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Não foi possível salvar a planilha {Path.GetFileName(path)} (aberta em outro programa?): {ex.Message}");
        }
    }

    /// <summary>Keeps the raw API answer of the last run for troubleshooting.</summary>
    internal void DumpDebug(string name, JsonElement root)
    {
        try
        {
            Directory.CreateDirectory(DebugFolder);
            File.WriteAllText(Path.Combine(DebugFolder, name), root.GetRawText());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Debug output only.
        }
    }
}

/// <summary>One CT-e waiting to be emitted.</summary>
public sealed class EmissionJob
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public required string Protocol { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "";

    /// <summary>Sender/receiver/payer of a normal or symbolic CT-e; null for a complementary one.</summary>
    public PartiesInput? Parties { get; init; }

    public CargoInput Cargo { get; init; } = new();

    /// <summary>Number of the CT-e a complementary one refers to.</summary>
    public string? OriginalCte { get; init; }

    public required TaxInput Tax { get; init; }
    public required PostPlan Plan { get; init; }

    /// <summary>Row of this job in the conference spreadsheet.</summary>
    public int Row { get; init; }
}

/// <param name="Skipped">Jobs left out because the protocol has an unresolved emission.</param>
/// <param name="Invalid">Jobs left out because their data was incomplete.</param>
/// <param name="Included">Jobs the operator added during the run (they are part of <paramref name="Total"/>).</param>
public sealed record RunOutcome(int Emitted, int Total, int Skipped, int Invalid, bool Stopped, int Included = 0)
{
    /// <summary>The closing message: the flow's own text, or what was done before a stop.</summary>
    public string Message(string success)
    {
        var text = Stopped
            ? $"Execução interrompida pelo operador.\n\nEmitidos: {Emitted} de {Total}."
            : success;
        if (Included > 0)
            text += $"\n\n{Included} protocolo(s) incluído(s) durante a execução.";
        if (Skipped > 0)
            text += $"\n\n{Skipped} protocolo(s) não emitido(s) por terem pendências. Veja a aba Pendências.";
        if (Invalid > 0)
            text += $"\n\n{Invalid} protocolo(s) não emitido(s) por dados incompletos. Veja o registro de atividade.";
        return text;
    }
}

/// <summary>Formatting of API service data for the conference spreadsheets.</summary>
internal static class ServiceText
{
    private static readonly Dictionary<string, string> Steps = new(StringComparer.Ordinal)
    {
        ["availableService"] = "AGUARDANDO DISPONIBILIZAÇÃO",
        ["toAllocateService"] = "AGUARDANDO ALOCAÇÃO",
        ["toDeliveryService"] = "EM ROTA DE ENTREGA",
        ["deliveringService"] = "ENTREGANDO",
        ["toLandingService"] = "DISPONÍVEL PARA RETIRADA",
        ["landingService"] = "DESEMBARCANDO",
        ["toBoardValidate"] = "VALIDAR EMBARQUE",
        ["toCollectService"] = "AGENDADO",
        ["collectingService"] = "COLETANDO",
        ["toBoardService"] = "EM ROTA DE EMBARQUE",
        ["boardingService"] = "EMBARCANDO SERVIÇO",
        ["finishedService"] = "FINALIZADO",
    };

    public static string StepLabel(string step) => Steps.GetValueOrDefault(step, "");

    /// <summary>"1. NAME - BRANCH" lines (no numbering for a single address).</summary>
    public static string Names(IReadOnlyList<JsonElement> addresses) =>
        Lines(addresses, (address, prefix) =>
            (addresses.Count == 1 ? "" : prefix) + address.Str("trading_name") + " - " + address.Str("branch"));

    public static string Meta(IReadOnlyList<JsonElement> addresses, string cityField = "city_name",
        string stateField = "city_state") =>
        Lines(addresses, (address, prefix) =>
            (addresses.Count == 1 ? "" : prefix)
            + PyFormat.Title(address.Str("street")) + ", " + address.Str("number") + " - "
            + PyFormat.Title(address.Str("neighborhood")) + ", " + PyFormat.Title(City(address, cityField))
            + " - " + address.Str(stateField) + ", " + address.Str("cep"));

    public static string CnpjsListed(IReadOnlyList<JsonElement> addresses) =>
        Lines(addresses, (address, prefix) => (addresses.Count == 1 ? "" : prefix) + address.Str("cnpj_cpf"));

    public static List<string> Cnpjs(IReadOnlyList<JsonElement> addresses) =>
        addresses.Select(a => a.Str("cnpj_cpf")).ToList();

    private static string City(JsonElement address, string cityField) =>
        cityField == "cityIDAddress.name" ? address.Get("cityIDAddress").Str("name") : address.Str(cityField);

    private static string Lines(IReadOnlyList<JsonElement> addresses, Func<JsonElement, string, string> line) =>
        string.Join("\n", addresses.Select((address, index) => line(address, $"{index + 1}. ")));

    /// <summary>The collect moment in Brasília time (UTC-3), as the spreadsheets always showed it.</summary>
    public static DateTime? CollectMoment(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.String } text) return null;
        return DateTimeOffset.TryParse(text.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime.AddHours(-3)
            : null;
    }

    public static string CollectDate(JsonElement? value) =>
        CollectMoment(value)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";

    /// <summary>A JSON value as a spreadsheet cell: numbers stay numbers.</summary>
    public static object? Cell(JsonElement? value)
    {
        if (value is not { } element) return null;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var whole) ? whole : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText(),
        };
    }

    /// <summary>A protocol as a spreadsheet cell: a number when it is one.</summary>
    public static object ProtocolCell(string protocol) =>
        long.TryParse(protocol, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : protocol;
}
