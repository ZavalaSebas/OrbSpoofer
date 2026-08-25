using System.Diagnostics;
using System.Windows;

namespace OrbSpoofer.Helpers;

public interface IDialogService
{
    void ShowInfo(string title, string message, string? hint = null);
}

public sealed class DialogService : IDialogService
{
    private Window? _owner;

    public void SetOwner(Window owner) => _owner = owner;

    public void ShowInfo(string title, string message, string? hint = null)
    {
        var full = string.IsNullOrWhiteSpace(hint) ? message : $"{message}\n\n{hint}";
        try
        {
            var owner = _owner as MainWindow ?? Application.Current?.MainWindow as MainWindow;
            if (owner != null)
            {
                try
                {
                    var host = owner.DialogHostControl;
                    var dialog = new Wpf.Ui.Controls.ContentDialog(host)
                    {
                        Title = title,
                        Content = new System.Windows.Controls.TextBlock
                        {
                            Text = full,
                            TextWrapping = System.Windows.TextWrapping.Wrap,
                            Margin = new Thickness(0, 8, 0, 0)
                        },
                        CloseButtonText = "OK",
                        IsPrimaryButtonEnabled = false,
                        IsSecondaryButtonEnabled = false
                    };
                    // ShowAsync must be awaited on UI thread; use ShowAsync().ConfigureAwait
                    _ = dialog.ShowAsync();
                    return;
                }
                catch (Exception ex) { Debug.WriteLine($"ContentDialog failed, fallback: {ex.Message}"); }
            }
            var fallback = new UI.Windows.InfoDialog(title, message, hint ?? "")
            {
                Owner = _owner ?? Application.Current?.MainWindow
            };
            fallback.ShowDialog();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DialogService.ShowInfo failed: {ex.Message}");
        }
    }
}

public static class UrlLauncher
{
    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Debug.WriteLine($"OpenUrl failed: {ex.Message}"); }
    }
}
