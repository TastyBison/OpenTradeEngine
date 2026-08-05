using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class ExploreTextScreen : UserControl
{
    private string[] _pages = [];
    private int _pageIndex;

    public string Topic { get; private set; } = string.Empty;
    public event EventHandler? ReturnRequested;
    public event EventHandler? MainMenuRequested;

    public ExploreTextScreen() => InitializeComponent();

    public void Load(GameInstallation installation, string heading, string body, string? swfName = null,
        string? helpText = null) =>
        Load(installation, heading, [body], swfName, showMainMenuButton: true, helpText);

    public void Load(GameInstallation installation, string heading, IEnumerable<string> pages, string? swfName,
        bool showMainMenuButton, string? helpText = null)
    {
        Topic = heading;
        Heading.Text = heading;
        _pages = pages.Where(page => !string.IsNullOrWhiteSpace(page)).ToArray();
        if (_pages.Length == 0) _pages = ["No information is available."];
        _pageIndex = 0;
        MainMenuButton.IsVisible = showMainMenuButton;
        PreviousButton.IsVisible = !showMainMenuButton;
        NextButton.IsVisible = !showMainMenuButton;
        HelpText.Text = helpText ?? $"The {heading} provides background information about Kukubia.";

        if (!string.IsNullOrWhiteSpace(swfName))
        {
            var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(
                Path.Combine(installation.SwfDirectory, swfName),
                $"EXPLORE_STATIC_{Path.GetFileNameWithoutExtension(swfName)}");
            if (art.IsSuccessful) Illustration.Source = new Bitmap(art.ImagePath!);
            if (swfName.Equals("CLOCK_L.SWF", StringComparison.OrdinalIgnoreCase))
            {
                Illustration.RenderTransform = new ScaleTransform(1.65, 1.65);
                Illustration.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }
        }
        RefreshPage();
    }

    private void RefreshPage()
    {
        Body.Text = _pages[_pageIndex];
        PageText.Text = _pages.Length > 1 ? $"(Page {_pageIndex + 1} of {_pages.Length})" : string.Empty;
        PreviousButton.IsEnabled = _pageIndex > 0;
        NextButton.IsEnabled = _pageIndex + 1 < _pages.Length;
    }

    private void PreviousButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pageIndex <= 0) return;
        _pageIndex--;
        RefreshPage();
    }

    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pageIndex + 1 >= _pages.Length) return;
        _pageIndex++;
        RefreshPage();
    }

    private void HelpButton_Click(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = true;
    private void CloseHelpButton_Click(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = false;
    private void MainMenuButton_Click(object? sender, RoutedEventArgs e) => MainMenuRequested?.Invoke(this, EventArgs.Empty);
    private void ReturnButton_Click(object? sender, RoutedEventArgs e) => ReturnRequested?.Invoke(this, EventArgs.Empty);
}
