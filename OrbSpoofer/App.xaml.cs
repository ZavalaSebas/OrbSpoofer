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
