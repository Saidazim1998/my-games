using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Launcher.Models;
using Launcher.Services;

namespace Launcher.ViewModels;

public enum GameState { NotInstalled, UpdateAvailable, Installed, Busy }

/// <summary>
/// Bitta o'yinning holat mashinasi. Barcha property o'zgarishlari UI threadda
/// bo'ladi: buyruqlar UI threaddan await qilinadi, Progress&lt;T&gt; ham UI threadda yaratiladi.
/// </summary>
public class GameViewModel : ObservableObject
{
    private readonly InstalledDb _db;
    private CancellationTokenSource? _cts;

    public CatalogGame Game { get; }

    private GameState _state;
    public GameState State
    {
        get => _state;
        private set
        {
            if (!Set(ref _state, value)) return;
            OnPropertyChanged(nameof(MainButtonLabel));
            OnPropertyChanged(nameof(ListStatusText));
            OnPropertyChanged(nameof(VersionText));
            OnPropertyChanged(nameof(ProgressVisibility));
            OnPropertyChanged(nameof(CancelVisibility));
            MainCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
            UninstallCommand.RaiseCanExecuteChanged();
        }
    }

    private string? _installedVersion;
    public string? InstalledVersion
    {
        get => _installedVersion;
        private set { if (Set(ref _installedVersion, value)) OnPropertyChanged(nameof(VersionText)); }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        private set { if (Set(ref _progress, value)) OnPropertyChanged(nameof(ProgressText)); }
    }

    private bool _isIndeterminate;
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set { if (Set(ref _isIndeterminate, value)) OnPropertyChanged(nameof(ProgressText)); }
    }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    private ImageSource _cover = CoverImageService.Placeholder;
    public ImageSource Cover { get => _cover; private set => Set(ref _cover, value); }

    /// <summary>Asosiy tugma: o'rnatilmagan bo'lsa O'RNATISH, aks holda O'YNASH.</summary>
    public string MainButtonLabel => State switch
    {
        GameState.NotInstalled => "O'RNATISH",
        GameState.Busy => "KUTING...",
        _ => "▶  O'YNASH",
    };

    /// <summary>Chap ro'yxatdagi kichik holat yozuvi.</summary>
    public string ListStatusText => State switch
    {
        GameState.NotInstalled => "O'rnatilmagan",
        GameState.UpdateAvailable => "Yangilanish bor!",
        GameState.Busy => "Yuklanmoqda...",
        _ => "O'rnatilgan",
    };

    public string VersionText => InstalledVersion == null
        ? $"Versiya: {Game.Version}"
        : VersionUtil.IsNewer(Game.Version, InstalledVersion)
            ? $"O'rnatilgan: {InstalledVersion}  →  Yangi: {Game.Version}"
            : $"O'rnatilgan: {InstalledVersion}";

    public string ProgressText => IsIndeterminate ? "..." : $"{Progress:0}%";

    public Visibility ProgressVisibility => State == GameState.Busy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CancelVisibility => ProgressVisibility;

    public RelayCommand MainCommand { get; }
    /// <summary>Faqat yangilanish mavjud bo'lganda yoqiladi; tugagach yana o'chadi.</summary>
    public RelayCommand UpdateCommand { get; }
    public RelayCommand UninstallCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public GameViewModel(CatalogGame game, InstalledDb db)
    {
        Game = game;
        _db = db;
        MainCommand = new RelayCommand(async () => await OnMainAsync(), () => State != GameState.Busy);
        UpdateCommand = new RelayCommand(async () => await InstallAsync(),
            () => State == GameState.UpdateAvailable);
        UninstallCommand = new RelayCommand(Uninstall,
            () => State is GameState.Installed or GameState.UpdateAvailable);
        CancelCommand = new RelayCommand(() => _cts?.Cancel());
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RefreshState();
        _ = LoadCoverAsync();
    }

    private void RefreshState()
    {
        InstalledVersion = _db.Get(Game.Id);
        State = InstalledVersion == null ? GameState.NotInstalled
            : VersionUtil.IsNewer(Game.Version, InstalledVersion) ? GameState.UpdateAvailable
            : GameState.Installed;
    }

    private async Task OnMainAsync()
    {
        if (State == GameState.NotInstalled) await InstallAsync();
        else Play();
    }

    private async Task InstallAsync()
    {
        if (State == GameState.Busy) return;
        State = GameState.Busy;
        StatusText = "";
        _cts = new CancellationTokenSource();
        var progress = new Progress<(long done, long? total)>(p =>
        {
            IsIndeterminate = p.total is null or 0;
            Progress = p.total > 0 ? p.done * 100.0 / p.total.Value : 0;
        });
        try
        {
            await InstallService.InstallAsync(Game, _db, progress, s => StatusText = s, _cts.Token);
            StatusText = "";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Bekor qilindi";
        }
        catch (IOException)
        {
            StatusText = "Fayllar band — o'yin ochiq bo'lsa, avval yoping";
        }
        catch (Exception ex)
        {
            StatusText = "Xato: " + ex.Message;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            Progress = 0;
            IsIndeterminate = false;
            RefreshState();
        }
    }

    private void Play()
    {
        try
        {
            InstallService.Launch(Game);
            StatusText = "";
        }
        catch (Exception ex)
        {
            StatusText = "Xato: " + ex.Message;
        }
    }

    private void Uninstall()
    {
        if (State == GameState.Busy) return;
        var answer = MessageBox.Show(
            $"\"{Game.Name}\" o'chirilsinmi?\n(Saqlangan o'yin ma'lumotlari saqlanib qoladi)",
            "O'chirish", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            InstallService.Uninstall(Game.Id, _db);
            StatusText = "";
        }
        catch (IOException)
        {
            StatusText = "Fayllar band — o'yin ochiq bo'lsa, avval yoping";
        }
        catch (Exception ex)
        {
            StatusText = "Xato: " + ex.Message;
        }
        RefreshState();
    }

    private void OpenFolder()
    {
        string dir = AppPaths.GameDir(Game.Id);
        if (Directory.Exists(dir)) Process.Start("explorer.exe", dir);
        else StatusText = "O'yin hali o'rnatilmagan";
    }

    private async Task LoadCoverAsync() => Cover = await CoverImageService.GetAsync(Game);
}
