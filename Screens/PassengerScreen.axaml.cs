using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class PassengerScreen : UserControl
{
    private CompanyState? _company;
    private int _waiting;

    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public PassengerScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _company = company;
        var seed = GameMath.StableHash(session.Seed, session.Week.ToString(), company.Name);
        _waiting = company.PassengersPickedUp ? 0 : company.PreviewPassengers(new Random(seed));

        var ship = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{company.ShipNumber}.SWF"), $"PASSENGER_SHIP_{company.ShipNumber}");
        if (ship.IsSuccessful) ShipImage.Source = new Bitmap(ship.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
        Refresh();
    }

    private void SetTicketPriceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        AmountEntry.Show("Set Ticket Price",
            $"Current ticket price:  {_company.TicketPrice:N0}\nNext ticket price:  {_company.NextTicketPrice:N0}",
            "Enter the ticket price for the next planet:", 10_000m, amount =>
            {
                var result = _company.SetNextTicketPrice(amount);
                if (!result.IsSuccessful) return;
                SoundRequested?.Invoke(this, "TICKET.MP3");
                Refresh();
            }, _company.NextTicketPrice, 1_000m, 3_000m, 5_000m, 100m);
    }

    private void PickUpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        var result = _company.BoardPassengers(_waiting);
        _waiting = 0;
        WaitingText.Text = result.Message;
        WaitingText.Foreground = result.IsSuccessful ? Brushes.DarkSlateGray : Brushes.OrangeRed;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "PICKUP.MP3");
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        var percent = _company.PassengerCapacity == 0 ? 0 : _company.Passengers * 100 / _company.PassengerCapacity;
        SeatsFilledText.Text = $"Passenger Seats Filled:   {percent}%   ({_company.Passengers}/{_company.PassengerCapacity})";
        ProfitText.Text = $"Profit This Week:  {_company.Passengers * _company.TicketPrice:N0}";
        CapacityText.Text = $"Passenger Capacity:  {_company.PassengerCapacity}";
        OnShipText.Text = $"Passengers On Ship:  {_company.Passengers}";
        WaitingText.Text = $"Passengers Waiting:  {_waiting}";
        WaitingText.Foreground = Brushes.DarkSlateGray;
        CurrentFareText.Text = $"Ticket Price This Week:  {_company.TicketPrice:N0}";
        NextFareText.Text = $"Ticket Price For Next Planet:  {_company.NextTicketPrice:N0}";
    }

    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Passenger Help", OriginalHelpCatalog.Passengers(_company));
    }
    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
