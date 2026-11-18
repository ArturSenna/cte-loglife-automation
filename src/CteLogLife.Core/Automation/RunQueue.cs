using System.Globalization;
using System.Text;

namespace CteLogLife.Core.Automation;

public enum QueueItemState { Pending, Running, Done, Failed, Skipped }

/// <summary>The answer to a request to add work to the running queue, in words for the operator.</summary>
public sealed record IncludeResult(bool Ok, string Message);

/// <summary>One unit of bot work as the operator sees it (a CT-e to emit, cancel or a slip to enter).</summary>
public sealed record QueueItem(string Id, string Title, string Subtitle)
{
    public QueueItemState State { get; init; } = QueueItemState.Pending;
    public string? Result { get; init; }
}

/// <summary>
/// The work list of the current run. The bot takes the first pending item each time, so reordering the
/// pending ones (allowed while paused) changes what is emitted next.
/// </summary>
public sealed class RunQueue
{
    private readonly object _lock = new();
    private readonly List<QueueItem> _items = [];
    private string _title = "";
    private string _step = "";
    private string _includeLabel = "";
    private Func<string, CancellationToken, Task<IncludeResult>>? _includer;

    /// <summary>Raised on the thread that made the change.</summary>
    public event Action? Changed;

    // ------------------------------------------------------------------ adding work during a run

    /// <summary>True while the running flow can fetch and add one more item (emissions can, by protocol).</summary>
    public bool CanInclude { get { lock (_lock) return _includer is not null; } }

    /// <summary>What the operator types to add an item ("protocolo").</summary>
    public string IncludeLabel { get { lock (_lock) return _includeLabel; } }

    /// <summary>Called by the running flow: lets the operator add items until the queue is loaded again.</summary>
    public void AllowInclude(string label, Func<string, CancellationToken, Task<IncludeResult>> includer)
    {
        lock (_lock)
        {
            _includeLabel = label;
            _includer = includer;
        }
        Changed?.Invoke();
    }

    public void DisallowInclude()
    {
        lock (_lock)
        {
            if (_includer is null) return;
            _includer = null;
            _includeLabel = "";
        }
        Changed?.Invoke();
    }

    /// <summary>Asks the running flow to add the item the operator typed. Never throws for a refusal.</summary>
    public Task<IncludeResult> IncludeAsync(string text, CancellationToken ct)
    {
        Func<string, CancellationToken, Task<IncludeResult>>? includer;
        lock (_lock) includer = _includer;
        return includer is null
            ? Task.FromResult(new IncludeResult(false, "Esta execução não aceita novos itens."))
            : includer(text, ct);
    }

    /// <summary>Adds a pending item: as the next one to run, or at the end of the queue.</summary>
    public void Add(QueueItem item, bool next)
    {
        lock (_lock)
        {
            var pending = item with { State = QueueItemState.Pending };
            var first = next ? _items.FindIndex(i => i.State == QueueItemState.Pending) : -1;
            if (first >= 0) _items.Insert(first, pending);
            else _items.Add(pending);
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Whether an item answers a search: every word typed must appear in its title or subtitle,
    /// ignoring case and accents.
    /// </summary>
    public static bool Matches(QueueItem item, string search)
    {
        var haystack = Fold(item.Title + " " + item.Subtitle);
        return Fold(search).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(haystack.Contains);
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(ch));
        return builder.ToString();
    }

    // ------------------------------------------------------------------ the list

    public string Title { get { lock (_lock) return _title; } }

    /// <summary>What the bot is doing on the current item (e.g. "Impostos").</summary>
    public string Step
    {
        get { lock (_lock) return _step; }
        set
        {
            lock (_lock)
            {
                if (_step == value) return;
                _step = value;
            }
            Changed?.Invoke();
        }
    }

    public void Load(string title, IEnumerable<QueueItem> items)
    {
        lock (_lock)
        {
            _title = title;
            _step = "";
            _includer = null;
            _includeLabel = "";
            _items.Clear();
            _items.AddRange(items);
        }
        Changed?.Invoke();
    }

    public void Clear() => Load("", []);

    public IReadOnlyList<QueueItem> Snapshot()
    {
        lock (_lock) return _items.ToArray();
    }

    public int Total { get { lock (_lock) return _items.Count; } }

    public int Finished
    {
        get { lock (_lock) return _items.Count(i => i.State is not (QueueItemState.Pending or QueueItemState.Running)); }
    }

    /// <summary>Marks the first pending item as running and returns it; null when none is left.</summary>
    public QueueItem? TakeNext()
    {
        QueueItem? taken = null;
        lock (_lock)
        {
            var index = _items.FindIndex(i => i.State == QueueItemState.Pending);
            if (index >= 0)
            {
                taken = _items[index] with { State = QueueItemState.Running };
                _items[index] = taken;
                _step = "";
            }
        }
        if (taken is not null) Changed?.Invoke();
        return taken;
    }

    public void Finish(string id, QueueItemState state, string? result = null)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(i => i.Id == id);
            if (index < 0) return;
            _items[index] = _items[index] with { State = state, Result = result };
            _step = "";
        }
        Changed?.Invoke();
    }

    /// <summary>Marks everything still pending as skipped (the run ended early).</summary>
    public void SkipPending(string reason)
    {
        lock (_lock)
        {
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].State == QueueItemState.Pending)
                    _items[i] = _items[i] with { State = QueueItemState.Skipped, Result = reason };
        }
        Changed?.Invoke();
    }

    /// <summary>Moves a pending item up (negative) or down (positive) among the pending ones.</summary>
    public bool Move(string id, int delta)
    {
        lock (_lock)
        {
            var pending = PendingIndexes();
            var position = pending.FindIndex(i => _items[i].Id == id);
            if (position < 0) return false;
            var target = Math.Clamp(position + delta, 0, pending.Count - 1);
            if (target == position) return false;
            Relocate(pending, position, target);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Makes a pending item the next one to run.</summary>
    public bool MoveToTop(string id) => Move(id, int.MinValue / 2);

    private List<int> PendingIndexes()
    {
        var indexes = new List<int>();
        for (var i = 0; i < _items.Count; i++)
            if (_items[i].State == QueueItemState.Pending) indexes.Add(i);
        return indexes;
    }

    private void Relocate(List<int> pending, int from, int to)
    {
        var order = pending.Select(i => _items[i]).ToList();
        var moved = order[from];
        order.RemoveAt(from);
        order.Insert(to, moved);
        for (var k = 0; k < pending.Count; k++)
            _items[pending[k]] = order[k];
    }
}
