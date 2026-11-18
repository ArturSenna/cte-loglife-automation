using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using CteLogLife.Core;

namespace CteLogLife.App;

public partial class App : Application
{
    private AppHost? _host;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var culture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));

        // Developer switch: render every window to PNG files without touching the real settings.
        if (e.Args is ["--snapshot", var folder, ..])
        {
            AppPaths.DataRoot = Path.Combine(Path.GetTempPath(), "cte-loglife-snapshot-" + Guid.NewGuid().ToString("N"));
            _host = new AppHost(Dispatcher);
            try
            {
                Snapshot.Render(_host, Path.GetFullPath(folder));
            }
            finally
            {
                _host.Dispose();
                try
                {
                    Directory.Delete(AppPaths.DataRoot, recursive: true);
                }
                catch (IOException)
                {
                }
            }
            Shutdown();
            return;
        }

        // Developer/diagnostic switch: checks the overlay and the shortcuts on this machine.
        if (e.Args is ["--selftest", var report, ..])
        {
            Snapshot.SelfTest(Path.GetFullPath(report));
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;

        _host = new AppHost(Dispatcher);
        _host.Log.Info($"CTe LogLife {AppHost.Version} iniciado.");
        _host.ImportLegacy();

        var window = new MainWindow(_host);
        MainWindow = window;
        window.Show();

        if (_host.Journal.Open().Count > 0)
            _host.Log.Warn("Há emissões pendentes de registro. Veja a página Pendências.");
        if (_host.Settings.CheckUpdatesOnStartup)
            _ = window.ViewModel.CheckUpdatesAsync(silent: true);
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _host?.Log.Error(e.Exception.ToString());
        MessageDialog.Show(MainWindow, "Erro inesperado",
            "❌ " + e.Exception.Message + "\n\nO detalhe foi gravado no registro de atividade.");
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
