using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CteLogLife.Core.Automation;

namespace CteLogLife.App;

public sealed record UpcomingItem(string Id, int Position, string Title, string Subtitle);

public partial class OverlayViewModel : ObservableObject
{
    private static readonly Brush Running = Frozen("#3DDC97"), RunningSoft = Frozen("#1A3DDC97");
    private static readonly Brush Waiting = Frozen("#FFC857"), WaitingSoft = Frozen("#26FFC857");
    private static readonly Brush Stopping = Frozen("#FF6B81"), StoppingSoft = Frozen("#26FF6B81");
    private static readonly Brush Quiet = Frozen("#A9B6C8");

    [ObservableProperty] private string _stateText = "EM EXECUÇÃO";
    [ObservableProperty] private Brush _stateBrush = Running;
    [ObservableProperty] private Brush _stateSoft = RunningSoft;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _counter = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _hasCurrent;
    [ObservableProperty] private string _currentTitle = "";
    [ObservableProperty] private string _currentSubtitle = "";
    [ObservableProperty] private string _step = "";
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";
    [ObservableProperty] private string _hint = "";
    [ObservableProperty] private bool _hasHint;
    [ObservableProperty] private string _upcomingCount = "";
    [ObservableProperty] private bool _noUpcoming;
    [ObservableProperty] private string _emptyText = "";
    [ObservableProperty] private double _listMaxHeight = 318;

    // search and include (while paused)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchIsEmpty), nameof(SearchHasText))]
    private string _searchText = "";

    [ObservableProperty] private bool _showSearch;
    [ObservableProperty] private string _searchHint = "";
    [ObservableProperty] private bool _offerInclude;
    [ObservableProperty] private string _includeText = "";
    [ObservableProperty] private string _includeStatus = "";
    [ObservableProperty] private bool _hasIncludeStatus;
    [ObservableProperty] private Brush _includeStatusBrush = Quiet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIncludeNow))]
    private bool _isIncluding;

    // unknown screen
    [ObservableProperty] private bool _hasAttention;
    [ObservableProperty] private string _attentionTitle = "";
    [ObservableProperty] private string _attentionMessage = "";
    [ObservableProperty] private bool _hasScreenshot;

    public bool SearchIsEmpty => SearchText.Length == 0;
    public bool SearchHasText => SearchText.Length > 0;
    public bool CanIncludeNow => !IsIncluding;
    public string? ScreenshotPath { get; private set; }

    public ObservableCollection<UpcomingItem> Upcoming { get; } = [];

    private static Brush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    /// <summary>The line under the queue telling what the last search or include did.</summary>
    public void Say(string message, bool? ok = null)
    {
        IncludeStatus = message;
        HasIncludeStatus = message.Length > 0;
        IncludeStatusBrush = ok switch { true => Running, false => Stopping, _ => Quiet };
    }

    public void Refresh(RunQueue queue, RunControl control)
    {
        var items = queue.Snapshot();
        var current = items.FirstOrDefault(i => i.State == QueueItemState.Running);
        var pending = items.Where(i => i.State == QueueItemState.Pending).ToList();
        var finished = items.Count - pending.Count - (current is null ? 0 : 1);
        var attention = control.Attention;
        var paused = control.IsPaused;

        Title = queue.Title.Length > 0 ? queue.Title : "Preparando…";
        Counter = items.Count == 0 ? "" : $"{Math.Min(finished + (current is null ? 0 : 1), items.Count)} / {items.Count}";
        Progress = items.Count == 0 ? 0 : 100.0 * finished / items.Count;

        HasCurrent = current is not null;
        CurrentTitle = current?.Title ?? "";
        CurrentSubtitle = current?.Subtitle ?? "";
        Step = queue.Step;

        // Searching and including only exist during a pause: leaving it clears them.
        if (!paused && (SearchText.Length > 0 || IncludeStatus.Length > 0))
        {
            SearchText = "";
            Say("");
        }
        var search = SearchText.Trim();
        var canInclude = queue.CanInclude;
        ShowSearch = paused && (pending.Count > 0 || canInclude);
        SearchHint = canInclude ? $"Buscar na fila ou incluir {queue.IncludeLabel}…" : "Buscar na fila…";

        // Positions are the real places in the queue, also when a search hides the other items.
        var upcoming = pending.Select((item, index) => (item, position: index + 1))
            .Where(entry => search.Length == 0 || RunQueue.Matches(entry.item, search))
            .Select(entry => new UpcomingItem(entry.item.Id, entry.position, entry.item.Title, entry.item.Subtitle))
            .ToList();
        // The list is rebuilt only when it really changed, so buttons do not jump under the mouse.
        if (!upcoming.SequenceEqual(Upcoming))
        {
            Upcoming.Clear();
            foreach (var item in upcoming) Upcoming.Add(item);
        }
        UpcomingCount = pending.Count == 0 ? ""
            : search.Length > 0 ? $"({upcoming.Count} de {pending.Count})"
            : $"({pending.Count})";
        NoUpcoming = upcoming.Count == 0;
        EmptyText = search.Length > 0 ? $"Nada na fila com \"{search}\"." : "Nada mais na fila.";
        ListMaxHeight = attention is null ? 318 : 150;

        OfferInclude = paused && canInclude && upcoming.Count == 0 && search.Length > 0 && search.All(char.IsAsciiDigit);
        IncludeText = IsIncluding ? $"Buscando {queue.IncludeLabel} {search}…" : $"Incluir {queue.IncludeLabel} {search}";

        HasAttention = attention is not null;
        AttentionTitle = attention?.Title ?? "";
        AttentionMessage = attention?.Message ?? "";
        ScreenshotPath = attention?.ScreenshotPath;
        HasScreenshot = ScreenshotPath is not null;

        IsPaused = paused;
        PausedText = attention is not null
            ? "O robô só continua quando você mandar."
            : canInclude
                ? "Pausado. Mude a ordem com as setas, busque um item ou digite um protocolo para incluir."
                : "Pausado. Mude a ordem com as setas ou busque um item da fila.";
        if (control.IsStopRequested)
        {
            (StateText, StateBrush, StateSoft) = ("PARANDO", Stopping, StoppingSoft);
            Hint = control.InCritical
                ? "Este CT-e já foi enviado: a execução para assim que ele for registrado."
                : "Parando no próximo passo.";
        }
        else if (attention is not null)
        {
            (StateText, StateBrush, StateSoft) = ("AGUARDANDO VOCÊ", Stopping, StoppingSoft);
            Hint = "";
        }
        else if (paused)
        {
            (StateText, StateBrush, StateSoft) = ("PAUSADO", Waiting, WaitingSoft);
            Hint = "";
        }
        else if (control.IsPauseRequested)
        {
            (StateText, StateBrush, StateSoft) = ("PAUSA SOLICITADA", Waiting, WaitingSoft);
            Hint = control.InCritical
                ? "Este CT-e já foi enviado: a pausa começa assim que ele for registrado."
                : "Pausando no próximo passo.";
        }
        else
        {
            (StateText, StateBrush, StateSoft) = ("EM EXECUÇÃO", Running, RunningSoft);
            Hint = "";
        }
        HasHint = Hint.Length > 0;
    }
}

