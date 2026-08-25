using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrbSpoofer.UI.Windows;

public partial class TimerWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly DispatcherTimer _timer;
    private readonly int _totalSeconds;
    private readonly string? _exePathToCleanup;
    private readonly string? _questId;
    private int _remaining;
    private bool _cleanedUp;
    private bool _completed;

    private static readonly SolidColorBrush CompleteBrush =
        new((Color)ColorConverter.ConvertFromString(Config.TimerCompleteColor));

    public TimerWindow(int durationMinutes, string? exePathToCleanup = null, string? gameName = null, string? questId = null)
    {
        InitializeComponent();

        _totalSeconds = durationMinutes * 60 + Config.TimerExtraSeconds;
        _remaining = _totalSeconds;
        _exePathToCleanup = exePathToCleanup;
        _questId = questId;

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
            var exeDir = Path.GetDirectoryName(_exePathToCleanup);
            var batPath = Path.Combine(
                Path.GetTempPath(),
                $"OrbSpoofer_cleanup_{Guid.NewGuid()}.bat");

            // Use timeout for reliability and /s for steam dirs that may have been created
            var batContent = $"""
@echo off
@timeout /t 2 /nobreak > nul
@del /f /q "{_exePathToCleanup}" 2>nul
@if exist "{exeDir}" @rmdir /s /q "{exeDir}" 2>nul
@del "%~f0" 2>nul
""";
            File.WriteAllText(batPath, batContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            // also try immediate delete for non-locked files (helps when timer already exited)
            try { if (File.Exists(_exePathToCleanup)) File.Delete(_exePathToCleanup); } catch { }
            try { if (exeDir != null && Directory.Exists(exeDir) && !Directory.EnumerateFileSystemEntries(exeDir).Any()) Directory.Delete(exeDir); } catch { }
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

            if (!string.IsNullOrEmpty(_questId))
            {
                var completedIds = Config.LoadCompletedQuestIds();
                completedIds.Add(_questId);
                Config.SaveCompletedQuestIds(completedIds);
            }
            _completed = true;
            Environment.ExitCode = 0;
            Cleanup();
            Application.Current.Shutdown();
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
        if (!_completed)
            Environment.ExitCode = 1;
        Cleanup();
        Application.Current.Shutdown();
        base.OnClosed(e);
    }
}
