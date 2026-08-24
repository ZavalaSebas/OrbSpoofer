using System.Windows;
using OrbSpoofer.Services;

namespace OrbSpoofer;

    public partial class App : Application
    {
        public static string? ExePathToCleanup { get; private set; }
        public static string? GameName { get; private set; }
        public static string? QuestId { get; private set; }

        private DiscordIpc? _discordIpc;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Fluent dark + accent before any window shows (mirrors Bridge/App.xaml.cs)
            try { Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark); } catch { }
            base.OnStartup(e);

            if (e.Args.Contains("--timer-mode"))
            {
                ExePathToCleanup = null;
                GameName = null;
                QuestId = null;
                string? discordAppId = null;

                for (int i = 0; i < e.Args.Length - 1; i++)
                {
                    if (e.Args[i] == "--exe-path")
                        ExePathToCleanup = e.Args[i + 1];
                    if (e.Args[i] == "--game-name")
                        GameName = e.Args[i + 1];
                    if (e.Args[i] == "--quest-id")
                        QuestId = e.Args[i + 1];
                    if (e.Args[i] == "--discord-app-id")
                        discordAppId = e.Args[i + 1];
                }

                if (!string.IsNullOrEmpty(discordAppId))
                    _discordIpc = DiscordIpc.TryConnect(discordAppId, GameName);

                Exit += (_, _) => _discordIpc?.Dispose();

                var timer = new UI.Windows.TimerWindow(Config.TimerDurationMinutes, ExePathToCleanup, GameName, QuestId);
                timer.Show();
                TryApplyMica(timer);
                MainWindow = timer;
            }
            else
            {
                var main = new MainWindow();
                main.Show();
                TryApplyMica(main);
                MainWindow = main;
            }
        }

        private static void TryApplyMica(Window window)
        {
            try
            {
                // Mica on Win11, fallback to solid on Win10 (Bridge pattern)
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    Wpf.Ui.Controls.WindowBackdropType.Mica,
                    updateAccent: false);
                // Also set per-window backdrop if API available
                if (window is Wpf.Ui.Controls.FluentWindow fw)
                    fw.WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.Mica;
            }
            catch { }
        }
    }
