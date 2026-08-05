using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class AiPlayerCountScreen : UserControl
{
    public event EventHandler<int>? PlayerCountSelected;

    public AiPlayerCountScreen() => InitializeComponent();

    public void LoadAssets(GameInstallation installation)
    {
        var path = Path.Combine(installation.SwfDirectory, "STARS2.SWF");
        var result = SwfImageExtractor.TryExtractLargestEmbeddedImage(path, "STARS2");
        if (result.IsSuccessful)
        {
            StarsImage.Source = new Bitmap(result.ImagePath!);
        }
    }

    private void PlayerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var count))
        {
            PlayerCountSelected?.Invoke(this, count);
        }
    }
}
