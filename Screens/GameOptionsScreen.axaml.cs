using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OpenTradeEngine.Screens;

public partial class GameOptionsScreen : UserControl
{
    public event EventHandler? ModeChanged;
    public event EventHandler? ContinueRequested;

    public AiEventVisibility SelectedMode { get; private set; } = AiEventVisibility.Default;

    public GameOptionsScreen() => InitializeComponent();

    public void Load(GameInstallation installation, AiEventVisibility mode)
    {
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        SetMode(mode, notify: false);
        RefreshModStatus();
    }

    private void SetMode(AiEventVisibility mode, bool notify = true)
    {
        SelectedMode = mode;
        FullButton.Content = mode == AiEventVisibility.Full ? "Full  ✓" : "Full";
        DefaultButton.Content = mode == AiEventVisibility.Default ? "Default  ✓" : "Default";
        NoneButton.Content = mode == AiEventVisibility.None ? "None  ✓" : "None";
        FullButton.BorderBrush = mode == AiEventVisibility.Full ? Brushes.LightGreen : Brush.Parse("#36B8F6");
        DefaultButton.BorderBrush = mode == AiEventVisibility.Default ? Brushes.LightGreen : Brush.Parse("#36B8F6");
        NoneButton.BorderBrush = mode == AiEventVisibility.None ? Brushes.LightGreen : Brush.Parse("#36B8F6");
        if (notify) ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FullButton_Click(object? sender, RoutedEventArgs e) => SetMode(AiEventVisibility.Full);
    private void DefaultButton_Click(object? sender, RoutedEventArgs e) => SetMode(AiEventVisibility.Default);
    private void NoneButton_Click(object? sender, RoutedEventArgs e) => SetMode(AiEventVisibility.None);
    private void ReloadModsButton_Click(object? sender, RoutedEventArgs e)
    {
        ModCatalog.Reload();
        RefreshModStatus();
    }

    private void OpenModsButton_Click(object? sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(ModCatalog.ModsDirectory);
        Process.Start(new ProcessStartInfo(ModCatalog.ModsDirectory) { UseShellExecute = true });
    }

    private void RefreshModStatus()
    {
        if (!ModCatalog.Enabled)
        {
            ModStatusText.Text = "Mods are disabled in the launcher. Return to the launcher and enable them before loading mod content.";
            return;
        }
        var summary = $"Loaded {ModCatalog.Mods.Count} mod(s), {ModCatalog.Planets.Count} planet definition(s), " +
                      $"and {ModCatalog.Events.Count} event definition(s).";
        if (ModCatalog.Errors.Count > 0)
            summary += "\nSkipped content: " + string.Join(" | ", ModCatalog.Errors.Take(3));
        ModStatusText.Text = summary;
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
