using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenTradeEngine.Screens;

public partial class TutorialScreen : UserControl
{
    private GameSession? _session;
    private CompanyState? _company;

    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public TutorialScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _session = session;
        _company = company;
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "TUTORIAL_STARS");
        if (stars.IsSuccessful) StarsImage.Source = GameBitmapCache.Load(stars.ImagePath!);
        Refresh();
    }

    private void Refresh()
    {
        if (_session is null || _company is null) return;
        _session.PrepareTutorialStage();
        var stage = _session.TutorialStage;
        var multiplayer = _session.Companies.Count(company => company.IsHuman && !company.IsBankrupt) >= 2;
        TutorialText.Text = TutorialCatalog.Text(stage, multiplayer, _company.Cash, _company.ZinnLoan);
        ProgressText.Text = $"Tutorial Completed: {Math.Floor(stage * 100m / 17m):0}%";
        ProgressBar.Value = stage;
        MoreButton.IsVisible = _session.CanAddTutorialFeature;
    }

    private void MoreButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session?.AddTutorialFeature() != true) return;
        SoundRequested?.Invoke(this, "MORECOMPLEX.MP3");
        Refresh();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
