using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class AuctionScreen : UserControl
{
    private GameSession? _session;
    private CompanyState? _company;
    public event EventHandler? BidCompleted;
    public AuctionScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _session = session;
        _company = company;
        var offer = session.CurrentAuction!;
        Heading.Text = offer.IsShipUpgrade ? "Ship Auction" : "Facility Auction";
        OfferText.Text = offer.IsShipUpgrade
            ? "The Traders' Union is auctioning off a 200-ton ship upgrade to the highest bidder.\n\n" +
              "The company that purchases this upgrade will be able to trade in their old ship for a new, larger ship.\n\n" +
              "You must now input your secret bid. You will be notified of the auction results at the beginning of your next turn."
            : "In an attempt to promote free trade in the Kukubian Colonies, the Emperor is privatizing government-owned facilities.\n\n" +
              $"This week, Dred is auctioning off the {offer.Name} on {offer.Planet} to the highest bidder. " +
              $"The company that buys the facility can charge other companies {offer.Fee:N0} kubars every time they visit {offer.Planet}.\n\n" +
              "You must now input your secret bid. You will be notified of the auction results at the beginning of your next turn.";
        var dred = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "DRED.SWF"), "AUCTION_DRED_STATIC");
        if (dred.IsSuccessful) AuctionImage.Source = new Bitmap(dred.ImagePath!);
    }

    private void BidButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _company is null) return;
        var offer = _session.CurrentAuction!;
        var maximum = _company.Cash + _company.Bank + Math.Max(0m, _company.StandardCreditLimit - _company.Loan);
        var heading = offer.IsShipUpgrade ? "Ship Auction" : $"{offer.Name} on {offer.Planet}";
        var summary = offer.IsShipUpgrade
            ? $"Bidder:  {_company.Name}\nCurrent Ship Size:  {_company.ShipTons:N0} tons\nCash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\n" +
              $"Loan:  {_company.Loan:N0}\nCredit Limit:  {_company.StandardCreditLimit:N0}\nMaximum Bid:  {maximum:N0}"
            : $"Bidder:  {_company.Name}\nFacility Revenue:  {offer.Fee:N0} kubars/ship\nCash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\n" +
              $"Loan:  {_company.Loan:N0}\nLoan Credit Limit:  {_company.StandardCreditLimit:N0}\nMaximum Bid:  {maximum:N0}";
        var presets = offer.BidPresets;
        AmountEntry.Show(heading, summary,
            "Enter the amount you wish to bid:", maximum, ConfirmBid,
            lowerPreset: presets.Lower,
            middlePreset: presets.Middle,
            upperPreset: presets.Upper);
    }

    private void ConfirmBid(decimal amount)
    {
        amount = decimal.Floor(Math.Max(0m, amount));
        BidConfirmation.Show(
            $"Confirm {_company?.Name ?? "Company"} Bid",
            $"\nAre you sure you want to bid {amount:N0} kubars?",
            "Yes",
            "No",
            () => PlaceBid(amount),
            () => StatusText.Text = "Your bid was not submitted.");
    }

    private void PlaceBid(decimal amount)
    {
        if (_session is null || _company is null) return;
        var result = _session.PlaceAuctionBid(_company, amount);
        if (!result.IsSuccessful) { StatusText.Text = result.Message; return; }
        BidCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void SkipButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _company is null) return;
        var result = _session.PlaceAuctionBid(_company, 0m);
        if (!result.IsSuccessful)
        {
            StatusText.Text = result.Message;
            return;
        }
        BidCompleted?.Invoke(this, EventArgs.Empty);
    }
}
