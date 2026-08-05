using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class AboutLavaMindScreen : UserControl
{
    private GameInstallation? _installation;
    private int _page;

    public event EventHandler? CloseRequested;

    public AboutLavaMindScreen() => InitializeComponent();

    public void LoadAssets(GameInstallation installation)
    {
        _installation = installation;
        LoadSwf(Path.Combine(installation.SwfDirectory, "STARS_BG.SWF"), "STARS_BG",
            bitmap => StarsImage.Source = bitmap);
        ShowPage();
    }

    private void ShowPage()
    {
        if (_installation is null) return;

        if (_page == 0)
        {
            PageHeading.Text = "LavaMind";
            PageBody.Text = "Gazillionaire was developed by the volcanic brains at LavaMind. For a long time, we’ve been designing and developing games, apps and websites.\n\nThe founders of LavaMind believe the process of creating is as important as the final result, and we are looking to work with companies and individuals who share a similar vision.";
            PageArtwork.Source = new Bitmap(Path.Combine(_installation.PngDirectory, "LAVAMIND.PNG"));
        }
        else if (_page == 1)
        {
            PageHeading.Text = "Zapitalism";
            PageBody.Text = "In Zapitalism, your goal is to become a retail tycoon by taking a small store and growing a business empire.\n\nBuild your company from the ground up, open larger stores, and outwit your wiley competitors in a game of super sales and savvy shoppers!\n\nZapitalism combines beautifully rendered graphic images and a built-in tutorial. It’s easy to learn and fun to play.";
            LoadSwf(Path.Combine(_installation.SwfDirectory, "ZAP_INTRO.SWF"), "ZAP_INTRO",
                bitmap => PageArtwork.Source = bitmap);
        }
        else
        {
            PageHeading.Text = "Profitania";
            PageBody.Text = "Profitania is the final installment in the LavaMind business simulation trilogy. In this quixotic game, you inhabit a subterranean world, where you run a factory.\n\nThe game revolves around inventing new and ever-more peculiar products that you can sell to the population of the surface world. You must invest in research and development, secure raw materials, and manufacture the best products in the land.\n\nYou’ll compete against other players, both human and computer, in a race to become an industrial titan.\n\nIf you like Zapitalism, you’ll love Profitania!";
            LoadSwf(Path.Combine(_installation.SwfDirectory, "PROPEOPLE.SWF"), "PROPEOPLE",
                bitmap => PageArtwork.Source = bitmap);
        }
    }

    private static void LoadSwf(string path, string cacheName, Action<Bitmap> apply)
    {
        if (!File.Exists(path)) return;
        var result = cacheName.Contains("STARS", StringComparison.OrdinalIgnoreCase)
            ? SwfImageExtractor.TryExtractLargestEmbeddedImage(path, cacheName)
            : SwfImageExtractor.TryExtractFirstEmbeddedImage(path, cacheName);
        if (result.IsSuccessful) apply(new Bitmap(result.ImagePath!));
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_page < 2)
        {
            _page++;
            ShowPage();
            return;
        }
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
