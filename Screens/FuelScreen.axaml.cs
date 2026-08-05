using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class FuelScreen : UserControl
{
    private CompanyState? _company;
    private PlanetMarket? _market;
    private readonly IBrush? _normalGaugeBrush;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public FuelScreen()
    {
        InitializeComponent();
        _normalGaugeBrush = GaugeFill.Background;
    }

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _company = company;
        _market = session.Markets[company.Planet];
        var ship = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{company.ShipNumber}.SWF"), $"FUEL_SHIP_{company.ShipNumber}");
        if (ship.IsSuccessful) ShipImage.Source = new Bitmap(ship.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
        Refresh();
    }

    private void FillButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null || _market is null) return;
        var capacity = _company.FuelCapacity - _company.Fuel;
        var affordable = _market.FuelPrice <= 0 ? 0 : _company.Cash / _market.FuelPrice;
        var maximum = Math.Min(capacity, affordable);
        AmountEntry.Show("Buy Fuel",
            $"Cash:  {_company.Cash:N0}\nFuel in tank:  {_company.Fuel:0.#}\nFuel capacity:  {_company.FuelCapacity:N0}\nPrice per ton:  {_market.FuelPrice:N0}",
            "How many tons of fuel?", maximum, Buy);
    }

    private void Buy(decimal quantity)
    {
        if (_company is null || _market is null) return;
        var result = _company.BuyFuel(_market, quantity);
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "FUEL.MP3");
        Refresh();
    }

    private void GaugeTrack_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_company is null || _market is null) return;
        var usableHeight = Math.Max(0d, GaugeTrack.Bounds.Height - GaugeInset * 2d);
        if (usableHeight <= 0d) return;
        var position = e.GetPosition(GaugeTrack);
        var ratio = Math.Clamp(1d - (position.Y - GaugeInset) / usableHeight, 0d, 1d);
        var target = decimal.Round(_company.FuelCapacity * (decimal)ratio, 1);
        var quantity = target - _company.Fuel;
        if (quantity <= 0m)
        {
            StatusText.Text = "Click above the current fuel level to buy up to that point.";
            StatusText.Foreground = Brushes.White;
            return;
        }
        Buy(quantity);
        e.Handled = true;
    }

    private void GaugeTrack_SizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateGaugeFill();

    private void Refresh()
    {
        if (_company is null || _market is null) return;
        PriceText.Text = _market.FuelPrice.ToString("N0");
        RemainingText.Text = _company.Fuel.ToString("0.#");
        RemainingText.Foreground = _company.IsLowOnFuel ? Brush.Parse("#F05555") : Brush.Parse("#303030");
        GaugeFill.Background = _company.IsLowOnFuel ? Brush.Parse("#F05555") : _normalGaugeBrush;
        CapacityText.Text = _company.FuelCapacity.ToString("N0");
        CashText.Text = _company.Cash.ToString("N0");
        UpdateGaugeFill();
    }

    private void UpdateGaugeFill()
    {
        if (_company is null) return;
        var percent = _company.FuelCapacity <= 0
            ? 0d
            : Math.Clamp((double)(_company.Fuel / _company.FuelCapacity), 0d, 1d);
        var usableHeight = Math.Max(0d, GaugeTrack.Bounds.Height - GaugeInset * 2d);
        GaugeFill.Height = usableHeight * percent;
    }

    private const double GaugeInset = 4d;

    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Fuel Help", OriginalHelpCatalog.Fuel(_company));
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
