using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class BankScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public BankScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(Path.Combine(installation.SwfDirectory, "BANK1_N.SWF"), "BANK_MANAGER_STATIC");
        if (art.IsSuccessful) ManagerImage.Source = new Bitmap(art.ImagePath!);
        Refresh();
    }

    private void DepositButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        AmountEntry.Show("Deposit Money", $"Cash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\nLoan:  {_company.Loan:N0}",
            "Enter the amount you wish to deposit:", _company.Cash, amount => Apply(_company.DepositToBank(amount), "BANK.MP3"));
    }
    private void DepositMaxButton_Click(object? sender, RoutedEventArgs e) => Apply(_company?.DepositToBank(_company.Cash), "BANK.MP3");
    private void WithdrawButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        AmountEntry.Show("Withdraw Money", $"Cash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\nLoan:  {_company.Loan:N0}",
            "Enter the amount you wish to take out:", _company.Bank, amount => Apply(_company.WithdrawFromBank(amount), "BANK2.MP3"));
    }
    private void WithdrawMaxButton_Click(object? sender, RoutedEventArgs e) => Apply(_company?.WithdrawFromBank(_company.Bank), "BANK2.MP3");
    private void BackButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        HelpOverlay.Show("Bank Help",
            $"Any time you are on a planet, you may deposit money in your Traders' Union Bank Account, and the Traders' Union will pay you {_company.SavingsRate:0.#}% interest, up to a maximum of 100,000 kubars.\n\nEvery time you travel to a new planet, the interest will be automatically added to your bank account. There is no limit to how much money you may deposit in your account.");
    }

    private void Apply(TradeResult? result, string sound)
    {
        if (result is null) return;
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, sound);
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        BalanceText.Text = _company.Bank.ToString("N0");
        RateText.Text = $"{_company.SavingsRate:0.#}% per week";
        InterestText.Text = _company.SavingsInterest.ToString("N0");
        CashText.Text = _company.Cash.ToString("N0");
    }
}
