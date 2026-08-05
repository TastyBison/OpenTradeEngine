using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class ShipInfoScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ReturnRequested;

    public ShipInfoScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        var ship = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{company.ShipNumber}.SWF"), $"SHIP{company.ShipNumber}");
        if (ship.IsSuccessful) ShipImage.Source = GameBitmapCache.Load(ship.ImagePath!);
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        CompanyText.Text = $"{_company.Name} — {_company.ShipModel}";
        StatsText.Text =
            $"Size: {_company.ShipTons:N0} tons       Engine Speed: {_company.EngineSpeed} kuarps\n" +
            $"Cargo: {_company.CargoUsed}/{_company.CargoCapacity} tons\n" +
            $"Passengers: {_company.Passengers}/{_company.PassengerCapacity}\n" +
            $"Fuel: {_company.Fuel:0.#}/{_company.FuelCapacity} tons       Crew: {_company.CrewCount}";
    }

    private void DetailButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null || sender is not Button { Tag: string section }) return;
        var heading = section switch
        {
            "size" => "Ship Size", "larger" => "Purchase Larger Ship", "tank" => "Fuel Tank Size",
            "crew" => "Crew", "engine" => "Engine", "passengers" => "Passenger Capacity",
            "cargo" => "Cargo Capacity", "fuel" => "Fuel Usage", _ => "Ship Information"
        };
        HelpOverlay.Show(heading, OriginalHelpCatalog.ShipDetail(_company, section));
    }

    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Ship Info Help", OriginalHelpCatalog.ShipInfo);
    private void ReturnButton_Click(object? sender, RoutedEventArgs e) => ReturnRequested?.Invoke(this, EventArgs.Empty);
}
