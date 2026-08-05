using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class OpeningTurnScreen : UserControl
{
    private static readonly int[] PassengerCapacity = [8, 8, 8, 11, 6, 8, 7, 5, 10, 1, 16, 8];
    private readonly IBrush? _normalFuelBrush;

    public event EventHandler? MarketplaceRequested;
    public event EventHandler? SupplyRequested;
    public event EventHandler? FuelRequested;
    public event EventHandler<string>? FinanceRequested;
    public event EventHandler? WarehouseRequested;
    public event EventHandler? StockMarketRequested;
    public event EventHandler? AdvertisingRequested;
    public event EventHandler? InsuranceRequested;
    public event EventHandler? ExploreRequested;
    public event EventHandler? FileOptionsRequested;
    public event EventHandler? CrewRequested;
    public event EventHandler? TaxesRequested;
    public event EventHandler? PassengersRequested;
    public event EventHandler? JourneyRequested;

    public OpeningTurnScreen()
    {
        InitializeComponent();
        _normalFuelBrush = FuelLevelFill.Background;
    }

    public void Load(
        GameInstallation installation,
        string companyName,
        string planet,
        decimal zinnLoan,
        decimal cash,
        int shipNumber)
    {
        CompanyHeading.Text = companyName;
        JourneyText.Text = $"Journey (Leave {planet})";
        ZinnLoanText.Text = $"Zinn's Loan: {zinnLoan:N0}";
        MoneyText.Text = $"Money: {cash:N0}";
        PassengerText.Text = $"Pick Up Passengers: {PassengerCapacity[Math.Clamp(shipNumber - 1, 0, PassengerCapacity.Length - 1)]}";
        var planetImage = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.ResourcesDirectory, $"{planet.ToUpperInvariant()}1.SWF"),
            $"PLANET_ICON_TRANSPARENT_{planet.ToUpperInvariant()}");
        if (planetImage.IsSuccessful) PlanetImage.Source = GameBitmapCache.Load(planetImage.ImagePath!);

        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);

        LoadEmbeddedIcon(installation, MarketIcon, "Gazillionaire__embed_mxml_i_market");
        LoadEmbeddedIcon(installation, SupplyIcon, "Gazillionaire__embed_mxml_i_supply");
        LoadEmbeddedIcon(installation, WarehouseIcon, "154");
        LoadEmbeddedIcon(installation, StockIcon, "Gazillionaire__embed_mxml_i_stock");
        LoadEmbeddedIcon(installation, MoneyIcon, "Gazillionaire__embed_mxml_i_money");
        LoadEmbeddedIcon(installation, BankIcon, "Gazillionaire__embed_mxml_i_bank");
        LoadEmbeddedIcon(installation, LoanIcon, "Gazillionaire__embed_mxml_i_loan");
        LoadEmbeddedIcon(installation, ZinnIcon, "Gazillionaire__embed_mxml_i_zinn");
        LoadEmbeddedIcon(installation, PassengerIcon, "Gazillionaire_BoyIconClass");
        LoadEmbeddedIcon(installation, AdvertisingIcon, "112");
        LoadEmbeddedIcon(installation, CrewIcon, "144");
        LoadEmbeddedIcon(installation, TaxIcon, "Gazillionaire__embed_mxml_i_tax");
        LoadEmbeddedIcon(installation, InsuranceIcon, "109");
        LoadEmbeddedIcon(installation, ExploreIcon, "Gazillionaire__embed_mxml_i_explore");
        LoadEmbeddedIcon(installation, FileIcon, "Gazillionaire__embed_mxml_i_file");
        LoadEmbeddedIcon(installation, FuelIcon, "Gazillionaire__embed_mxml_i_fuel");
        LoadEmbeddedIcon(installation, HelpIcon, "Gazillionaire__embed_mxml_i_help");
    }

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        Load(installation, company.Name, company.Planet, company.ZinnLoan, company.Cash, company.ShipNumber);
        MoneyText.Text = $"Money: {company.Cash:N0}";
        BankText.Text = $"Bank: {company.Bank:N0}";
        LoanText.Text = $"Loan: {company.Loan:N0}";
        ZinnLoanText.Text = $"Zinn's Loan: {company.ZinnLoan:N0}";
        LoanText.Foreground = company.WouldExceedUnionCreditLimit
            ? Brush.Parse("#F05555") : Brushes.White;
        ZinnLoanText.Foreground = company.WouldExceedZinnCreditLimit
            ? Brush.Parse("#F05555") : Brushes.White;
        JourneyText.Text = $"Journey (Leave {company.Planet})";
        var waitingPassengers = company.PassengersPickedUp
            ? 0
            : company.PreviewPassengers(new Random(GameMath.StableHash(
                session.Seed, session.Week.ToString(), company.Name)));
        PassengerText.Text = $"Pick Up Passengers: {waitingPassengers}";
        LoadEmbeddedIcon(installation, AdvertisingIcon,
            company.AdvertisingLightOn
                ? "Gazillionaire_LampOnIconClass"
                : "112");
        FuelPriceText.Text = session.Markets[company.Planet].FuelPrice.ToString("N0");
        FuelButton.Content = company.IsLowOnFuel
            ? "LOW ON FUEL"
            : $"Fuel {company.Fuel:0.#}/{company.FuelCapacity}";
        FuelButton.Foreground = company.IsLowOnFuel ? Brush.Parse("#F05555") : Brushes.White;
        FuelLevelFill.Background = company.IsLowOnFuel ? Brush.Parse("#F05555") : _normalFuelBrush;
        if (FuelLevelFill.RenderTransform is ScaleTransform fuelScale)
        {
            fuelScale.ScaleY = company.FuelCapacity <= 0
                ? 0
                : Math.Clamp((double)company.Fuel / company.FuelCapacity, 0, 1);
        }
        CrewText.Text = $"Crew Wages Owed: {company.CrewWagesOwed:N0}";
        TaxesText.Text = $"Taxes Owed: {company.TaxesOwed + company.TariffsOwed:N0}";
        CrewText.Foreground = company.CrewWagesOwed >= company.CrewCount * company.CrewSalary * 4m
            ? Brush.Parse("#F05555") : Brushes.White;
        TaxesText.Foreground = company.IsTaxAuditRisk
            ? Brush.Parse("#F05555") : Brushes.White;
        InsuranceText.Text = company.InsuranceLevel > 0
            ? $"Insurance/Cost: Yes/{company.InsuranceCost:N0}"
            : $"Insurance/Cost: None/{company.InsuranceCost:N0}";
        ApplyTutorialLocks(session);
    }

    private void ApplyTutorialLocks(GameSession session)
    {
        if (!session.IsTutorial) return;
        session.PrepareTutorialStage();
        var stage = session.TutorialStage;
        SupplyButton.IsEnabled = stage >= 3;
        ZinnLoanButton.IsEnabled = stage >= 2;
        LoanButton.IsEnabled = stage >= 4;
        BankButton.IsEnabled = stage >= 12;
        InsuranceButton.IsEnabled = stage >= 5;
        ExploreButton.IsEnabled = stage >= 6;
        FuelButton.IsEnabled = stage >= 7;
        FuelPanel.IsEnabled = stage >= 7;
        PassengerButton.IsEnabled = stage >= 8;
        CrewButton.IsEnabled = stage >= 9;
        AdvertisingButton.IsEnabled = stage >= 10;
        TaxesButton.IsEnabled = stage >= 11;
        WarehouseButton.IsEnabled = stage >= 13;
        StockMarketButton.IsEnabled = stage >= 17;
    }

    private static void LoadEmbeddedIcon(GameInstallation installation, Image image, string identifier)
    {
        var result = SwfImageExtractor.TryExtractEmbeddedImage(installation.MainSwfPath, identifier);
        if (result.IsSuccessful) image.Source = GameBitmapCache.Load(result.ImagePath!);
    }

    private void MarketplaceButton_Click(object? sender, RoutedEventArgs e) =>
        MarketplaceRequested?.Invoke(this, EventArgs.Empty);

    private void PassengerButton_Click(object? sender, RoutedEventArgs e) =>
        PassengersRequested?.Invoke(this, EventArgs.Empty);

    private void SupplyButton_Click(object? sender, RoutedEventArgs e) =>
        SupplyRequested?.Invoke(this, EventArgs.Empty);

    private void JourneyButton_Click(object? sender, RoutedEventArgs e) =>
        JourneyRequested?.Invoke(this, EventArgs.Empty);

    private void FuelPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!FuelPanel.IsEnabled) return;
        e.Handled = true;
        FuelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FinanceButton_Click(object? sender, RoutedEventArgs e)
    {
        var action = sender switch
        {
            Button button when button == BankButton => "bank",
            Button button when button == LoanButton => "loan",
            Button button when button == ZinnLoanButton => "zinn",
            _ => "money"
        };
        FinanceRequested?.Invoke(this, action);
    }

    private void WarehouseButton_Click(object? sender, RoutedEventArgs e) =>
        WarehouseRequested?.Invoke(this, EventArgs.Empty);

    private void StockMarketButton_Click(object? sender, RoutedEventArgs e) =>
        StockMarketRequested?.Invoke(this, EventArgs.Empty);

    private void AdvertisingButton_Click(object? sender, RoutedEventArgs e) =>
        AdvertisingRequested?.Invoke(this, EventArgs.Empty);

    private void InsuranceButton_Click(object? sender, RoutedEventArgs e) =>
        InsuranceRequested?.Invoke(this, EventArgs.Empty);

    private void ExploreButton_Click(object? sender, RoutedEventArgs e) =>
        ExploreRequested?.Invoke(this, EventArgs.Empty);

    private void FileOptionsButton_Click(object? sender, RoutedEventArgs e) =>
        FileOptionsRequested?.Invoke(this, EventArgs.Empty);

    private void CrewButton_Click(object? sender, RoutedEventArgs e) =>
        CrewRequested?.Invoke(this, EventArgs.Empty);

    private void TaxesButton_Click(object? sender, RoutedEventArgs e) =>
        TaxesRequested?.Invoke(this, EventArgs.Empty);

    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Main Menu Help", OriginalHelpCatalog.MainMenu);
}
