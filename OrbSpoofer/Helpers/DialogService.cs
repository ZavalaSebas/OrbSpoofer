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
        try
        {
            var owner = OwnerWindow;
            var lines = new List<string> { message };
            if (!string.IsNullOrWhiteSpace(hint)) lines.Add(hint);
            var dialog = new UI.Windows.InfoDialog(title, lines.ToArray());
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DialogService.ShowInfo failed: {ex.Message}");
        }
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText = "Later")
    {
        try
        {
            var owner = OwnerWindow;
            if (owner != null)
            {
                var dialog = new UI.Windows.ConfirmDialog(title, message, confirmText, cancelText) { Owner = owner };
                dialog.ShowDialog();
                return Task.FromResult(dialog.Result);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"ConfirmDialog failed, fallback: {ex.Message}"); }

        try
        {
            return Task.FromResult(MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        }
        catch { return Task.FromResult(false); }
    }

    public Task<bool> ShowInformedConsentAsync(string title, string message, int readSeconds, string confirmText, string cancelText = "Go back")
    {
        try
        {
            var owner = OwnerWindow;
            if (owner == null) return Task.FromResult(false);
            var dialog = new UI.Windows.ConfirmDialog(title, message, confirmText, cancelText) { Owner = owner };
            dialog.StartCountdown(readSeconds, confirmText);
            dialog.ShowDialog();
            return Task.FromResult(dialog.Result);
        }
        catch (Exception ex) { Debug.WriteLine($"Informed consent failed: {ex.Message}"); return Task.FromResult(false); }
    }

    /// <summary>Window dialogs should hang off the visible shell; the timer window
    /// or a closed main window both make ShowDialog throw.</summary>
    private Window? OwnerWindow
    {
        get
        {
            if (_owner is { IsVisible: true }) return _owner;
            var main = Application.Current?.MainWindow;
            return main is { IsVisible: true } ? main : null;
        }
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
