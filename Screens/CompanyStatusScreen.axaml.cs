using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class CompanyStatusScreen : UserControl
{
    private static readonly string[] Difficulties = ["Tutorial", "Novice", "Beginner", "Intermediate", "Expert", "Master"];
    private static readonly string[] StatusAdjectives =
    [
        "Destitute", "Disasterous", "Insolvent", "Debtor", "Unsuccessful", "Unprosperous", "Indigent",
        "Needy", "Inefficient", "Unfortunate", "Struggling", "Average", "Striving", "Industrious",
        "Intelligent", "Diligent", "Advantageous", "Assiduous", "Capitalist", "Ardent", "First Rate",
        "Successful", "Enterprising", "Prosperous", "Thriving", "Exceptional", "Wealthy", "Elite",
        "Outstanding", "Paramount", "Supreme"
    ];

    public event EventHandler? ContinueRequested;
    public event EventHandler? GraphRequested;
    public event EventHandler? ComputerPlayersRequested;
    public event EventHandler? ShipInfoRequested;

    public CompanyStatusScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        var ship = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{company.ShipNumber}.SWF"), $"STATUS_SHIP_{company.ShipNumber}");
        if (ship.IsSuccessful) ShipImage.Source = new Bitmap(ship.ImagePath!);
        CompanyText.Text = company.Name;
        DifficultyText.Text = Difficulties[Math.Clamp(session.Level - 1, 0, Difficulties.Length - 1)];
        StartWorthText.Text = company.StartOfWeekNetWorth.ToString("N0");
        var worth = session.NetWorthOf(company);
        CurrentWorthText.Text = worth.ToString("N0");
        StatusText.Text = GetStatus(worth);
        RenderCargo(company);
    }

    private void RenderCargo(CompanyState company)
    {
        TopCargo.Items.Clear();
        BottomCargo.Items.Clear();
        var cargo = company.Cargo
            .Where(pair => pair.Value.Quantity > 0)
            .OrderBy(pair => pair.Key)
            .Take(12)
            .ToArray();
        for (var slot = 0; slot < 12; slot++)
        {
            var quantity = slot < cargo.Length ? cargo[slot].Value.Quantity.ToString("N0") : string.Empty;
            var chip = new Border
            {
                Width = 75,
                Height = 60,
                Background = new SolidColorBrush(Color.Parse("#1A2E61")),
                CornerRadius = new Avalonia.CornerRadius(7),
                Child = new TextBlock
                {
                    Text = quantity,
                    Foreground = Brushes.White,
                    FontSize = 17,
                    FontWeight = FontWeight.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            };
            if (slot < 6) TopCargo.Items.Add(chip); else BottomCargo.Items.Add(chip);
        }
    }

    private static string GetStatus(decimal netWorth)
    {
        // Original status_name(): floor(net worth / 50,000), clamped to -10..20.
        var band = Math.Clamp((int)decimal.Floor(netWorth / 50_000m), -10, 20);
        return $"{StatusAdjectives[band + 10]} Merchant";
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void GraphButton_Click(object? sender, RoutedEventArgs e) => GraphRequested?.Invoke(this, EventArgs.Empty);
    private void ComputerPlayersButton_Click(object? sender, RoutedEventArgs e) => ComputerPlayersRequested?.Invoke(this, EventArgs.Empty);
    private void ShipInfoButton_Click(object? sender, RoutedEventArgs e) => ShipInfoRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Money Help", OriginalHelpCatalog.Money(GameSession.StandardWinTarget));
}
