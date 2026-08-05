using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenTradeEngine.Screens;

namespace OpenTradeEngine;

public partial class MainWindow : Window
{
    private const string WelcomeHeading = "Welcome to Gazillionaire";
    private const string WelcomeText =
        "For the first time in 700 years, Emperor Dred Nicolson has granted you and a handful of other newly formed trading companies permission to operate inside the Kukubian Colonies.\n\n"
        + "As president of a trading company, you must make a profit transporting essential commodities between the seven planets of Kukubia.\n\n"
        + "Your goal is to build a trade empire by investing in larger ships, buying warehouses and skillfully out-maneuvering your competitors.";

    private GameInstallation? _installation;
    private bool _soundEnabled = true;
    private readonly GameAudioPlayer _audioPlayer = new();
    private int _selectedLevel;
    private int _humanPlayerCount;
    private int _aiPlayerCount;
    private int _currentHumanPlayer;
    private string _companyName = "Player 1 Inc.";
    private int _selectedShipNumber;
    private int _firstHumanShipNumber = 1;
    private readonly List<string> _humanCompanies = [];
    private readonly List<int> _humanShipNumbers = [];
    private readonly List<AiOpponentProfile> _selectedAiOpponents = [];
    private readonly List<string> _selectedPlanets = [];
    private GameSession? _gameSession;
    private int _newGameSeed;
    private bool _showNewsAfterArrival;
    private string _startingPlanet = "Bass";
    private bool _launcherSettingsReady;

    private static readonly string[] DifficultyNames =
        ["Tutorial", "Novice", "Beginner", "Intermediate", "Expert", "Master"];

    public MainWindow()
    {
        var settings = OpenTradeEngineSettings.Load();
        ModCatalog.SetEnabled(settings.ModsEnabled);
        InitializeComponent();
        EnableModsCheckBox.IsChecked = settings.ModsEnabled;
        EnableGameplayLoggingCheckBox.IsChecked = settings.GameplayLoggingEnabled;
        GameplayLogLimitComboBox.SelectedIndex = settings.EffectiveGameplayLogLimitMb switch
        {
            25 => 0,
            50 => 1,
            250 => 3,
            _ => 2
        };
        GameplayLogLimitPanel.IsEnabled = settings.GameplayLoggingEnabled;
        GameplayLogger.Configure(settings.GameplayLoggingEnabled, settings.EffectiveGameplayLogLimitMb);
        UpdateLauncherModsStatus();
        _launcherSettingsReady = true;
        AddHandler(
            InputElement.PointerPressedEvent,
            MainWindow_PointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            InputElement.PointerReleasedEvent,
            MainWindow_PointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            InputElement.KeyDownEvent,
            MainWindow_KeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        RestoreSavedInstallation(settings);
    }

    private void RestoreSavedInstallation(OpenTradeEngineSettings? savedSettings = null)
    {
        var settings = savedSettings ?? OpenTradeEngineSettings.Load();
        if (string.IsNullOrWhiteSpace(settings.InstallationPath))
        {
            return;
        }

        InstallationPathTextBox.Text = settings.InstallationPath;
        var result = GameInstallation.TryOpen(settings.InstallationPath);
        if (!result.IsValid)
        {
            StatusTextBlock.Foreground = Brushes.Firebrick;
            StatusTextBlock.Text =
                "The previously selected Gazillionaire installation could not be found. Please browse for it again.";
            return;
        }

        _installation = result.Installation!;
        _audioPlayer.SetInstallation(_installation);
        ContinueButton.IsEnabled = true;
        StatusTextBlock.Foreground = Brushes.ForestGreen;
        StatusTextBlock.Text = "Saved Gazillionaire installation found and ready.";
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select the Gazillionaire installation folder",
                AllowMultiple = false
            });

        if (folders.Count == 0)
        {
            return;
        }

