using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CteLogLife.Bsoft;
using CteLogLife.Core;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Flows;
using CteLogLife.Core.Journal;
using CteLogLife.Core.Tax;

namespace CteLogLife.App;

/// <summary>The LogLife login, encrypted for the current Windows user (DPAPI).</summary>
public sealed class DpapiCredentialStore(string path) : ICredentialSource
{
    private static readonly byte[] Entropy = "CTe LogLife credentials v1"u8.ToArray();

    public ApiCredentials? Load()
    {
        try
        {
            if (!File.Exists(path)) return null;
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<ApiCredentials>(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            return null;
        }
    }

    public void Save(ApiCredentials credentials)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(credentials);
        File.WriteAllBytes(path, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
    }
}

public sealed record LogEntry(DateTime Time, string Level, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");
    public string Glyph => Level switch { "ERRO" => "", "AVISO" => "", _ => "" };
}

/// <summary>Activity log: shown in the window and appended to a daily file.</summary>
public sealed class UiLog(Dispatcher dispatcher) : IRunLog
{
    private const int MaxEntries = 400;
    private readonly object _fileLock = new();

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public void Info(string message) => Add("INFO", message);
    public void Warn(string message) => Add("AVISO", message);
    public void Error(string message) => Add("ERRO", message);

    private void Add(string level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);
        try
        {
            lock (_fileLock)
            {
                Directory.CreateDirectory(AppPaths.LogFolder);
                File.AppendAllText(Path.Combine(AppPaths.LogFolder, $"{entry.Time:yyyy-MM-dd}.log"),
                    $"{entry.Time:HH:mm:ss} {level,-5} {message.Replace("\n", " | ")}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The on-screen log still works.
        }

        dispatcher.BeginInvoke(() =>
        {
            Entries.Add(entry);
            while (Entries.Count > MaxEntries) Entries.RemoveAt(0);
        });
    }
}

/// <summary>Questions and notices for the person at the screen, raised from the bot's thread.</summary>
public sealed class DialogOperator(Dispatcher dispatcher, IRunLog log) : IOperator
{
    private readonly List<string> _notices = [];

    public string? AskText(string title, string message, string? prefill = null) =>
        dispatcher.Invoke(() => MessageDialog.Prompt(null, title, message, prefill));

    public void Notice(string message)
    {
        log.Warn(message);
        lock (_notices) _notices.Add(message);
    }

    /// <summary>The notices raised since the last call, for the closing message of a run.</summary>
    public IReadOnlyList<string> TakeNotices()
    {
        lock (_notices)
        {
            var taken = _notices.ToArray();
            _notices.Clear();
            return taken;
        }
    }
}

/// <summary>Builds and owns everything the window needs: settings, API client, journal, run state.</summary>
public sealed class AppHost : IDisposable
{
    private readonly List<BsoftDriver> _drivers = [];
    private readonly Lazy<TaxTables> _tax;
    private readonly Lazy<TemplateStore> _templates;

    public AppHost(Dispatcher dispatcher)
    {
        Directory.CreateDirectory(AppPaths.DataRoot);
        Settings = SettingsStore.Load();
        Log = new UiLog(dispatcher);
        Operator = new DialogOperator(dispatcher, Log);
        Credentials = new DpapiCredentialStore(AppPaths.CredentialsFile);
        Api = new LogLifeApi(Credentials);
        Journal = new EmissionJournal(AppPaths.JournalFile);
        LearnedScreens = new LearnedScreenStore(AppPaths.LearnedScreensFolder);
        _tax =new Lazy<TaxTables>(() => TaxTables.Load(Path.Combine(AppContext.BaseDirectory, "Data")));
        _templates = new Lazy<TemplateStore>(() => new TemplateStore(Path.Combine(AppContext.BaseDirectory, "Templates")));

        Context = new FlowContext
        {
            Api = Api,
            Tax = () => _tax.Value,
            CreateDriver = CreateDriver,
            Journal = Journal,
            Operator = Operator,
            Log = Log,
            Control = Control,
            Queue = Queue,
            Settings = Settings,
        };
    }

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    public AppSettings Settings { get; }
    public UiLog Log { get; }
    public DialogOperator Operator { get; }
    public DpapiCredentialStore Credentials { get; }
    public LogLifeApi Api { get; }
    public EmissionJournal Journal { get; }

    /// <summary>The unknown screens the operator already taught the bot to handle.</summary>
    public LearnedScreenStore LearnedScreens { get; }

    public RunControl Control { get; } = new();
    public RunQueue Queue { get; } = new();
    public FlowContext Context { get; }

    public void SaveSettings()
    {
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Não foi possível salvar as configurações: {ex.Message}");
        }
    }

    /// <summary>
    /// First start after the Python app: brings over its folder choices and its .env login, looked up
    /// next to the exe, in the working folder and a few levels above.
    /// </summary>
    public void ImportLegacy()
    {
        var folders = new List<string> { Environment.CurrentDirectory, AppPaths.DataRoot };
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 7 && directory is not null; depth++, directory = directory.Parent)
            folders.Add(directory.FullName);
        folders = folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (!Settings.LegacyImported)
        {
            if (SettingsStore.ImportLegacy(Settings, folders))
                Log.Info("Pastas e relatórios importados da versão anterior.");
            Settings.LegacyImported = true;
            SaveSettings();
        }

        if (Credentials.Load() is not null) return;
        foreach (var folder in folders)
        {
            if (new EnvFileCredentialSource(Path.Combine(folder, ".env")).Load() is not { } found) continue;
            Credentials.Save(found);
            Log.Info("Login do sistema LogLife importado do arquivo .env da versão anterior.");
            return;
        }
    }

    private IBsoftDriver CreateDriver()
    {
        var templates = _templates.Value;
        if (templates.Count == 0)
            throw new FlowException(
                $"A pasta de imagens do Bsoft está vazia ou não existe: {Path.Combine(AppContext.BaseDirectory, "Templates")}");
        var unknown = new UnknownScreens(LearnedScreens, Control, Log, AppPaths.UnknownScreensFolder)
        {
            Learn = Settings.LearnUnknownScreens,
        };
        var driver = new BsoftDriver(new DesktopBot(templates, new ScreenGrabber(), Control, Log, unknown), Log);
        lock (_drivers) _drivers.Add(driver);
        return driver;
    }

    /// <summary>Releases what the bot held during a run (screen capture buffers).</summary>
    public void EndRun()
    {
        lock (_drivers)
        {
            foreach (var driver in _drivers) driver.Dispose();
            _drivers.Clear();
        }
    }

    public void Dispose()
    {
        EndRun();
        Api.Dispose();
        LearnedScreens.Dispose();
        if (_templates.IsValueCreated) _templates.Value.Dispose();
    }
}

/// <summary>Small helpers shared by the windows.</summary>
public static class Ui
{
    public static Window? MainWindow => Application.Current?.MainWindow;

    /// <summary>The newline-joined notices as a block to append to a result message.</summary>
    public static string WithNotices(string message, IReadOnlyList<string> notices) =>
        notices.Count == 0 ? message : message + "\n\nAvisos:\n" + string.Join("\n", notices.Select(n => "• " + n));
}
