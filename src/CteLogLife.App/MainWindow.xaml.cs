using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace CteLogLife.App;

public sealed class PageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Enum.Parse<AppPage>(parameter.ToString()!) : Binding.DoNothing;
}

public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public partial class MainWindow : Window, IRunWindows
{
    private readonly AppHost _host;
    private readonly MainViewModel _viewModel;
    private OverlayWindow? _overlay;
    private GlobalHotkeys? _hotkeys;
    private WindowState _stateBeforeRun = WindowState.Normal;

    public MainWindow(AppHost host)
    {
        InitializeComponent();
        _host = host;
        _viewModel = new MainViewModel(host, this);
        DataContext = _viewModel;

        AddHandler(PreviewTextInputEvent, new TextCompositionEventHandler(OnPreviewTextInput));
        DataObject.AddPastingHandler(this, OnPaste);
        ((INotifyCollectionChanged)host.Log.Entries).CollectionChanged += OnLogChanged;
        Closing += OnClosing;
    }

    public MainViewModel ViewModel => _viewModel;

    /// <summary>Scrolls the visible page back to the top (used after switching pages).</summary>
    public void ScrollPageToTop() => PageScroll.ScrollToTop();

    public void ScrollPageToBottom() => PageScroll.ScrollToBottom();

    // ------------------------------------------------------------------ input rules

    /// <summary>Fields tagged "digits" take only numbers; "list" also takes ';' and spaces.</summary>
    private static bool Accepts(object? source, string text)
    {
        if (source is not TextBox { Tag: string rule }) return true;
        return rule switch
        {
            "digits" => text.All(char.IsAsciiDigit),
            "list" => text.All(c => char.IsAsciiDigit(c) || c is ';' or ' '),
            _ => true,
        };
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!Accepts(e.OriginalSource, e.Text)) e.Handled = true;
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "";
        if (!Accepts(e.OriginalSource, text.Trim())) e.CancelCommand();
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void OnSaveCredentials(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCredentials(ApiPassword.Password);
        ApiPassword.Clear();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.IsBusy) return;
        e.Cancel = !MessageDialog.Confirm(this, "Execução em andamento",
            "Há uma execução em andamento. Fechar agora interrompe o robô no meio do que estiver fazendo.\n\n"
            + "Prefira parar a execução primeiro.", "Fechar mesmo assim");
    }

    // ------------------------------------------------------------------ IRunWindows

    public void BeginBotRun()
    {
        _stateBeforeRun = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;

        _overlay = new OverlayWindow(_host);
        _overlay.Show();

        _hotkeys = new GlobalHotkeys(this);
        var pauseKey = _hotkeys.Add(0, Interop.VkPause, _host.Control.TogglePause);
        var pauseCombo = _hotkeys.Add(Interop.ModControl | Interop.ModAlt, Interop.VkP, _host.Control.TogglePause);
        var stopCombo = _hotkeys.Add(Interop.ModControl | Interop.ModAlt, Interop.VkQ, _host.Control.RequestStop);
        if (!pauseKey && !pauseCombo)
            _host.Log.Warn("Os atalhos de pausa (Pause e Ctrl+Alt+P) estão em uso por outro programa. Use os botões desta janela.");
        if (!stopCombo)
            _host.Log.Warn("O atalho de parada (Ctrl+Alt+Q) está em uso por outro programa.");

        // Out of the way: the bot needs to see Bsoft.
        WindowState = WindowState.Minimized;
    }

    public void EndBotRun()
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
        _overlay?.Close();
        _overlay = null;
        WindowState = _stateBeforeRun;
        Activate();
    }

    public void ShowResult(string title, string message) => MessageDialog.Show(this, title, message);

    public bool Confirm(string title, string message, string confirmText) =>
        MessageDialog.Confirm(this, title, message, confirmText);

    public string? Prompt(string title, string message, string? prefill) =>
        MessageDialog.Prompt(this, title, message, prefill);

    public string? PickFolder(string title, string current)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (Directory.Exists(current)) dialog.InitialDirectory = current;
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    public string? PickFile(string title, string current)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Planilhas Excel (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|Todos os arquivos (*.*)|*.*",
        };
        if (File.Exists(current)) dialog.InitialDirectory = Path.GetDirectoryName(current);
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    public void OpenConference() => new ConferenceWindow(_viewModel) { Owner = this }.ShowDialog();
}
