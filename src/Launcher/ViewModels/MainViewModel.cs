using System.Collections.ObjectModel;
using Launcher.Models;
using Launcher.Services;

namespace Launcher.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly LauncherConfig _config;
    private readonly InstalledDb _db;

    public ObservableCollection<GameViewModel> Games { get; } = new();

    private string _launcherStatus = "";
    public string LauncherStatus { get => _launcherStatus; private set => Set(ref _launcherStatus, value); }

    public RelayCommand RefreshCommand { get; }

    public MainViewModel()
    {
        InstallService.CleanTemp();
        _config = LauncherConfig.Load();
        _db = InstalledDb.Load();
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Games.Any(g => g.State == GameState.Busy))
        {
            LauncherStatus = "Avval yuklanish tugashini kuting";
            return;
        }

        LauncherStatus = "Katalog yuklanmoqda...";
        Games.Clear();
        try
        {
            var catalog = await CatalogService.FetchAsync(_config.CatalogUrl);
            foreach (var game in catalog.Games)
                Games.Add(new GameViewModel(game, _db));
            LauncherStatus = Games.Count == 0 ? "Katalogda o'yin yo'q" : $"{Games.Count} ta o'yin";
        }
        catch (Exception ex)
        {
            LauncherStatus = "Katalogni yuklab bo'lmadi: " + ex.Message;
        }
    }
}
