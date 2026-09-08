using System.Windows.Controls;

namespace OrbSpoofer.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void OpenDiscordLogin_Click(object sender, System.Windows.RoutedEventArgs e) =>
        Helpers.UrlLauncher.Open("https://discord.com/login");
}
