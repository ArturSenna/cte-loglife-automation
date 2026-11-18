using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Flows;
using CteLogLife.Core.Journal;

namespace CteLogLife.App;

/// <summary>
/// Developer tool behind <c>--snapshot &lt;folder&gt;</c>: renders every page and window to PNG with sample
/// data, off screen, so the design can be reviewed without Bsoft, the API or the real settings.
/// </summary>
internal static class Snapshot
{
    private const double OffScreen = -20000;

    public static void Render(AppHost host, string folder)
    {
        Directory.CreateDirectory(folder);
        Seed(host);

        var window = new MainWindow(host)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = OffScreen,
            Top = OffScreen,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        Pump();

        foreach (var page in Enum.GetValues<AppPage>())
        {
            window.ViewModel.Page = page;
            window.ScrollPageToTop();
            Pump();
            Save(window, Path.Combine(folder, $"page-{page}.png"));
        }

        window.ViewModel.Page = AppPage.Settings;
        window.ScrollPageToBottom();
        Pump();
        Save(window, Path.Combine(folder, "page-Settings-bottom.png"));

        window.ViewModel.Page = AppPage.Emission;
        window.ScrollPageToTop();
        window.ViewModel.IsBusy = true;
        window.ViewModel.StatusText = "Emissão unificada…";
        Pump();
        Save(window, Path.Combine(folder, "page-Emission-busy.png"));
        window.ViewModel.IsBusy = false;

        RenderOverlay(folder);

        var conference = new ConferenceWindow(window.ViewModel)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = OffScreen,
            Top = OffScreen,
            ShowActivated = false,
        };
        conference.ShowRows(
        [
            new PayerInconsistency("15790", 14, "LABORATÓRIO EXEMPLO LTDA", "12.345.678/0001-90", "98.765.432/0001-10", "CNPJ/CPF divergente"),
            new PayerInconsistency("15804", 31, "CLÍNICA MODELO S/A", "01.234.567/0001-89", "", "CNPJ/CPF divergente"),
            new PayerInconsistency("15811", 47, "INSTITUTO DE ANÁLISES", "123.456.789-09", "11.222.333/0001-44", "CNPJ/CPF divergente"),
        ]);
        conference.Show();
        Pump();
        Save(conference, Path.Combine(folder, "window-Conference.png"));
        conference.Close();

        foreach (var (name, dialog) in new[]
                 {
                     ("dialog-Result", MessageDialog.Preview("Emissão unificada",
                         "Emissões de CTE unificado realizadas com sucesso!\n\nTotal: 27\nSimbólicos: 9\nNormais: 18",
                         MessageKind.Success, null, null)),
                     ("dialog-Confirm", MessageDialog.Preview("Confirmar cancelamento",
                         "Tem certeza que deseja cancelar a lista de CTes?\n\nEsta ação não pode ser desfeita.",
                         MessageKind.Warning, "Cancelar CT-es", null)),
                     ("dialog-Prompt", MessageDialog.Preview("Número do CT-e",
                         "Não foi possível copiar o número do CT-e que acabou de ser emitido (protocolo 48213).\n\n"
                         + "O CT-e já foi autorizado. Digite o número que aparece no campo \"Nº CT-e\" do Bsoft para continuar a emissão:",
                         MessageKind.Question, null, "15872")),
                 })
        {
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = OffScreen;
            dialog.Top = OffScreen;
            dialog.ShowActivated = false;
            dialog.Show();
            Pump();
            Save(dialog, Path.Combine(folder, name + ".png"));
            dialog.Close();
        }

