using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class AboutGazillionaireScreen : UserControl
{
    public event EventHandler? CloseRequested;

    public AboutGazillionaireScreen()
    {
        InitializeComponent();
    }

    public void LoadAssets(GameInstallation installation)
    {
        LoadImage(
            Path.Combine(installation.SwfDirectory, "GAZINFO.SWF"),
            "GAZINFO",
            bitmap => GazillionaireInfoImage.Source = bitmap);

        LoadImage(
            Path.Combine(installation.SwfDirectory, "STARS_BG.SWF"),
            "STARS_BG",
            bitmap => StarsImage.Source = bitmap);
    }

    private void LoadImage(string sourcePath, string cacheName, Action<Bitmap> applyImage)
    {
        if (!File.Exists(sourcePath))
        {
            ShowLoadError($"The installation is missing SWF\\{Path.GetFileName(sourcePath)}.");
            return;
        }

        var extraction = cacheName.Contains("STARS", StringComparison.OrdinalIgnoreCase)
            ? SwfImageExtractor.TryExtractLargestEmbeddedImage(sourcePath, cacheName)
            : SwfImageExtractor.TryExtractFirstEmbeddedImage(sourcePath, cacheName);
        if (!extraction.IsSuccessful)
        {
            ShowLoadError(extraction.ErrorMessage);
            return;
        }

        applyImage(new Bitmap(extraction.ImagePath!));
    }

    private void ShowLoadError(string message)
    {
        LoadErrorTextBlock.Text = message;
        LoadErrorTextBlock.IsVisible = true;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
