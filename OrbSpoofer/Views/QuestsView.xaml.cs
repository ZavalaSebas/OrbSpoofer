using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbSpoofer.Models;
using OrbSpoofer.ViewModels;

namespace OrbSpoofer.Views;

public partial class QuestsView : UserControl
{
    private CancellationTokenSource? _animCts;

    public QuestsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }
    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is QuestsViewModel oldVm) oldVm.OnQuestsLoaded -= Animate;
        if (e.NewValue is QuestsViewModel vm) { vm.OnQuestsLoaded -= Animate; vm.OnQuestsLoaded += Animate; }
    }
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is QuestsViewModel vm) { vm.OnQuestsLoaded -= Animate; vm.OnQuestsLoaded += Animate; }
    }
    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _animCts?.Cancel();
        _animCts?.Dispose();
        _animCts = null;
        if (DataContext is QuestsViewModel vm) vm.OnQuestsLoaded -= Animate;
    }
    private void Animate()
    {
        _animCts?.Cancel();
        _animCts?.Dispose();
        _animCts = new CancellationTokenSource();
        var token = _animCts.Token;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(async () =>
        {
            try { await AnimateListAsync(token); }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }));
    }
    private async Task AnimateListAsync(CancellationToken token)
    {
        try
        {
            var list = QuestsList;
            // ensure containers are generated after the ItemsSource reset
            list.UpdateLayout();
            await Task.Delay(15, token);
            if (token.IsCancellationRequested) return;

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var dur = TimeSpan.FromSeconds(0.28);

        // prepare only realized containers (virtualization friendly)
        var containers = new List<ListBoxItem>();
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (token.IsCancellationRequested) return;
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem it)
                containers.Add(it);
        }
        if (containers.Count == 0) return;

        foreach (var it in containers)
        {
            it.Opacity = 0;
            it.RenderTransform = new TranslateTransform(0, 10);
        }

        for (int i = 0; i < containers.Count; i++)
        {
            if (token.IsCancellationRequested) return;
            var item = containers[i];
            // skip if container was recycled mid-animation
            if (!item.IsLoaded) continue;
            item.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
            if (item.RenderTransform is TranslateTransform tr)
                tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, dur) { EasingFunction = ease });
            try { await Task.Delay(45, token); } catch (TaskCanceledException) { return; } catch (OperationCanceledException) { return; }
        }
        }
        catch (TaskCanceledException) { return; }
        catch (OperationCanceledException) { return; }
    }
    private void ToggleCompleted_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: QuestItem q } && DataContext is QuestsViewModel vm)
            vm.ToggleCompletedCommand.Execute(q);
    }
}
