using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class DistanceChartScreen : UserControl
{
    private readonly List<Button> _planetButtons = [];
    private GameSession? _session;
    private GameInstallation? _installation;
    private string _selectedPlanet = string.Empty;
    private bool _showFacilities;

    public event EventHandler? ReturnRequested;

    public DistanceChartScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company, bool showFacilities = false)
    {
        _installation = installation;
        _session = session;
        _selectedPlanet = company.Planet;
        _showFacilities = showFacilities;

        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = GameBitmapCache.Load(help.ImagePath!);

        BuildPlanetList();
        RefreshChart();
    }

    private void BuildPlanetList()
    {
        if (_session is null || _installation is null) return;
        PlanetList.Children.Clear();
        _planetButtons.Clear();
        foreach (var planet in _session.Planets)
        {
            var art = SwfImageExtractor.TryExtractLargestEmbeddedImage(
                Path.Combine(_installation.ResourcesDirectory, $"{planet.ToUpperInvariant()}1.SWF"),
                $"CHART_PLANET_{planet.ToUpperInvariant()}");
            var image = new Image { Width = 70, Height = 60, Stretch = Stretch.Uniform };
            if (art.IsSuccessful) image.Source = GameBitmapCache.Load(art.ImagePath!);
            var label = new TextBlock
            {
                Text = planet,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("78,*") };
            content.Children.Add(image);
            Grid.SetColumn(label, 1);
            content.Children.Add(label);
            var button = new Button
            {
                Tag = planet,
                Content = content,
                Height = 86,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            button.Classes.Add("planet-choice");
            button.Click += PlanetButton_Click;
            PlanetList.Children.Add(button);
            _planetButtons.Add(button);
        }
        UpdatePlanetSelection();
    }

    private void PlanetButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string planet }) return;
        _selectedPlanet = planet;
        UpdatePlanetSelection();
        RefreshChart();
    }

    private void UpdatePlanetSelection()
    {
        foreach (var button in _planetButtons)
            button.Classes.Set("selected", string.Equals(button.Tag as string, _selectedPlanet, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshChart()
    {
        if (_session is null) return;
        TitleText.Text = (_showFacilities ? "Facilities On " : "Distance To ") + _selectedPlanet;
        DistanceButton.IsEnabled = _showFacilities;
        FacilitiesButton.IsEnabled = !_showFacilities;

        HeaderGrid.Children.Clear();
        ChartRows.Children.Clear();
        if (_showFacilities)
        {
            HeaderGrid.ColumnDefinitions = new ColumnDefinitions("2.1*,1*,1.25*,1.25*");
            AddHeader("Company", 0);
            AddHeader("Facilities", 1);
            AddHeader("Fees", 2);
            AddHeader("Revenue", 3);
        }
        else
        {
            HeaderGrid.ColumnDefinitions = new ColumnDefinitions("2.1*,1.25*,1.1*,1.5*");
            AddHeader("Company", 0);
            AddHeader("Location", 1);
            AddHeader("Engine", 2);
            AddHeader("Distance", 3);
        }

        foreach (var company in _session.Companies.Where(candidate => !candidate.IsBankrupt))
            ChartRows.Children.Add(_showFacilities ? BuildFacilityRow(company) : BuildDistanceRow(company));
    }

    private void AddHeader(string text, int column)
    {
        var heading = new TextBlock { Text = text };
        heading.Classes.Add("chart-head");
        Grid.SetColumn(heading, column);
        HeaderGrid.Children.Add(heading);
    }

    private Grid BuildDistanceRow(CompanyState company)
    {
        var row = CreateRow("2.1*,1.25*,1.1*,1.5*");
        AddCell(row, company.Name, 0, TextAlignment.Left, company.IsHuman);
        AddCell(row, company.Planet, 1);
        AddCell(row, $"{company.EngineSpeed} kuarps", 2);
        var distance = string.Equals(company.Planet, _selectedPlanet, StringComparison.OrdinalIgnoreCase)
            ? "-----"
            : $"{DistanceBetween(company.Planet, _selectedPlanet)} million kuters";
        AddCell(row, distance, 3);
        return row;
    }

    private Grid BuildFacilityRow(CompanyState company)
    {
        var row = CreateRow("2.1*,1*,1.25*,1.25*");
        var facilities = _session!.Facilities.Where(facility =>
            facility.Planet.Equals(_selectedPlanet, StringComparison.OrdinalIgnoreCase) &&
            facility.OwnerName.Equals(company.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        AddCell(row, company.Name, 0, TextAlignment.Left, company.IsHuman);
        AddCell(row, facilities.Length.ToString("N0"), 1);
        AddCell(row, $"{facilities.Sum(facility => facility.Fee):N0} kubars", 2);
        AddCell(row, $"{facilities.Sum(facility => facility.Revenue):N0} kubars", 3);
        return row;
    }

    private static Grid CreateRow(string columns) => new()
    {
        Height = 47,
        ColumnDefinitions = new ColumnDefinitions(columns)
    };

    private static void AddCell(Grid row, string text, int column, TextAlignment alignment = TextAlignment.Center, bool bold = false)
    {
        var cell = new TextBlock
        {
            Text = text,
            TextAlignment = alignment,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal
        };
        cell.Classes.Add("chart-cell");
        Grid.SetColumn(cell, column);
        row.Children.Add(cell);
    }

    private int DistanceBetween(string from, string to)
    {
        if (_session is null) return 0;
        var fromIndex = PlanetIndex(from);
        var toIndex = PlanetIndex(to);
        if (fromIndex < 0 || toIndex < 0) return 0;
        var fromPosition = TravelRules.MapPosition(fromIndex);
        var toPosition = TravelRules.MapPosition(toIndex);
        var dx = fromPosition.X - toPosition.X;
        var dy = fromPosition.Y - toPosition.Y;
        return (int)Math.Floor(Math.Sqrt(dx * dx + dy * dy));
    }

    private int PlanetIndex(string planet)
    {
        if (_session is null) return -1;
        for (var index = 0; index < _session.Planets.Count; index++)
            if (_session.Planets[index].Equals(planet, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private void DistanceButton_Click(object? sender, RoutedEventArgs e)
    {
        _showFacilities = false;
        RefreshChart();
    }

    private void FacilitiesButton_Click(object? sender, RoutedEventArgs e)
    {
        _showFacilities = true;
        RefreshChart();
    }

    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        HelpText.Text = _showFacilities
            ? "Supreme Commander Dred Nicolson auctions off government facilities, such as fuel depots, launch pads and passenger ticket offices, to the highest bidder.\n\nThis chart shows which company owns how many facilities on each planet, how much you must pay each company, and how much it may collect when it lands there."
            : "Click a planet on the left to see how far every company is from it. Divide distance by engine speed to estimate travel time. The companies with the shortest travel times will usually arrive first.";
        HelpOverlay.IsVisible = true;
    }

    private void CloseHelpButton_Click(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = false;
    private void ReturnButton_Click(object? sender, RoutedEventArgs e) => ReturnRequested?.Invoke(this, EventArgs.Empty);
}
