using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenTradeEngine.Screens;

public partial class AdvertisingScreen : UserControl
{
    private static readonly string[] Names =
        ["Do Not Advertise", "Fliers", "Newspaper", "Magazine", "Radio", "TV", "Everything"];

    private CompanyState? _company;
    private bool _editingPassengers = true;
    private int _pendingPassenger;
    private int _pendingCommodity;

    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public AdvertisingScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = GameBitmapCache.Load(help.ImagePath!);
        _pendingPassenger = Math.Clamp(company.PreferredPassengerAdvertising, 0, 6);
        _pendingCommodity = Math.Clamp(company.PreferredCommodityAdvertising, 0, 6);
        Refresh();
    }

    private void PassengerTab_Click(object? sender, RoutedEventArgs e)
    {
        _editingPassengers = true;
        StatusText.Text = string.Empty;
        Refresh();
    }

    private void CommodityTab_Click(object? sender, RoutedEventArgs e)
    {
        _editingPassengers = false;
        StatusText.Text = string.Empty;
        Refresh();
    }

    private void LevelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var level)) return;
        if (_editingPassengers) _pendingPassenger = level;
        else _pendingCommodity = level;
        _company?.RememberAdvertisingCampaign(_pendingPassenger, _pendingCommodity);
        StatusText.Text = string.Empty;
        Refresh();
    }

    private void PlaceAdsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        var result = _company.SetAdvertisingCampaign(_pendingPassenger, _pendingCommodity);
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        Refresh();
        if (result.IsSuccessful)
        {
            // Returning to the turn menu stops the previous screen's voice.
            // Navigate first, then start the confirmation sound so the normal
            // path behaves exactly like the quick-advertising path.
            ContinueRequested?.Invoke(this, EventArgs.Empty);
            SoundRequested?.Invoke(this, "ADVERT.MP3");
        }
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Advertising Help", OriginalHelpCatalog.Advertising);

    private void Refresh()
    {
        if (_company is null) return;
        var selected = _editingPassengers ? _pendingPassenger : _pendingCommodity;
        var buttons = new[]
        {
            Level0Button, Level1Button, Level2Button, Level3Button,
            Level4Button, Level5Button, Level6Button
        };
        foreach (var button in buttons)
        {
            if (button.Tag is not string tag || !int.TryParse(tag, out var level)) continue;
            var label = $"{Names[level]} ({_company.AdvertisingCost(level):N0} kubars)";
            button.Content = level == selected ? SelectedLevelContent(label) : label;
            button.BorderBrush = level == selected ? Brushes.DeepSkyBlue : Brushes.DimGray;
        }

        PassengerTab.Background = _editingPassengers ? Brushes.SlateBlue : Brushes.DimGray;
        CommodityTab.Background = _editingPassengers ? Brushes.DimGray : Brushes.SlateBlue;
        CampaignText.Text = _pendingPassenger == 0 && _pendingCommodity == 0
            ? "None"
            : $"Passenger: {Names[_pendingPassenger]}  |  Commodity: {Names[_pendingCommodity]}";
        PassengerText.Text = $"Level {_pendingPassenger}  ·  {_company.AdvertisingCost(_pendingPassenger):N0} kubars";
        CommodityText.Text = $"Level {_pendingCommodity}  ·  {_company.AdvertisingCost(_pendingCommodity):N0} kubars  ·  " +
                             $"+{(int)decimal.Floor(_company.AdvertisingCost(_pendingCommodity) / 50m)} shared units";
        CashText.Text = $"{_company.Cash:N0} kubars";
        PassengerText.Text = _company.AdvertisingCost(_pendingPassenger).ToString("N0");
        CommodityText.Text = _company.AdvertisingCost(_pendingCommodity).ToString("N0");
        CashText.Text = _company.Cash.ToString("N0");
    }

    private static Control SelectedLevelContent(string label) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 14,
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
            new PathIcon
            {
                Width = 30,
                Height = 26,
                Foreground = new SolidColorBrush(Color.Parse("#19BFFF")),
                Data = Geometry.Parse("M 1,2 L 29,13 L 1,24 L 8,13 Z")
            },
            new TextBlock
            {
                Text = label,
                FontSize = 19,
                Foreground = Brushes.DarkSlateGray,
                VerticalAlignment = VerticalAlignment.Center
            }
        }
    };
}
