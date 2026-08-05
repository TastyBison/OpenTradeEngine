using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class InsuranceScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;
    public InsuranceScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _company = company;
        var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(Path.Combine(installation.SwfDirectory, "INSURE_N.SWF"), "INSURE_N_STATIC");
        if (art.IsSuccessful) AgentImage.Source = new Bitmap(art.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
        if (company.InsuranceCost <= 0) company.InsuranceCost = session.GenerateInsuranceQuote(company);
        Refresh();
    }

    private void BuyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        var result = _company.SetInsurance(1);
        StatusText.Text = result.Message;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "INSURE.MP3");
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        CashText.Text = _company.Cash.ToString("N0");
        RangeText.Text = $"{_company.InsurancePriceRange:N0} - {_company.InsurancePriceRange * 1_000:N0}";
        QuoteText.Text = _company.InsuranceCost.ToString("N0");
        CoverageText.Text = _company.InsuranceLevel > 0 ? "Yes" : "No";
        BuyButton.IsEnabled = _company.InsuranceLevel == 0 && _company.Cash >= _company.InsuranceCost;
        if (_company.InsuranceLevel > 0) StatusText.Text = "Insurance coverage next trip: Yes";
    }

    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Voyager's Insurance Help", OriginalHelpCatalog.Insurance);
    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
