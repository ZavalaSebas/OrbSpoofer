using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace OrbSpoofer.Views;

public partial class CreditsView : UserControl
{
    public CreditsView() => InitializeComponent();
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) { try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true}); } catch {} e.Handled=true; }
    private void Kofi_Click(object sender, System.Windows.RoutedEventArgs e) => Open(Config.KofiUrl);
    private void Kofi_Click(object sender, MouseButtonEventArgs e) => Open(Config.KofiUrl);
    private void Trang_Click(object sender, MouseButtonEventArgs e) => Open("https://ko-fi.com/home/coffeeshop?ReturnUrl=/&txid=a305e485-d6ed-4d27-9f32-8504df66f072");
    private void Sponsor_Click(object sender, System.Windows.RoutedEventArgs e) => Open(Config.GitHubSponsorUrl);
    private void Strykey_Click(object sender, MouseButtonEventArgs e) => Open("https://github.com/Strykey");
    private void Orbshacker_Click(object sender, MouseButtonEventArgs e) => Open("https://github.com/strykey/orbshacker");
    private void GitHubProfile_Click(object sender, MouseButtonEventArgs e) => Open(Config.RepoUrl);
    private void Share_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var url = $"{Config.RepoUrl}/releases/latest";
            System.Windows.Clipboard.SetText(url);
            // Light feedback via clipboard + open fallback if needed
            try
            {
                var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
                var host = win?.DialogHostControl;
                if (host != null)
                {
                    var dialog = new Wpf.Ui.Controls.ContentDialog(host)
                    {
                        Title = "Link copied",
                        Content = new System.Windows.Controls.TextBlock
                        {
                            Text = $"Copied to clipboard:\n{url}\n\nPaste it in your Discord server to share OrbSpoofer.",
                            TextWrapping = System.Windows.TextWrapping.Wrap,
                            Margin = new System.Windows.Thickness(0, 8, 0, 0)
                        },
                        CloseButtonText = "OK",
                        IsPrimaryButtonEnabled = false,
                        IsSecondaryButtonEnabled = false
                    };
                    _ = dialog.ShowAsync();
                    return;
                }
            }
            catch { }
            System.Windows.MessageBox.Show($"Copied to clipboard:\n{url}", "OrbSpoofer — Share", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex) { Debug.WriteLine($"Share copy failed: {ex.Message}"); }
    }
    private static void Open(string url) { try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); } catch {} }
}
