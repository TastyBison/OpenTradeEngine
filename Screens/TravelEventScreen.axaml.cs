using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class TravelEventScreen : UserControl
{
    private GameInstallation? _installation;
    private TravelEventResult? _result;
    public bool IsGoodEvent { get; private set; }
    public string AudioAsset { get; private set; } = string.Empty;
    public TravelEventResult? ResolvedResult => _result;
    public event EventHandler? OutcomeRevealed;
    public event EventHandler? ContinueRequested;
    public event EventHandler? DebugPreviousRequested;
    public event EventHandler? DebugExitRequested;
    public TravelEventScreen() => InitializeComponent();

    public void Load(GameInstallation installation, TravelEventResult result)
    {
        _installation = installation;
        _result = result;
        Display(result);
        ChoicePanel.IsVisible = result.Choice is not null;
        ContinueButton.IsVisible = result.Choice is null;
        if (result.Choice is not null)
        {
            AcceptButton.Content = result.Choice.AcceptLabel;
            DeclineButton.Content = result.Choice.DeclineLabel;
        }
    }

    public void EnableDebugNavigation(int eventIndex, int eventCount)
    {
        DebugToolbar.IsVisible = true;
        DebugEventNumber.Text = $"Event {eventIndex} of {eventCount}";
    }

    private void Display(TravelEventResult result)
    {
        IsGoodEvent = result.IsGood;
        AudioAsset = result.AudioAsset;
        Heading.Text = result.Heading;
        Message.Text = result.Message;
        EventImageBackground.Background = result.ImageAsset.StartsWith("SHIP", StringComparison.OrdinalIgnoreCase)
            ? Brushes.Black
            : Brushes.White;
        EventImage.Source = null;
        if (_installation is null) return;
        if (!string.IsNullOrWhiteSpace(result.ImageAsset))
        {
            if (Path.GetExtension(result.ImageAsset).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                var pngPath = Path.Combine(_installation.PngDirectory, result.ImageAsset);
                if (File.Exists(pngPath)) EventImage.Source = GameBitmapCache.Load(pngPath);
                return;
            }
            var swfPath = Path.Combine(_installation.SwfDirectory, result.ImageAsset);
            var cacheName = Path.GetFileNameWithoutExtension(result.ImageAsset);
            // MONEY_N is pure vector artwork, so it has no embedded bitmap for the
            // normal extractor to find. Export its largest static shape (the full
            // stack of cash) without taking an animation frame.
            var artwork = result.ImageAsset.Equals("MONEY_N.SWF", StringComparison.OrdinalIgnoreCase)
                ? SwfImageExtractor.TryExtractLargestVectorShape(swfPath, cacheName)
                : SwfImageExtractor.TryExtractFirstEmbeddedImage(swfPath, cacheName);
            if (artwork.IsSuccessful)
                EventImage.Source = new Bitmap(artwork.ImagePath!);
        }
    }

    private void AcceptButton_Click(object? sender, RoutedEventArgs e) => ResolveChoice(true);
    private void DeclineButton_Click(object? sender, RoutedEventArgs e) => ResolveChoice(false);

    private void ResolveChoice(bool accepted)
    {
        if (_result?.Choice is null) return;
        _result = _result.Choice.Resolve(accepted);
        if (!accepted || _result.SkipOutcomeScreen)
        {
            ContinueRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        Display(_result);
        ChoicePanel.IsVisible = false;
        ContinueButton.IsVisible = true;
        OutcomeRevealed?.Invoke(this, EventArgs.Empty);
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void DebugPreviousButton_Click(object? sender, RoutedEventArgs e) => DebugPreviousRequested?.Invoke(this, EventArgs.Empty);
    private void DebugExitButton_Click(object? sender, RoutedEventArgs e) => DebugExitRequested?.Invoke(this, EventArgs.Empty);
}