        window.Close();
    }

    /// <summary>
    /// Behind <c>--selftest &lt;file&gt;</c>: checks on this machine the three things the overlay depends on
    /// (it stays out of the bot's screenshots, it lets clicks through, the shortcuts can be registered)
    /// and writes what it found. The overlay shows on the real screen for about a second.
    /// </summary>
    public static void SelfTest(string reportPath)
    {
        var lines = new List<string>();
        var control = new RunControl();
        var overlay = new OverlayWindow(SampleQueue(), control, onLeft: false);
        overlay.Show();
        Pump();
        Thread.Sleep(400);
        Pump();

        var origin = overlay.PointToScreen(new Point(0, 0));
        var scale = PresentationSource.FromVisual(overlay)!.CompositionTarget.TransformToDevice.M11;
        var area = new OpenCvSharp.Rect((int)origin.X + 24, (int)origin.Y + 24,
            (int)(overlay.ActualWidth * scale) - 48, (int)(overlay.ActualHeight * scale) - 48);

        using (var grabber = new Bsoft.ScreenGrabber())
        {
            OpenCvSharp.Mat Shot(bool exclude)
            {
                var applied = Interop.ExcludeFromCapture(overlay, exclude);
                Pump();
                Thread.Sleep(450);
                lines.Add($"exclusion {(exclude ? "ON " : "OFF")}: api call {(applied ? "ok" : "FAILED")}");
                return grabber.Capture();
            }

            // The overlay's exact light-blue step text and progress bar (#00A5E7) are its fingerprint.
            static int BrandPixels(OpenCvSharp.Mat shot, OpenCvSharp.Rect area)
            {
                var count = 0;
                for (var y = area.Top; y < area.Bottom && y < shot.Height; y++)
                for (var x = area.Left; x < area.Right && x < shot.Width; x++)
                {
                    var pixel = shot.At<OpenCvSharp.Vec3b>(y, x);
                    if (Math.Abs(pixel.Item0 - 0xE7) < 10 && Math.Abs(pixel.Item1 - 0xA5) < 10 && pixel.Item2 < 12) count++;
                }
                return count;
            }

            using var shown = Shot(false);
            using var excluded = Shot(true);
            var differing = 0;
            var total = 0;
            for (var y = area.Top; y < area.Bottom && y < shown.Height; y++)
            for (var x = area.Left; x < area.Right && x < shown.Width; x++)
            {
                total++;
                if (shown.At<OpenCvSharp.Vec3b>(y, x) != excluded.At<OpenCvSharp.Vec3b>(y, x)) differing++;
            }
            int brandShown = BrandPixels(shown, area), brandExcluded = BrandPixels(excluded, area);
            lines.Add($"overlay-coloured pixels in the capture: {brandShown} with exclusion off, {brandExcluded} with it on");
            lines.Add($"pixels that changed between the two captures: {100.0 * differing / Math.Max(1, total):F1}%");
            lines.Add(brandShown > 50 && brandExcluded == 0
                ? "RESULT overlay: hidden from the bot's screenshots as intended"
                : "RESULT overlay: NOT hidden from screenshots on this machine");
        }

        using (var hotkeys = new GlobalHotkeys(overlay))
        {
            lines.Add($"hotkey Pause: {(hotkeys.Add(0, Interop.VkPause, () => { }) ? "registered" : "IN USE by another program")}");
            lines.Add($"hotkey Ctrl+Alt+P: {(hotkeys.Add(Interop.ModControl | Interop.ModAlt, Interop.VkP, () => { }) ? "registered" : "IN USE by another program")}");
            lines.Add($"hotkey Ctrl+Alt+Q: {(hotkeys.Add(Interop.ModControl | Interop.ModAlt, Interop.VkQ, () => { }) ? "registered" : "IN USE by another program")}");
        }

        lines.Add($"overlay took keyboard focus: {(overlay.IsActive ? "YES (unexpected)" : "no")}");
        lines.Add($"screen {SystemParameters.PrimaryScreenWidth * scale:F0}x{SystemParameters.PrimaryScreenHeight * scale:F0}, scale {scale:P0}");

        // The rest needs the panel as it is during a pause: clickable, with the search box.
        control.RequestPause();
        var parked = new Thread(() =>
        {
            try
            {
                control.Checkpoint();
            }
            catch (RunStoppedException)
            {
            }
        });
        parked.Start();
        while (!control.IsPaused) Thread.Sleep(10);
        overlay.RefreshNow();
        Pump();
        Thread.Sleep(200);
        Pump();

        // What the bot records on an unknown screen: one test key (F24) and one click on the panel's title.
        var target = overlay.PointToScreen(new Point(overlay.ActualWidth / 2, 46));
        var (installed, keySeen, click) = Bsoft.InputRecorder.Probe((int)target.X, (int)target.Y);
        Pump();
        lines.Add($"recorder hooks: {(installed ? "installed" : "REFUSED by Windows")}");
        lines.Add($"recorder saw the test key: {(keySeen ? "yes" : "NO")}");
        lines.Add(click is null
            ? "recorder saw the test click: NO"
            : $"recorder saw the test click at {click.X},{click.Y} (sent to {(int)target.X},{(int)target.Y})");
        lines.Add(installed && keySeen && click is not null && Math.Abs(click.X - (int)target.X) <= 1 && Math.Abs(click.Y - (int)target.Y) <= 1
            ? "RESULT recorder: sees keys and clicks at the right place"
            : "RESULT recorder: NOT working on this machine (unknown screens will only be logged)");

        // Typing in the search box needs the keyboard, and it must come back when the pause ends.
        var before = Interop.ForegroundWindow();
        var focused = overlay.FocusSearch();
        Pump();
        lines.Add($"search box took the keyboard when asked: {(focused && overlay.IsActive ? "yes" : "NO")}");
        control.Resume();
        parked.Join();
        overlay.RefreshNow();
        Pump();
        Thread.Sleep(300);
        Pump();
        lines.Add($"keyboard given back after the pause: {(Interop.ForegroundWindow() == before || !overlay.IsActive ? "yes" : "NO")}");

        // The panel stays open until the end: closing the app's last window would start its shutdown.
        try
        {
            LearnAndRepeat(lines);
        }
        catch (Exception ex)
        {
            lines.Add($"RESULT unknown screen: the test itself failed: {ex}");
        }
        overlay.Close();
        File.WriteAllLines(reportPath, lines);
    }

    private sealed class LineLog(List<string> lines) : IRunLog
    {
        public void Info(string message) => Add(message);
        public void Warn(string message) => Add(message);
        public void Error(string message) => Add(message);

        private void Add(string message)
        {
            lock (lines) lines.Add("    log: " + message);
        }
    }

    /// <summary>
    /// The unknown-screen cycle on the real desktop, with two small windows of this program standing in
    /// for Bsoft: a "control" the bot needs and a dialog that covers it. First time the bot must stop
    /// and learn from one click on OK; second time it must close the dialog by itself.
    /// </summary>
    private static void LearnAndRepeat(List<string> lines)
    {
        var root = Path.Combine(Path.GetTempPath(), "cte-loglife-selftest-" + Guid.NewGuid().ToString("N"));
        var templates = Directory.CreateDirectory(Path.Combine(root, "templates")).FullName;
        var area = SystemParameters.WorkArea;
        double left = area.Left + 180, top = area.Top + 180;

        var target = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            ShowActivated = false, Left = left + 60, Top = top + 50, Width = 230, Height = 64,
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x3C, 0x5D)),
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "CTE · AUTOTESTE · 7Q4", Foreground = Brushes.Gold, FontSize = 17, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Window? dialog = null;
        System.Windows.Controls.Button? ok = null;

        void ShowDialog(double x, double y)
        {
            ok = new System.Windows.Controls.Button
            {
                Content = "OK", Width = 90, Height = 30, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 22, 0, 0),
            };
            var body = new System.Windows.Controls.StackPanel { Margin = new Thickness(18) };
            body.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Autoteste: aviso que o robô não conhece.", FontSize = 14, TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Um clique em OK fecha este aviso.", FontSize = 12, Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 6, 0, 0),
            });
            body.Children.Add(ok);
            var window = new Window
            {
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
                ShowActivated = false, Left = x, Top = y, Width = 360, Height = 170, Background = Brushes.WhiteSmoke,
                BorderBrush = Brushes.SteelBlue, BorderThickness = new Thickness(2), Content = body,
            };
            ok.Click += (_, _) => window.Close();
            window.Closed += (_, _) => dialog = null;
            dialog = window;
            window.Show();
            Settle();
        }

        // Waits for a background step while this thread keeps the two windows alive.
        bool PumpUntil(Func<bool> done, int timeoutMs)
        {
            var until = Environment.TickCount64 + timeoutMs;
            while (!done())
            {
                if (Environment.TickCount64 > until) return false;
                Pump();
                Thread.Sleep(15);
            }
            return true;
        }

        void Settle()
        {
            for (var i = 0; i < 12; i++)
            {
                Pump();
                Thread.Sleep(25);
            }
        }

        var control = new RunControl();
        var log = new LineLog(lines);
        using var store = new Bsoft.LearnedScreenStore(Path.Combine(root, "learned"));
        lines.Add("unknown-screen cycle on the real desktop:");
        try
        {
            target.Show();
            Settle();

            // The picture of the "control", taken from the screen as the bot will see it.
            var scale = PresentationSource.FromVisual(target)!.CompositionTarget.TransformToDevice.M11;
            var origin = target.PointToScreen(new Point(0, 0));
            using (var grabber = new Bsoft.ScreenGrabber())
            using (var shot = grabber.Capture())
            using (var piece = new OpenCvSharp.Mat(shot, new OpenCvSharp.Rect((int)origin.X + 8, (int)origin.Y + 8,
                       (int)(target.ActualWidth * scale) - 16, (int)(target.ActualHeight * scale) - 16)))
            {
                OpenCvSharp.Cv2.ImEncode(".png", piece, out var png);
                File.WriteAllBytes(Path.Combine(templates, "autoteste.png"), png);
            }

            using var templateStore = new Bsoft.TemplateStore(templates);
            var unknown = new Bsoft.UnknownScreens(store, control, log, Path.Combine(root, "shots"),
                Bsoft.InputRecorder.ForSelfTest);
            using var bot = new Bsoft.DesktopBot(templateStore, new Bsoft.ScreenGrabber(), control, log, unknown)
            {
                RecoverAfterMs = 400,
                RecoverEveryMs = 300,
            };
            var operatorHand = new Bsoft.SendInputRecovery();

            // First time: the dialog hides the control, the bot stops, "the operator" clicks OK and continues.
            ShowDialog(left, top);
            var first = Task.Run(() => bot.Require("autoteste", waitMs: 900));
            var stopped = PumpUntil(() => control.Attention is not null || first.IsCompleted, 8000) && control.Attention is not null;
            lines.Add($"  bot stopped and asked for the operator: {(stopped ? "yes" : "NO")}");
            if (stopped)
            {
                var button = ok!.PointToScreen(new Point(ok.ActualWidth / 2, ok.ActualHeight / 2));
                operatorHand.Click((int)button.X, (int)button.Y, Bsoft.RecordedKind.Click);
                PumpUntil(() => dialog is null, 3000);
                Settle();
                control.Resume();
            }
            var solved = PumpUntil(() => first.IsCompleted, 8000) && first.IsCompletedSuccessfully;
            lines.Add($"  control found after the operator's click: {(solved ? "yes" : "NO")}");
            lines.Add($"  screen learned from that click: {(store.Count == 1 ? "yes" : "NO")}");

            // Second time, a little to the side: no question, the bot clicks OK itself.
            ShowDialog(left + 24, top + 16);
            var second = Task.Run(() => bot.Require("autoteste", waitMs: 6000));
            var again = PumpUntil(() => second.IsCompleted || control.Attention is not null, 12000);
            var alone = again && second.IsCompletedSuccessfully && dialog is null && control.Attention is null;
            if (control.Attention is not null)
            {
                control.RequestStop();
                PumpUntil(() => second.IsCompleted, 3000);
            }
            lines.Add($"  same screen closed by the bot alone the second time: {(alone ? "yes" : "NO")}");
            lines.Add(stopped && solved && store.Count == 1 && alone
                ? "RESULT unknown screen: stop, learn and repeat work on this machine"
                : "RESULT unknown screen: NOT working end to end on this machine (see the log lines above)");
        }
        finally
        {
            dialog?.Close();
            target.Close();
            Pump();
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static RunQueue SampleQueue()
    {
        var queue = new RunQueue();
        queue.Load("Emissão unificada",
        [
            new QueueItem("1", "48201 · LAB CENTRAL", "SIMBÓLICO · BELO HORIZONTE → SÃO PAULO"),
            new QueueItem("2", "48207 · HOSPITAL MODELO", "SIMBÓLICO · CONTAGEM → RIO DE JANEIRO"),
            new QueueItem("3", "48213 · CLÍNICA EXEMPLO", "NORMAL RODOVIÁRIO · UBERLÂNDIA → GOIÂNIA"),
            new QueueItem("4", "48219 · INSTITUTO DE ANÁLISES", "NORMAL RODOVIÁRIO · BELO HORIZONTE → VITÓRIA"),
            new QueueItem("5", "48222 · LABORATÓRIO SÃO LUCAS", "NORMAL RODOVIÁRIO · SÃO PAULO → CURITIBA"),
            new QueueItem("6", "48230 · CENTRO DIAGNÓSTICO", "NORMAL · BELO HORIZONTE → RECIFE"),
            new QueueItem("7", "48231 · LAB CENTRAL", "NORMAL · BELO HORIZONTE → MANAUS"),
            new QueueItem("8", "48236 · HOSPITAL MODELO", "NORMAL · CONTAGEM → PORTO ALEGRE"),
        ]);
        queue.Finish(queue.TakeNext()!.Id, QueueItemState.Done);
        queue.Finish(queue.TakeNext()!.Id, QueueItemState.Done);
        queue.TakeNext();
        queue.Step = "Valores e impostos";
        return queue;
    }

    private static void RenderOverlay(string folder)
    {
        var queue = SampleQueue();
        var control = new RunControl();
        var overlay = new OverlayWindow(queue, control, onLeft: false, dock: false) { Left = OffScreen, Top = OffScreen };
        overlay.Show();
        Pump();
        Save(overlay, Path.Combine(folder, "overlay-Running.png"));

        // A real pause: a worker parks at a checkpoint, exactly as the bot does.
        control.RequestPause();
        var worker = new Thread(() =>
        {
            try
            {
                control.Checkpoint();
            }
            catch (RunStoppedException)
            {
            }
        });
        worker.Start();
        while (!control.IsPaused) Thread.Sleep(10);
        overlay.RefreshNow();
        Pump();
        Save(overlay, Path.Combine(folder, "overlay-Paused.png"));

        // Searching the queue, then a protocol that is not in it (the flow accepts includes).
        queue.AllowInclude("protocolo", (text, _) =>
            Task.FromResult(new IncludeResult(true, $"Protocolo {text} incluído: é o próximo da fila.")));
        overlay.ViewModel.SearchText = "lab";
        Pump();
        Save(overlay, Path.Combine(folder, "overlay-Search.png"));
        overlay.ViewModel.SearchText = "48300";
        Pump();
        Save(overlay, Path.Combine(folder, "overlay-Include.png"));
        overlay.ViewModel.SearchText = "";
        overlay.ViewModel.Say("Protocolo 48300 incluído: é o próximo da fila.", ok: true);
        queue.Add(new QueueItem("9", "48300 · LAB URGENTE", "NORMAL · BELO HORIZONTE → SALVADOR"), next: true);
        Pump();
        Save(overlay, Path.Combine(folder, "overlay-Included.png"));

        control.Resume();
        worker.Join();
        overlay.RefreshNow();
        Pump();

        // The bot stuck at a screen it does not know.
        var stuck = new Thread(() => control.WaitForOperator(new OperatorAttention("Tela desconhecida",
            "O robô esperava o elemento \"finalizeEmission\" e ele não apareceu.\n"
            + "Resolva a tela no Bsoft e depois clique em Continuar.\n"
            + "O que você fizer nela fica gravado e é repetido quando ela aparecer de novo.",
            Path.Combine(folder, "overlay-Running.png"))));
        stuck.Start();
        while (control.Attention is null) Thread.Sleep(10);
        overlay.RefreshNow();
        Pump();
        Save(overlay, Path.Combine(folder, "overlay-UnknownScreen.png"));
        control.Resume();
        stuck.Join();
        overlay.Close();
    }

    private static void Seed(AppHost host)
    {
        var settings = host.Settings;
        settings.NormalFolder = @"C:\LogLife\CT-e\Normal";
        settings.ComplementaryFolder = @"C:\LogLife\CT-e\Complementar";
        settings.CtePdfFolder = @"C:\Bsoft\CTe\PDF";
        settings.BsoftReportPath = @"C:\LogLife\Relatórios\Relatorio CTe novembro.xlsx";
        settings.TargetReportPath = @"C:\LogLife\Relatórios\comparar-GNRE-17-11-2026-09-12.xlsx";

        host.Log.Info("CTe LogLife iniciado.");
        host.Log.Info("Buscando serviços para emissão unificada de 17/11/2026 a 17/11/2026...");
        host.Log.Info("27 serviço(s): 9 simbólico(s), 18 normal(is).");
        host.Log.Info("Protocolo 48201: CT-e 15870 emitido e registrado.");
        host.Log.Warn("Número do CT-e não copiado (tentativa 1/5).");
        host.Log.Info("Protocolo 48207: CT-e 15871 emitido e registrado.");
        host.Log.Error("CT-e 15872 emitido para o protocolo 48213, mas o registro no sistema não terminou: O sistema LogLife não respondeu a tempo.");

        var plan = new PostPlan { Kind = CteKind.LogLife, UploadType = "CTE LOGLIFE", CsvProtocol = "48213" };
        host.Journal.Append(new JournalEntry
        {
            EmissionId = "sample-1", Stage = EmissionStage.NumberCaptured, Protocol = "48213", CteNumber = 15872,
            EmissionDate = "17/11/2026", Plan = plan,
        });
        host.Journal.Append(new JournalEntry
        {
            EmissionId = "sample-2", Stage = EmissionStage.Uncertain, Protocol = "48219",
            EmissionDate = "17/11/2026", Plan = plan with { Symbolic = true },
        });
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Save(Window window, string path)
    {
        var root = (FrameworkElement)window.Content;
        var width = (int)Math.Ceiling(root.ActualWidth);
        var height = (int)Math.Ceiling(root.ActualHeight);
        var bounds = new Rect(0, 0, width, height);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // A transparent window is drawn over a neutral backdrop standing in for the desktop.
            context.DrawRectangle(window.AllowsTransparency ? Brushes.SlateGray : window.Background, null, bounds);
            context.DrawRectangle(
                new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, bounds);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
