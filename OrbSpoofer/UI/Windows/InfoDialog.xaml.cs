using System.Windows;
using System.Windows.Documents;

namespace OrbSpoofer.UI.Windows;

public partial class InfoDialog : Window
{
    public InfoDialog(string title, params string[] lines)
    {
        InitializeComponent();
        TitleText.Text = title;

        for (int i = 0; i < lines.Length; i++)
        {
            MessageText.Inlines.Add(new Run(lines[i]));
            if (i < lines.Length - 1)
                MessageText.Inlines.Add(new LineBreak());
        }

        Owner = Application.Current.MainWindow;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
}
