using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class TradersUnionScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public TradersUnionScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(Path.Combine(installation.SwfDirectory, "LOAN_N.SWF"), "TRADERS_UNION_OFFICIAL_STATIC");
        if (art.IsSuccessful) OfficialImage.Source = new Bitmap(art.ImagePath!);
        Refresh();
    }

    private void RepayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        AmountEntry.Show("Pay Back Loan", $"Cash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\nLoan:  {_company.Loan:N0}",
            "Enter the amount you wish to pay back:", Math.Min(_company.Cash, _company.Loan), amount => Apply(_company.RepayTradersUnion(amount)));
    }
    private void RepayMaxButton_Click(object? sender, RoutedEventArgs e) => Apply(_company?.RepayTradersUnion(Math.Min(_company.Cash, _company.Loan)));
    private void BorrowButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        AmountEntry.Show("Borrow Money", $"Cash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\nLoan:  {_company.Loan:N0}",
            "Enter the amount you wish to borrow:", _company.AvailableSafeUnionCredit,
            amount => Apply(_company.BorrowFromTradersUnion(amount)));
    }
    private void BorrowMaxButton_Click(object? sender, RoutedEventArgs e) =>
        Apply(_company?.BorrowFromTradersUnion(_company.AvailableSafeUnionCredit));
    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        HelpOverlay.Show("Traders' Union Loan Help",
            $"Any time you are on a planet, you may take out a loan from the Traders' Union.\n\nYou will be charged {_company.StandardLoanRate:0.#}% interest per week. A new week begins every time you travel from one planet to another. The interest accrued on your loan will be automatically added to your debt.\n\nUnder no circumstances must you allow your loan to exceed your credit limit of {_company.StandardCreditLimit:N0} kubars. If it does, the Traders' Union will force you into bankruptcy.");
    }

    private void Apply(TradeResult? result)
    {
        if (result is null) return;
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "LOAN.MP3");
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        LoanText.Text = _company.Loan.ToString("N0");
        LoanText.Foreground = _company.WouldExceedUnionCreditLimit
            ? Brush.Parse("#F05555") : Brush.Parse("#202020");
        CreditText.Text = _company.StandardCreditLimit.ToString("N0");
        RateText.Text = $"{_company.StandardLoanRate:0.#}% per week";
        InterestText.Text = _company.LoanInterest.ToString("N0");
        CashText.Text = _company.Cash.ToString("N0");
    }
}
