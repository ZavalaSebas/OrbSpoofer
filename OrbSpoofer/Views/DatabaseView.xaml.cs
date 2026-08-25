using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbSpoofer.ViewModels;

namespace OrbSpoofer.Views;

public partial class DatabaseView : UserControl
{
    public DatabaseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DatabaseSearchViewModel vm)
        {
            vm.OnSearchPerformed -= Animate;
            vm.OnSearchPerformed += Animate;
        }
    }

    private void Animate(bool animate)
    {
        if (!animate) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(async () => await AnimateListAsync()));
    }

    private async Task AnimateListAsync()
    {
        var list = ResultsList;
        for (int i = 0; i < list.Items.Count; i++)
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem it) { it.Opacity = 0; it.RenderTransform = new TranslateTransform(0, 16); }
        await Task.Delay(15);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromSeconds(0.35);
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
            {
                item.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
                var tr = new TranslateTransform(0, 16); item.RenderTransform = tr;
                tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, duration) { EasingFunction = ease });
            }
            await Task.Delay(60);
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DatabaseSearchViewModel vm && ResultsList.SelectedItem is Models.GameDisplayItem item)
            vm.SpoofCommand.Execute(item);
    }
}
