using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class FileOptionsScreen : UserControl
{
    public event EventHandler? NewGameRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? LoadRequested;
    public event EventHandler? ContinueRequested;
    public event EventHandler? ToggleSoundRequested;
    public event EventHandler? ShortcutsRequested;
    public event EventHandler? AboutGazillionaireRequested;
    public event EventHandler? AboutLavaMindRequested;
    public event EventHandler? FullScreenRequested;
    public event EventHandler? DebugEventsRequested;
    public event EventHandler? OptionsRequested;
    public event EventHandler? QuitRequested;

    public FileOptionsScreen() => InitializeComponent();

    public void Load(GameInstallation installation, bool soundEnabled, GameSession? session = null)
    {
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        SetSoundState(soundEnabled);
        ShortcutsButton.IsEnabled = session is null || !session.IsTutorial || session.TutorialStage >= 17;
    }

    public void ShowSaved(string path)
    {
        StatusText.Text = $"Game saved to {path}";
        StatusPanel.IsVisible = true;
    }

    public void SetSoundState(bool enabled) => SoundButton.Content = enabled ? "Sound On" : "Sound Off";
    private void NewGameButton_Click(object? sender, RoutedEventArgs e) => NewGameRequested?.Invoke(this, EventArgs.Empty);
    private void SaveButton_Click(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void LoadButton_Click(object? sender, RoutedEventArgs e) => LoadRequested?.Invoke(this, EventArgs.Empty);
    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void SoundButton_Click(object? sender, RoutedEventArgs e) => ToggleSoundRequested?.Invoke(this, EventArgs.Empty);
    private void ShortcutsButton_Click(object? sender, RoutedEventArgs e) => ShortcutsRequested?.Invoke(this, EventArgs.Empty);
    private void AboutGazillionaireButton_Click(object? sender, RoutedEventArgs e) => AboutGazillionaireRequested?.Invoke(this, EventArgs.Empty);
    private void AboutLavaMindButton_Click(object? sender, RoutedEventArgs e) => AboutLavaMindRequested?.Invoke(this, EventArgs.Empty);
    private void FullScreenButton_Click(object? sender, RoutedEventArgs e) => FullScreenRequested?.Invoke(this, EventArgs.Empty);
    private void DebugEventsButton_Click(object? sender, RoutedEventArgs e) => DebugEventsRequested?.Invoke(this, EventArgs.Empty);
    private void OptionsButton_Click(object? sender, RoutedEventArgs e) => OptionsRequested?.Invoke(this, EventArgs.Empty);
    private void QuitButton_Click(object? sender, RoutedEventArgs e) => QuitRequested?.Invoke(this, EventArgs.Empty);
}
