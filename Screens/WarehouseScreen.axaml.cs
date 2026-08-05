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

public partial class WarehouseScreen : UserControl
{
    private readonly List<Row> _rows = [];
    private CompanyState? _company;
    private PlanetMarket? _market;
    private int _selectedCommodity;
    private WarehouseFilter _filter = WarehouseFilter.Cargo;

    public event EventHandler? ContinueRequested;
    public event EventHandler? SupplyRequested;
    public event EventHandler? MarketplaceRequested;
    public event EventHandler<string>? SoundRequested;

    public WarehouseScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _company = company;
        _market = session.Markets[company.Planet];
        company.Warehouses.TryAdd(company.Planet, []);
        QuickText.Text = company.Shortcuts.GetValueOrDefault("warehouse") ? "Quick Warehouse On" : "Quick Warehouse Off";
        BuildRows(); Refresh();
    }

    private void BuildRows()
    {
        Rows.Children.Clear(); _rows.Clear();
        for (var index = 0; index < CommodityCatalog.All.Length; index++)
        {
            var commodity = index;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,1.2*,1.5*,1.2*"), MinHeight = 43,
                Background = Brushes.White, Cursor = new Cursor(StandardCursorType.Hand) };
            grid.PointerPressed += (_, args) => Select(commodity, args);
            var name = Cell(CommodityCatalog.All[index].Name, HorizontalAlignment.Left);
            var ship = Cell(string.Empty); var stored = Cell(string.Empty); var price = Cell(string.Empty);
            ship.PointerPressed += (_, args) => QuickTransfer(commodity, storing: true, args);
            stored.PointerPressed += (_, args) => QuickTransfer(commodity, storing: false, args);
            Add(grid, name, 0); Add(grid, ship, 1); Add(grid, stored, 2); Add(grid, price, 3);
            Rows.Children.Add(grid); _rows.Add(new Row(grid, ship, stored, price));
        }
    }

    private void Select(int commodity, PointerPressedEventArgs args)
    {
        _selectedCommodity = commodity;
        args.Handled = true;
        if (_company?.Shortcuts.GetValueOrDefault("warehouse") == true)
        {
            if (ShortcutInputState.BypassRequested)
                Transfer(storing: true, useShortcut: false);
            else
                ApplyTransfer(storing: true, TransferQuantity(commodity, storing: true));
            return;
        }
        Refresh();
    }

    private void QuickTransfer(int commodity, bool storing, PointerPressedEventArgs args)
    {
        if (_company?.Shortcuts.GetValueOrDefault("warehouse") != true) return;
        _selectedCommodity = commodity;
        args.Handled = true;
        if (ShortcutInputState.BypassRequested)
        {
            Transfer(storing, useShortcut: false);
            return;
        }
        ApplyTransfer(storing, TransferQuantity(commodity, storing));
    }

    private void StoreButton_Click(object? sender, RoutedEventArgs e) =>
        Transfer(storing: true, useShortcut: !ShortcutInputState.BypassRequested);

    private void TakeButton_Click(object? sender, RoutedEventArgs e) =>
        Transfer(storing: false, useShortcut: !ShortcutInputState.BypassRequested);

    private void Transfer(bool storing, bool useShortcut)
    {
        if (_company is null) return;
        var quantity = TransferQuantity(_selectedCommodity, storing);
        if (useShortcut && _company.Shortcuts.GetValueOrDefault("warehouse"))
        {
            ApplyTransfer(storing, quantity);
            return;
        }
        var commodity = CommodityCatalog.All[_selectedCommodity].Name;
        var ship = _company.Cargo.GetValueOrDefault(_selectedCommodity)?.Quantity ?? 0;
        var stored = _company.Warehouses.GetValueOrDefault(_company.Planet)?.GetValueOrDefault(_selectedCommodity)?.Quantity ?? 0;
        AmountEntry.Show(storing ? "Store Commodities" : "Take Commodities",
            $"On ship:  {ship:N0}\nIn warehouse:  {stored:N0}\nFree cargo space:  {_company.CargoFree:N0}",
            $"Enter the tons of {commodity} you wish to {(storing ? "store" : "take")}:", quantity,
            amount => ApplyTransfer(storing, (int)amount));
    }

    private int TransferQuantity(int commodity, bool storing)
    {
        if (_company is null) return 0;
        if (storing)
            return _company.Cargo.GetValueOrDefault(commodity)?.Quantity ?? 0;

        var stored = _company.Warehouses.GetValueOrDefault(_company.Planet)?
            .GetValueOrDefault(commodity)?.Quantity ?? 0;
        return Math.Min(stored, _company.CargoFree);
    }

    private void ApplyTransfer(bool storing, int quantity)
    {
        var result = storing ? _company!.StoreCargo(_company.Planet, _selectedCommodity, quantity) :
                               _company!.RetrieveCargo(_company.Planet, _selectedCommodity, quantity);
        Show(result);
        if (result.IsSuccessful) SoundRequested?.Invoke(this, CommodityCatalog.AudioFile(_selectedCommodity));
    }

    private void ShowAvailableButton_Click(object? sender, RoutedEventArgs e) { _filter = WarehouseFilter.Available; Refresh(); }
    private void ShowCargoButton_Click(object? sender, RoutedEventArgs e) { _filter = WarehouseFilter.Cargo; Refresh(); }
    private void ShowAllButton_Click(object? sender, RoutedEventArgs e) { _filter = WarehouseFilter.All; Refresh(); }
    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void SupplyButton_Click(object? sender, RoutedEventArgs e) => SupplyRequested?.Invoke(this, EventArgs.Empty);
    private void MarketplaceButton_Click(object? sender, RoutedEventArgs e) => MarketplaceRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Warehouse Help", OriginalHelpCatalog.Warehouse(_company));
    }

    private void Show(TradeResult result)
    {
        StatusText.Text = result.Message; StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed; Refresh();
    }

    private void Refresh()
    {
        if (_company is null || _market is null) return;
        var warehouse = _company.Warehouses.GetValueOrDefault(_company.Planet);
        var storedTotal = warehouse?.Values.Sum(lot => lot.Quantity) ?? 0;
        var cargoPercent = _company.CargoCapacity == 0 ? 0 : _company.CargoUsed * 100 / _company.CargoCapacity;
        var warehousePercent = _company.WarehouseCapacity == 0 ? 0 : storedTotal * 100 / _company.WarehouseCapacity;
        CargoText.Text = $"Ship's cargo bay = {cargoPercent}% filled ({_company.CargoUsed}/{_company.CargoCapacity})";
        WarehouseText.Text = $"Warehouse space = {warehousePercent}% filled ({storedTotal}/{_company.WarehouseCapacity})";
        for (var index = 0; index < _rows.Count; index++)
        {
            var ship = _company.Cargo.GetValueOrDefault(index)?.Quantity ?? 0;
            var stored = warehouse?.GetValueOrDefault(index)?.Quantity ?? 0;
            var row = _rows[index];
            row.Grid.IsVisible = _filter switch { WarehouseFilter.Available => stored > 0, WarehouseFilter.Cargo => ship > 0 || stored > 0, _ => true };
            row.Grid.Background = index == _selectedCommodity ? new SolidColorBrush(Color.Parse("#D7EFFF")) : Brushes.White;
            row.Ship.Text = ship.ToString(); row.Stored.Text = stored.ToString(); row.Price.Text = _market.Listings[index].Price.ToString("N0");
        }
    }

    private static TextBlock Cell(string text, HorizontalAlignment alignment = HorizontalAlignment.Center) => new()
    { Text = text, Foreground = Brushes.DarkSlateGray, FontSize = 16, Margin = new Thickness(8, 7),
      VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = alignment };
    private static void Add(Grid grid, Control control, int column) { Grid.SetColumn(control, column); grid.Children.Add(control); }
    private sealed record Row(Grid Grid, TextBlock Ship, TextBlock Stored, TextBlock Price);
    private enum WarehouseFilter { All, Available, Cargo }
}
