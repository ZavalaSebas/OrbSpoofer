using System.Windows;

namespace OrbSpoofer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--timer-mode"))
        {
            var timer = new UI.Windows.TimerWindow(Config.TimerDurationMinutes);
            timer.Show();
            MainWindow = timer;
        }
        else
        {
            var main = new MainWindow();
            main.Show();
            MainWindow = main;
        }
    }
}
