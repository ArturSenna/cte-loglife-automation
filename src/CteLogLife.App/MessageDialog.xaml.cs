using System.Windows;
using System.Windows.Media;

namespace CteLogLife.App;

public enum MessageKind { Info, Success, Warning, Error, Question }

/// <summary>The app's one dialog: a message, a yes/no question, or a line of text to type.</summary>
public partial class MessageDialog : Window
{
    private bool _requireInput;

    public MessageDialog() => InitializeComponent();

    /// <summary>A closing message. The kind is taken from a leading ✅, ⚠️ or ❌ when not given.</summary>
    public static void Show(Window? owner, string title, string message, MessageKind? kind = null)
    {
        var (detected, text) = Classify(message);
        var dialog = Create(owner, title, text, kind ?? detected);
        dialog.Secondary.Visibility = Visibility.Collapsed;
        dialog.ShowDialog();
    }

    public static bool Confirm(Window? owner, string title, string message, string confirmText, bool danger = true)
    {
        var dialog = Create(owner, title, message, danger ? MessageKind.Warning : MessageKind.Question);
        dialog.Primary.Content = confirmText;
        if (danger) dialog.Primary.Style = (Style)dialog.FindResource("DangerButton");
        dialog.Secondary.Content = "Voltar";
        // The safe answer is the default one for anything that cannot be undone.
        dialog.Primary.IsDefault = !danger;
        dialog.Secondary.IsDefault = danger;
        return dialog.ShowDialog() == true;
    }

    /// <summary>Asks for a line of text. Null when cancelled; an empty answer is not accepted.</summary>
    public static string? Prompt(Window? owner, string title, string message, string? prefill)
    {
        var dialog = Create(owner, title, message, MessageKind.Question);
        dialog._requireInput = true;
        dialog.Input.Visibility = Visibility.Visible;
        dialog.Input.Text = prefill ?? "";
        dialog.Input.SelectAll();
        dialog.Loaded += (_, _) => dialog.Input.Focus();
        return dialog.ShowDialog() == true ? dialog.Input.Text.Trim() : null;
    }

    /// <summary>A dialog set up but not shown (used to render previews).</summary>
    internal static MessageDialog Preview(string title, string message, MessageKind kind, string? dangerText,
        string? input)
    {
        var dialog = Create(null, title, message, kind);
        dialog.Topmost = false;
        dialog.ShowInTaskbar = false;
        if (dangerText is not null)
        {
            dialog.Primary.Content = dangerText;
            dialog.Primary.Style = (Style)dialog.FindResource("DangerButton");
            dialog.Secondary.Content = "Voltar";
        }
        else if (input is null)
        {
            dialog.Secondary.Visibility = Visibility.Collapsed;
        }
        if (input is not null)
        {
            dialog.Input.Visibility = Visibility.Visible;
            dialog.Input.Text = input;
        }
        return dialog;
    }

    private static MessageDialog Create(Window? owner, string title, string message, MessageKind kind)
    {
        var dialog = new MessageDialog { Title = "CTe LogLife" };
        var usableOwner = owner is { IsLoaded: true, IsVisible: true } && owner.WindowState != WindowState.Minimized;
        if (usableOwner)
        {
            dialog.Owner = owner;
        }
        else
        {
            // Raised while the bot has the screen: stay above Bsoft, in the middle of the monitor.
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
            dialog.ShowInTaskbar = true;
        }
        dialog.Heading.Text = title;
        dialog.Body.Text = message;
        dialog.Apply(kind);
        return dialog;
    }

    private void Apply(MessageKind kind)
    {
        var (glyph, color, soft) = kind switch
        {
            MessageKind.Success => ("", "Success", "#E4F5EC"),
            MessageKind.Warning => ("", "Warning", "#FFF4DB"),
            MessageKind.Error => ("", "Danger", "#FCE9EC"),
            MessageKind.Question => ("", "Brand", "#E5F6FD"),
            _ => ("", "Brand", "#E5F6FD"),
        };
        Glyph.Text = glyph;
        Glyph.Foreground = (Brush)FindResource(color);
        Badge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(soft));
    }

    /// <summary>Turns the status emoji the flows put in front of their messages into the dialog's icon.</summary>
    public static (MessageKind Kind, string Text) Classify(string message)
    {
        var text = message.TrimStart();
        foreach (var (prefix, kind) in new[]
                 {
                     ("✅", MessageKind.Success), ("❌", MessageKind.Error),
                     ("⚠️", MessageKind.Warning), ("⚠", MessageKind.Warning),
                 })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return (kind, text[prefix.Length..].TrimStart());
        }
        return (MessageKind.Info, message);
    }

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        if (_requireInput && Input.Text.Trim().Length == 0)
        {
            Input.Focus();
            return;
        }
        DialogResult = true;
    }

    private void OnSecondary(object sender, RoutedEventArgs e) => DialogResult = false;
}