        var selectedPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            ShowError("The selected folder is not available as a local path.");
            return;
        }

        InstallationPathTextBox.Text = selectedPath;

        var result = GameInstallation.TryOpen(selectedPath);
        if (!result.IsValid)
        {
            _installation = null;
            ContinueButton.IsEnabled = false;
            ShowError(result.ErrorMessage);
            return;
        }

        _installation = result.Installation!;
        _audioPlayer.SetInstallation(_installation);
        SaveLauncherSettings(selectedPath);
        ContinueButton.IsEnabled = true;
        StatusTextBlock.Foreground = Brushes.ForestGreen;
        StatusTextBlock.Text = "Gazillionaire installation found. All required game files are available.";
    }

    private void EnableModsCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_launcherSettingsReady) return;
        var enabled = EnableModsCheckBox.IsChecked == true;
        ModCatalog.SetEnabled(enabled);
        var installationPath = _installation?.RootPath;
        if (string.IsNullOrWhiteSpace(installationPath))
        {
            var saved = OpenTradeEngineSettings.Load();
            installationPath = saved.InstallationPath;
        }
        SaveLauncherSettings(installationPath, enabled);
        UpdateLauncherModsStatus();
    }

    private void GameplayLoggingSetting_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_launcherSettingsReady) return;
        var enabled = EnableGameplayLoggingCheckBox.IsChecked == true;
        GameplayLogLimitPanel.IsEnabled = enabled;
        var limit = SelectedGameplayLogLimit();
        GameplayLogger.Configure(enabled, limit);
        SaveLauncherSettings();
        if (enabled && _gameSession is not null)
            GameplayLogger.StartSession(_gameSession, "logging enabled during game");
    }

    private int SelectedGameplayLogLimit()
    {
        if (GameplayLogLimitComboBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var value))
            return value;
        return 100;
    }

    private void SaveLauncherSettings(string? installationPath = null, bool? modsEnabled = null)
    {
        var saved = OpenTradeEngineSettings.Load();
        installationPath ??= _installation?.RootPath ?? saved.InstallationPath;
        new OpenTradeEngineSettings(
            installationPath,
            modsEnabled ?? EnableModsCheckBox.IsChecked == true,
            EnableGameplayLoggingCheckBox.IsChecked == true,
            SelectedGameplayLogLimit()).Save();
    }

    private void UpdateLauncherModsStatus()
    {
        if (!ModCatalog.Enabled)
        {
            LauncherModsStatusText.Text = "Mods are disabled. The Mods folder will not affect this session.";
            return;
        }

        LauncherModsStatusText.Text =
            $"Mods enabled: {ModCatalog.Mods.Count} mod(s), {ModCatalog.Planets.Count} planet definition(s), " +
            $"{ModCatalog.Events.Count} event definition(s) loaded.";
        if (ModCatalog.Errors.Count > 0)
            LauncherModsStatusText.Text += $" {ModCatalog.Errors.Count} file(s) were skipped.";
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_installation is null)
        {
            return;
        }

        var sourcePath = Path.Combine(_installation.SwfDirectory, "ZILE2.SWF");
        if (!File.Exists(sourcePath))
        {
            ShowError("The installation is missing SWF\\ZILE2.SWF, which is required by the main menu.");
            return;
        }

        var background = SwfImageExtractor.TryExtractFirstEmbeddedImage(sourcePath, "TITLE_STARS");
        var planet = SwfImageExtractor.TryExtractLargestEmbeddedImage(sourcePath, "TITLE_ZILE");
        if (!background.IsSuccessful || !planet.IsSuccessful)
        {
            ShowError(!background.IsSuccessful ? background.ErrorMessage : planet.ErrorMessage);
            return;
        }

        MainMenuBackgroundImage.Source = new Bitmap(background.ImagePath!);
        MainMenuPlanetImage.Source = new Bitmap(planet.ImagePath!);
        InstallationPanel.IsVisible = false;
        MainMenuPanel.IsVisible = true;
        ShowMenuText(WelcomeHeading, WelcomeText);
    }

    private void StartNewGameButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_installation is null) return;

        var screen = new DifficultyScreen();
        screen.LevelSelected += DifficultyScreen_LevelSelected;
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void DifficultyScreen_LevelSelected(object? sender, int level)
    {
        if (_installation is null) return;

        _selectedLevel = level;
        var screen = new PlayerCountScreen();
        screen.PlayerCountSelected += PlayerCountScreen_PlayerCountSelected;
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void PlayerCountScreen_PlayerCountSelected(object? sender, int playerCount)
    {
        if (_installation is null) return;

        _humanPlayerCount = playerCount;
        var screen = new AiPlayerCountScreen();
        screen.PlayerCountSelected += AiPlayerCountScreen_PlayerCountSelected;
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void AiPlayerCountScreen_PlayerCountSelected(object? sender, int playerCount)
    {
        if (_installation is null) return;

        _aiPlayerCount = playerCount;
        _newGameSeed = Random.Shared.Next();
        _selectedAiOpponents.Clear();
        _selectedAiOpponents.AddRange(AiOpponentCatalog.Select(_aiPlayerCount, _newGameSeed));
        _currentHumanPlayer = 1;
        _humanCompanies.Clear();
        _humanShipNumbers.Clear();
        _selectedPlanets.Clear();
        _gameSession = null;

        var screen = new PlanetLayoutScreen();
        screen.ContinueRequested += PlanetLayoutScreen_ContinueRequested;
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void PlanetLayoutScreen_ContinueRequested(object? sender, EventArgs e)
    {
        if (_installation is null) return;

        if (sender is PlanetLayoutScreen planetScreen && planetScreen.SelectedPlanetNames.Count > 0)
        {
            _selectedPlanets.AddRange(planetScreen.SelectedPlanetNames);
            var layoutRandom = new Random(GameMath.StableHash(_newGameSeed, "initial planet"));
            _startingPlanet = _selectedPlanets[layoutRandom.Next(_selectedPlanets.Count)];
        }

        if (_aiPlayerCount == 0)
        {
            ShowShipSelection();
            return;
        }

        var screen = new CompetitorScreen();
        screen.ContinueRequested += CompetitorScreen_ContinueRequested;
        screen.LoadCompetitors(_installation, _selectedAiOpponents);
        ShowScreen(screen);
    }

    private void CompetitorScreen_ContinueRequested(object? sender, EventArgs e) =>
        ShowShipSelection();

    private void ShowShipSelection(bool requestCompanyName = true)
    {
        if (_installation is null) return;

        var screen = new ShipSelectionScreen();
        screen.ShipSelected += ShipSelectionScreen_ShipSelected;
        screen.LoadShips(
            _installation,
            requestCompanyName ? $"Player {_currentHumanPlayer} Inc." : _companyName,
            requestCompanyName,
            new HashSet<int>(_humanShipNumbers));
        ShowScreen(screen);
    }

    private void ShipSelectionScreen_ShipSelected(object? sender, ShipSelectedEventArgs e)
    {
        if (_installation is null) return;
        if (_humanShipNumbers.Contains(e.ShipNumber))
        {
            ShowShipSelection(requestCompanyName: false);
            return;
        }

        _companyName = e.CompanyName;
        _selectedShipNumber = e.ShipNumber;
        if (_currentHumanPlayer == 1) _firstHumanShipNumber = e.ShipNumber;

        var screen = new ShipConfirmationScreen();
        screen.Confirmed += ShipConfirmationScreen_Confirmed;
        screen.Cancelled += ShipConfirmationScreen_Cancelled;
        screen.LoadShip(_installation, _selectedShipNumber, StartingLoan);
        ShowScreen(screen);
        PlayShipMusic(_selectedShipNumber);
    }

    private void ShipConfirmationScreen_Cancelled(object? sender, EventArgs e)
    {
        // The ship theme is only a preview until the purchase is confirmed.
        // Returning to the selection grid should be silent.
        _audioPlayer.StopVoice();
        ShowShipSelection(requestCompanyName: false);
    }

    private void ShipConfirmationScreen_Confirmed(object? sender, EventArgs e)
    {
        if (_installation is null) return;

        var screen = new ZinnFinancingScreen();
        screen.ContinueRequested += ZinnFinancingScreen_ContinueRequested;
        screen.Load(_installation, StartingLoan);
        ShowScreen(screen);
    }

    private void ZinnFinancingScreen_ContinueRequested(object? sender, EventArgs e)
    {
        _humanCompanies.Add(_companyName);
        _humanShipNumbers.Add(_selectedShipNumber);

        if (_currentHumanPlayer < _humanPlayerCount)
        {
            _currentHumanPlayer++;
            ShowShipSelection();
            return;
        }

        ShowPlayerTurn();
    }

    private void ShowPlayerTurn()
    {
        if (_installation is null || _humanCompanies.Count == 0) return;

        EnsureGameSession();
        if (TryShowCampaignOutcome()) return;

        var screen = new PlayerTurnScreen();
        screen.BeginTurnRequested += PlayerTurnScreen_BeginTurnRequested;
        screen.ShipInfoRequested += (_, _) => ShowPreTurnShipInfo();
        screen.Load(
            _installation,
            _gameSession!,
            HumanCompany,
            DifficultyNames[Math.Clamp(_selectedLevel - 1, 0, DifficultyNames.Length - 1)]);
        ShowScreen(screen);
    }

    private bool TryShowCreditCrisis()
    {
        if (_installation is null || _gameSession is null ||
            !HumanCompany.CreditCrisisNoticePending || HumanCompany.IsBankrupt) return false;

        var company = HumanCompany;
        var union = Math.Max(0m, company.Loan - company.MaximumSafeUnionPrincipal);
        var zinn = Math.Max(0m, company.ZinnLoan - company.MaximumSafeZinnPrincipal);
        var creditor = union > 0m && zinn > 0m
            ? "the Traders' Union and Mr. Zinn"
            : union > 0m ? "the Traders' Union" : "Mr. Zinn";
        var details = union > 0m ? $"Traders' Union repayment required: {union:N0} kubars\n" : string.Empty;
        if (zinn > 0m) details += $"Mr. Zinn repayment required: {zinn:N0} kubars\n";

        var choice = new TravelEventChoice("Pay Required", "Manage Finances", false, accepted =>
        {
            if (!accepted)
                return new TravelEventResult("Credit Limit Exceeded", string.Empty, false);
            var available = company.Cash + company.Bank;
            var required = company.RequiredCreditPayment;
            if (available < required)
                return new TravelEventResult("Payment Not Possible",
                    $"You need {required:N0} kubars, but only have {available:N0} in cash and savings. Sell cargo or shares, collect income, or arrange your finances before departing.",
                    false, union > 0m ? "LOAN_N.SWF" : "ZINN_N.SWF");
            var paid = company.PayRequiredCreditBalance();
            GameSaveService.SaveAutosave(_gameSession);
            return new TravelEventResult("Credit Restored",
                $"You repaid {paid:N0} kubars. Your projected debt is now within both credit limits.",
                true, union > 0m ? "LOAN_N.SWF" : "ZINN_N.SWF");
        });
        var screen = new TravelEventScreen();
        screen.ContinueRequested += (_, _) =>
        {
            company.CreditCrisisNoticePending = false;
            GameSaveService.SaveAutosave(_gameSession);
            ShowPlayerTurn();
        };
        screen.Load(_installation, new TravelEventResult(
            $"{company.Name}: Credit Limit Exceeded",
            $"{company.Name}'s debt was pushed above the limit set by {creditor} by this week's interest. The company is not bankrupt yet.\n\n" +
            details + $"Available cash and savings: {company.Cash + company.Bank:N0} kubars\n\n" +
            "Pay now, or return to the game and raise the money. You cannot safely depart until the debt is back within the limit.",
            false, union > 0m ? "LOAN_N.SWF" : "ZINN_N.SWF", union > 0m ? "LOAN.MP3" : "ZINN.MP3", choice));
        ShowScreen(screen);
        return true;
    }

    private void ShowPreTurnShipInfo()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new ShipInfoScreen();
        screen.ReturnRequested += (_, _) => ShowPlayerTurn();
        screen.Load(_installation, HumanCompany);
        ShowScreen(screen);
    }

    private bool TryShowPendingAuction(CompanyState company, Action continueDeparture)
    {
        if (_installation is null || _gameSession?.CurrentAuction is null) return false;
        if (!ReferenceEquals(_gameSession.CurrentTurnCompany, company)) return false;
        if (!_gameSession.CurrentAuction.IsShipUpgrade && !_gameSession.FacilityAuctionsUnlocked) return false;
        if (_gameSession.CurrentAuction.Bids.ContainsKey(company.Name)) return false;
        var screen = new AuctionScreen();
        screen.BidCompleted += (_, _) =>
        {
            GameSaveService.SaveAutosave(_gameSession);
            continueDeparture();
        };
        screen.Load(_installation, _gameSession, company);
        ShowScreen(screen);
        return true;
    }

    private bool TryShowPendingAuctionResult()
    {
        if (_installation is null || _gameSession is null ||
            !_gameSession.HasPendingAuctionResult(HumanCompany)) return false;
        var notice = _gameSession.PendingAuctionResult!;
        if (notice.Heading.EndsWith("won the auction!", StringComparison.OrdinalIgnoreCase))
        {
            var announcement = new CompanyAnnouncementScreen();
            announcement.ContinueRequested += (_, _) =>
            {
                _gameSession.AcknowledgeAuctionResult(HumanCompany);
                GameSaveService.SaveAutosave(_gameSession);
                ContinueBeginningTurn();
            };
            announcement.Load(_installation, notice.Heading, notice.Message,
                notice.ImageAsset, notice.AudioAsset, true);
            ShowScreen(announcement);
            return true;
        }

        var screen = new TravelEventScreen();
        screen.ContinueRequested += (_, _) =>
        {
            _gameSession.AcknowledgeAuctionResult(HumanCompany);
            GameSaveService.SaveAutosave(_gameSession);
            ContinueBeginningTurn();
        };
        screen.Load(_installation, new TravelEventResult(
            notice.Heading, notice.Message, true, notice.ImageAsset, notice.AudioAsset));
        ShowScreen(screen);
        return true;
    }

    private bool TryShowCampaignOutcome()
    {
        if (_gameSession is null) return false;
        var winner = _gameSession.Winner;
        var humans = _gameSession.Companies.Where(company => company.IsHuman).ToArray();
        if (winner is null && humans.Any(company => !company.IsBankrupt)) return false;

        var screen = new CampaignOutcomeScreen();
        screen.MainMenuRequested += (_, _) => ShowMainMenu();
        if (winner is not null && _gameSession.WinTarget < 10_000_000_000m)
        {
            screen.ContinueGameRequested += (_, _) =>
            {
                _gameSession.WinTarget = Math.Min(10_000_000_000m, _gameSession.WinTarget * 2m);
                GameSaveService.SaveAutosave(_gameSession);
                ShowPlayerTurn();
            };
        }
        screen.Load(winner is not null
            ? $"{winner.Name} Wins!"
            : "Game Over",
            winner is not null
                ? $"{winner.Name} is the first company to reach the { _gameSession.WinTarget:N0}-kubar target. " +
                  (_gameSession.WinTarget < 10_000_000_000m
                      ? "You may end the campaign here or keep playing toward a greater victory."
                      : "The ultimate ten-billion-kubar victory has been achieved.")
                : "Every human trading company has gone bankrupt. The Imperial Magistrate has revoked the remaining trading licences.",
            winner is not null && _gameSession.WinTarget < 10_000_000_000m);
        ShowScreen(screen);
        return true;
    }

    private void PlayerTurnScreen_BeginTurnRequested(object? sender, EventArgs e)
    {
        // The ranking screen begins the next company's turn. Arrival, news and
        // the graph belong after this button, not at the end of the old turn.
        if (_gameSession is not null) _startingPlanet = HumanCompany.Planet;
        if (TryShowPendingAuctionResult()) return;
        ContinueBeginningTurn();
    }

    private void ContinueBeginningTurn()
    {
        if (TryShowPendingExternalNotice()) return;
        if (TryShowPendingAiEventNotice()) return;
        if (TryShowPendingFacilityNotice()) return;
        if (_installation is not null && _gameSession?.ShouldShowTutorial == true)
        {
            var screen = new TutorialScreen();
            screen.ContinueRequested += (_, _) => ShowPlanetArrival();
            screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
            screen.Load(_installation, _gameSession, HumanCompany);
            ShowScreen(screen);
            return;
        }
        ShowPlanetArrival();
    }

    private bool TryShowPendingExternalNotice()
    {
        if (_installation is null || _gameSession is null ||
            string.IsNullOrWhiteSpace(HumanCompany.PendingExternalMessage)) return false;

        var company = HumanCompany;
        var screen = new TravelEventScreen();
        screen.ContinueRequested += (_, _) =>
        {
            company.ClearExternalNotice();
            GameSaveService.SaveAutosave(_gameSession);
            ContinueBeginningTurn();
        };
        screen.Load(_installation, new TravelEventResult(
            string.IsNullOrWhiteSpace(company.PendingExternalHeading)
                ? "Sabotage Report"
                : company.PendingExternalHeading,
            company.PendingExternalMessage,
            false,
            string.IsNullOrWhiteSpace(company.PendingExternalImage) ? "SABOTAGE.SWF" : company.PendingExternalImage,
            string.IsNullOrWhiteSpace(company.PendingExternalAudio) ? "SABOTAGE.MP3" : company.PendingExternalAudio));
        ShowScreen(screen);
        return true;
    }

    private bool TryShowPendingFacilityNotice()
    {
        if (_installation is null || _gameSession is null) return false;
        var company = HumanCompany;
        if (company.PendingFacilityFees <= 0m && company.PendingFacilityRevenue <= 0m) return false;

        var payingFees = company.PendingFacilityFees > 0m;
        var message = payingFees
            ? $"You must pay {company.PendingFacilityFees:N0} kubars in facility fees while visiting {company.Planet}.\n\n" +
              "This money goes to the companies who own the facilities."
            : $"You collect {company.PendingFacilityRevenue:N0} kubars earned by your facilities on {company.Planet}.";

        var screen = new TravelEventScreen();
        screen.ContinueRequested += (_, _) =>
        {
            if (payingFees) company.PendingFacilityFees = 0m;
            else company.PendingFacilityRevenue = 0m;
            GameSaveService.SaveAutosave(_gameSession);
            ContinueBeginningTurn();
        };
        screen.Load(_installation, new TravelEventResult(
            payingFees ? "Pay Facility Fees" : "Facility Revenue",
            message, !payingFees, payingFees ? "LOAN_N.SWF" : "MONEY_N.SWF",
            payingFees ? "LOAN.MP3" : "GOOD2.MP3"));
        ShowScreen(screen);
        return true;
    }

    private bool TryShowPendingAiEventNotice()
    {
        if (_installation is null || _gameSession is null || HumanCompany.PendingTurnNotices.Count == 0)
            return false;

        var company = HumanCompany;
        var notice = company.PendingTurnNotices[0];
        // Older saves may already contain these notices as planet/company
        // announcements. Rate changes belong to the Tax Man event card.
        var governmentRateChange =
            notice.Heading.Contains("Passenger Tax Rate", StringComparison.OrdinalIgnoreCase) ||
            notice.Heading.Contains("Export Tariff Rate", StringComparison.OrdinalIgnoreCase) ||
            notice.Heading.Contains("Import Tariff Rate", StringComparison.OrdinalIgnoreCase);
        if (notice.UseCompanyAnnouncement && !governmentRateChange)
        {
            var announcement = new CompanyAnnouncementScreen();
            announcement.ContinueRequested += (_, _) =>
            {
                if (company.PendingTurnNotices.Count > 0) company.PendingTurnNotices.RemoveAt(0);
                GameSaveService.SaveAutosave(_gameSession);
                ContinueBeginningTurn();
            };
            announcement.Load(_installation, notice.Heading, notice.Message,
                notice.ImageAsset, notice.AudioAsset,
                !notice.Heading.Contains("Sabotag", StringComparison.OrdinalIgnoreCase));
            ShowScreen(announcement);
            return true;
        }

        var screen = new TravelEventScreen();
        screen.ContinueRequested += (_, _) =>
        {
            if (company.PendingTurnNotices.Count > 0) company.PendingTurnNotices.RemoveAt(0);
            GameSaveService.SaveAutosave(_gameSession);
            ContinueBeginningTurn();
        };
        screen.Load(_installation, new TravelEventResult(
            notice.Heading,
            notice.Message,
            false,
            governmentRateChange ? "TAX1_N.SWF" : notice.ImageAsset,
            governmentRateChange ? "TAX.MP3" : notice.AudioAsset));
        ShowScreen(screen);
        return true;
    }

    private decimal StartingLoan => 90_000m + (_selectedLevel * 10_000m);
    private decimal InitialCash => _selectedLevel switch
    {
        1 => 50_000m,
        2 => 25_000m,
        _ => 0m
    };
    private int InitialGoodEventChance => _selectedLevel switch
    {
        1 => 85,
        2 => 75,
        3 => 65,
        // Intermediate is the neutral ruleset: humans and AI both begin at
        // the original 50/50 good-event chance.
        4 => 50,
        _ => 50
    };

    private void ShowPlanetArrival()
    {
        if (_installation is null) return;

        var screen = new PlanetArrivalScreen();
        screen.ContinueRequested += PlanetArrivalScreen_ContinueRequested;
        screen.Load(_installation, _startingPlanet);
        ShowScreen(screen);
    }

    private void PlanetArrivalScreen_ContinueRequested(object? sender, EventArgs e)
    {
        if (_gameSession is not null)
        {
            _showNewsAfterArrival = false;
            _gameSession.LastTurnNews.Clear();
            ShowActualNetWorth();
            return;
        }

        var companies = new List<string>(_humanCompanies);
        companies.AddRange(_selectedAiOpponents.Select(opponent => opponent.Name));

        var screen = new NetWorthScreen();
        screen.ContinueRequested += NetWorthScreen_ContinueRequested;
        screen.LoadCompanies(companies, StartingLoan);
        ShowScreen(screen);
    }

    private void ShowActualNetWorth()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new NetWorthScreen();
        screen.ContinueRequested += NetWorthScreen_ContinueRequested;
        screen.LoadCompanies(_installation, _gameSession);
        ShowScreen(screen);
    }

    private void NetWorthScreen_ContinueRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null || _gameSession.Companies.Count == 0) return;

        var screen = new OpeningTurnScreen();
        ConnectOpeningTurnScreen(screen);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private CompanyState HumanCompany
    {
        get
        {
            var scheduled = _gameSession!.CurrentTurnCompany;
            if (scheduled is { IsHuman: true }) return scheduled;
            var humans = _gameSession!.Companies.Where(company => company.IsHuman).ToArray();
            var index = Math.Clamp(_gameSession.ActiveHumanIndex, 0, humans.Length - 1);
            return humans[index];
        }
    }

    private void EnsureGameSession()
    {
        if (_gameSession is not null) return;
        var planets = _selectedPlanets.Count > 0 ? _selectedPlanets : [_startingPlanet];
        _gameSession = new GameSession(_selectedLevel, planets,
            _newGameSeed == 0 ? Random.Shared.Next() : _newGameSeed);
        var usedShips = new HashSet<int>(_humanShipNumbers);
        var aiRandom = new Random(GameMath.StableHash(_gameSession.Seed, "AI ship selection"));
        var spawnRandom = new Random(GameMath.StableHash(_gameSession.Seed, "starting planets"));
        var planetOccupancy = planets.ToDictionary(
            planet => planet, _ => 0, StringComparer.OrdinalIgnoreCase);
        // Turn order, auction bids and queued notices use company names as
        // persistent keys. Reserve AI identities and make every human name
        // unique so one player can never resolve to another player's state.
        var usedCompanyNames = _selectedAiOpponents
            .Select(opponent => opponent.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < _humanCompanies.Count; index++)
        {
            var companyName = MakeUniqueCompanyName(_humanCompanies[index], usedCompanyNames);
            _humanCompanies[index] = companyName;
            var ship = index < _humanShipNumbers.Count ? _humanShipNumbers[index] : 1;
            var planet = GameSession.PickStartingPlanet(planets, planetOccupancy, spawnRandom);
            planetOccupancy[planet]++;
            if (index == 0) _startingPlanet = planet;
            _gameSession.Companies.Add(new CompanyState(
                companyName, true, ship, planet, InitialCash, StartingLoan)
                { Luck = InitialGoodEventChance });
        }

        for (var index = 0; index < _aiPlayerCount; index++)
        {
            var availableShips = Enumerable.Range(1, 12)
                .Where(candidate => !usedShips.Contains(candidate)).ToArray();
            var ship = availableShips.Length > 0
                ? availableShips[aiRandom.Next(availableShips.Length)]
                : aiRandom.Next(1, 13);
            usedShips.Add(ship);
            var planet = GameSession.PickStartingPlanet(planets, planetOccupancy, spawnRandom);
            planetOccupancy[planet]++;
            var opponent = index < _selectedAiOpponents.Count
                ? _selectedAiOpponents[index]
                : AiOpponentCatalog.All[index % AiOpponentCatalog.All.Count];
            _gameSession.Companies.Add(new CompanyState(
                opponent.Name, false, ship, planet, InitialCash, StartingLoan)
                { Luck = InitialGoodEventChance });
        }
        _gameSession.InitializeStocks();
        _gameSession.InitializeTurnOrder();
        foreach (var company in _gameSession.Companies)
            company.InsuranceCost = _gameSession.GenerateInsuranceQuote(company);
        GameplayLogger.StartSession(_gameSession, "new game");
    }

    private static string MakeUniqueCompanyName(string requestedName, HashSet<string> usedNames)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName)
            ? "Trading Company"
            : requestedName.Trim();
        if (usedNames.Add(baseName)) return baseName;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";
            if (usedNames.Add(candidate)) return candidate;
        }
    }

    private void ShowOpeningTurn()
    {
        if (_installation is null || _gameSession is null) return;
        GameSaveService.SaveAutosave(_gameSession);
        var screen = new OpeningTurnScreen();
        ConnectOpeningTurnScreen(screen);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void ConnectOpeningTurnScreen(OpeningTurnScreen screen)
    {
        screen.MarketplaceRequested += OpeningTurnScreen_MarketplaceRequested;
        screen.SupplyRequested += OpeningTurnScreen_SupplyRequested;
        screen.FuelRequested += OpeningTurnScreen_FuelRequested;
        screen.FinanceRequested += OpeningTurnScreen_FinanceRequested;
        screen.WarehouseRequested += OpeningTurnScreen_WarehouseRequested;
        screen.StockMarketRequested += OpeningTurnScreen_StockMarketRequested;
        screen.AdvertisingRequested += OpeningTurnScreen_AdvertisingRequested;
        screen.InsuranceRequested += OpeningTurnScreen_InsuranceRequested;
        screen.ExploreRequested += OpeningTurnScreen_ExploreRequested;
        screen.FileOptionsRequested += OpeningTurnScreen_FileOptionsRequested;
        screen.CrewRequested += OpeningTurnScreen_CrewRequested;
        screen.TaxesRequested += OpeningTurnScreen_TaxesRequested;
        screen.PassengersRequested += OpeningTurnScreen_PassengersRequested;
        screen.JourneyRequested += OpeningTurnScreen_JourneyRequested;
    }

    private void OpeningTurnScreen_MarketplaceRequested(object? sender, EventArgs e)
        => ShowMarketplace();

    private void ShowMarketplace()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new MarketplaceScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SupplyRequested += (_, _) => ShowSupply();
        screen.WarehouseRequested += (_, _) => ShowWarehouse();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_PassengersRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("passengers")))
        {
            var pickedUp = false;
            if (!HumanCompany.PassengersPickedUp)
            {
                HumanCompany.GeneratePassengers(new Random(GameMath.StableHash(
                    _gameSession.Seed, _gameSession.Week.ToString(), HumanCompany.Name)));
                pickedUp = true;
            }
            ShowOpeningTurn();
            if (pickedUp) _audioPlayer.PlayVoice("PICKUP.MP3");
            return;
        }
        var screen = new PassengerScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_SupplyRequested(object? sender, EventArgs e)
        => ShowSupply();

    private void ShowSupply()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new SupplyScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.MarketplaceRequested += (_, _) => ShowMarketplace();
        screen.WarehouseRequested += (_, _) => ShowWarehouse();
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_FuelRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("fuel")))
        {
            var result = HumanCompany.BuyFuel(_gameSession.Markets[HumanCompany.Planet],
                HumanCompany.FuelCapacity - HumanCompany.Fuel);
            ShowOpeningTurn();
            if (result.IsSuccessful) _audioPlayer.PlayVoice("FUEL.MP3");
            return;
        }
        var screen = new FuelScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_FinanceRequested(object? sender, string action)
    {
        if (_installation is null || _gameSession is null) return;
        if (action == "bank" && ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("bank")))
        {
            string? sound = null;
            if (HumanCompany.Cash > 0m)
            {
                var result = HumanCompany.DepositToBank(HumanCompany.Cash);
                if (result.IsSuccessful) sound = "BANK.MP3";
            }
            else if (HumanCompany.Bank > 0m)
            {
                var result = HumanCompany.WithdrawFromBank(HumanCompany.Bank);
                if (result.IsSuccessful) sound = "BANK2.MP3";
            }
            ShowOpeningTurn();
            if (sound is not null) _audioPlayer.PlayVoice(sound);
            return;
        }
        if (action == "loan" && ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("borrow")))
        {
            TradeResult result;
            if (HumanCompany.Loan > 0m && HumanCompany.Cash > 0m)
            {
                result = HumanCompany.RepayTradersUnion(Math.Min(HumanCompany.Cash, HumanCompany.Loan));
            }
            else
            {
                result = HumanCompany.BorrowFromTradersUnion(
                    HumanCompany.AvailableSafeUnionCredit);
            }
            ShowOpeningTurn();
            if (result.IsSuccessful) _audioPlayer.PlayVoice("LOAN.MP3");
            return;
        }
        switch (action)
        {
            case "bank":
            {
                var screen = new BankScreen();
                screen.ContinueRequested += (_, _) => ShowOpeningTurn();
                screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
                screen.Load(_installation, HumanCompany);
                ShowScreen(screen);
                break;
            }
            case "loan":
            {
                var screen = new TradersUnionScreen();
                screen.ContinueRequested += (_, _) => ShowOpeningTurn();
                screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
                screen.Load(_installation, HumanCompany);
                ShowScreen(screen);
                break;
            }
            case "zinn":
            {
                var screen = new ZinnLoanScreen();
                screen.ContinueRequested += (_, _) => ShowOpeningTurn();
                screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
                screen.Load(_installation, HumanCompany);
                ShowScreen(screen);
                break;
            }
            default:
                ShowCompanyStatus();
                break;
        }
    }

    private void ShowCompanyStatus()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new CompanyStatusScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.GraphRequested += (_, _) =>
        {
            var graph = new NetWorthScreen();
            graph.ContinueRequested += (_, _) => ShowCompanyStatus();
            graph.LoadCompanies(_installation, _gameSession);
            ShowScreen(graph);
        };
        screen.ComputerPlayersRequested += (_, _) =>
        {
            var graph = new NetWorthScreen();
            graph.ContinueRequested += (_, _) => ShowCompanyStatus();
            graph.LoadCompanies(_installation, _gameSession);
            ShowScreen(graph);
        };
        screen.ShipInfoRequested += (_, _) =>
        {
            var ship = new ShipInfoScreen();
            ship.ReturnRequested += (_, _) => ShowCompanyStatus();
            ship.Load(_installation, HumanCompany);
            ShowScreen(ship);
        };
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_WarehouseRequested(object? sender, EventArgs e)
        => ShowWarehouse();

    private void ShowWarehouse()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new WarehouseScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SupplyRequested += (_, _) => ShowSupply();
        screen.MarketplaceRequested += (_, _) => ShowMarketplace();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_StockMarketRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new StockMarketScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_AdvertisingRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("advertise")))
        {
            var result = HumanCompany.RepeatPreferredAdvertising();
            ShowOpeningTurn();
            if (result.IsSuccessful) _audioPlayer.PlayVoice("ADVERT.MP3");
            return;
        }
        var screen = new AdvertisingScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_InsuranceRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("insurance")))
        {
            var result = HumanCompany.SetInsurance(1);
            ShowOpeningTurn();
            if (result.IsSuccessful) _audioPlayer.PlayVoice("INSURE.MP3");
            return;
        }
        var screen = new InsuranceScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_ExploreRequested(object? sender, EventArgs e)
    {
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("explore"))) ShowPlanetSpecial();
        else ShowPlanetExplore();
    }

    private void ShowPlanetExplore()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new PlanetExploreScreen();
        screen.SpecialRequested += (_, _) => ShowPlanetSpecial();
        screen.NewsRequested += (_, _) => ShowExploreNews();
        screen.WeatherRequested += (_, _) => ShowExploreWeather();
        screen.AboutRequested += (_, _) => ShowExploreAbout();
        screen.TimeRequested += (_, _) => ShowExploreTime();
        screen.ReturnRequested += (_, _) => ShowOpeningTurn();
        screen.Load(_installation, HumanCompany.Planet);
        ShowScreen(screen);
    }

    private void ShowPlanetSpecial()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new PlanetServicesScreen();
        screen.ContinueRequested += (_, _) => ShowPlanetExplore();
        screen.MainMenuRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }

    private void ShowExploreNews()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new ExploreTextScreen();
        screen.ReturnRequested += (_, _) => ShowPlanetExplore();
        screen.MainMenuRequested += (_, _) => ShowOpeningTurn();
        screen.Load(_installation, "News Center",
            ExploreContentCatalog.NewsReport(_gameSession), "NEWS_L.SWF",
            "The News Center reports local trivia and events from around Kukubia.");
        ShowScreen(screen);
    }

    private void ShowExploreWeather()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new ExploreTextScreen();
        screen.ReturnRequested += (_, _) => ShowPlanetExplore();
        screen.MainMenuRequested += (_, _) => ShowOpeningTurn();
        screen.Load(_installation, "Weather Bureau", _gameSession.WeatherForecast(), "WEATHER_L.SWF",
            "The Weather Bureau warns merchants about dangerous space weather near the planets of Kukubia.");
        ShowScreen(screen);
    }

    private void ShowExploreAbout()
    {
        if (_installation is null || _gameSession is null) return;
        var planet = HumanCompany.Planet;
        var screen = new ExploreTextScreen();
        screen.ReturnRequested += (_, _) => ShowPlanetExplore();
        screen.Load(_installation, $"History of {planet}", ExploreContentCatalog.HistoryPages(planet),
            "HISTORY.SWF", showMainMenuButton: false,
            "The History of Kukubia contains background information about every planet in the system.");
        ShowScreen(screen);
    }

    private void ShowExploreTime()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new ExploreTextScreen();
        screen.ReturnRequested += (_, _) => ShowPlanetExplore();
        var pages = ExploreContentCatalog.TimePages.ToArray();
        pages[3] = pages[3].Replace("The date is 139 A.B.",
            $"The date is {_gameSession.KukubianDate}", StringComparison.Ordinal);
        pages[3] += $"\n\nYour trading company has been in business {_gameSession.Week} kuku weeks.";
        screen.Load(_installation, "Ministry of Time", pages, "CLOCK_L.SWF", showMainMenuButton: false,
            "The Ministry of Time provides background information on how time functions in the Kukubian system.");
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_FileOptionsRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new FileOptionsScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.NewGameRequested += (_, _) => StartNewGameButton_Click(null, new RoutedEventArgs());
        screen.LoadRequested += (_, _) => ShowSaveSlots(saveMode: false, returnToMainMenu: false);
        screen.AboutGazillionaireRequested += (_, _) => ShowFileOptionsAboutGazillionaire();
        screen.AboutLavaMindRequested += (_, _) => ShowFileOptionsAboutLavaMind();
        screen.FullScreenRequested += (_, _) =>
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        };
        screen.QuitRequested += (_, _) => Close();
        screen.ShortcutsRequested += (_, _) => ShowQuickOptions();
        screen.OptionsRequested += (_, _) => ShowGameOptions();
        screen.DebugEventsRequested += (_, _) => ShowDebugTravelEvent(1);
        screen.ToggleSoundRequested += (_, _) =>
        {
            _soundEnabled = !_soundEnabled;
            _audioPlayer.SetEnabled(_soundEnabled);
            SoundButton.Content = _soundEnabled ? "Sound On" : "Sound Off";
            screen.SetSoundState(_soundEnabled);
        };
        screen.SaveRequested += (_, _) => ShowSaveSlots(saveMode: true, returnToMainMenu: false);
        screen.Load(_installation, _soundEnabled, _gameSession);
        ShowScreen(screen);
    }

    private void ShowGameOptions()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new GameOptionsScreen();
        screen.ModeChanged += (_, _) =>
        {
            _gameSession.AiEventVisibility = screen.SelectedMode;
            GameSaveService.SaveAutosave(_gameSession);
        };
        screen.ContinueRequested += (_, _) =>
        {
            _gameSession.AiEventVisibility = screen.SelectedMode;
            GameSaveService.SaveAutosave(_gameSession);
            OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        };
        screen.Load(_installation, _gameSession.AiEventVisibility);
        ShowScreen(screen);
    }

    private void ShowDebugTravelEvent(int eventIndex)
    {
        if (_installation is null || _gameSession is null) return;
        eventIndex = Math.Clamp(eventIndex, 1, GameSession.DebugTravelEventCount);

        var debugSession = new GameSession(6, _gameSession.Planets, _gameSession.Seed + eventIndex * 7919);
        debugSession.InitializeStocks();
        var planet = debugSession.Planets.FirstOrDefault() ?? "Bass";
        var company = new CompanyState("Event Debug Company", true, 11, planet, 5_000_000m, 100_000m)
        {
            Bank = 250_000m,
            Loan = 75_000m,
            CrewWagesOwed = 25_000m,
            TaxesOwed = 15_000m,
            TariffsOwed = 10_000m,
            CrewSalary = 2_000m,
            BaseEngineSpeed = 6,
            Passengers = 1
        };
        company.Cargo[0] = new CargoLot { Quantity = 10, AverageCost = 500m };
        foreach (var exchange in debugSession.Planets) company.Shares[exchange] = 10;
        debugSession.Companies.Add(company);
        debugSession.Companies.Add(new CompanyState("Debug Rival", false, 2, planet, 1_000_000m, 0m));

        var result = debugSession.ResolveDebugTravelEvent(company, eventIndex);
        var screen = new TravelEventScreen();
        screen.EnableDebugNavigation(eventIndex, GameSession.DebugTravelEventCount);
        screen.OutcomeRevealed += (_, _) =>
            _audioPlayer.PlayVoice(string.IsNullOrWhiteSpace(screen.AudioAsset)
                ? screen.IsGoodEvent ? "GOOD.MP3" : "BAD.MP3"
                : screen.AudioAsset);
        screen.ContinueRequested += (_, _) =>
        {
            if (eventIndex < GameSession.DebugTravelEventCount) ShowDebugTravelEvent(eventIndex + 1);
            else OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        };
        screen.DebugPreviousRequested += (_, _) => ShowDebugTravelEvent(Math.Max(1, eventIndex - 1));
        screen.DebugExitRequested += (_, _) => OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        screen.Load(_installation, result);
        ShowScreen(screen);
    }

    private void ShowFileOptionsAboutGazillionaire()
    {
        if (_installation is null) return;
        var screen = new AboutGazillionaireScreen();
        screen.CloseRequested += (_, _) => OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void ShowFileOptionsAboutLavaMind()
    {
        if (_installation is null) return;
        var screen = new AboutLavaMindScreen();
        screen.CloseRequested += (_, _) => OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void ShowQuickOptions()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new QuickOptionsScreen();
        screen.OptionsChanged += (_, _) => GameSaveService.SaveAutosave(_gameSession);
        screen.ContinueRequested += (_, _) =>
        {
            GameSaveService.SaveAutosave(_gameSession);
            OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        };
        screen.Load(_installation, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_CrewRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("crew")))
        {
            var result = HumanCompany.PayCrew();
            ShowOpeningTurn();
            if (result.IsSuccessful) _audioPlayer.PlayVoice("CREW.MP3");
            return;
        }
        var screen = new CrewScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, HumanCompany);
        ShowScreen(screen);
    }

    private void OpeningTurnScreen_TaxesRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        if (ShortcutInputState.ShouldUse(HumanCompany.Shortcuts.GetValueOrDefault("tax")))
        {
            var result = HumanCompany.PayTaxes();
            ShowOpeningTurn();
            if (result.IsSuccessful) _audioPlayer.PlayVoice("TAX2.MP3");
            return;
        }
        var screen = new TaxScreen();
        screen.ContinueRequested += (_, _) => ShowOpeningTurn();
        screen.SoundRequested += (_, fileName) => _audioPlayer.PlayVoice(fileName);
        screen.Load(_installation, HumanCompany);
        ShowScreen(screen);
        _audioPlayer.PlayVoice("TAX.MP3");
    }

    private void OpeningTurnScreen_JourneyRequested(object? sender, EventArgs e)
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new JourneyScreen();
        screen.ReturnRequested += (_, _) => ShowOpeningTurn();
        screen.FacilitiesRequested += (_, _) => ShowFacilities();
        screen.DistanceRequested += (_, _) => ShowDistanceChart();
        var turnCompany = HumanCompany;
        screen.BankruptcyRequested += (_, _) =>
        {
            if (_gameSession is null) return;
            _gameSession.AdvanceScheduledTurnsAfterHuman(turnCompany);
            GameSaveService.SaveAutosave(_gameSession);
            ShowBankruptcies([turnCompany], 0);
        };
        screen.DestinationSelected += (_, destination) =>
            JourneyScreen_DestinationSelected(turnCompany, destination);
        screen.Load(_installation, _gameSession, turnCompany);
        ShowScreen(screen);
    }

    private void ShowFacilities()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new DistanceChartScreen();
        screen.ReturnRequested += (_, _) => OpeningTurnScreen_JourneyRequested(this, EventArgs.Empty);
        screen.Load(_installation, _gameSession, HumanCompany, showFacilities: true);
        ShowScreen(screen);
    }

    private void ShowDistanceChart()
    {
        if (_installation is null || _gameSession is null) return;
        var screen = new DistanceChartScreen();
        screen.ReturnRequested += (_, _) => OpeningTurnScreen_JourneyRequested(this, EventArgs.Empty);
        screen.Load(_installation, _gameSession, HumanCompany);
        ShowScreen(screen);
    }


    private void JourneyScreen_DestinationSelected(CompanyState currentHuman, string destination)
    {
        if (_gameSession is null ||
            !ReferenceEquals(_gameSession.CurrentTurnCompany, currentHuman))
            return;
        var previouslyBankrupt = _gameSession.Companies.Where(company => company.IsBankrupt)
            .Select(company => company.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _startingPlanet = destination;
        _gameSession.ApplyFacilityFees(currentHuman);
        var sequence = _gameSession.BeginJourneyEvents(currentHuman);

        void CompleteTurn()
        {
            _gameSession.RecordTravelTime(currentHuman);
            var advancedWeek = _gameSession.AdvanceScheduledTurnsAfterHuman(currentHuman);
            var newBankruptcies = _gameSession.Companies
                .Where(company => company.IsBankrupt && !previouslyBankrupt.Contains(company.Name))
                .ToArray();
            _showNewsAfterArrival = advancedWeek;
            GameSaveService.SaveAutosave(_gameSession);
            ShowBankruptcies(newBankruptcies, 0);
        }

        void ShowNextEvent(TravelEventResult? completed = null)
        {
            if (completed is not null) sequence.Complete(completed);
            var result = sequence.Next();
            if (result is null)
            {
                CompleteTurn();
                return;
            }

            if (result.Heading.Equals("A Quiet Journey", StringComparison.OrdinalIgnoreCase))
            {
                sequence.Complete(result);
                ShowNextEvent();
                return;
            }

            if (_installation is not null)
            {
                var screen = new TravelEventScreen();
                screen.OutcomeRevealed += (_, _) =>
                    _audioPlayer.PlayVoice(string.IsNullOrWhiteSpace(screen.AudioAsset)
                        ? screen.IsGoodEvent ? "GOOD.MP3" : "BAD.MP3"
                        : screen.AudioAsset);
                screen.ContinueRequested += (_, _) => ShowNextEvent(screen.ResolvedResult ?? result);
                screen.Load(_installation, result);
                ShowScreen(screen);
                return;
            }
            ShowNextEvent(result.Choice is null ? result : result.Choice.Resolve(false));
        }

        if (!TryShowPendingAuction(currentHuman, () => ShowNextEvent()))
            ShowNextEvent();
    }

    private void ShowBankruptcies(IReadOnlyList<CompanyState> companies, int index)
    {
        if (_installation is null || index >= companies.Count)
        {
            ShowPlayerTurn();
            return;
        }
        var remainingHumans = _gameSession?.Companies.Any(company => company.IsHuman && !company.IsBankrupt) == true;
        var screen = new BankruptcyScreen();
        screen.ContinueRequested += (_, _) => ShowBankruptcies(companies, index + 1);
        screen.Load(_installation, companies[index], !remainingHumans);
        ShowScreen(screen);
    }

    private void LoadSavedGameButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowSaveSlots(saveMode: false, returnToMainMenu: true);
    }

    private void ShowSaveSlots(bool saveMode, bool returnToMainMenu)
    {
        if (_installation is null) return;
        var screen = new SaveSlotsScreen();
        screen.BackRequested += (_, _) =>
        {
            if (returnToMainMenu) ShowMainMenu();
            else OpeningTurnScreen_FileOptionsRequested(this, EventArgs.Empty);
        };
        screen.SlotSelected += (_, slot) =>
        {
            if (saveMode)
            {
                if (_gameSession is null) return;
                GameSaveService.SaveSlot(_gameSession, slot);
                screen.Load(true);
                screen.ShowStatus($"Game saved in Slot {slot}.");
                return;
            }
            LoadSession(GameSaveService.LoadSlot(slot), screen);
        };
        screen.AutosaveSelected += (_, _) => LoadSession(GameSaveService.LoadAutosave(), screen);
        screen.Load(saveMode);
        ShowScreen(screen);
    }

    private void LoadSession(GameSession? session, SaveSlotsScreen screen)
    {
        if (session is null || session.Companies.All(company => !company.IsHuman))
        {
            screen.ShowStatus("That save slot is empty or invalid.");
            return;
        }

        _gameSession = session;
        _selectedLevel = session.Level;
        _selectedPlanets.Clear();
        _selectedPlanets.AddRange(session.Planets);
        _humanCompanies.Clear();
        _humanCompanies.AddRange(session.Companies.Where(company => company.IsHuman).Select(company => company.Name));
        _humanShipNumbers.Clear();
        _humanShipNumbers.AddRange(session.Companies.Where(company => company.IsHuman).Select(company => company.ShipNumber));
        _selectedAiOpponents.Clear();
        foreach (var company in session.Companies.Where(company => !company.IsHuman))
        {
            var opponent = AiOpponentCatalog.All.FirstOrDefault(profile =>
                profile.Name.Equals(company.Name, StringComparison.OrdinalIgnoreCase));
            if (opponent is not null) _selectedAiOpponents.Add(opponent);
        }
        _humanPlayerCount = _humanCompanies.Count;
        _aiPlayerCount = session.Companies.Count(company => !company.IsHuman);
        _startingPlanet = HumanCompany.Planet;
        _firstHumanShipNumber = HumanCompany.ShipNumber;
        GameplayLogger.StartSession(_gameSession, $"loaded save at week {_gameSession.Week}");
        ShowPlayerTurn();
    }

    private void AboutGazillionaireButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_installation is null)
        {
            return;
        }

        var screen = new AboutGazillionaireScreen();
        screen.CloseRequested += AboutGazillionaireScreen_CloseRequested;
        screen.LoadAssets(_installation);

        ShowScreen(screen);
    }

    private void AboutGazillionaireScreen_CloseRequested(object? sender, EventArgs e)
    {
        ShowMainMenu();
    }

    private void AboutLavaMindButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_installation is null) return;

        var screen = new AboutLavaMindScreen();
        screen.CloseRequested += AboutLavaMindScreen_CloseRequested;
        screen.LoadAssets(_installation);
        ShowScreen(screen);
    }

    private void AboutLavaMindScreen_CloseRequested(object? sender, EventArgs e) =>
        ShowMainMenu();

    private void AboutOpenTradeEngineButton_Click(object? sender, RoutedEventArgs e) =>
        ShowMenuText(
            "About OpenTradeEngine",
            "OpenTradeEngine is an open-source, modern reimplementation of the Gazillionaire game engine. It loads the original presentation assets from a legally installed copy of Gazillionaire.");

    private void FullScreenButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;

        FullScreenButton.Content = WindowState == WindowState.FullScreen
            ? "Exit Full Screen"
            : "Enter Full Screen";
    }

    private void SoundButton_Click(object? sender, RoutedEventArgs e)
    {
        _soundEnabled = !_soundEnabled;
        _audioPlayer.SetEnabled(_soundEnabled);
        SoundButton.Content = _soundEnabled ? "Sound On" : "Sound Off";
    }

    private void QuitGameButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void ShowScreen(Control screen)
    {
        MainMenuPanel.IsVisible = false;
        ScreenHost.Content = screen;
        ScreenHost.IsVisible = true;

        switch (screen)
        {
            case PlayerTurnScreen when _gameSession?.CurrentTurnCompany is { IsHuman: true } company:
                PlayShipMusic(company.ShipNumber);
                break;
            case ZinnFinancingScreen:
                _audioPlayer.PlayVoice("ZINN.MP3");
                break;
            case PlanetArrivalScreen:
                _audioPlayer.PlayVoice($"{_startingPlanet.ToUpperInvariant()}.MP3");
                break;
            case AboutLavaMindScreen:
                _audioPlayer.PlayVoice("LAVAMIND.MP3");
                break;
            case OpeningTurnScreen:
                _audioPlayer.StopVoice();
                break;
            case PlanetExploreScreen when _gameSession?.CurrentTurnCompany is { IsHuman: true } explorer:
                _audioPlayer.PlayVoice($"{explorer.Planet.ToUpperInvariant()}.MP3");
                break;
            case StockMarketScreen:
                _audioPlayer.PlayVoice("STOCK.MP3");
                break;
            case BankScreen:
                _audioPlayer.PlayVoice("BANK3.MP3");
                break;
            case TradersUnionScreen:
                _audioPlayer.PlayVoice("LOAN.MP3");
                break;
            case ZinnLoanScreen:
                _audioPlayer.PlayVoice("ZINN.MP3");
                break;
            case NewsScreen:
                _audioPlayer.PlayVoice("NEWS.MP3");
                break;
            case ExploreTextScreen text when text.Topic.Contains("Weather", StringComparison.OrdinalIgnoreCase):
                _audioPlayer.PlayVoice("WEATHER.MP3");
                break;
            case ExploreTextScreen text when text.Topic.Contains("About", StringComparison.OrdinalIgnoreCase):
                _audioPlayer.PlayVoice("HISTORY.MP3");
                break;
            case ExploreTextScreen text when text.Topic.Contains("Time", StringComparison.OrdinalIgnoreCase):
                _audioPlayer.PlayVoice("CLOCK.MP3");
                break;
            case TravelEventScreen travelEvent:
                _audioPlayer.PlayVoice(string.IsNullOrWhiteSpace(travelEvent.AudioAsset)
                    ? travelEvent.IsGoodEvent ? "GOOD.MP3" : "BAD.MP3"
                    : travelEvent.AudioAsset);
                break;
            case CompanyAnnouncementScreen announcement:
                _audioPlayer.PlayVoice(string.IsNullOrWhiteSpace(announcement.AudioAsset)
                    ? announcement.IsGoodAnnouncement ? "GOOD.MP3" : "BAD.MP3"
                    : announcement.AudioAsset);
                break;
            case BankruptcyScreen:
                _audioPlayer.PlayVoice("BANKRUPT.MP3");
                break;
            case PlanetServicesScreen services:
                _audioPlayer.PlayVoice($"{services.PlanetName.ToUpperInvariant()}.MP3");
                break;
            case CampaignOutcomeScreen:
                _audioPlayer.PlayVoice("THANKYOU.MP3");
                break;
            case AuctionScreen:
                _audioPlayer.PlayVoice("AUCTION.MP3");
                break;
        }
    }

    private void PlayShipMusic(int shipNumber)
    {
        var shipMusicNumber = shipNumber <= 6 ? shipNumber : shipNumber - 6;
        _audioPlayer.PlayVoice($"SHIP{shipMusicNumber}.MP3");
    }

    private void ShowMainMenu()
    {
        ScreenHost.IsVisible = false;
        ScreenHost.Content = null;
        MainMenuPanel.IsVisible = true;
    }

    private void ShowMenuText(string heading, string body)
    {
        MenuHeadingTextBlock.Text = heading;
        MenuBodyTextBlock.Text = body;
    }

    private void ShowError(string message)
    {
        StatusTextBlock.Foreground = Brushes.Firebrick;
        StatusTextBlock.Text = message;
    }

    private void MainWindow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var rightClick = point.Properties.IsRightButtonPressed;
        ShortcutInputState.BypassRequested = rightClick || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (rightClick && FindButton(e.Source) is { } button)
        {
            e.Handled = true;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.Post(
                () => ShortcutInputState.BypassRequested = false,
                DispatcherPriority.Background);
        }

        if (MainMenuPanel.IsVisible &&
            (e.Source is Button || e.Source is Control control && control.GetLogicalAncestors().OfType<Button>().Any()))
            _audioPlayer.PlayMenuClick();
    }

    private void MainWindow_PointerReleased(object? sender, PointerReleasedEventArgs e) =>
        Dispatcher.UIThread.Post(
            () => ShortcutInputState.BypassRequested = false,
            DispatcherPriority.Background);

    private static Button? FindButton(object? source) => source switch
    {
        Button button => button,
        Control control => control.GetLogicalAncestors().OfType<Button>().FirstOrDefault(),
        _ => null
    };

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10 && _installation is not null && _gameSession is not null)
        {
            e.Handled = true;
            ShowDebugTravelEvent(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 48 : 1);
            return;
        }
        if (MainMenuPanel.IsVisible &&
            (e.Key == Key.Enter || e.Key == Key.Space) &&
            e.Source is Button)
            _audioPlayer.PlayMenuClick();
    }

    protected override void OnClosed(EventArgs e)
    {
        GameplayLogger.Shutdown();
        _audioPlayer.Dispose();
        base.OnClosed(e);
    }
}
