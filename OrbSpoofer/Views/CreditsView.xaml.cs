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
            // Light feedback via clipboard + themed confirmation
            var owner = System.Windows.Window.GetWindow(this);
            var dialog = new OrbSpoofer.UI.Windows.InfoDialog("Link copied",
                $"Copied to clipboard:\n{url}",
                "Paste it in your Discord server to share OrbSpoofer.");
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }
        catch (Exception ex) { Debug.WriteLine($"Share copy failed: {ex.Message}"); }
    }
    private static void Open(string url) { try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); } catch {} }
}
