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
    private void Sponsor_Click(object sender, System.Windows.RoutedEventArgs e) => Open(Config.GitHubSponsorUrl);
    private void Strykey_Click(object sender, MouseButtonEventArgs e) => Open("https://github.com/Strykey");
    private void Orbshacker_Click(object sender, MouseButtonEventArgs e) => Open("https://github.com/strykey/orbshacker");
    private void GitHubProfile_Click(object sender, MouseButtonEventArgs e) => Open(Config.RepoUrl);
    private static void Open(string url) { try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); } catch {} }
}
