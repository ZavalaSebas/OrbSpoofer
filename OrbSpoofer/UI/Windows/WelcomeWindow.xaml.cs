using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrbSpoofer.UI.Windows;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadWhatsNewFromChangelog();

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var headerSlide = new TranslateTransform(0, -15);
        HeaderBorder.RenderTransform = headerSlide;
        HeaderBorder.Opacity = 0;

        var cards = new[] { CardWhatsNew, CardHowItWorks, CardSupport };
        var cardSlides = new TranslateTransform[3];
        for (int i = 0; i < 3; i++)
        {
            cardSlides[i] = new TranslateTransform(0, 20);
            cards[i].RenderTransform = cardSlides[i];
            cards[i].Opacity = 0;
        }

        var footerSlide = new TranslateTransform(0, 15);
        FooterBorder.RenderTransform = footerSlide;
        FooterBorder.Opacity = 0;

        HeaderBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35)) { EasingFunction = ease });
        headerSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-15, 0, TimeSpan.FromSeconds(0.35)) { EasingFunction = ease });

        await Task.Delay(180);

        for (int i = 0; i < 3; i++)
        {
            cards[i].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)) { EasingFunction = ease });
            cardSlides[i].BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(20, 0, TimeSpan.FromSeconds(0.3)) { EasingFunction = ease });
            await Task.Delay(100);
        }

        FooterBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25)) { EasingFunction = ease });
        footerSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(15, 0, TimeSpan.FromSeconds(0.25)) { EasingFunction = ease });
    }

    private void LoadWhatsNewFromChangelog()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("OrbSpoofer.Resources.WhatsNew.txt");
            if (stream == null) { SetFallbackWhatsNew(); return; }

            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0) { SetFallbackWhatsNew(); return; }

            WhatsNewHeader.Text = $"What's new in v{Config.Version}";
            foreach (var line in lines)
            {
                WhatsNewItems.Children.Add(new TextBlock
                {
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    LineHeight = 18,
                    Text = $"• {line.Trim()}",
                });
            }
        }
        catch
        {
            SetFallbackWhatsNew();
        }
    }

    private void SetFallbackWhatsNew()
    {
        WhatsNewHeader.Text = $"What's new in v{Config.Version}";
        var brush = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        WhatsNewItems.Children.Add(new TextBlock
        {
            FontSize = 11, Foreground = brush, LineHeight = 18,
            Text = "• Timer grace period to ensure Discord detects the game",
        });
        WhatsNewItems.Children.Add(new TextBlock
        {
            FontSize = 11, Foreground = brush, LineHeight = 18,
            Text = "• Initial release quality-of-life improvements",
        });
    }

    public static bool ShouldShow()
    {
        try
        {
            var flagPath = Path.Combine(Config.AppDataPath, Config.WelcomeSentinelFile);
            return !File.Exists(flagPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WelcomeWindow.ShouldShow failed: {ex.Message}");
            return true;
        }
    }

    private void Kofi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Config.KofiUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to open Kofi: {ex.Message}"); }
    }

    private void GitHubSponsor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Config.GitHubSponsorUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to open GitHub sponsor: {ex.Message}"); }
    }

    private void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        if (DontShowAgainCheck.IsChecked == true)
        {
            try
            {
                Directory.CreateDirectory(Config.AppDataPath);
                var flagPath = Path.Combine(Config.AppDataPath, Config.WelcomeSentinelFile);
                File.WriteAllText(flagPath, Config.Version);
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to write welcome sentinel: {ex.Message}"); }
        }

        Close();
    }
}
