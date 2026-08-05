using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class TaxScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;
    public TaxScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "TAX1_N.SWF"), "TAX1_N_STATIC");
        if (art.IsSuccessful) TaxImage.Source = new Bitmap(art.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        PassengerTaxText.Text = $"{_company.TaxesOwed:N0}  ({_company.PassengerTaxRate}%)";
        TariffText.Text = $"{_company.TariffsOwed:N0}  ({_company.ImportTariffRate}% in / {_company.ExportTariffRate}% out)";
        var total = _company.TaxesOwed + _company.TariffsOwed;
        TotalText.Text = $"{total:N0}";
        var auditRisk = _company.IsTaxAuditRisk;
        var amountColor = auditRisk ? Brush.Parse("#F05555") : Brush.Parse("#303030");
        PassengerTaxText.Foreground = amountColor;
        TariffText.Foreground = amountColor;
        TotalText.Foreground = amountColor;
        CashText.Text = $"{_company.Cash:N0}";
        AuditorText.Text = total switch
        {
            _ when total >= 35m * _company.ShipTons => "The Tax Auditor warns you to pay your taxes immediately.",
            _ when total >= 25m * _company.ShipTons => "The Tax Auditor reminds you not to wait until the last minute to pay your taxes.",
            _ when total >= 15m * _company.ShipTons => "The Tax Auditor says it never hurts to pay your taxes ahead of time.",
            0m => "The Tax Auditor says you don't owe the government any money.",
            _ => "The Tax Auditor says you don't have to pay your taxes right away."
        };
        AuditorText.Foreground = auditRisk ? Brush.Parse("#F05555") : Brushes.White;
    }

    private void PayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        var result = _company.PayTaxes();
        StatusText.Text = result.Message;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "TAX2.MP3");
        Refresh();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);

    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Tax Help", OriginalHelpCatalog.Taxes(_company));
    }
}
