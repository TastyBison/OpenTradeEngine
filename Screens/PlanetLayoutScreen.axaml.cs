using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class PlanetLayoutScreen : UserControl
{
    private readonly List<PlanetDefinition> _selectedPlanets = [];
    private readonly Dictionary<string, Bitmap> _planetImages = [];
    private GameInstallation? _installation;
    private int _slotBeingReplaced;

    public event EventHandler? ContinueRequested;
    public IReadOnlyList<string> SelectedPlanetNames => _selectedPlanets.Select(planet => planet.Name).ToArray();

    public PlanetLayoutScreen() => InitializeComponent();

    public void LoadAssets(GameInstallation installation)
    {
        _installation = installation;
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        RandomizeSelection();
    }

    private void RandomizeSelection()
    {
        _selectedPlanets.Clear();
        _selectedPlanets.AddRange(PlanetCatalog.All.OrderBy(_ => Random.Shared.Next()).Take(7));
        DisplayPlanets();
    }

    private void DisplayPlanets()
    {
        var slots = Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            var content = (StackPanel)slots[index].Content!;
            ((TextBlock)content.Children[0]).Text = _selectedPlanets[index].Name;
            ((Image)content.Children[1]).Source = LoadPlanetImage(_selectedPlanets[index]);
        }
    }

    private Button[] Slots =>
        [PlanetSlot0, PlanetSlot1, PlanetSlot2, PlanetSlot3, PlanetSlot4, PlanetSlot5, PlanetSlot6];

    private Bitmap? LoadPlanetImage(PlanetDefinition planet)
    {
        if (_installation is null) return null;
        if (_planetImages.TryGetValue(planet.Name, out var cached)) return cached;

        var modIcon = ModCatalog.ResolvePlanetAsset(planet.Name, definition => definition.Icon);
        if (modIcon is not null)
        {
            var modBitmap = GameBitmapCache.Load(modIcon);
            _planetImages[planet.Name] = modBitmap;
            return modBitmap;
        }

        var extraction = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(_installation.ResourcesDirectory, $"{planet.Name.ToUpperInvariant()}1.SWF"),
            $"PLANET_ICON_TRANSPARENT_{planet.Name.ToUpperInvariant()}");
        if (!extraction.IsSuccessful) return null;

        var bitmap = GameBitmapCache.Load(extraction.ImagePath!);
        _planetImages[planet.Name] = bitmap;
        return bitmap;
    }

    private void PlanetButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out _slotBeingReplaced)) return;

        var current = _selectedPlanets[_slotBeingReplaced];
        SelectorHeading.Text = $"Click on a planet to replace {current.Name}:";
        SelectorGrid.Children.Clear();

        var choices = PlanetCatalog.All.Where(candidate =>
            candidate.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase) ||
            _selectedPlanets.All(selected => !selected.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase))).ToArray();

        for (var index = 0; index < choices.Length; index++)
        {
            var planet = choices[index];
            var button = new Button
            {
                Tag = planet.Name,
                Background = Brushes.Black,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("125,*"),
                    Children =
                    {
                        new Image { Source = LoadPlanetImage(planet), Width = 112, Height = 112, Stretch = Stretch.Uniform },
                        new TextBlock
                        {
                            Text = $"{planet.Name.ToUpperInvariant()} is {planet.Description}",
                            Foreground = Brushes.White, FontSize = 17, TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        }
                    }
                }
            };
            Grid.SetColumn(((Grid)button.Content).Children[1], 1);
            Grid.SetColumn(button, (index % 2) * 2);
            Grid.SetRow(button, (index / 2) * 2);
            button.Click += SelectorButton_Click;
            SelectorGrid.Children.Add(button);
        }

        SelectorPanel.IsVisible = true;
    }

    private void SelectorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name }) return;
        _selectedPlanets[_slotBeingReplaced] = PlanetCatalog.All.Single(planet =>
            planet.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        SelectorPanel.IsVisible = false;
        DisplayPlanets();
    }

    private void RandomizeButton_Click(object? sender, RoutedEventArgs e) => RandomizeSelection();

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
