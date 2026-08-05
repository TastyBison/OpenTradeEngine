using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class PlanetArrivalScreen : UserControl
{
    private static readonly Dictionary<string, string> Subtitles = new()
    {
        ["Vexx"] = "The Capital of Kukubia",
        ["Pyke"] = "Home of L-Tech Engines",
        ["Mira"] = "Home of Kukubian Religion",
        ["Stye"] = "Headquarters of the Traders' Union",
        ["Loro"] = "The Pleasure Planet",
        ["Zile"] = "Home of Mr. Zinn",
        ["Frac"] = "Home of Voyager's Insurance",
        ["Tilo"] = "The Gambler's Planet",
        ["Queg"] = "The Smuggler's Haven",
        ["Xeen"] = "A Mechanic's Dream",
        ["Ooom"] = "The Fortune Teller's Planet",
        ["Hork"] = "The Media Capital of Kukubia",
        ["Bass"] = "A Playground for Stock Market Analysts",
        ["Nosh"] = "The Gas Station of Kukubia"
    };

    public event EventHandler? ContinueRequested;

    public PlanetArrivalScreen() => InitializeComponent();

    public void Load(GameInstallation installation, string planet)
    {
        WelcomeText.Text = $"Welcome to {planet}";
        var modPlanet = ModCatalog.FindPlanet(planet);
        SubtitleText.Text = !string.IsNullOrWhiteSpace(modPlanet?.Subtitle)
            ? modPlanet.Subtitle
            : Subtitles.TryGetValue(planet, out var subtitle) ? subtitle : string.Empty;
        var planetPath = ModCatalog.ResolvePlanetAsset(planet, definition => definition.ArrivalImage) ??
                         Path.Combine(installation.PngDirectory, $"{planet.ToUpperInvariant()}3.PNG");
        if (File.Exists(planetPath)) PlanetImage.Source = GameBitmapCache.Load(planetPath);

        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
