using System.Globalization;
using CteLogLife.Bsoft;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Tests;

/// <summary>
/// Stands in for the mouse, the keyboard, the clipboard and the clock. Everything the bot sends is
/// written down in order; time only moves when the bot sleeps, so a whole emission runs in an instant
/// and what happens "after 120 ms" happens at exactly that moment.
/// </summary>
public sealed class FakeDesktop : IDesktop
{
    private readonly object _lock = new();
    private readonly List<(long At, Action Run)> _scheduled = [];
    private readonly HashSet<Key> _held = [];
    private string? _clipboard = "";
    private long _now;

    /// <summary>Every mouse, keyboard and clipboard event, in the order the bot sent them.</summary>
    public List<string> Events { get; } = [];

    /// <summary>Called after each event with its text (to make the fake screen react).</summary>
    public Action<string>? OnEvent { get; set; }

    /// <summary>What Ctrl+C "copies" (the CT-e number field).</summary>
    public string CopyResult { get; set; } = "4321";

    /// <summary>Another program replacing whatever the bot puts on the clipboard.</summary>
    public string? ClipboardThief { get; set; }

    /// <summary>The clipboard is held by another program: it cannot be read.</summary>
    public bool ClipboardBusy { get; set; }

    public IntPtr ForegroundWindow { get; set; } = new(0xB50F7);
    public uint ForegroundProcess { get; set; } = 4242;
    public List<IntPtr> GivenForeground { get; } = [];

    public long Now { get { lock (_lock) return _now; } }

    /// <summary>Runs <paramref name="action"/> once the clock has advanced by <paramref name="ms"/>.</summary>
    public void After(int ms, Action action)
    {
        lock (_lock) _scheduled.Add((_now + ms, action));
    }

    public void Sleep(int ms) => Advance(ms);

    public void Advance(long ms)
    {
        long target;
        lock (_lock) target = _now + Math.Max(0, ms);
        while (true)
        {
            Action? due = null;
            lock (_lock)
            {
                var index = _scheduled.FindIndex(entry => entry.At <= target);
                if (index >= 0)
                {
                    // Earliest first, so a chain of reactions plays in order.
                    for (var i = 0; i < _scheduled.Count; i++)
                        if (_scheduled[i].At <= target && _scheduled[i].At < _scheduled[index].At) index = i;
                    _now = Math.Max(_now, _scheduled[index].At);
                    due = _scheduled[index].Run;
                    _scheduled.RemoveAt(index);
                }
                else
                {
                    _now = target;
                }
            }
            if (due is null) return;
            due();
        }
    }

    private void Record(string text)
    {
        lock (_lock) Events.Add(text);
        OnEvent?.Invoke(text);
    }

    public void MoveTo(double x, double y) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"move {Math.Round(x)},{Math.Round(y)}"));

    public void MouseClick(bool right = false) => Record(right ? "rclick" : "click");

    public void KeyDown(Key key)
    {
        _held.Add(key);
        Record($"down {key}");
    }

    public void KeyUp(Key key)
    {
        _held.Remove(key);
        Record($"up {key}");
    }

    public void Tap(Key key) => Record($"tap {key}");

    public void TapChar(char ch)
    {
        if (ch == 'c' && _held.Contains(Key.Control)) _clipboard = CopyResult;
        Record($"char {ch}");
    }

    public void TypeLikeAPerson(char ch) => Record($"person {ch}");

    public void SetClipboard(string text)
    {
        _clipboard = ClipboardThief ?? text;
        Record($"clipboard {text.Replace("\n", "\\n")}");
    }

    public void ClearClipboard()
    {
        _clipboard = "";
        Record("clipboard-clear");
    }

    public string? TryGetClipboard() => ClipboardBusy ? null : _clipboard;

    public IntPtr Foreground() => ForegroundWindow;
    public uint ProcessOf(IntPtr window) => window == ForegroundWindow ? ForegroundProcess : 0;

    public bool GiveForeground(IntPtr window)
    {
        GivenForeground.Add(window);
        return true;
    }

    /// <summary>Where the window of "Bsoft" is on the fake screen (null: the answer is looked for everywhere).</summary>
    public ScreenBox? Area { get; set; }

    public ScreenBox? ForegroundArea() => Area;

    public string? FocusedText() => null;
}

/// <summary>Says, like the real hooks would, whether "Bsoft" was used during a pause.</summary>
public sealed class FakePauseWatcher : IPauseWatcher
{
    private Action? _used;

    public bool Started { get; private set; }
    public uint Process { get; private set; }

    /// <summary>What <see cref="Stop"/> answers: true (used), false (untouched) or null (could not watch).</summary>
    public bool? Used { get; set; } = false;

    public void Start(uint process, Action used)
    {
        Started = true;
        Process = process;
        _used = used;
    }

    /// <summary>The operator clicks or types in Bsoft while the bot is paused.</summary>
    public void UseBsoft()
    {
        Used = true;
        _used?.Invoke();
    }

    public bool? Stop() => Used;
}

/// <summary>A fake screen showing every element the driver looks for, each in its own place.</summary>
public static class DriverScreen
{
    public const int Width = 1920;

    /// <summary>The labels the driver uses (the same list the template test checks).</summary>
    public static readonly string[] Labels =
    [
        "iniciarEmissao", "searchSender", "CNPJ_input", "Found1", "confirmCNPJ", "searchReceiver", "Tomador1",
        "Others", "searchPayer", "Part3", "IncludeNat", "NF-e", "Nat", "Weight1", "confirmNAT", "emissionComp",
        "includeCTe", "searchCTe", "clearDate", "inputCTE", "findCTe", "selectAll", "confirmCTE", "part4",
        "valueShipping22", "icms", "CST", "ibs-cbs", "ibs-cst", "Obs2", "Obs3", "payerObs", "include_tp_info",
        "confirm_tp_info", "zeroValue", "confirmValue", "finalizeEmission", "creditconfirmPopUp", "secondPopUp",
        "thirdPopUp", "fourthPopUp", "contingencia", "fifthPopUp", "clicarCte", "2consultas",
        "10ClicarBordaInferior", "4Localizar", "5duploClickStatus", "5.1ct-e", "5.2Cancelar_CTE", "8confirmar",
        "9-sucesso", "cancelled", "icms_start", "icms_document", "icms_emission_date", "icms_payment_date",
        "icms_state", "icms_cost_center", "icms_payment", "icms_bank", "icms_barcode", "icms_obs", "icms_confirm",
    ];

    /// <summary>Where each label goes: rows of tiles, in the order of <see cref="Labels"/>.</summary>
    public static Dictionary<string, Rect> Layout(TemplateStore templates, out int height)
    {
        var places = new Dictionary<string, Rect>();
        int x = 20, y = 20, row = 0;
        foreach (var label in Labels)
        {
            var image = templates.Image(label) ?? throw new InvalidOperationException($"sem imagem para '{label}'");
            if (x + image.Width + 20 > Width)
            {
                x = 20;
                y += row + 30;
                row = 0;
            }
            places[label] = new Rect(x, y, image.Width, image.Height);
            x += image.Width + 30;
            row = Math.Max(row, image.Height);
        }
        height = y + row + 20;
        return places;
    }

    /// <summary>A screen with every label on it except the ones named.</summary>
    public static FakeScreen Build(TemplateStore templates, params string[] without)
    {
        var places = Layout(templates, out var height);
        var screen = new FakeScreen(Width, height);
        foreach (var (label, place) in places)
            if (!without.Contains(label)) screen.Show(templates.Image(label)!, place.X, place.Y);
        return screen;
    }
}
