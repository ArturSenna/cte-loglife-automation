using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace CteLogLife.Bsoft;

/// <summary>A recorded action, with clicks stored relative to where the screen's picture is found.</summary>
public sealed record LearnedStep(RecordedKind Kind, int Dx = 0, int Dy = 0, int Key = 0, bool Alt = false,
    bool Shift = false, int DelayMs = 0);

/// <summary>
/// A screen the operator handled once: its picture (a PNG beside the index, named by <see cref="Id"/>)
/// and what was done on it.
/// </summary>
public sealed record LearnedScreen
{
    public string Id { get; init; } = "";
    public DateTime Learned { get; init; }

    /// <summary>The control the bot was waiting for when the screen first showed up.</summary>
    public string WaitingFor { get; init; } = "";

    public int Width { get; init; }
    public int Height { get; init; }
    public IReadOnlyList<LearnedStep> Steps { get; init; } = [];
    public int Uses { get; init; }
    public DateTime? LastUsed { get; init; }
}

/// <summary>The learned screens of this computer, kept in a folder: "telas.json" plus one PNG each.</summary>
public sealed class LearnedScreenStore : IDisposable
{
    private const string IndexName = "telas.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _lock = new();
    private readonly string _folder;
    private readonly List<(LearnedScreen Screen, Mat Picture)> _screens = [];
    private bool _loaded;

    public LearnedScreenStore(string folder) => _folder = folder;

    public string Folder => _folder;

