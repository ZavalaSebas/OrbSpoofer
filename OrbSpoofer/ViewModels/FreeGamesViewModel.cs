using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Helpers;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class FreeGamesViewModel : ObservableObject
{
    private readonly FreeGamesService _service;

    [ObservableProperty] private ObservableCollection<FreeGameNotification> _games = [];
    [ObservableProperty] private bool _hasUnseen;
    [ObservableProperty] private int _unseenCount;
    [ObservableProperty] private bool _isEmpty = true;

    public FreeGamesViewModel(FreeGamesService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var all = await _service.GetFreeGamesAsync();
            var visible = all.Take(10).ToList();
            Games = new ObservableCollection<FreeGameNotification>(visible);
            IsEmpty = visible.Count == 0;

            var seen = FreeGamesSeenStore.Load();
            var unseen = visible.Count(g => !seen.Contains(g.Id));
            UnseenCount = unseen;
            HasUnseen = unseen > 0;
        }
        catch { }
    }

    [RelayCommand]
    private void MarkAllSeen()
    {
        FreeGamesSeenStore.MarkSeen(Games.Select(g => g.Id));
        HasUnseen = false;
        UnseenCount = 0;
    }

    [RelayCommand]
    private void Claim(FreeGameNotification? game)
    {
        if (game == null) return;
        var url = !string.IsNullOrWhiteSpace(game.OpenGiveawayUrl) ? game.OpenGiveawayUrl : game.GamerpowerUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        UrlLauncher.Open(url);
        FreeGamesSeenStore.MarkSeen([game.Id]);
    }

    public void MarkSeenOnOpen()
    {
        if (!HasUnseen) return;
        FreeGamesSeenStore.MarkSeen(Games.Select(g => g.Id));
        HasUnseen = false;
        UnseenCount = 0;
    }
}
