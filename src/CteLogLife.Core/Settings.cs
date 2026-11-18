using System.Text;
using System.Text.Json;
using CteLogLife.Core.Api;

namespace CteLogLife.Core;

/// <summary>Everything the operator configures. Saved as JSON under %LOCALAPPDATA%\CTe LogLife.</summary>
public sealed class AppSettings
{
    public const string DefaultPayerColumn = "Pagador do Frete - CNPJ/CPF";

    /// <summary>Folder for the normal/unified CT-e spreadsheets ("CTe normal").</summary>
    public string NormalFolder { get; set; } = "";

    /// <summary>Folder for the complementary CT-e spreadsheets ("CTe complementar").</summary>
    public string ComplementaryFolder { get; set; } = "";

    /// <summary>Folder where Bsoft saves the CT-e PDFs ("Pasta CTE").</summary>
    public string CtePdfFolder { get; set; } = "";

    public string BsoftReportPath { get; set; } = "";
    public string TargetReportPath { get; set; } = "";
    public string PayerColumn { get; set; } = DefaultPayerColumn;

    public bool OverlayOnLeft { get; set; }

    /// <summary>
    /// Keeps the overlay out of screen captures so it can never hide a Bsoft element from the image
    /// matcher. Remote-desktop viewers do not see it either while this is on.
    /// </summary>
    public bool HideOverlayFromCapture { get; set; } = true;

    /// <summary>How long to wait for Bsoft to write a CT-e PDF before the emission is parked as pending.</summary>
    public int PdfWaitSeconds { get; set; } = 300;

    /// <summary>
    /// When the bot stops at a screen it does not know, record what the operator does there and repeat
    /// it the next time the same screen shows up. Off: the bot still stops, captures and waits.
    /// </summary>
    public bool LearnUnknownScreens { get; set; } = true;

    public bool CheckUpdatesOnStartup { get; set; } = true;
    public bool LegacyImported { get; set; }
}

public static class AppPaths
{
    public static string DataRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CTe LogLife");

    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public static string CredentialsFile => Path.Combine(DataRoot, "credentials.bin");
    public static string JournalFile => Path.Combine(DataRoot, "journal", "emissions.jsonl");
    public static string LogFolder => Path.Combine(DataRoot, "logs");
    public static string DebugFolder => Path.Combine(DataRoot, "debug");

    /// <summary>Screenshots taken when the bot stopped at a screen it did not know.</summary>
    public static string UnknownScreensFolder => Path.Combine(DataRoot, "telas-desconhecidas");

    /// <summary>The screens the operator taught the bot to handle, with the recorded action of each.</summary>
    public static string LearnedScreensFolder => Path.Combine(DataRoot, "telas-aprendidas");
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string? path = null)
    {
        path ??= AppPaths.SettingsFile;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged settings file must not keep the app from starting.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= AppPaths.SettingsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// One-time import of the Python app's loose config files (filename.txt, folderpath.txt, ...),
    /// looked up in the given folders. Returns true when something was imported.
    /// </summary>
    public static bool ImportLegacy(AppSettings settings, IEnumerable<string> folders)
    {
        var imported = false;
        foreach (var folder in folders.Where(Directory.Exists))
        {
            imported |= Import(folder, "folderpath.txt", settings.NormalFolder, v => settings.NormalFolder = v);
            imported |= Import(folder, "folderpath2.txt", settings.ComplementaryFolder, v => settings.ComplementaryFolder = v);
            imported |= Import(folder, "filename.txt", settings.CtePdfFolder, v => settings.CtePdfFolder = v);
            imported |= Import(folder, "relatorio_bsoft.txt", settings.BsoftReportPath, v => settings.BsoftReportPath = v);
            imported |= Import(folder, "relatorio_target.txt", settings.TargetReportPath, v => settings.TargetReportPath = v);
            imported |= Import(folder, "conferencia_tomador_column.txt",
                settings.PayerColumn == AppSettings.DefaultPayerColumn ? "" : settings.PayerColumn,
                v => settings.PayerColumn = v);
        }
        return imported;
    }

    private static bool Import(string folder, string file, string current, Action<string> assign)
    {
        if (!string.IsNullOrWhiteSpace(current)) return false;
        var path = Path.Combine(folder, file);
        if (!File.Exists(path)) return false;
        var value = ReadFirstLine(path);
        if (value.Length == 0) return false;
        assign(value.Replace('/', '\\'));
        return true;
    }

    private static string ReadFirstLine(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(1252).GetString(bytes);
        }
        return text.Split('\n')[0].Trim().Trim('﻿');
    }
}

/// <summary>Reads LOGLIFE_USER / LOGLIFE_PASSWORD from a .env file (the Python app's format).</summary>
public sealed class EnvFileCredentialSource(string path) : ICredentialSource
{
    public ApiCredentials? Load()
    {
        if (!File.Exists(path)) return null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
                value = value[1..^1];
            values[key] = value;
        }
        return values.TryGetValue("LOGLIFE_USER", out var user) &&
               values.TryGetValue("LOGLIFE_PASSWORD", out var password) &&
               user.Length > 0 && password.Length > 0
            ? new ApiCredentials(user, password)
            : null;
    }
}