    /// <summary>Raised after a screen is added, used or forgotten.</summary>
    public event Action? Changed;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                Load();
                return _screens.Count;
            }
        }
    }

    /// <summary>The screens with their pictures. The pictures belong to the store: do not dispose them.</summary>
    public IReadOnlyList<(LearnedScreen Screen, Mat Picture)> Snapshot()
    {
        lock (_lock)
        {
            Load();
            return _screens.ToArray();
        }
    }

    public LearnedScreen Add(LearnedScreen screen, Mat picture)
    {
        lock (_lock)
        {
            Load();
            Directory.CreateDirectory(_folder);
            Cv2.ImEncode(".png", picture, out var png);
            File.WriteAllBytes(PicturePath(screen.Id), png);
            _screens.Add((screen, picture.Clone()));
            SaveIndex();
        }
        Changed?.Invoke();
        return screen;
    }

    public void MarkUsed(string id, DateTime when)
    {
        lock (_lock)
        {
            var index = _screens.FindIndex(entry => entry.Screen.Id == id);
            if (index < 0) return;
            var (screen, picture) = _screens[index];
            _screens[index] = (screen with { Uses = screen.Uses + 1, LastUsed = when }, picture);
            SaveIndex();
        }
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        lock (_lock)
        {
            var index = _screens.FindIndex(entry => entry.Screen.Id == id);
            if (index < 0) return;
            _screens[index].Picture.Dispose();
            _screens.RemoveAt(index);
            TryDelete(PicturePath(id));
            SaveIndex();
        }
        Changed?.Invoke();
    }

    /// <summary>Forgets every learned screen.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            Load();
            foreach (var (screen, picture) in _screens)
            {
                picture.Dispose();
                TryDelete(PicturePath(screen.Id));
            }
            _screens.Clear();
            SaveIndex();
        }
        Changed?.Invoke();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var (_, picture) in _screens) picture.Dispose();
            _screens.Clear();
            _loaded = true;
        }
    }

    private string PicturePath(string id) => Path.Combine(_folder, id + ".png");

    /// <summary>Reads the index once. A damaged index or a missing picture only loses that screen.</summary>
    private void Load()
    {
        if (_loaded) return;
        _loaded = true;
        var index = Path.Combine(_folder, IndexName);
        if (!File.Exists(index)) return;
        List<LearnedScreen>? listed;
        try
        {
            listed = JsonSerializer.Deserialize<List<LearnedScreen>>(File.ReadAllText(index), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return;
        }
        foreach (var screen in listed ?? [])
        {
            if (screen.Id.Length == 0 || screen.Steps.Count == 0) continue;
            var path = PicturePath(screen.Id);
            if (!File.Exists(path)) continue;
            // Decoding from memory keeps paths with accents working.
            var picture = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
            if (picture.Empty())
            {
                picture.Dispose();
                continue;
            }
            _screens.Add((screen, picture));
        }
    }

    private void SaveIndex()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, IndexName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_screens.Select(entry => entry.Screen).ToList(), Options));
        File.Move(temp, path, overwrite: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Finds what an unknown screen was by comparing the capture taken when the bot got stuck with the one
/// taken after the operator solved it: the part that went away is the screen (a dialog, a warning).
/// </summary>
public static class ScreenChange
{
    private const int PixelThreshold = 18;
    private const int JoinGap = 40;
    private const int MinWidth = 80, MinHeight = 40;
    private const double MaxScreenShare = 0.45;

    /// <summary>How far outside the changed area a recorded click may be and still belong to it.</summary>
    public const int ClickMargin = 150;

    /// <summary>
    /// The area that changed around <paramref name="anchor"/> (the operator's first click), or the
    /// largest changed area when there was no click. Null with the reason when no area identifies
    /// the screen well enough.
    /// </summary>
    public static (Rect? Area, string? Reason) Find(Mat before, Mat after, Point? anchor)
    {
        if (before.Width != after.Width || before.Height != after.Height)
            return (null, "a resolução da tela mudou");

        // A pixel changed when any of its three colours did.
        using var difference = new Mat();
        Cv2.Absdiff(before, after, difference);
        using var strongest = new Mat();
        Cv2.ExtractChannel(difference, strongest, 0);
        for (var channel = 1; channel < 3; channel++)
        {
            using var one = new Mat();
            Cv2.ExtractChannel(difference, one, channel);
            Cv2.Max(strongest, one, strongest);
        }
        using var mask = new Mat();
        Cv2.Threshold(strongest, mask, PixelThreshold, 255, ThresholdTypes.Binary);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 9));
        using var joined = new Mat();
        Cv2.MorphologyEx(mask, joined, MorphTypes.Close, kernel);
        Cv2.FindContours(joined, out var contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var pieces = contours.Select(Cv2.BoundingRect).Where(r => r.Width >= 3 && r.Height >= 3).ToList();
        if (pieces.Count == 0) return (null, "nada mudou na tela depois da ação");

        Rect seed;
        if (anchor is { } point)
        {
            var under = pieces.Where(r => Grow(r, 8).Contains(point)).OrderByDescending(Area).ToList();
            if (under.Count == 0) return (null, "o clique não foi sobre algo que saiu da tela");
            seed = under[0];
        }
        else
        {
            seed = pieces.OrderByDescending(Area).First();
        }

        // A dialog shows up as several pieces (frame, text, buttons): join the ones close to each other.
        var area = seed;
        var rest = pieces.Where(r => r != seed).ToList();
        for (var grew = true; grew;)
        {
            grew = false;
            for (var i = rest.Count - 1; i >= 0; i--)
            {
                if (!Grow(area, JoinGap).IntersectsWith(rest[i])) continue;
                area = area.Union(rest[i]);
                rest.RemoveAt(i);
                grew = true;
            }
        }

        // Without a click there is nothing saying which change was the screen: it has to stand out.
        if (anchor is null && rest.Any(r => Area(r) * 3 > Area(area)))
            return (null, "mais de uma parte da tela mudou e não houve clique para indicar qual era a tela");

        area = Grow(area, 4).Intersect(new Rect(0, 0, before.Width, before.Height));
        if (area.Width < MinWidth || area.Height < MinHeight)
            return (null, "a parte da tela que mudou é pequena demais para reconhecê-la com segurança");
        if (Area(area) > MaxScreenShare * before.Width * before.Height)
            return (null, "a tela inteira mudou, não só um aviso");
        return (area, null);
    }

    private static long Area(Rect rect) => (long)rect.Width * rect.Height;

    private static Rect Grow(Rect rect, int by) =>
        new(rect.X - by, rect.Y - by, rect.Width + 2 * by, rect.Height + 2 * by);
}
