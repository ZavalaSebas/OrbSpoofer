using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbSpoofer.ViewModels;

namespace OrbSpoofer.Views;

public partial class SteamView : UserControl
{
    public SteamView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SteamSearchViewModel vm) { vm.OnSearchPerformed -= Animate; vm.OnSearchPerformed += Animate; }
    }
    private void Animate()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(async () => await AnimateListAsync()));
    }
    private async Task AnimateListAsync()
    {
        var list = SteamResultsList;
        for (int i = 0; i < list.Items.Count; i++) if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem it) { it.Opacity = 0; it.RenderTransform = new TranslateTransform(0,16); }
        await Task.Delay(15);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var dur = TimeSpan.FromSeconds(0.35);
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
            {
                item.BeginAnimation(OpacityProperty, new DoubleAnimation(0,1,dur){EasingFunction=ease});
                var tr=new TranslateTransform(0,16); item.RenderTransform=tr; tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16,0,dur){EasingFunction=ease});
            }
            await Task.Delay(60);
        }
    }
    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SteamSearchViewModel vm && SteamResultsList.SelectedItem is Models.SteamGameDisplayItem item) vm.SpoofCommand.Execute(item);
    }
}
