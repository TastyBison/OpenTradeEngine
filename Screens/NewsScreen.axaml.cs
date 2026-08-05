using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class NewsScreen : UserControl
{
    public event EventHandler? ContinueRequested;
    public NewsScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session)
    {
        NewsText.Text = session.LastTurnNews.Count == 0
            ? "There is no major competitor news this week."
            : string.Join("\n\n", session.LastTurnNews);
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = new Bitmap(stars.ImagePath!);
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
