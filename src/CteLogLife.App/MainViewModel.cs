using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CteLogLife.Core;
using CteLogLife.Core.Api;
using CteLogLife.Core.Flows;
using CteLogLife.Core.Journal;

namespace CteLogLife.App;

public enum AppPage { Emission, Complementary, ByUf, Cancel, Tools, Pending, Settings }

/// <summary>What the view model needs from the windows around it.</summary>
public interface IRunWindows
{
    /// <summary>Gets the main window out of the way and shows the overlay with the pause shortcuts.</summary>
    void BeginBotRun();

    void EndBotRun();
    void ShowResult(string title, string message);
    bool Confirm(string title, string message, string confirmText);
    string? Prompt(string title, string message, string? prefill);
    string? PickFolder(string title, string current);
    string? PickFile(string title, string current);
    void OpenConference();
    void Close();
}

/// <summary>An emission that was sent but is not fully recorded, as shown on the Pendências page.</summary>
public sealed record PendingItem(JournalEntry Entry)
{
    public string Protocol => Entry.Protocol;
    public string Number => Entry.CteNumber?.ToString(CultureInfo.InvariantCulture) ?? "não informado";
    public string When => Entry.Time.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public string Kind => Entry.Plan.Kind == CteKind.Complementary ? "Complementar"
        : Entry.Plan.Symbolic ? "Simbólico" : "Normal";
    public bool HasNumber => Entry.CteNumber is not null;
    public bool NeedsNumber => Entry.CteNumber is null;

    public string Stage => Entry.Stage switch
    {
        EmissionStage.Transmitting => "Enviado ao Bsoft, sem confirmação",
        EmissionStage.NumberCaptured => "Emitido; falta registrar no sistema",
        EmissionStage.Reported => "Registrado; falta enviar o PDF",
        EmissionStage.PdfUploaded => "PDF enviado; falta associar ao protocolo",
        EmissionStage.Uncertain => "Sem confirmação: confira no Bsoft",
        _ => Entry.Stage.ToString(),
    };
}

