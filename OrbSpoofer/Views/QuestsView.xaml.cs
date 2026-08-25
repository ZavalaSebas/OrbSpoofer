using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbSpoofer.Models;
using OrbSpoofer.ViewModels;

namespace OrbSpoofer.Views;

public partial class QuestsView : UserControl
{
    public QuestsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is QuestsViewModel vm) { vm.OnQuestsLoaded -= Animate; vm.OnQuestsLoaded += Animate; }
    }
    private void Animate()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(async () => await AnimateListAsync()));
    }
    private async Task AnimateListAsync()
    {
        var list = QuestsList;
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
    private void ToggleCompleted_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: QuestItem q } && DataContext is QuestsViewModel vm)
            vm.ToggleCompletedCommand.Execute(q);
    }
}
