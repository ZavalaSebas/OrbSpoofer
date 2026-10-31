using System.Windows;
using System.Windows.Threading;

namespace OrbSpoofer.UI.Windows;

/// <summary>Themed two-button dialog, same chrome/brushes as <see cref="InfoDialog"/>.
/// Native MessageBox and Wpf.Ui ContentDialog both ignore the app's custom theme,
/// so every confirmation (restart prompt, token warnings…) uses this window.</summary>
public partial class ConfirmDialog : Wpf.Ui.Controls.FluentWindow
{
    private int _remaining;

    public bool Result { get; private set; }

    public ConfirmDialog(string title, string message, string primaryText, string cancelText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;
        CancelButton.Content = cancelText;
    }

    /// <summary>Keeps the primary button disabled (and counting down) so the text
    /// is actually read before accepting. Use the same timer for informed consent.</summary>
    public void StartCountdown(int seconds, string primaryText)
    {
        _remaining = Math.Max(1, seconds);
        PrimaryButton.IsEnabled = false;
        PrimaryButton.Content = $"{primaryText} ({_remaining})";

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            _remaining--;
            if (_remaining <= 0)
            {
                timer.Stop();
                PrimaryButton.IsEnabled = true;
                PrimaryButton.Content = primaryText;
            }
            else PrimaryButton.Content = $"{primaryText} ({_remaining})";
        };
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
