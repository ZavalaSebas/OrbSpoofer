using System.Windows;

namespace OrbSpoofer;

public partial class App : Application
{
    public static string? ExePathToCleanup { get; private set; }
    public static string? GameName { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--timer-mode"))
        {
            ExePathToCleanup = null;
            GameName = null;

            for (int i = 0; i < e.Args.Length - 1; i++)
            {
                if (e.Args[i] == "--exe-path")
                    ExePathToCleanup = e.Args[i + 1];
                if (e.Args[i] == "--game-name")
                    GameName = e.Args[i + 1];
            }

            var timer = new UI.Windows.TimerWindow(Config.TimerDurationMinutes, ExePathToCleanup, GameName);
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
