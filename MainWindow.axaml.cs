using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

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

    public MainWindow()
    {
        InitializeComponent();
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

        _installation = result.Installation;
        ContinueButton.IsEnabled = true;
        StatusTextBlock.Foreground = Brushes.ForestGreen;
        StatusTextBlock.Text = "Gazillionaire installation found. All required game files are available.";
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

        var extraction = SwfImageExtractor.TryExtractFirstFrame(sourcePath, "ZILE2");
        if (!extraction.IsSuccessful)
        {
            ShowError(extraction.ErrorMessage);
            return;
        }

        MainMenuBackgroundImage.Source = new Bitmap(extraction.ImagePath!);
        InstallationPanel.IsVisible = false;
        MainMenuPanel.IsVisible = true;
        ShowMenuText(WelcomeHeading, WelcomeText);
    }

    private void StartNewGameButton_Click(object? sender, RoutedEventArgs e) =>
        ShowMenuText("Start New Game", "New-game setup will be connected here next.");

    private void LoadSavedGameButton_Click(object? sender, RoutedEventArgs e) =>
        ShowMenuText("Load Saved Game", "Save-game loading has not been implemented yet.");

    private void AboutGazillionaireButton_Click(object? sender, RoutedEventArgs e) =>
        ShowMenuText(
            "About Gazillionaire",
            "Gazillionaire is the fantasy business simulation game created by LavaMind. OpenTradeEngine uses the artwork, sounds, and game data from your installed copy.");

    private void AboutLavaMindButton_Click(object? sender, RoutedEventArgs e) =>
        ShowMenuText(
            "About LavaMind",
            "Gazillionaire and its original assets were created by LavaMind. OpenTradeEngine does not distribute those assets and requires an installed copy of the game.");

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
        SoundButton.Content = _soundEnabled ? "Sound On" : "Sound Off";
    }

    private void QuitGameButton_Click(object? sender, RoutedEventArgs e) => Close();

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
}
