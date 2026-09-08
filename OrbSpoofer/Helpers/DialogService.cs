using System.Diagnostics;
using System.Windows;

namespace OrbSpoofer.Helpers;

public interface IDialogService
{
    void ShowInfo(string title, string message, string? hint = null);
    Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText = "Later");
    /// <summary>Confirm dialog whose accept button unlocks after N seconds of reading.</summary>
    Task<bool> ShowInformedConsentAsync(string title, string message, int readSeconds, string confirmText, string cancelText = "Go back");
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

    public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText = "Later")
    {
        try
        {
            var owner = _owner as MainWindow ?? Application.Current?.MainWindow as MainWindow;
            if (owner != null)
            {
                var host = owner.DialogHostControl;
                var dialog = new Wpf.Ui.Controls.ContentDialog(host)
                {
                    Title = title,
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = message,
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    PrimaryButtonText = confirmText,
                    CloseButtonText = cancelText,
                    IsSecondaryButtonEnabled = false,
                };
                var result = await dialog.ShowAsync();
                return result == Wpf.Ui.Controls.ContentDialogResult.Primary;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"ContentDialog confirm failed, fallback: {ex.Message}"); }
        try
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
        catch { return false; }
    }

    public async Task<bool> ShowInformedConsentAsync(string title, string message, int readSeconds, string confirmText, string cancelText = "Go back")
    {
        try
        {
            var owner = _owner as MainWindow ?? Application.Current?.MainWindow as MainWindow;
            if (owner == null) return false;
            var host = owner.DialogHostControl;
            var remaining = Math.Max(1, readSeconds);
            var dialog = new Wpf.Ui.Controls.ContentDialog(host)
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                },
                PrimaryButtonText = $"{confirmText} ({remaining})",
                CloseButtonText = cancelText,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
            };
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                try
                {
                    remaining--;
                    if (remaining <= 0)
                    {
                        timer.Stop();
                        dialog.IsPrimaryButtonEnabled = true;
                        dialog.PrimaryButtonText = confirmText;
                    }
                    else dialog.PrimaryButtonText = $"{confirmText} ({remaining})";
                }
                catch { }
            };
            timer.Start();
            try
            {
                var result = await dialog.ShowAsync();
                return result == Wpf.Ui.Controls.ContentDialogResult.Primary;
            }
            finally { try { timer.Stop(); } catch { } }
        }
        catch (Exception ex) { Debug.WriteLine($"Informed consent failed: {ex.Message}"); return false; }
    }
}

public static class UrlLauncher
{
    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Debug.WriteLine($"OpenUrl failed: {ex.Message}"); }
    }

    /// <summary>
    /// Opens Discord quest home in the desktop app via deep link
    /// (<c>discord://-/quest-home</c>), falling back to the browser when
    /// the desktop client or its protocol handler isn't available.
    /// Honors the "use Discord app link" setting (false = browser always).
    /// </summary>
    public static void OpenDiscordQuestHome()
    {
        bool useAppLink = true;
        try { useAppLink = new Infrastructure.Settings.AppSettingsStore().Load().UseDiscordAppLink; } catch { }
        if (!useAppLink) { Open(Config.QuestHomeUrl); return; }
        try { Process.Start(new ProcessStartInfo(Config.QuestHomeDeepLink) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            Debug.WriteLine($"Deep link failed, falling back to browser: {ex.Message}");
            Open(Config.QuestHomeUrl);
        }
    }
}
