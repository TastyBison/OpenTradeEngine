using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class DifficultyScreen : UserControl
{
    public event EventHandler<int>? LevelSelected;

    public DifficultyScreen() => InitializeComponent();

    public void LoadAssets(GameInstallation installation)
    {
        LoadSwf(Path.Combine(installation.SwfDirectory, "STARS_BG.SWF"), "STARS_BG",
            bitmap => StarsImage.Source = bitmap);
        LoadSwf(Path.Combine(installation.SwfDirectory, "LEVEL_PLANET.SWF"), "LEVEL_PLANET",
            bitmap => PlanetImage.Source = bitmap);
    }

    private static void LoadSwf(string path, string cacheName, Action<Bitmap> apply)
    {
        var result = cacheName.Contains("STARS", StringComparison.OrdinalIgnoreCase)
            ? SwfImageExtractor.TryExtractLargestEmbeddedImage(path, cacheName)
            : SwfImageExtractor.TryExtractFirstEmbeddedImage(path, cacheName);
        if (result.IsSuccessful) apply(new Bitmap(result.ImagePath!));
    }

    private void LevelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var level))
            LevelSelected?.Invoke(this, level);
    }
}
