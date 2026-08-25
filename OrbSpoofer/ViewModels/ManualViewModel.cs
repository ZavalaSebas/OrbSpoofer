using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class ManualViewModel : ObservableObject
{
    private readonly GameFaker _faker;

    [ObservableProperty] private string _exeName = "";
    [ObservableProperty] private bool _canSpoof;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _statusMessage = "";

    public ManualViewModel(GameFaker faker)
    {
        _faker = faker;
    }

    partial void OnExeNameChanged(string value)
    {
        CanSpoof = !string.IsNullOrWhiteSpace(value);
    }

    [RelayCommand(CanExecute = nameof(CanSpoof))]
    private void Spoof()
    {
        var exeName = ExeName.Trim();
        if (string.IsNullOrEmpty(exeName)) return;
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeName += ".exe";
        exeName = Path.GetFileName(exeName);

        StatusMessage = $"Creating fake process: {exeName}...";
        var path = _faker.CreateFakeGame(exeName);
        if (path != null && _faker.LaunchExecutable(path, out var proc, exeName))
        {
            StatusMessage = $"Running: {exeName}";
            ResultText = $"✓ {exeName} launched successfully!";
            HasResult = true;
            if (proc != null)
            {
                try
                {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try { StatusMessage = proc.ExitCode != 0 ? "Spoof ended" : "Spoof completed"; } catch { }
                    });
                }
                catch { }
            }
        }
        else
        {
            StatusMessage = $"Failed to launch: {exeName}";
            ResultText = "✗ Failed to create the executable";
            HasResult = true;
        }
    }
}
