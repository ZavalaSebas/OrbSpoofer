using System.Diagnostics;
using System.IO;
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

    private static readonly SolidColorBrush CompleteBrush =
        new((Color)ColorConverter.ConvertFromString(Config.TimerCompleteColor));

    public TimerWindow(int durationMinutes, string? exePathToCleanup = null)
    {
        InitializeComponent();

        _totalSeconds = durationMinutes * 60;
        _remaining = _totalSeconds;
        _exePathToCleanup = exePathToCleanup;

        PositionToTopRight();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void PositionToTopRight()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        Left = screenWidth - Width - 16;
        Top = 16;
    }

    private void Cleanup()
    {
        if (_exePathToCleanup == null) return;

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
        {
            _remaining--;
        }
        else
        {
            _timer.Stop();
            TimerText.Text = "00:00";
            TimerText.Foreground = CompleteBrush;
            StatusText.Text = "Complete";
            StatusText.Foreground = CompleteBrush;
            StatusDot.Fill = CompleteBrush;
            TimerProgress.Value = 0;

            Dispatcher.BeginInvoke(() =>
            {
                Cleanup();
                Application.Current.Shutdown();
            });
            return;
        }

        var m = _remaining / 60;
        var s = _remaining % 60;
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
