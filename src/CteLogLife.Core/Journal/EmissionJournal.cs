using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CteLogLife.Core.Journal;

public enum CteKind { LogLife, Complementary }

/// <summary>How far one emission got. Each change is written to disk before the next step starts.</summary>
public enum EmissionStage
{
    /// <summary>Filling the form. Nothing was sent.</summary>
    Started,
    /// <summary>Past the key that saves the CT-e; authorization is in flight.</summary>
    Transmitting,
    /// <summary>Authorized and the number is known, but the LogLife system does not have it yet.</summary>
    NumberCaptured,
    /// <summary>The number was sent to the LogLife system.</summary>
    Reported,
    /// <summary>The PDF was uploaded but not yet linked to the protocol.</summary>
    PdfUploaded,
    Completed,
    /// <summary>Failed or stopped before anything was sent: safe to emit again.</summary>
    Aborted,
    /// <summary>Failed after the point of no return and nobody confirmed what happened.</summary>
    Uncertain,
    /// <summary>The operator dealt with it by hand.</summary>
    Resolved,
}

/// <summary>What has to happen after a CT-e is authorized, with everything needed to do it later.</summary>
public sealed record PostPlan
{
    public CteKind Kind { get; init; } = CteKind.LogLife;
    public bool Symbolic { get; init; }

    /// <summary>Send the number through the JSON endpoint.</summary>
    public bool PostJson { get; init; } = true;

    /// <summary>Path of the older CSV upload of the number, when the flow uses it.</summary>
    public string? UploadCsvPath { get; init; }

    /// <summary>When false a failed CSV upload is only a warning (the JSON call already recorded the number).</summary>
    public bool UploadCsvRequired { get; init; }

    public bool KeepUploadCsv { get; init; }
    public string AssociateCsvPath { get; init; } = "";
    public string PdfFolder { get; init; } = "";
    public string UploadType { get; init; } = "";

    /// <summary>The protocol exactly as it goes in the CSV files.</summary>
    public string CsvProtocol { get; init; } = "";
}

public sealed record JournalEntry
{
    public string EmissionId { get; init; } = "";
    public DateTime Time { get; init; }
    public EmissionStage Stage { get; init; }
    public string Protocol { get; init; } = "";
    public string Title { get; init; } = "";
    public long? CteNumber { get; init; }
    public string? EmissionDate { get; init; }
    public string? Note { get; init; }
    public PostPlan Plan { get; init; } = new();

    [JsonIgnore]
    public bool IsOpen => Stage is EmissionStage.Transmitting or EmissionStage.NumberCaptured
        or EmissionStage.Reported or EmissionStage.PdfUploaded or EmissionStage.Uncertain;
}

/// <summary>
/// Append-only record of every emission, one JSON line per stage change. It is what makes a crash or a
/// failed upload recoverable, and what keeps a protocol from being emitted twice.
/// </summary>
public sealed class EmissionJournal
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _lock = new();
    private readonly string _path;
    private readonly Dictionary<string, JournalEntry> _latest = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    public EmissionJournal(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                if (JsonSerializer.Deserialize<JournalEntry>(line, Options) is { EmissionId.Length: > 0 } entry)
                    Remember(entry);
            }
            catch (JsonException)
            {
                // A line cut short by a crash: the stages written before it are still valid.
            }
        }
    }

    public event Action? Changed;

    /// <summary>Writes a new stage for an emission and returns the entry as stored.</summary>
    public JournalEntry Append(JournalEntry entry)
    {
        entry = entry with { Time = DateTime.Now };
        var line = JsonSerializer.Serialize(entry, Options) + "\n";
        lock (_lock)
        {
            using (var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                var bytes = Encoding.UTF8.GetBytes(line);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            Remember(entry);
        }
        Changed?.Invoke();
        return entry;
    }

    private void Remember(JournalEntry entry)
    {
        if (!_latest.ContainsKey(entry.EmissionId)) _order.Add(entry.EmissionId);
        _latest[entry.EmissionId] = entry;
    }

    /// <summary>Emissions that were sent (or may have been) and are not fully recorded yet, oldest first.</summary>
    public IReadOnlyList<JournalEntry> Open()
    {
        lock (_lock)
            return _order.Select(id => _latest[id]).Where(e => e.IsOpen).ToArray();
    }

    /// <summary>The open emission of this protocol and kind, if any.</summary>
    public JournalEntry? FindOpen(string protocol, CteKind kind) =>
        Open().LastOrDefault(e => e.Protocol == protocol && e.Plan.Kind == kind);
}
