using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenTradeEngine.Screens;

public partial class ShipConfirmationScreen : UserControl
{
    private static readonly string[] Names =
    ["The Stinger XII", "The Fly Catcher", "Le Rock", "Whaler 2000", "Retina", "Cerebralis", "The Globulizer", "Locomotis", "Mantagon", "Kegger", "Worm Shuttle", "Squidocity"];
    private static readonly string[] Details =
    [
        "a speed demon. It comes with a high-powered 7-kuarp engine, requires 4 crew members to operate, and can carry up to 100 tons of cargo and 8 passengers. The one disadvantage is that the fuel tank is only 20 tons.",
        "designed especially for hauling cargo. It can carry up to 120 tons of cargo and comes with a 40-ton fuel tank, a 5-kuarp engine and room for 8 passengers. The one disadvantage is that it requires 5 crew members to operate.",
        "sure to please even the most frugal merchants. It requires only 3 crew members and has a 65-ton fuel tank, a 5-kuarp engine and room for 8 passengers. The one disadvantage is that it can hold only 80 tons of cargo.",
        "a monster of a ship. It can carry up to 130 tons of cargo, 50 tons of fuel and 11 passengers. The disadvantages are that it only has a 2-kuarp engine and requires 6 crew members to operate.",
        "a sleek, slim ship. It requires only 3 crew members to operate and comes with a 5-kuarp engine, 40-ton fuel tank and 100-ton cargo capacity. The one disadvantage is that it only has room for 6 passengers.",
        "the industry standard. It comes equipped with 100 tons of cargo capacity, a 5-kuarp engine, a 40-ton fuel tank, 4 crew members and room for 8 passengers.",
        "incredibly light weight and quick. It has a turbocharged 7-kuarp engine, requires 4 crew members to operate, and comes with a 30-ton fuel tank and seating for 7 passengers. The disadvantage is that it has only an 80-ton cargo bay.",
        "a sturdy workhorse. It requires 4 crew members to operate and comes with a 110-ton cargo bay, a powerful 6-kuarp engine, a 40-ton fuel tank, and room for 5 passengers.",
        "a highly efficient and well-designed merchant ship. It can carry up to 10 passengers, has a 4-kuarp engine, a 40-ton fuel tank, and a 90-ton cargo bay. The nice thing is that it only requires 3 crew members to operate.",
        "a massive transport vessel. It can carry up to 150 tons of cargo and 35 tons of fuel, but it only has room for 1 passenger. It requires 2 crew members to operate and comes with a 3-kuarp engine.",
        "a deluxe passenger liner. It can accommodate up to 16 passengers and comes with a speedy 6-kuarp engine and 30-ton fuel tank. The drawbacks are that it requires 12 crew members to operate and has a small 75-ton cargo bay.",
        "a smartly designed freighter. It can carry up to 110 tons of cargo and comes with a 40-ton fuel tank, room for 8 passengers, and a fast 6-kuarp engine. The one disadvantage is that it requires 6 crew members to operate."
    ];

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public ShipConfirmationScreen() => InitializeComponent();

    public void LoadShip(GameInstallation installation, int shipNumber, decimal price)
    {
        var index = shipNumber - 1;
        ShipHeading.Text = Names[index];
        ShipDescription.Text = index == 3
            ? $"Whaler 2000 is {Details[index].Replace("It can", $"It costs {price:N0} kubars and can")}\n\nDo you wish to purchase this ship?"
            : $"{Names[index]} costs {price:N0} kubars and is {Details[index]}\n\nDo you wish to purchase this ship?";
        var result = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{shipNumber}.SWF"), $"SHIP{shipNumber}");
        if (result.IsSuccessful) ShipImage.Source = GameBitmapCache.Load(result.ImagePath!);
    }

    private void YesButton_Click(object? sender, RoutedEventArgs e) => Confirmed?.Invoke(this, EventArgs.Empty);
    private void NoButton_Click(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
}
