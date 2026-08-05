using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class PlanetExploreScreen : UserControl
{
    private string _planet = string.Empty;
    public event EventHandler? SpecialRequested;
    public event EventHandler? NewsRequested;
    public event EventHandler? WeatherRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? TimeRequested;
    public event EventHandler? ReturnRequested;

    public PlanetExploreScreen() => InitializeComponent();

    public void Load(GameInstallation installation, string planet)
    {
        _planet = planet;
        AboutButton.Content = $"About {planet}";
        var modCity = ModCatalog.ResolvePlanetAsset(planet, definition => definition.CityImage);
        if (modCity is not null) PlanetImage.Source = GameBitmapCache.Load(modCity);
        else
        {
            var image = SwfImageExtractor.TryExtractLargestEmbeddedImage(
                Path.Combine(installation.SwfDirectory, $"{planet.ToUpperInvariant()}.SWF"),
                $"EXPLORE_CITY_{planet.ToUpperInvariant()}");
            if (image.IsSuccessful) PlanetImage.Source = new Bitmap(image.ImagePath!);
        }
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
    }

    private void SpecialButton_Click(object? sender, RoutedEventArgs e) => SpecialRequested?.Invoke(this, EventArgs.Empty);
    private void NewsButton_Click(object? sender, RoutedEventArgs e) => NewsRequested?.Invoke(this, EventArgs.Empty);
    private void WeatherButton_Click(object? sender, RoutedEventArgs e) => WeatherRequested?.Invoke(this, EventArgs.Empty);
    private void AboutButton_Click(object? sender, RoutedEventArgs e) => AboutRequested?.Invoke(this, EventArgs.Empty);
    private void TimeButton_Click(object? sender, RoutedEventArgs e) => TimeRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Explore Planet Help", OriginalHelpCatalog.Explore(_planet));
    private void ReturnButton_Click(object? sender, RoutedEventArgs e) => ReturnRequested?.Invoke(this, EventArgs.Empty);
}
