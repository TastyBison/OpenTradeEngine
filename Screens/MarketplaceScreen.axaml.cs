using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenTradeEngine.Screens;

public partial class MarketplaceScreen : UserControl
{
    private readonly List<MarketRow> _rows = [];
    private CompanyState? _company;
    private PlanetMarket? _market;
    private int _selectedCommodity;
    private MarketFilter _filter = MarketFilter.Available;

    public event EventHandler? ContinueRequested;
    public event EventHandler? SupplyRequested;
    public event EventHandler? WarehouseRequested;
    public event EventHandler<string>? SoundRequested;

    public MarketplaceScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _company = company;
        _market = session.Markets[company.Planet];
        PlanetQuantityHeading.Text = $"On {company.Planet}";
        QuickTradeText.Text = company.Shortcuts.GetValueOrDefault("buy") ? "Quick Trade On" : "Quick Trade Off";
        BuildRows();
        Refresh();
    }

    private void BuildRows()
    {
        if (_company is null || _market is null) return;
        CommodityRows.Children.Clear();
        _rows.Clear();
        for (var index = 0; index < CommodityCatalog.All.Length; index++)
        {
            var commodity = index;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("190,*,*,*,*,1.15*"), MinHeight = 42,
                Background = Brushes.White, Cursor = new Cursor(StandardCursorType.Hand) };
            grid.PointerPressed += (_, args) => Select(commodity, args);
            var name = Cell(CommodityCatalog.All[index].Name, HorizontalAlignment.Left);
            var cargo = Cell(string.Empty); var available = Cell(string.Empty); var paid = Cell(string.Empty);
            var priceRange = _market.PriceRange(index);
            var price = Cell(string.Empty); var range = Cell($"{priceRange.Minimum:N0} - {priceRange.Maximum:N0}");
            cargo.PointerPressed += (_, args) => QuickTrade(commodity, args);
            available.PointerPressed += (_, args) => QuickTrade(commodity, args);
            Add(grid, name, 0); Add(grid, cargo, 1); Add(grid, available, 2); Add(grid, paid, 3); Add(grid, price, 4); Add(grid, range, 5);
            CommodityRows.Children.Add(grid);
            _rows.Add(new MarketRow(grid, cargo, available, paid, price));
        }
    }

    private void Select(int commodity, PointerPressedEventArgs args)
    {
        _selectedCommodity = commodity;
        args.Handled = true;
        if (_company?.Shortcuts.GetValueOrDefault("buy") == true && _market is not null)
        {
            TradeContextually(useShortcut: !ShortcutInputState.BypassRequested);
            return;
        }
        Refresh();
    }

    private void QuickTrade(int commodity, PointerPressedEventArgs args)
    {
        if (_company?.Shortcuts.GetValueOrDefault("buy") != true || _market is null) return;
        _selectedCommodity = commodity;
        args.Handled = true;
        TradeContextually(useShortcut: !ShortcutInputState.BypassRequested);
    }

    private void TradeContextually(bool useShortcut)
    {
        if (CargoQuantity(_selectedCommodity) > 0)
            Sell(useShortcut);
        else
            Buy(useShortcut);
    }

    private void BuyButton_Click(object? sender, RoutedEventArgs e) =>
        Buy(useShortcut: !ShortcutInputState.BypassRequested);

    private void Buy(bool useShortcut)
    {
        if (_company is null || _market is null) return;
        var maximum = MaximumBuy(_selectedCommodity);
        if (useShortcut && _company.Shortcuts.GetValueOrDefault("buy"))
        {
            ShowTrade(_company.Buy(_market, _selectedCommodity, maximum));
            return;
        }
        var commodity = CommodityCatalog.All[_selectedCommodity].Name;
        var listing = _market.Listings[_selectedCommodity];
        AmountEntry.Show("Buy Commodities", $"Cash:  {_company.Cash:N0}\nAvailable:  {listing.Quantity:N0}\nPrice per ton:  {listing.Price:N0}",
            $"Enter the tons of {commodity} you wish to buy:", maximum,
            amount => ShowTrade(_company.Buy(_market, _selectedCommodity, (int)amount)), maximum);
    }

    private void SellButton_Click(object? sender, RoutedEventArgs e) =>
        Sell(useShortcut: !ShortcutInputState.BypassRequested);

    private void Sell(bool useShortcut)
    {
        if (_company is null || _market is null) return;
        var maximum = CargoQuantity(_selectedCommodity);
        if (useShortcut && _company.Shortcuts.GetValueOrDefault("buy"))
        {
            ShowTrade(_company.Sell(_market, _selectedCommodity, maximum));
            return;
        }
        var commodity = CommodityCatalog.All[_selectedCommodity].Name;
        var price = _market.Listings[_selectedCommodity].Price;
        AmountEntry.Show("Sell Commodities", $"Your cargo:  {maximum:N0}\nMarket price per ton:  {price:N0}",
            $"Enter the tons of {commodity} you wish to sell:", maximum,
            amount => ShowTrade(_company.Sell(_market, _selectedCommodity, (int)amount)));
    }

    private void ShowAvailableButton_Click(object? sender, RoutedEventArgs e) { _filter = MarketFilter.Available; Refresh(); }
    private void ShowCargoButton_Click(object? sender, RoutedEventArgs e) { _filter = MarketFilter.Cargo; Refresh(); }
    private void ShowAllButton_Click(object? sender, RoutedEventArgs e) { _filter = MarketFilter.All; Refresh(); }
    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void SupplyButton_Click(object? sender, RoutedEventArgs e) => SupplyRequested?.Invoke(this, EventArgs.Empty);
    private void WarehouseButton_Click(object? sender, RoutedEventArgs e) => WarehouseRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Marketplace Help", OriginalHelpCatalog.Marketplace(_company));
    }

    private int MaximumBuy(int index)
    {
        if (_company is null || _market is null) return 0;
        var listing = _market.Listings[index];
        var affordable = listing.Price <= 0 ? 0 : (int)(_company.Cash / listing.Price);
        return Math.Min(_company.AccessibleCommodityQuantity(_market, index),
            Math.Min(_company.CargoFree, affordable));
    }

    private int CargoQuantity(int index) => _company is not null && _company.Cargo.TryGetValue(index, out var lot) ? lot.Quantity : 0;

    private void ShowTrade(TradeResult result)
    {
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, CommodityCatalog.AudioFile(_selectedCommodity));
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null || _market is null) return;
        var percent = _company.CargoCapacity == 0 ? 0 : _company.CargoUsed * 100 / _company.CargoCapacity;
        CargoText.Text = $"Ship's cargo bay = {percent}% filled ({_company.CargoUsed}/{_company.CargoCapacity})";
        CashText.Text = _company.Cash.ToString("N0");
        ProfitText.Text = _company.CommodityProfitThisWeek.ToString("N0");
        AvailableFilterButton.Classes.Set("selected", _filter == MarketFilter.Available);
        CargoFilterButton.Classes.Set("selected", _filter == MarketFilter.Cargo);
        AllFilterButton.Classes.Set("selected", _filter == MarketFilter.All);
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index]; var listing = _market.Listings[index];
            var lot = _company.Cargo.GetValueOrDefault(index);
            row.Grid.IsVisible = _filter switch
            {
                MarketFilter.Available => listing.Quantity > 0 || lot?.Quantity > 0,
                MarketFilter.Cargo => lot?.Quantity > 0,
                _ => true
            };
            row.Grid.Background = index == _selectedCommodity ? new SolidColorBrush(Color.Parse("#D7EFFF")) : Brushes.White;
            row.Cargo.Text = (lot?.Quantity ?? 0).ToString();
            row.Available.Text = _company.AccessibleCommodityQuantity(_market, index).ToString();
            row.Paid.Text = lot is null ? "-----" : lot.AverageCost.ToString("N0");
            row.Price.Text = listing.Price.ToString("N0");
        }
    }

    private static TextBlock Cell(string text, HorizontalAlignment alignment = HorizontalAlignment.Center) => new()
    { Text = text, Foreground = Brushes.DarkSlateGray, FontSize = 16, Margin = new Thickness(8),
      VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = alignment };
    private static void Add(Grid grid, Control control, int column) { Grid.SetColumn(control, column); grid.Children.Add(control); }
    private sealed record MarketRow(Grid Grid, TextBlock Cargo, TextBlock Available, TextBlock Paid, TextBlock Price);
    private enum MarketFilter { All, Available, Cargo }
}
