using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using OrbSpoofer.ViewModels;

namespace OrbSpoofer;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public Wpf.Ui.Controls.ContentDialogHost DialogHostControl => DialogHost;
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _vm.Cleanup();
        _vm.PropertyChanged += OnVmPropertyChanged;
        LocationChanged += (_, _) => { if (NotificationsPopup.IsOpen) NotificationsPopup.IsOpen = false; if (AccentPopup.IsOpen) AccentPopup.IsOpen = false; };
        Deactivated += (_, _) => { if (NotificationsPopup.IsOpen) NotificationsPopup.IsOpen = false; };
        PreviewMouseDown += OnPreviewMouseDown;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Close popups when clicking outside them (StaysOpen=False already handles most, but ensure)
        if (NotificationsPopup.IsOpen && !NotificationsPopup.IsMouseOver && !BellButton.IsMouseOver)
            NotificationsPopup.IsOpen = false;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync(msg => { /* progress handled via binding */ });
        if (UI.Windows.WelcomeWindow.ShouldShow())
        {
            var w = new UI.Windows.WelcomeWindow { Owner = this };
            w.ShowDialog();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SidebarCollapsed))
        {
            AnimateSidebar(_vm.SidebarCollapsed);
        }
    }

    private void AnimateSidebar(bool collapsed)
    {
        var target = collapsed ? 48 : 220;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Animate ColumnDefinition.Width via animation on Width property workaround
        // We animate the Border width and sync column via storyboard completing
        var widthAnim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = ease
        };
        SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);

        // Animate GridLength via timer — smooth interpolation
        var from = SidebarColumn.Width.Value;
        var anim = new DoubleAnimation(from, target, new Duration(TimeSpan.FromMilliseconds(280))) { EasingFunction = ease };
        var clock = anim.CreateClock();
        clock.CurrentTimeInvalidated += (_, _) =>
        {
            if (clock.CurrentProgress is double p && !double.IsNaN(p))
            {
                var v = from + (target - from) * p;
                Dispatcher.Invoke(() => SidebarColumn.Width = new GridLength(v));
            }
        };
        clock.Completed += (_, _) => SidebarColumn.Width = new GridLength(target);
        clock.Controller?.Begin();
    }

    // Bell popup positioning (view-only, stays in code-behind)
    private void BellButton_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationsPopup.IsOpen) { NotificationsPopup.IsOpen = false; return; }
        PositionNotificationsPopup();
        NotificationsPopup.IsOpen = true;
        _vm.FreeGames.MarkSeenOnOpen();
        _ = _vm.FreeGames.RefreshCommand.ExecuteAsync(null);
    }

    private void PositionNotificationsPopup()
    {
        if (NotificationsPopup.Child is not FrameworkElement content) return;
        content.Measure(new Size(360, double.PositiveInfinity));
        var width = content.DesiredSize.Width > 0 ? content.DesiredSize.Width : 360;
        var transform = BellButton.TransformToVisual(this);
        var bellPos = transform.Transform(new Point(0, 0));
        NotificationsPopup.PlacementTarget = this;
        NotificationsPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
        NotificationsPopup.HorizontalOffset = bellPos.X + BellButton.ActualWidth - width;
        NotificationsPopup.VerticalOffset = bellPos.Y + BellButton.ActualHeight + 8;
    }

    private void AccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (AccentPopup.IsOpen) { AccentPopup.IsOpen = false; return; }
        if (AccentPopup.Child is FrameworkElement content)
        {
            content.Measure(new Size(220, double.PositiveInfinity));
            var w = content.DesiredSize.Width > 0 ? content.DesiredSize.Width : 220;
            var t = AccentButton.TransformToVisual(this).Transform(new Point(0, 0));
            AccentPopup.PlacementTarget = this;
            AccentPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            AccentPopup.HorizontalOffset = t.X + AccentButton.ActualWidth - w;
            AccentPopup.VerticalOffset = t.Y + AccentButton.ActualHeight + 8;
        }
        AccentPopup.IsOpen = true;
    }

    private void AccentColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string hex })
        {
            Services.ThemeManager.ApplyAccent(hex);
            AccentPopup.IsOpen = false;
        }
    }

    private void AdvancedToggle_Click(object sender, MouseButtonEventArgs e) => _vm.ToggleAdvancedCommand.Execute(null);

    private void BtnMarkAllSeen_Click(object sender, RoutedEventArgs e)
    {
        _vm.FreeGames.MarkAllSeenCommand.Execute(null);
        NotificationsPopup.IsOpen = false;
    }

    private void BtnClosePopup_Click(object sender, RoutedEventArgs e) => NotificationsPopup.IsOpen = false;

    private void NotificationsPopup_Closed(object sender, EventArgs e) { }

    private void ClaimButton_Click(object sender, RoutedEventArgs e)
    {
        Services.FreeGameNotification? game = null;
        if (sender is System.Windows.Controls.Button btn)
        {
            if (btn.Tag is Services.FreeGameNotification t) game = t;
            else if (btn.DataContext is Services.FreeGameNotification dc) game = dc;
            else if (btn.CommandParameter is Services.FreeGameNotification cp) game = cp;
        }
        if (game == null && sender is FrameworkElement fe && fe.DataContext is Services.FreeGameNotification pdc) game = pdc;
        if (game == null)
        {
            try
            {
                var parent = (sender as FrameworkElement)?.Parent as FrameworkElement;
                while (parent != null && game == null)
                {
                    if (parent.DataContext is Services.FreeGameNotification pd) game = pd;
                    parent = parent.Parent as FrameworkElement;
                }
            }
            catch { }
        }
        if (game == null) return;
        try { _vm.FreeGames.ClaimCommand.Execute(game); } catch { }
    }

    private void UpdateReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.PendingUpdateTag == null || _vm.PendingUpdateUrl == null) return;
        var w = new UI.Windows.UpdateWindow(_vm.PendingUpdateTag, _vm.PendingUpdateUrl) { Owner = this };
        w.ShowDialog();
    }

    private void Kofi_HeartClick(object sender, MouseButtonEventArgs e) => Helpers.UrlLauncher.Open(Config.KofiUrl);

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = $"{Config.RepoUrl}/releases/latest";
            System.Windows.Clipboard.SetText(url);
            _vm.StatusMessage = $"Share link copied — {url}";
            try
            {
                var host = DialogHostControl;
                var dialog = new Wpf.Ui.Controls.ContentDialog(host)
                {
                    Title = "Link copied",
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = $"Copied to clipboard:\n{url}\n\nPaste it in your Discord server to share OrbSpoofer.",
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    CloseButtonText = "OK",
                    IsPrimaryButtonEnabled = false,
                    IsSecondaryButtonEnabled = false
                };
                _ = dialog.ShowAsync();
            }
            catch { }
        }
        catch (Exception ex) { Debug.WriteLine($"Share copy failed: {ex.Message}"); }
    }
}