/// <summary>
/// The panel docked to the screen edge while the bot works: what is being emitted, what comes next
/// and the pause shortcuts. While the bot runs it lets every click through and never takes the
/// keyboard. Once the bot is actually paused it becomes clickable: the queue can be reordered,
/// searched and added to. It only takes the keyboard when the search box is clicked, and gives it
/// back to the window that had it when the pause ends.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly AppHost? _host;
    private readonly RunQueue _queue;
    private readonly RunControl _control;
    private readonly OverlayViewModel _viewModel = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly bool _live;
    private bool _refreshQueued;
    private bool _closed;
    private bool _hadAttention;
    private IntPtr _focusOwner;

    public OverlayWindow(AppHost host) : this(host.Queue, host.Control, host.Settings.OverlayOnLeft)
    {
        _host = host;
    }

    /// <param name="dock">
    /// False keeps the window where it was put and silent (used to render previews off screen).
    /// </param>
    public OverlayWindow(RunQueue queue, RunControl control, bool onLeft, bool dock = true)
    {
        InitializeComponent();
        _queue = queue;
        _control = control;
        _live = dock;
        DataContext = _viewModel;
        if (onLeft)
        {
            Shell.CornerRadius = new CornerRadius(0, 14, 14, 0);
            Shell.BorderThickness = new Thickness(0, 1, 1, 1);
            Shell.Padding = new Thickness(16, 16, 18, 16);
        }

        SourceInitialized += (_, _) =>
        {
            Interop.MakeOverlay(this);
            Interop.SetClickThrough(this, true);
            if (_host?.Settings.HideOverlayFromCapture == true && !Interop.ExcludeFromCapture(this, true))
                _host.Log.Warn("Este Windows não permite ocultar o painel das capturas de tela; mantenha-o fora dos campos do Bsoft.");
        };
        if (dock)
        {
            Loaded += (_, _) => Dock(onLeft);
            SizeChanged += (_, _) => Dock(onLeft);
        }
        Closed += (_, _) =>
        {
            _closed = true;
            _closing.Cancel();
            _queue.Changed -= QueueRefresh;
            _control.Changed -= QueueRefresh;
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.SearchText)) RefreshNow();
        };
        _queue.Changed += QueueRefresh;
        _control.Changed += QueueRefresh;
        _viewModel.Refresh(_queue, _control);
    }

    public OverlayViewModel ViewModel => _viewModel;

    private void Dock(bool onLeft)
    {
        var area = SystemParameters.WorkArea;
        MaxHeight = Math.Max(300, area.Height - 96);
        Left = onLeft ? area.Left : area.Right - ActualWidth;
        Top = area.Top + Math.Max(48, Math.Min((area.Height - ActualHeight) / 2 - 60, area.Height - ActualHeight - 24));
    }

    /// <summary>Queue and control events come from the bot's thread, in bursts: one refresh covers them.</summary>
    private void QueueRefresh()
    {
        if (_closed || _refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _refreshQueued = false;
            if (_closed) return;
            RefreshNow();
        });
    }

    public void RefreshNow()
    {
        _viewModel.Refresh(_queue, _control);
        var paused = _control.IsPaused;
        Interop.SetClickThrough(this, !paused);
        if (!paused) ReleaseKeyboard();

        var attention = _viewModel.HasAttention;
        if (attention && !_hadAttention && _live) SystemSounds.Exclamation.Play();
        _hadAttention = attention;
    }

    // ------------------------------------------------------------------ keyboard for the search box

    /// <summary>
    /// The panel never takes the keyboard by itself. Clicking the search box is the one way in: the
    /// window that had it is remembered and gets it back when the pause ends.
    /// </summary>
    public bool FocusSearch()
    {
        if (!_viewModel.ShowSearch) return false;
        if (!IsActive)
        {
            var owner = Interop.ForegroundWindow();
            if (!Interop.IsOurs(owner)) _focusOwner = owner;
            Interop.SetNoActivate(this, false);
            Activate();
        }
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        return SearchBox.IsKeyboardFocused;
    }

    private void ReleaseKeyboard()
    {
        Interop.SetNoActivate(this, true);
        if (IsActive && _focusOwner != IntPtr.Zero) Interop.BringToFront(_focusOwner);
        _focusOwner = IntPtr.Zero;
    }

    private void OnSearchMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (SearchBox.IsKeyboardFocused) return;
        FocusSearch();
        // A click on the frame around the box counts as a click in it.
        if (!ReferenceEquals(sender, SearchBox)) e.Handled = true;
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = "";
        _viewModel.Say("");
    }

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            OnClearSearch(sender, e);
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var search = _viewModel.SearchText.Trim();
        if (search.Length == 0) return;
        if (_viewModel.Upcoming.FirstOrDefault() is { } first)
        {
            // Enter on a search sends its first result to the front of the queue.
            _queue.MoveToTop(first.Id);
            _viewModel.SearchText = "";
            _viewModel.Say($"{first.Title} é o próximo da fila.", ok: true);
        }
        else if (_viewModel.OfferInclude)
        {
            await IncludeAsync();
        }
    }

    // ------------------------------------------------------------------ queue actions

    private static string IdOf(object sender) => (string)((Button)sender).Tag;

    private void OnMoveTop(object sender, RoutedEventArgs e) => _queue.MoveToTop(IdOf(sender));
    private void OnMoveUp(object sender, RoutedEventArgs e) => _queue.Move(IdOf(sender), -1);
    private void OnMoveDown(object sender, RoutedEventArgs e) => _queue.Move(IdOf(sender), 1);
    private void OnResume(object sender, RoutedEventArgs e) => _control.Resume();
    private void OnStop(object sender, RoutedEventArgs e) => _control.RequestStop();

    private async void OnInclude(object sender, RoutedEventArgs e) => await IncludeAsync();

    /// <summary>Asks the running flow to fetch what was typed and put it next in the queue.</summary>
    public async Task IncludeAsync()
    {
        var text = _viewModel.SearchText.Trim();
        if (text.Length == 0 || _viewModel.IsIncluding) return;

        _viewModel.IsIncluding = true;
        _viewModel.Say($"Buscando {_queue.IncludeLabel} {text} no sistema…");
        RefreshNow();
        IncludeResult result;
        try
        {
            result = await Task.Run(() => _queue.IncludeAsync(text, _closing.Token));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            result = new IncludeResult(false, ex.Message);
        }
        finally
        {
            _viewModel.IsIncluding = false;
        }
        // The pause may have ended while the system was answering: the panel no longer shows the
        // outcome then, so a refusal goes to the activity log.
        if (!result.Ok && !_control.IsPaused) _host?.Log.Warn($"Inclusão de {_queue.IncludeLabel} {text}: {result.Message}");
        if (_closed) return;

        if (result.Ok) _viewModel.SearchText = "";
        _viewModel.Say(result.Message, result.Ok);
        RefreshNow();
    }

    private void OnOpenScreenshot(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ScreenshotPath is not { } path || !File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _viewModel.Say($"Não foi possível abrir a captura: {ex.Message}", ok: false);
        }
    }
}
