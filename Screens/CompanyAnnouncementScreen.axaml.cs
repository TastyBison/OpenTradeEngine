using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class CompanyAnnouncementScreen : UserControl
{
    public bool IsGoodAnnouncement { get; private set; }
    public string AudioAsset { get; private set; } = string.Empty;
    public event EventHandler? ContinueRequested;

    public CompanyAnnouncementScreen() => InitializeComponent();

    public void Load(GameInstallation installation, string heading, string message,
        string imageAsset, string audioAsset, bool isGood)
    {
        Heading.Text = heading;
        Message.Text = message;
        AudioAsset = audioAsset;
        IsGoodAnnouncement = isGood;
        BackdropImage.Source = null;
        BackdropImage.IsVisible = false;
        if (imageAsset.StartsWith("PLANET:", StringComparison.OrdinalIgnoreCase))
        {
            var planet = imageAsset["PLANET:".Length..].Trim();
            CompanyImageBackground.Background = Brushes.Black;
            var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
                Path.Combine(installation.SwfDirectory, "STARS_BG.SWF"), "GLOBAL_EVENT_STARS");
            if (stars.IsSuccessful)
            {
                BackdropImage.Source = GameBitmapCache.Load(stars.ImagePath!);
                BackdropImage.IsVisible = true;
            }
            CompanyImage.Source = null;
            var modIcon = ModCatalog.ResolvePlanetAsset(planet, definition => definition.Icon);
            if (modIcon is not null)
            {
                CompanyImage.Source = GameBitmapCache.Load(modIcon);
                return;
            }
            var planetArtwork = SwfImageExtractor.TryExtractLargestEmbeddedImage(
                Path.Combine(installation.ResourcesDirectory, $"{planet.ToUpperInvariant()}1.SWF"),
                $"GLOBAL_EVENT_PLANET_{planet.ToUpperInvariant()}");
            if (planetArtwork.IsSuccessful)
                CompanyImage.Source = GameBitmapCache.Load(planetArtwork.ImagePath!);
            return;
        }
        CompanyImageBackground.Background = imageAsset.StartsWith("SHIP", StringComparison.OrdinalIgnoreCase)
            ? Brushes.Black
            : Brushes.White;
        CompanyImage.Source = null;
        if (string.IsNullOrWhiteSpace(imageAsset)) return;

        if (Path.GetExtension(imageAsset).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            var pngPath = Path.Combine(installation.PngDirectory, imageAsset);
            if (File.Exists(pngPath)) CompanyImage.Source = GameBitmapCache.Load(pngPath);
            return;
        }

        var swfPath = Path.Combine(installation.SwfDirectory, imageAsset);
        var cacheName = Path.GetFileNameWithoutExtension(imageAsset);
        var artwork = SwfImageExtractor.TryExtractFirstEmbeddedImage(swfPath, cacheName);
        if (artwork.IsSuccessful) CompanyImage.Source = new Bitmap(artwork.ImagePath!);
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
