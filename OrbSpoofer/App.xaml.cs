using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OrbSpoofer.Helpers;
using OrbSpoofer.Infrastructure.SingleInstance;
using OrbSpoofer.Services;
using OrbSpoofer.ViewModels;

namespace OrbSpoofer;

public partial class App : Application
{
    public static string? ExePathToCleanup { get; private set; }
    public static string? GameName { get; private set; }
    public static string? QuestId { get; private set; }

    private DiscordIpc? _discordIpc;
    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        try { Infrastructure.AppData.AppDataMigrator.MigrateToLatest(); } catch { }
        try { Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark); } catch { }
        global::OrbSpoofer.Services.ThemeManager.ApplyAccent();
        base.OnStartup(e);

        // Single instance (except timer child processes)
        if (!e.Args.Contains("--timer-mode"))
        {
            const string mutex = "OrbSpoofer.SingleInstance.Mutex";
            const string evt = "OrbSpoofer.SingleInstance.ShowWindow";
            if (!ApplicationSingleInstance.TryBecomeOwner(mutex, evt))
            {
                Shutdown();
                return;
            }
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        try { NetworkHelper.Initialize(Services.GetRequiredService<IHttpClientFactory>()); } catch { }

        if (e.Args.Contains("--timer-mode"))
        {
            ExePathToCleanup = null;
            GameName = null;
            QuestId = null;
            string? discordAppId = null;
            for (int i = 0; i < e.Args.Length - 1; i++)
            {
                if (e.Args[i] == "--exe-path") ExePathToCleanup = e.Args[i + 1];
                if (e.Args[i] == "--game-name") GameName = e.Args[i + 1];
                if (e.Args[i] == "--quest-id") QuestId = e.Args[i + 1];
                if (e.Args[i] == "--discord-app-id") discordAppId = e.Args[i + 1];
            }
            if (!string.IsNullOrEmpty(discordAppId))
                _discordIpc = DiscordIpc.TryConnect(discordAppId, GameName);
            Exit += (_, _) => _discordIpc?.Dispose();
            var timer = new UI.Windows.TimerWindow(Config.TimerDurationMinutes, ExePathToCleanup, GameName, QuestId);
            MainWindow = timer;
            timer.Show();
        }
        else
        {
            var main = Services.GetRequiredService<MainWindow>();
            var dialogService = Services.GetRequiredService<DialogService>();
            dialogService.SetOwner(main);
            MainWindow = main;
            main.Show();
            ApplicationSingleInstance.ListenForShowWindowRequests(() =>
            {
                if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
                main.Activate();
                main.Focus();
            });
            Exit += (_, _) => ApplicationSingleInstance.Dispose();
        }
    }

    private static void ConfigureServices(ServiceCollection s)
    {
        s.AddHttpClient("OrbSpoofer", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(Config.RequestTimeout);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("OrbSpoofer/1.0");
        });
        s.AddSingleton<DiscordDatabase>();
        s.AddSingleton<GameFaker>();
        s.AddSingleton<FreeGamesService>();
        s.AddSingleton<DialogService>();
        s.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

        s.AddSingleton<SteamSearchViewModel>();
        s.AddSingleton<DatabaseSearchViewModel>();
        s.AddSingleton<UnifiedSearchViewModel>();
        s.AddSingleton<QuestsViewModel>();
        s.AddSingleton<ManualViewModel>();
        s.AddSingleton<FreeGamesViewModel>();
        s.AddSingleton<MainViewModel>();
        s.AddTransient<MainWindow>();
    }

    private static void TryApplyMica(Window window)
    {
        try
        {
            if (window is Wpf.Ui.Controls.FluentWindow fw)
                fw.WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.Mica;
        }
        catch { }
    }
}
