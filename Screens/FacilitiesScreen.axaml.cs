using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class FacilitiesScreen : UserControl
{
    public event EventHandler? ReturnRequested;
    public FacilitiesScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session)
    {
        FacilityText.Text = session.Facilities.Count == 0
            ? "The Imperial Magistrate has not yet sold any government facilities. Auctions begin during the campaign."
            : string.Join("\n\n", session.Facilities.OrderBy(facility => facility.Planet)
                .ThenBy(facility => facility.Name)
                .Select(facility => $"{facility.Planet} — {facility.Name}\n" +
                                    $"Owner: {facility.OwnerName}    Landing fee: {facility.Fee:N0} kubars"));
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = new Bitmap(stars.ImagePath!);
    }

    private void ReturnButton_Click(object? sender, RoutedEventArgs e) => ReturnRequested?.Invoke(this, EventArgs.Empty);
}
