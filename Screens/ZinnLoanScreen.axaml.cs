using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class ZinnLoanScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public ZinnLoanScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(Path.Combine(installation.SwfDirectory, "ZINN_N.SWF"), "ZINN_LOAN_STATIC");
        if (art.IsSuccessful) ZinnImage.Source = new Bitmap(art.ImagePath!);
        Refresh();
    }

    private void RepayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        AmountEntry.Show("Pay Back Mr. Zinn", $"Cash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\nLoan:  {_company.Loan:N0}\nZinn's Loan:  {_company.ZinnLoan:N0}",
            "Enter the amount you wish to pay back:", Math.Min(_company.Cash, _company.ZinnLoan), amount => Apply(_company.RepayZinn(amount)));
    }
    private void RepayMaxButton_Click(object? sender, RoutedEventArgs e) => Apply(_company?.RepayZinn(Math.Min(_company.Cash, _company.ZinnLoan)));
    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        HelpOverlay.Show("Mr. Zinn's Loan Help",
            $"Mr. Zinn is a wealthy and somewhat fickle financier. He made his fortune as a merchant on Zile, and now he increases his wealth by investing in other businesses.\n\nMr. Zinn lends you money at {_company.ZinnRate:0.#}% interest per week. The interest is charged every time you travel to a new planet and is added to your total debt.\n\nNever allow this loan to exceed the credit limit of {_company.ZinnCreditLimit:N0} kubars. If it does, Mr. Zinn will repossess your ship and force you into bankruptcy.");
    }

    private void Apply(TradeResult? result)
    {
        if (result is null) return;
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "ZINN.MP3");
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        LoanText.Text = _company.ZinnLoan.ToString("N0");
        LoanText.Foreground = _company.WouldExceedZinnCreditLimit
            ? Brush.Parse("#F05555") : Brush.Parse("#202020");
        CreditText.Text = _company.ZinnCreditLimit.ToString("N0");
        RateText.Text = $"{_company.ZinnRate:0.#}% per week";
        InterestText.Text = _company.ZinnInterest.ToString("N0");
        CashText.Text = _company.Cash.ToString("N0");
    }
}
