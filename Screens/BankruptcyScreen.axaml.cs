using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class BankruptcyScreen : UserControl
{
    private bool _showingHeadline;
    public event EventHandler? ContinueRequested;

    public BankruptcyScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company, bool gameWillEnd)
    {
        AnnouncementText.Text = $"{company.Name} Bankrupts!!!";
        var unionExceeded = company.Loan > company.StandardCreditLimit;
        var zinnExceeded = company.ZinnLoan > company.ZinnCreditLimit;
        var excess = Math.Max(0m, company.Loan - company.StandardCreditLimit) +
                     Math.Max(0m, company.ZinnLoan - company.ZinnCreditLimit);
        var creditor = unionExceeded && zinnExceeded
            ? "the Traders' Union and Mr. Zinn"
            : unionExceeded ? "the Traders' Union" : "Mr. Zinn";
        var avoidable = company.Cash + company.Bank >= excess;

        HeadlineText.Text = avoidable
            ? $"Big news on the financial front. {company.Name} has gone bankrupt! Our sources inform us that the government has revoked its trading license because it exceeded the credit limit set by {creditor}.\n\n{company.Name} had enough money to keep the loan below the credit limit, but for some unknown reason refused to pay. A full investigation is now underway."
            : $"Big news on the financial front. {company.Name} has gone bankrupt! Our sources inform us that all assets are being liquidated to pay off creditors. Angry citizens are demanding a full investigation, but there is little anyone can do.\n\n{company.Name} is too deeply in debt to continue operating, and {creditor} will lend it no more money.";
        HeadlineText.Text += gameWillEnd
            ? "\n\nSupreme Commander Dred Nicolson says the bankruptcy is a major setback for the Empire and promises to end this game before the Kukubian Colonies suffer further."
            : "\n\nSupreme Commander Dred Nicolson promises to support the remaining companies so the Kukubian Colonies may continue to grow and prosper.";

        SetStaticImage(installation, "STARS2.SWF", "BANKRUPTCY_STARS", StarsImage, largest: true);
        SetStaticImage(installation, "LOSE.SWF", "BANKRUPTCY_LOSE", BankruptcyImage);
        SetStaticImage(installation, "NEWS_L.SWF", "BANKRUPTCY_NEWS", NewsImage);
    }

    private static void SetStaticImage(GameInstallation installation, string fileName, string cacheName,
        Image target, bool largest = false)
    {
        var path = Path.Combine(installation.SwfDirectory, fileName);
        var art = largest
            ? SwfImageExtractor.TryExtractLargestEmbeddedImage(path, cacheName)
            : SwfImageExtractor.TryExtractFirstEmbeddedImage(path, cacheName);
        if (art.IsSuccessful) target.Source = new Bitmap(art.ImagePath!);
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_showingHeadline)
        {
            _showingHeadline = true;
            AnnouncementPanel.IsVisible = false;
            HeadlinePanel.IsVisible = true;
            return;
        }
        ContinueRequested?.Invoke(this, EventArgs.Empty);
    }
}
