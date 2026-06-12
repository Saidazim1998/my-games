using System.Collections.ObjectModel;
using System.Diagnostics;
using Launcher.Models;
using Launcher.Services;

namespace Launcher.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly LauncherConfig _config;
    private readonly InstalledDb _db;

    public ObservableCollection<GameViewModel> Games { get; } = new();

    private GameViewModel? _selectedGame;
    public GameViewModel? SelectedGame { get => _selectedGame; set => Set(ref _selectedGame, value); }

    private string _launcherStatus = "";
    public string LauncherStatus { get => _launcherStatus; private set => Set(ref _launcherStatus, value); }

    // Sozlamalar tabi
    private string _catalogUrlInput = "";
    public string CatalogUrlInput { get => _catalogUrlInput; set => Set(ref _catalogUrlInput, value); }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand OpenRootCommand { get; }

    public MainViewModel()
    {
        InstallService.CleanTemp();
        _config = LauncherConfig.Load();
        _db = InstalledDb.Load();
        CatalogUrlInput = _config.CatalogUrl;
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        SaveSettingsCommand = new RelayCommand(async () => await SaveSettingsAsync());
        OpenRootCommand = new RelayCommand(() => Process.Start("explorer.exe", AppPaths.Root));
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
        string? keepSelected = SelectedGame?.Game.Id;
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
        SelectedGame = Games.FirstOrDefault(g => g.Game.Id == keepSelected) ?? Games.FirstOrDefault();
    }

    private async Task SaveSettingsAsync()
    {
        _config.CatalogUrl = CatalogUrlInput.Trim();
        _config.Save();
        LauncherStatus = "Sozlamalar saqlandi";
        await LoadAsync();
    }
}
