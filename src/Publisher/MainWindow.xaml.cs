using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace Publisher;

public partial class MainWindow : Window
{
    private PublisherConfig _config = new();
    private Catalog _catalog = new();
    private bool _loading;
    private const string NewGameItem = "➕  Yangi o'yin";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        _config = PublisherConfig.Load();
        RepoRoot.Text = _config.RepoRoot;
        GhPath.Text = _config.GhPath;
        ReloadCatalog();
    }

    private void ReloadCatalog()
    {
        _loading = true;
        _catalog = CatalogIo.Load(Path.Combine(_config.RepoRoot, "games.json"));
        GameSelect.Items.Clear();
        GameSelect.Items.Add(NewGameItem);
        foreach (var g in _catalog.Games)
            GameSelect.Items.Add($"{g.Name}  ({g.Id})  —  v{g.Version}");
        GameSelect.SelectedIndex = _catalog.Games.Count > 0 ? 1 : 0;
        _loading = false;
        GameSelect_Changed(GameSelect, null!);
    }

    private void GameSelect_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        int idx = GameSelect.SelectedIndex;
        if (idx <= 0) // yangi o'yin
        {
            GameId.IsReadOnly = false;
            GameId.Text = "";
            GameName.Text = "";
            Version.Text = "1.0.0";
            ExeName.Text = "";
            Description.Text = "";
            CoverFile.Text = "";
            Changelog.Text = "Birinchi versiya";
            return;
        }

        var game = _catalog.Games[idx - 1];
        GameId.IsReadOnly = true; // mavjud o'yinning id'si o'zgarmaydi
        GameId.Text = game.Id;
        GameName.Text = game.Name;
        ExeName.Text = game.Exe;
        Version.Text = SuggestNextVersion(game.Version);
        Description.Text = game.Description ?? "";
        CoverFile.Text = ""; // bo'sh = eski muqova qoladi
        Changelog.Text = "";
    }

    // 1.0.0 -> 1.0.1 (oxirgi raqamni oshiradi)
    private static string SuggestNextVersion(string v)
    {
        var parts = v.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[^1], out int last))
        {
            parts[^1] = (last + 1).ToString();
            return string.Join('.', parts);
        }
        return v;
    }

    private void PickBuild_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog { Description = "Unity build papkasini tanlang" };
        if (Directory.Exists(BuildDir.Text)) dlg.SelectedPath = BuildDir.Text;
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            BuildDir.Text = dlg.SelectedPath;
    }

    private void BuildDir_Changed(object sender, TextChangedEventArgs e)
    {
        // Exe nomini avtomatik aniqlash (agar hali kiritilmagan bo'lsa)
        if (!string.IsNullOrWhiteSpace(ExeName.Text)) return;
        try
        {
            if (!Directory.Exists(BuildDir.Text)) return;
            var exe = Directory.EnumerateFiles(BuildDir.Text, "*.exe")
                .Select(Path.GetFileName)
                .FirstOrDefault(n => !string.Equals(n, "UnityCrashHandler64.exe", StringComparison.OrdinalIgnoreCase));
            if (exe != null) ExeName.Text = exe;
        }
        catch { }
    }

    private void PickCover_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Rasmlar (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Barcha fayllar (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) CoverFile.Text = dlg.FileName;
    }

    private async void Release_Click(object sender, RoutedEventArgs e)
    {
        // Sozlamalarni saqlab olamiz
        _config.RepoRoot = RepoRoot.Text.Trim();
        _config.GhPath = GhPath.Text.Trim();
        _config.Save();

        var input = new GameInput
        {
            BuildDir = BuildDir.Text.Trim(),
            GameId = GameId.Text.Trim(),
            GameName = GameName.Text.Trim(),
            Version = Version.Text.Trim(),
            ExeName = ExeName.Text.Trim(),
            Changelog = Changelog.Text.Trim(),
            Description = Description.Text.Trim(),
            CoverFile = CoverFile.Text.Trim(),
        };

        LogText.Text = "";
        ReleaseBtn.IsEnabled = false;
        var service = new PublishService(_config.RepoRoot, _config.GhPath);
        var progress = new Progress<string>(Log);
        bool ok = await service.ReleaseAsync(input, PublishGitHub.IsChecked == true,
            s => ((IProgress<string>)progress).Report(s), CancellationToken.None);
        ReleaseBtn.IsEnabled = true;

        if (ok && PublishGitHub.IsChecked == true)
            ReloadCatalog(); // ro'yxatdagi versiyalar yangilansin
    }

    private void Log(string line)
    {
        LogText.Text += line + "\n";
        LogScroll.ScrollToEnd();
    }
}
