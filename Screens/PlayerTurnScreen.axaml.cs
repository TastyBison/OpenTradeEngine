using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class PlayerTurnScreen : UserControl
{
    public event EventHandler? BeginTurnRequested;
    public event EventHandler? ShipInfoRequested;

    public PlayerTurnScreen() => InitializeComponent();

    public void Load(
        GameInstallation installation,
        string companyName,
        int shipNumber,
        IReadOnlyList<string> companies,
        decimal zinnLoan,
        string difficultyName)
    {
        CompanyHeading.Text = companyName;
        RankingText.Text = string.Join("\n", companies.Select(
            (company, index) => $"{index + 1})  {company}   -{zinnLoan:N0}"));
        DifficultyText.Text = $"Game Difficulty Level:  {difficultyName}";
        TutorialText.Text = string.Empty;
        TargetText.Text = "The companies are ranked according to their wealth. The first company to reach 5,000,000 kubars wins the game!";

        var result = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{shipNumber}.SWF"), $"SHIP{shipNumber}");
        if (result.IsSuccessful) ShipImage.Source = GameBitmapCache.Load(result.ImagePath!);
    }

    public void Load(GameInstallation installation, GameSession session, CompanyState company, string difficultyName)
    {
        CompanyHeading.Text = company.Name;
        WeekText.Text = $"Week {session.Week}";
        StatusText.Text = $"Status:  {GetStatus(session.NetWorthOf(company))}";
        var bankrupt = session.Companies.Where(candidate => candidate.IsBankrupt).Select(candidate => candidate.Name).ToArray();
        BankruptText.Text = bankrupt.Length == 0
            ? "Bankrupt Companies:  none"
            : $"Bankrupt Companies:  {string.Join(", ", bankrupt)}";
        RankingText.Text = string.Join("\n", session.Companies
            .Where(candidate => !candidate.IsBankrupt)
            .OrderByDescending(session.NetWorthOf)
            .Select((candidate, index) => $"{index + 1})  {candidate.Name}   {session.NetWorthOf(candidate):N0}"));
        DifficultyText.Text = $"Game Difficulty Level:  {difficultyName}";
        TargetText.Text = $"The companies are ranked according to their wealth. The first company to reach {session.WinTarget:N0} kubars wins the game!";
        TutorialText.Text = session.IsTutorial && !session.TutorialCompleted
            ? $"Tutorial progress: {session.TutorialStage}/17"
            : string.Empty;
        var result = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, $"SHIP{company.ShipNumber}.SWF"), $"SHIP{company.ShipNumber}");
        if (result.IsSuccessful) ShipImage.Source = GameBitmapCache.Load(result.ImagePath!);
    }

    private static string GetStatus(decimal netWorth) => netWorth switch
    {
        < 0m => "Inefficient Business Person",
        < 100_000m => "Fledgling Merchant",
        < 500_000m => "Prosperous Trader",
        < 1_000_000m => "Interstellar Tycoon",
        < GameSession.StandardWinTarget => "Near Gazillionaire",
        _ => "Gazillionaire"
    };

    private void BeginTurnButton_Click(object? sender, RoutedEventArgs e) =>
        BeginTurnRequested?.Invoke(this, EventArgs.Empty);

    private void ShipPanel_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ShipInfoRequested?.Invoke(this, EventArgs.Empty);
}
