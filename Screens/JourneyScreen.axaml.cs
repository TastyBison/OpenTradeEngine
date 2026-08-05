using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace OpenTradeEngine.Screens;

public partial class JourneyScreen : UserControl
{
    private readonly List<(Button Button, int Index)> _planetButtons = [];
    private GameSession? _session;
    private CompanyState? _company;
    private bool _departureCommitted;

    public event EventHandler<string>? DestinationSelected;
    public event EventHandler? BankruptcyRequested;
    public event EventHandler? ReturnRequested;
    public event EventHandler? FacilitiesRequested;
    public event EventHandler? DistanceRequested;

    public JourneyScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _departureCommitted = false;
        _session = session;
        _company = company;
        if (session.IsTutorial)
        {
            session.PrepareTutorialStage();
            DistanceButton.IsEnabled = session.TutorialStage >= 15;
            FacilitiesButton.IsEnabled = session.TutorialStage >= 16;
        }
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = GameBitmapCache.Load(help.ImagePath!);

        PlanetCanvas.Children.Clear();
        _planetButtons.Clear();
        for (var index = 0; index < session.Planets.Count; index++)
        {
            var planet = session.Planets[index];
            var image = new Image { Width = 118, Height = 108, Stretch = Stretch.Uniform };
            var modIcon = ModCatalog.ResolvePlanetAsset(planet, definition => definition.Icon);
            if (modIcon is not null) image.Source = GameBitmapCache.Load(modIcon);
            else
            {
                var art = SwfImageExtractor.TryExtractLargestEmbeddedImage(
                    Path.Combine(installation.ResourcesDirectory, $"{planet.ToUpperInvariant()}1.SWF"),
                    $"JOURNEY_PLANET_{planet.ToUpperInvariant()}");
                if (art.IsSuccessful) image.Source = GameBitmapCache.Load(art.ImagePath!);
            }
            var isCurrent = planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase);
            var isPlanned = planet.Equals(company.PlannedDestination, StringComparison.OrdinalIgnoreCase);
            var heading = new TextBlock
            {
                Text = planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase) ? $"●  {planet}" : planet,
                Foreground = isCurrent
                    ? new SolidColorBrush(Color.Parse("#FF8A18"))
                    : isPlanned ? Brushes.DeepSkyBlue : Brushes.White,
                FontSize = 18, FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var content = new StackPanel { Spacing = 0, Children = { heading, image } };
            var button = new Button
            {
                Tag = planet, Content = content, Width = 150, Height = 145, Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = isPlanned ? Brushes.DeepSkyBlue : Brushes.Transparent,
                BorderThickness = isPlanned ? new Thickness(4) : new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Classes.Add("planet-map");
            button.Click += DestinationButton_Click;
            PlanetCanvas.Children.Add(button);
            _planetButtons.Add((button, index));
        }
        Dispatcher.UIThread.Post(LayoutPlanets);
    }

    private void LayoutPlanets()
    {
        if (PlanetCanvas.Bounds.Width < 200 || PlanetCanvas.Bounds.Height < 200) return;
        var usableWidth = Math.Max(1, PlanetCanvas.Bounds.Width - 150);
        var usableHeight = Math.Max(1, PlanetCanvas.Bounds.Height - 145);
        foreach (var (button, index) in _planetButtons)
        {
            var position = TravelRules.MapPosition(index);
            Canvas.SetLeft(button, usableWidth * position.X / 21d);
            Canvas.SetTop(button, usableHeight * position.Y / 13d);
        }
    }

    private void DestinationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null || sender is not Button { Tag: string destination } ||
            destination.Equals(_company.Planet, StringComparison.OrdinalIgnoreCase)) return;
        if (_company.WouldExceedAnyCreditLimit)
        {
            var creditor = _company.WouldExceedUnionCreditLimit && _company.WouldExceedZinnCreditLimit
                ? "the Traders' Union and Mr. Zinn"
                : _company.WouldExceedUnionCreditLimit ? "the Traders' Union" : "Mr. Zinn";
            var paymentDetails = string.Empty;
            if (_company.RequiredUnionCreditPayment > 0m)
                paymentDetails += $"Traders' Union repayment required: {_company.RequiredUnionCreditPayment:N0} kubars\n";
            if (_company.RequiredZinnCreditPayment > 0m)
                paymentDetails += $"Mr. Zinn repayment required: {_company.RequiredZinnCreditPayment:N0} kubars\n";
            CreditWarning.Show("Credit Limit Warning",
                $"If you leave now you will go bankrupt. Your current debt is above the credit limit set by {creditor}.\n\n" +
                $"{paymentDetails}Available cash and savings: {_company.Cash + _company.Bank:N0} kubars\n\n" +
                "Return to the Main Menu to straighten out your finances. Leaving anyway will bankrupt your company.",
                "Return to Main Menu", "Leave Anyway",
                () => ReturnRequested?.Invoke(this, EventArgs.Empty),
                () =>
                {
                    _company.IsBankrupt = true;
                    BankruptcyRequested?.Invoke(this, EventArgs.Empty);
                });
            return;
        }
        _company.BankruptcyAccepted = false;
        CompleteDeparture(destination);
    }

    private void CompleteDeparture(string destination)
    {
        if (_company is null || _departureCommitted) return;
        _departureCommitted = true;
        var fuelCost = TravelRules.FuelCost(
            _company.Planet, destination, _company, _session?.Planets, _session?.Week ?? 0);
        if (ShortcutInputState.ShouldUse(_company.AutoBankOnDeparture))
            _company.BankAllCash();
        _company.Fuel -= fuelCost;
        if (_company.Fuel <= 0m) _company.ApplyEmergencyRefuel();
        _company.LastPlanet = _company.Planet;
        _company.Planet = destination;
        _company.PlannedDestination = string.Empty;
        DestinationSelected?.Invoke(this, destination);
    }

    private void PlanetCanvas_SizeChanged(object? sender, SizeChangedEventArgs e) => LayoutPlanets();
    private void ReturnButton_Click(object? sender, RoutedEventArgs e) => ReturnRequested?.Invoke(this, EventArgs.Empty);
    private void FacilitiesButton_Click(object? sender, RoutedEventArgs e) => FacilitiesRequested?.Invoke(this, EventArgs.Empty);
    private void DistanceButton_Click(object? sender, RoutedEventArgs e) => DistanceRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Journey Help", OriginalHelpCatalog.Journey(_company.Planet));
    }
}