public partial class MainViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly IRunWindows _windows;
    private CancellationTokenSource? _cancellation;

    public MainViewModel(AppHost host, IRunWindows windows)
    {
        _host = host;
        _windows = windows;

        var settings = host.Settings;
        _normalFolder = settings.NormalFolder;
        _complementaryFolder = settings.ComplementaryFolder;
        _ctePdfFolder = settings.CtePdfFolder;
        _bsoftReportPath = settings.BsoftReportPath;
        _targetReportPath = settings.TargetReportPath;
        _overlayOnLeft = settings.OverlayOnLeft;
        _hideOverlayFromCapture = settings.HideOverlayFromCapture;
        _checkUpdatesOnStartup = settings.CheckUpdatesOnStartup;
        _pdfWaitSeconds = settings.PdfWaitSeconds.ToString(CultureInfo.InvariantCulture);
        _learnUnknownScreens = settings.LearnUnknownScreens;
        host.LearnedScreens.Changed += () =>
            App.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(LearnedScreensText)));

        var credentials = host.Credentials.Load();
        _apiUser = credentials?.Email ?? "";
        _hasStoredPassword = credentials is not null;

        host.Journal.Changed += () => App.Current.Dispatcher.BeginInvoke(RefreshPending);
        RefreshPending();
    }

    public string Version => AppHost.Version;
    public ObservableCollection<LogEntry> Log => _host.Log.Entries;
    public ObservableCollection<PendingItem> Pending { get; } = [];

    // ------------------------------------------------------------------ state

    [ObservableProperty] private AppPage _page = AppPage.Emission;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;

    [ObservableProperty] private string _statusText = "Pronto";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending))]
    private int _pendingCount;

    public bool HasPending => PendingCount > 0;

    // ------------------------------------------------------------------ form fields

    [ObservableProperty] private DateTime? _emissionStart = DateTime.Today;
    [ObservableProperty] private DateTime? _emissionEnd = DateTime.Today;
    [ObservableProperty] private string _singleProtocols = "";
    [ObservableProperty] private bool _singleSymbolic;
    [ObservableProperty] private string _singleVolumes = "1";

    [ObservableProperty] private DateTime? _complementaryStart = DateTime.Today;
    [ObservableProperty] private DateTime? _complementaryEnd = DateTime.Today;
    [ObservableProperty] private string _complementaryProtocols = "";

    [ObservableProperty] private DateTime? _ufStart = DateTime.Today;
    [ObservableProperty] private DateTime? _ufEnd = DateTime.Today;

    [ObservableProperty] private DateTime? _cancelStart = DateTime.Today;
    [ObservableProperty] private DateTime? _cancelEnd = DateTime.Today;
    [ObservableProperty] private string _cancelCte = "";
    [ObservableProperty] private string _cancelProtocol = "";

    // ------------------------------------------------------------------ settings

    [ObservableProperty] private string _normalFolder;
    [ObservableProperty] private string _complementaryFolder;
    [ObservableProperty] private string _ctePdfFolder;
    [ObservableProperty] private string _bsoftReportPath;
    [ObservableProperty] private string _targetReportPath;
    [ObservableProperty] private bool _overlayOnLeft;
    [ObservableProperty] private bool _hideOverlayFromCapture;
    [ObservableProperty] private bool _checkUpdatesOnStartup;
    [ObservableProperty] private string _pdfWaitSeconds;
    [ObservableProperty] private string _apiUser;
    [ObservableProperty] private bool _hasStoredPassword;

    partial void OnNormalFolderChanged(string value) => Save(s => s.NormalFolder = value);
    partial void OnComplementaryFolderChanged(string value) => Save(s => s.ComplementaryFolder = value);
    partial void OnCtePdfFolderChanged(string value) => Save(s => s.CtePdfFolder = value);
    partial void OnBsoftReportPathChanged(string value) => Save(s => s.BsoftReportPath = value);
    partial void OnTargetReportPathChanged(string value) => Save(s => s.TargetReportPath = value);
    partial void OnOverlayOnLeftChanged(bool value) => Save(s => s.OverlayOnLeft = value);
    partial void OnHideOverlayFromCaptureChanged(bool value) => Save(s => s.HideOverlayFromCapture = value);
    partial void OnCheckUpdatesOnStartupChanged(bool value) => Save(s => s.CheckUpdatesOnStartup = value);

    // ------------------------------------------------------------------ unknown screens

    [ObservableProperty] private bool _learnUnknownScreens;

    partial void OnLearnUnknownScreensChanged(bool value) => Save(s => s.LearnUnknownScreens = value);

    public string LearnedScreensText => _host.LearnedScreens.Count switch
    {
        0 => "Nenhuma tela aprendida até agora.",
        1 => "1 tela aprendida.",
        var count => $"{count} telas aprendidas.",
    };

    [RelayCommand]
    private void OpenLearnedScreens() => OpenFolder(_host.LearnedScreens.Folder);

    [RelayCommand]
    private void OpenUnknownScreenshots() => OpenFolder(AppPaths.UnknownScreensFolder);

    [RelayCommand]
    private void ForgetLearnedScreens()
    {
        if (IsBusy || _host.LearnedScreens.Count == 0) return;
        if (!_windows.Confirm("Esquecer telas aprendidas",
                "Apagar todas as telas aprendidas?\n\nO robô volta a parar e perguntar quando elas aparecerem.",
                "Esquecer todas"))
            return;
        _host.LearnedScreens.Clear();
        _host.Log.Info("Telas aprendidas apagadas pelo operador.");
    }

    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _windows.ShowResult("Abrir pasta", $"❌ Não foi possível abrir {folder}:\n{ex.Message}");
        }
    }

    partial void OnPdfWaitSecondsChanged(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds is >= 5 and <= 3600)
            Save(s => s.PdfWaitSeconds = seconds);
    }

    private void Save(Action<AppSettings> change)
    {
        change(_host.Settings);
        _host.SaveSettings();
    }

    public string BsoftReportName => FileLabel(BsoftReportPath, "nenhum arquivo selecionado");
    public string TargetReportName => FileLabel(TargetReportPath, "nenhum arquivo selecionado");

    private static string FileLabel(string path, string empty) =>
        string.IsNullOrWhiteSpace(path) ? empty : Path.GetFileName(path);

    [RelayCommand]
    private void Browse(string target)
    {
        switch (target)
        {
            case "normal":
                if (_windows.PickFolder("Pasta para CT-e normal", NormalFolder) is { } normal) NormalFolder = normal;
                break;
            case "complementary":
                if (_windows.PickFolder("Pasta para CT-e complementar", ComplementaryFolder) is { } complementary)
                    ComplementaryFolder = complementary;
                break;
            case "pdf":
                if (_windows.PickFolder("Pasta onde o Bsoft salva os PDFs dos CT-es", CtePdfFolder) is { } pdf)
                    CtePdfFolder = pdf;
                break;
            case "bsoft":
                if (_windows.PickFile("Relatório B-soft", BsoftReportPath) is { } bsoft) BsoftReportPath = bsoft;
                OnPropertyChanged(nameof(BsoftReportName));
                break;
            case "target":
                if (_windows.PickFile("Relatório Target", TargetReportPath) is { } targetFile) TargetReportPath = targetFile;
                OnPropertyChanged(nameof(TargetReportName));
                break;
        }
    }

    /// <summary>Stores the login. An empty password keeps the one already saved.</summary>
    public void SaveCredentials(string password)
    {
        var existing = _host.Credentials.Load();
        var effective = password.Length > 0 ? password : existing?.Password ?? "";
        if (ApiUser.Trim().Length == 0 || effective.Length == 0)
        {
            _windows.ShowResult("Acesso ao sistema LogLife", "⚠️ Informe usuário e senha.");
            return;
        }
        _host.Credentials.Save(new ApiCredentials(ApiUser.Trim(), effective));
        _host.Api.ForgetSession();
        HasStoredPassword = true;
        _host.Log.Info("Login do sistema LogLife atualizado.");
        _windows.ShowResult("Acesso ao sistema LogLife", "✅ Login salvo neste computador, protegido pela sua conta do Windows.");
    }

    // ------------------------------------------------------------------ running things

    private DateOnly Day(DateTime? value) => DateOnly.FromDateTime(value ?? DateTime.Today);
    private EmissionFlows Emission => new(_host.Context);
    private MaintenanceFlows Maintenance => new(_host.Context);

    /// <summary>Runs a flow off the UI thread and shows its closing message.</summary>
    private async Task RunAsync(string title, bool usesBot, Func<CancellationToken, Task<string>> work)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = title + "…";
        _host.Control.Reset();
        _host.Operator.TakeNotices();
        _cancellation = new CancellationTokenSource();
        _host.Log.Info($"Iniciando: {title}");
        if (usesBot) _windows.BeginBotRun();

        string message;
        try
        {
            var token = _cancellation.Token;
            message = await Task.Run(() => work(token));
        }
        catch (OperationCanceledException)
        {
            message = "Operação interrompida.";
        }
        catch (Exception ex) when (ex is FlowException or ApiException or InvalidDataException or IOException)
        {
            message = "❌ " + ex.Message;
        }
        catch (Exception ex)
        {
            _host.Log.Error(ex.ToString());
            message = "❌ Erro inesperado: " + ex.Message;
        }
        finally
        {
            _host.EndRun();
            if (usesBot) _windows.EndBotRun();
            _host.Queue.Clear();
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
            StatusText = "Pronto";
            RefreshPending();
        }

        _host.Log.Info($"{title}: {message.Split('\n')[0]}");
        _windows.ShowResult(title, Ui.WithNotices(message, _host.Operator.TakeNotices()));
    }

    [RelayCommand]
    private void TogglePause() => _host.Control.TogglePause();

    [RelayCommand]
    private void Stop()
    {
        _host.Control.RequestStop();
        _cancellation?.Cancel();
    }

    // ------------------------------------------------------------------ emission

    [RelayCommand]
    private Task EmitUnified() => RunAsync("Emissão unificada", true,
        ct => Emission.UnifiedAsync(Day(EmissionStart), Day(EmissionEnd), ct));

    [RelayCommand]
    private Task EmitSingle() => RunAsync("Emissão avulsa", true,
        ct => Emission.SingleAsync(Day(EmissionStart), SingleSymbolic, SingleProtocols, SingleVolumes, ct));

    [RelayCommand]
    private Task EmitSymbolic() => RunAsync("CT-e simbólico", true,
        ct => Emission.SymbolicAsync(Day(EmissionStart), Day(EmissionEnd), ct));

    [RelayCommand]
    private Task EmitComplementary() => RunAsync("CT-e complementar", true,
        ct => Emission.ComplementaryAsync(Day(ComplementaryStart), Day(ComplementaryEnd), null, ct));

    [RelayCommand]
    private Task EmitComplementarySingle() => RunAsync("CT-e complementar avulso", true,
        ct => Emission.ComplementaryAsync(Day(ComplementaryStart), Day(ComplementaryEnd), ComplementaryProtocols, ct));

    [RelayCommand]
    private Task EmitByUf() => RunAsync("CT-e por UF", true,
        ct => Emission.GroupedByUfAsync(Day(UfStart), Day(UfEnd), ct));

    // ------------------------------------------------------------------ cancellation and utilities

    [RelayCommand]
    private Task CancelBatch()
    {
        if (!_windows.Confirm("Confirmar cancelamento",
                "Tem certeza que deseja cancelar a lista de CTes?\n\nEsta ação não pode ser desfeita.", "Cancelar CT-es"))
            return Task.CompletedTask;
        return RunAsync("Cancelamento em lote", true,
            ct => Maintenance.CancelBatchAsync(Day(CancelStart), Day(CancelEnd), ct));
    }

    [RelayCommand]
    private Task CancelSingle()
    {
        if (!_windows.Confirm("Confirmar cancelamento",
                $"Tem certeza que deseja cancelar o CTe {CancelCte}?\n\nEsta ação não pode ser desfeita.", "Cancelar CT-e"))
            return Task.CompletedTask;
        return RunAsync("Cancelamento avulso", true,
            ct => Maintenance.CancelSingleAsync(CancelCte, CancelProtocol, ct));
    }

    [RelayCommand]
    private Task ClearSymbolic()
    {
        if (!_windows.Confirm("Confirmar limpeza",
                "Tem certeza que deseja limpar os CTe's simbólicos?\n\nEsta ação não pode ser desfeita.", "Limpar"))
            return Task.CompletedTask;
        return RunAsync("Limpar CT-e simbólico", false,
            ct => Maintenance.ClearSymbolicAsync(Day(EmissionStart), Day(EmissionEnd), ct));
    }

    [RelayCommand]
    private Task ValidateAndCancel()
    {
        if (!_windows.Confirm("Confirmar cancelamento",
                "Tem certeza que deseja validar e cancelar os CTes?\n\nEsta ação não pode ser desfeita.", "Validar e cancelar"))
            return Task.CompletedTask;
        return RunAsync("Validar e cancelar (B-soft)", true, ct => Maintenance.ValidateAndCancelAsync(ct));
    }

    [RelayCommand]
    private Task CompareGnre() => RunAsync("Comparar GNRE", false, ct => Maintenance.CompareGnreAsync(ct));

    [RelayCommand]
    private Task EnterSlips() => RunAsync("Lançar guias de ICMS", true, ct => Maintenance.EnterIcmsSlipsAsync(ct));

    [RelayCommand]
    private void OpenConference() => _windows.OpenConference();

    /// <summary>The payer conference: only reads the spreadsheet and the system.</summary>
    public async Task<PayerConferenceResult?> ConferPayersAsync(string file, string column)
    {
        if (IsBusy) return null;
        IsBusy = true;
        StatusText = "Conferindo tomadores…";
        try
        {
            var result = await Task.Run(() => Maintenance.ConferPayersAsync(file, column, CancellationToken.None));
            _host.Log.Info($"Conferência de tomador: {result.Inconsistencies.Count} divergência(s) em {result.CheckedRows} linha(s).");
            return result;
        }
        finally
        {
            IsBusy = false;
            StatusText = "Pronto";
        }
    }

    public Task CancelFromConferenceAsync(IReadOnlyList<string> cteNumbers) =>
        RunAsync("Cancelamento da conferência", true, ct => Maintenance.CancelSelectedAsync(cteNumbers, ct));

    public string PayerColumn
    {
        get => _host.Settings.PayerColumn;
        set => Save(s => s.PayerColumn = value);
    }

    // ------------------------------------------------------------------ pending emissions

    private void RefreshPending()
    {
        Pending.Clear();
        foreach (var entry in _host.Journal.Open().Reverse()) Pending.Add(new PendingItem(entry));
        PendingCount = Pending.Count;
    }

    [RelayCommand]
    private Task RetryPending(PendingItem item) => FinishPendingAsync(item.Entry);

    private Task FinishPendingAsync(JournalEntry entry) =>
        RunAsync($"Pendência do protocolo {entry.Protocol}", false, async ct =>
        {
            await new PostEmission(_host.Context).RunAsync(entry, null, ct);
            return $"✅ CT-e {entry.CteNumber} do protocolo {entry.Protocol} registrado no sistema.";
        });

    [RelayCommand]
    private Task SetPendingNumber(PendingItem item)
    {
        var typed = _windows.Prompt("Número do CT-e",
            $"Digite o número do CT-e emitido para o protocolo {item.Protocol}, como aparece no Bsoft.", null);
        if (typed is null) return Task.CompletedTask;
        if (!long.TryParse(typed.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            _windows.ShowResult("Número do CT-e", $"⚠️ \"{typed}\" não é um número válido.");
            return Task.CompletedTask;
        }
        var entry = _host.Journal.Append(item.Entry with { Stage = EmissionStage.NumberCaptured, CteNumber = number });
        return FinishPendingAsync(entry);
    }

    [RelayCommand]
    private void ResolvePending(PendingItem item)
    {
        if (!_windows.Confirm("Marcar como resolvido",
                $"Marcar a pendência do protocolo {item.Protocol} como resolvida?\n\n"
                + "Use isto quando o CT-e não foi emitido, ou quando você já o registrou no sistema por conta própria. "
                + "O protocolo volta a poder ser emitido.", "Marcar como resolvido"))
            return;
        _host.Journal.Append(item.Entry with { Stage = EmissionStage.Resolved, Note = "Resolvido pelo operador." });
        _host.Log.Info($"Pendência do protocolo {item.Protocol} marcada como resolvida.");
    }

    // ------------------------------------------------------------------ updates and about

    [RelayCommand]
    private Task CheckUpdates() => CheckUpdatesAsync(silent: false);

    public async Task CheckUpdatesAsync(bool silent)
    {
        if (IsBusy) return;
        using var checker = new UpdateChecker(AppHost.Version);
        UpdateInfo info;
        try
        {
            info = await checker.CheckAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!silent)
                _windows.ShowResult("Atualização", $"❌ Não foi possível verificar atualizações:\n{ex.Message}");
            return;
        }

        if (!info.UpdateAvailable)
        {
            if (!silent)
                _windows.ShowResult("Atualização", $"✅ Você está usando a versão mais recente!\n\nVersão atual: {AppHost.Version}");
            return;
        }
        if (info.DownloadUrl is null)
        {
            _windows.ShowResult("Atualização",
                $"Nova versão {info.LatestVersion} disponível, mas sem instalador para download.\nBaixe manualmente no GitHub.");
            return;
        }
        if (!_windows.Confirm("Nova versão disponível",
                $"Versão atual: {info.CurrentVersion}\nNova versão: {info.LatestVersion}\n\nNovidades:\n{info.ReleaseNotes}",
                "Baixar e instalar"))
            return;

        IsBusy = true;
        try
        {
            var progress = new Progress<int>(percent => StatusText = $"Baixando atualização… {percent}%");
            var installer = await checker.DownloadInstallerAsync(info.DownloadUrl, progress, CancellationToken.None);
            if (!_windows.Confirm("Instalar atualização",
                    "A atualização será instalada agora.\n\nO aplicativo será fechado durante a instalação.\n\nDeseja continuar?",
                    "Instalar"))
                return;
            Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
            _windows.Close();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _windows.ShowResult("Atualização", $"❌ Falha ao baixar a atualização:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            StatusText = "Pronto";
        }
    }

    [RelayCommand]
    private void About() => _windows.ShowResult("Sobre o CTe LogLife",
        $"CTe LogLife\nVersão {AppHost.Version}\n\nAutomação de emissão de CT-e no Bsoft, integrada ao sistema LogLife.");
}
