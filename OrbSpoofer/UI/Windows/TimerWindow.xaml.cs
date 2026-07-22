using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrbSpoofer.UI.Windows;

public partial class TimerWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly int _totalSeconds;
    private readonly string? _exePathToCleanup;
    private int _remaining;
    private bool _cleanedUp;

    private static readonly SolidColorBrush CompleteBrush =
        new((Color)ColorConverter.ConvertFromString(Config.TimerCompleteColor));

    public TimerWindow(int durationMinutes, string? exePathToCleanup = null, string? gameName = null)
    {
        InitializeComponent();

        _totalSeconds = durationMinutes * 60 + Config.TimerExtraSeconds;
        _remaining = _totalSeconds;
        _exePathToCleanup = exePathToCleanup;

        GameNameText.Text = gameName ?? "Unknown";
        GameNameText.ToolTip = gameName ?? "Unknown";

        CenterOnScreen();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void CenterOnScreen()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = (screenWidth - Width) / 2;
        Top = (screenHeight - Height) / 2;
    }

    private void Cleanup()
    {
        if (_exePathToCleanup == null || _cleanedUp) return;
        _cleanedUp = true;

        try
        {
            if (File.Exists(_exePathToCleanup))
                File.Delete(_exePathToCleanup);

            var dir = Path.GetDirectoryName(_exePathToCleanup);
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cleanup failed: {ex.Message}");
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_remaining > 0)
            _remaining--;

        if (_remaining <= 0)
        {
            _timer.Stop();
            TimerText.Text = "00:00";
            TimerText.Foreground = CompleteBrush;
            StatusText.Text = "Complete";
            StatusText.Foreground = CompleteBrush;
            StatusDot.Fill = CompleteBrush;
            TimerProgress.Value = 0;

            SystemSounds.Asterisk.Play();

            Activate();
            Topmost = true;

            Dispatcher.BeginInvoke(() =>
            {
                Cleanup();
                Application.Current.Shutdown();
            });
            return;
        }

        var displaySecs = Math.Max(0, _remaining - Config.TimerExtraSeconds);
        var m = displaySecs / 60;
        var s = displaySecs % 60;
        TimerText.Text = $"{m:D2}:{s:D2}";
        TimerProgress.Value = (double)_remaining / _totalSeconds * 100;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        Cleanup();
        Application.Current.Shutdown();
        base.OnClosed(e);
    }
}
