using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenTradeEngine.Screens;

public partial class SupplyScreen : UserControl
{
    private readonly List<(Grid Grid, int Commodity)> _commodityRows = [];
    private readonly List<(Border Cell, string Planet)> _planetCells = [];
    private GameSession? _session;
    private CompanyState? _company;
    private SupplyFilter _filter = SupplyFilter.Available;

    public event EventHandler? ContinueRequested;
    public event EventHandler? MarketplaceRequested;
    public event EventHandler? WarehouseRequested;

    public SupplyScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _session = session;
        _company = company;
        _filter = SupplyFilter.Available;
        BuildRows();
        Refresh();
    }

    private void BuildRows()
    {
        if (_session is null) return;
        Rows.Children.Clear(); _commodityRows.Clear(); _planetCells.Clear();
        var planets = OrderedPlanets().ToArray();
        var columns = "190," + string.Join(',', planets.Select(_ => "86"));
        Rows.Children.Add(CreateHeadingRow(columns, planets));
        for (var commodity = 0; commodity < CommodityCatalog.All.Length; commodity++)
        {
            var supplies = planets.Select(planet => _session.Markets[planet].Listings[commodity].Supply).ToArray();
            var minimum = supplies.Min();
            var values = supplies.Select(value => $"{(value == minimum ? "➤ " : string.Empty)}{value}%");
            var row = CreateSupplyRow(columns, CommodityCatalog.All[commodity].Name, values, planets);
            Rows.Children.Add(row); _commodityRows.Add((row, commodity));
        }
    }

    private IEnumerable<string> OrderedPlanets()
    {
        if (_session is null || _company is null) return [];
        return _session.Planets.OrderByDescending(planet =>
            planet.Equals(_company.Planet, StringComparison.OrdinalIgnoreCase));
    }

    private Grid CreateHeadingRow(string columns, IReadOnlyList<string> planets)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            MinHeight = 48,
            Background = new SolidColorBrush(Color.Parse("#74749B"))
        };
        Add(grid, string.Empty, 0, true, HorizontalAlignment.Left);
        for (var index = 0; index < planets.Count; index++)
        {
            var planet = planets[index];
            var cell = new Border
            {
                Tag = planet,
                Margin = new Thickness(2),
                Padding = new Thickness(3),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Child = new TextBlock
                {
                    Text = planet,
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            cell.PointerPressed += PlanetCell_PointerPressed;
            Grid.SetColumn(cell, index + 1);
            grid.Children.Add(cell);
            _planetCells.Add((cell, planet));
        }
        return grid;
    }

    private void PlanetCell_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_company is null || sender is not Border { Tag: string planet }) return;
        SelectPlanet(planet);
        e.Handled = true;
    }

    private void SelectPlanet(string planet)
    {
        if (_company is null) return;
        _company.PlannedDestination = planet.Equals(_company.Planet, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : planet;
        Refresh();
    }

    private void ShowAvailableButton_Click(object? sender, RoutedEventArgs e) { _filter = SupplyFilter.Available; Refresh(); }
    private void ShowCargoButton_Click(object? sender, RoutedEventArgs e) { _filter = SupplyFilter.Cargo; Refresh(); }
    private void ShowAllButton_Click(object? sender, RoutedEventArgs e) { _filter = SupplyFilter.All; Refresh(); }
    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void MarketplaceButton_Click(object? sender, RoutedEventArgs e) => MarketplaceRequested?.Invoke(this, EventArgs.Empty);
    private void WarehouseButton_Click(object? sender, RoutedEventArgs e) => WarehouseRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Supply Chart Help", OriginalHelpCatalog.Supply);

    private void Refresh()
    {
        if (_session is null || _company is null) return;
        var percent = _company.CargoCapacity == 0 ? 0 : _company.CargoUsed * 100 / _company.CargoCapacity;
        CargoText.Text = $"Ship's cargo bay = {percent}% filled ({_company.CargoUsed}/{_company.CargoCapacity})";
        CashText.Text = _company.Cash.ToString("N0");
        foreach (var (cell, planet) in _planetCells)
        {
            var planned = planet.Equals(_company.PlannedDestination, StringComparison.OrdinalIgnoreCase);
            var current = planet.Equals(_company.Planet, StringComparison.OrdinalIgnoreCase);
            cell.BorderBrush = planned ? Brushes.DeepSkyBlue : current ? Brushes.Orange : Brushes.Transparent;
            cell.Background = planned ? new SolidColorBrush(Color.Parse("#374E86")) : Brushes.Transparent;
        }
        foreach (var (grid, commodity) in _commodityRows)
        {
            var listing = _session.Markets[_company.Planet].Listings[commodity];
            var cargo = _company.Cargo.GetValueOrDefault(commodity)?.Quantity ?? 0;
            grid.IsVisible = _filter switch
            {
                SupplyFilter.Available => listing.Quantity > 0 || cargo > 0,
                SupplyFilter.Cargo => cargo > 0,
                _ => true
            };
        }
    }

    private Grid CreateSupplyRow(
        string columns,
        string commodityName,
        IEnumerable<string> values,
        IReadOnlyList<string> planets)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            MinHeight = 42,
            Background = Brushes.White
        };
        Add(grid, commodityName, 0, false, HorizontalAlignment.Left);
        var column = 1;
        foreach (var value in values)
        {
            var cell = new Border
            {
                Tag = planets[column - 1],
                Margin = new Thickness(0),
                Padding = new Thickness(4),
                MinHeight = 42,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = new TextBlock
                {
                    Text = value,
                    Foreground = Brushes.DarkSlateGray,
                    FontSize = 16,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            cell.PointerPressed += PlanetCell_PointerPressed;
            Grid.SetColumn(cell, column++);
            grid.Children.Add(cell);
        }
        return grid;
    }

    private static void Add(Grid grid, string text, int column, bool heading, HorizontalAlignment alignment)
    {
        var block = new TextBlock { Text = text, Foreground = heading ? Brushes.White : Brushes.DarkSlateGray,
            FontSize = heading ? 17 : 16, FontWeight = heading ? FontWeight.Bold : FontWeight.Normal,
            Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = alignment };
        Grid.SetColumn(block, column); grid.Children.Add(block);
    }

    private enum SupplyFilter { All, Available, Cargo }
}
