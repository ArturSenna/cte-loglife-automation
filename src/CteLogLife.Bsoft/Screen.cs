using System.Runtime.InteropServices;
using OpenCvSharp;

namespace CteLogLife.Bsoft;

/// <summary>Where a template was found on screen, in physical pixels.</summary>
public readonly record struct ScreenBox(int Left, int Top, int Width, int Height)
{
    public double CenterX => Left + Width / 2.0;
    public double CenterY => Top + Height / 2.0;
}

public interface IScreenSource : IDisposable
{
    /// <summary>The primary screen as a BGR image. The caller disposes it.</summary>
    Mat Capture();
}

/// <summary>Captures the primary monitor through GDI, reusing one bitmap between captures.</summary>
public sealed class ScreenGrabber : IScreenSource
{
    private readonly int _width;
    private readonly int _height;
    private readonly IntPtr _memoryDc;
    private readonly IntPtr _bitmap;
    private readonly IntPtr _previous;
    private readonly IntPtr _bits;

    public ScreenGrabber()
    {
        _width = Native.GetSystemMetrics(Native.SmCxScreen);
        _height = Native.GetSystemMetrics(Native.SmCyScreen);
        var screen = Native.GetDC(IntPtr.Zero);
        try
        {
            _memoryDc = Native.CreateCompatibleDC(screen);
            var header = new Native.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                biWidth = _width,
                biHeight = -_height, // top-down rows
                biPlanes = 1,
                biBitCount = 32,
            };
            _bitmap = Native.CreateDIBSection(screen, ref header, 0, out _bits, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero) throw new InvalidOperationException("Não foi possível preparar a captura de tela.");
            _previous = Native.SelectObject(_memoryDc, _bitmap);
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    public Mat Capture()
    {
        var screen = Native.GetDC(IntPtr.Zero);
        try
        {
            if (!Native.BitBlt(_memoryDc, 0, 0, _width, _height, screen, 0, 0, Native.SrcCopy))
                throw new InvalidOperationException(
                    "A captura de tela falhou. A sessão do Windows está bloqueada ou desconectada?");
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, screen);
        }

        using var bgra = Mat.FromPixelData(_height, _width, MatType.CV_8UC4, _bits);
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    public void Dispose()
    {
        Native.SelectObject(_memoryDc, _previous);
        Native.DeleteObject(_bitmap);
        Native.DeleteDC(_memoryDc);
    }
}

/// <summary>
/// The screenshots of Bsoft's controls, looked up by label. A label may have alternates named
/// "label-alt", "label-alt2", ... for other looks of the same control.
/// </summary>
public sealed class TemplateStore : IDisposable
{
    private const string AltSuffix = "-alt";

    private readonly string _directory;
    private readonly string[] _files;
    private readonly Dictionary<string, Mat?> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string[]> _candidates = new(StringComparer.OrdinalIgnoreCase);

    public TemplateStore(string directory)
    {
        _directory = directory;
        _files = Directory.Exists(directory)
            ? Directory.GetFiles(directory).Select(Path.GetFileName).OfType<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    public string Directory_ => _directory;
    public int Count => _files.Length;

    /// <summary>The label followed by its alternates that exist on disk.</summary>
    public IReadOnlyList<string> Candidates(string label)
    {
        if (_candidates.TryGetValue(label, out var cached)) return cached;
        var list = new List<string> { label };
        for (var n = 1; ; n++)
        {
            var alternate = $"{label}{AltSuffix}{(n > 1 ? n : "")}";
            if (FileFor(alternate) is null) break;
            list.Add(alternate);
        }
        return _candidates[label] = list.ToArray();
    }

    /// <summary>The image for a label (any extension), or null when there is no such file.</summary>
    public Mat? Image(string label)
    {
        if (_images.TryGetValue(label, out var cached)) return cached;
        Mat? image = null;
        if (FileFor(label) is { } file)
        {
            // Decoding from memory keeps paths with accents working.
            image = Cv2.ImDecode(File.ReadAllBytes(Path.Combine(_directory, file)), ImreadModes.Color);
            if (image.Empty())
            {
                image.Dispose();
                image = null;
            }
        }
        return _images[label] = image;
    }

    private string? FileFor(string label)
    {
        var prefix = label + ".";
        return _files.FirstOrDefault(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        foreach (var image in _images.Values) image?.Dispose();
        _images.Clear();
    }
}

public static class TemplateMatcher
{
    /// <summary>
    /// The best place where <paramref name="template"/> appears in <paramref name="screen"/>, when its
    /// normalized correlation is above <paramref name="matching"/>.
    /// </summary>
    public static ScreenBox? Find(Mat screen, Mat template, double matching)
    {
        if (template.Width > screen.Width || template.Height > screen.Height) return null;
        using var result = new Mat();
        Cv2.MatchTemplate(screen, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var best, out _, out var location);
        return best > matching ? new ScreenBox(location.X, location.Y, template.Width, template.Height) : null;
    }
}
