using System;
using System.Windows;
using OrbSpoofer.Services;

namespace OrbSpoofer.UI.Windows;

public partial class UpdateWindow : Window
{
    private readonly string _tagName;
    private readonly string _downloadUrl;

    public bool WasSkipped { get; private set; }

    public UpdateWindow(string tagName, string downloadUrl)
    {
        InitializeComponent();
        _tagName = tagName;
        _downloadUrl = downloadUrl;
        VersionText.Text = $"OrbSpoofer {tagName} is available";
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        SkipBtn.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        var success = await Updater.DownloadAndApplyUpdateAsync(_downloadUrl,
            new Progress<double>(p =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    var pct = Math.Min(100, (int)(p * 100));
                    ProgressBar.Value = pct;
                    ProgressText.Text = pct >= 100 ? "Restarting..." : $"{pct}%";
                });
            }));

        if (!success)
        {
            ProgressText.Text = "Update failed";
            UpdateBtn.IsEnabled = true;
            SkipBtn.IsEnabled = true;
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        WasSkipped = true;
        Close();
    }
}
