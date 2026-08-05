using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OpenTradeEngine.Screens;

public partial class ExpensesScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public ExpensesScreen() => InitializeComponent();
    public void Load(CompanyState company)
    {
        _company = company;
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        var company = _company;
        var weeklyPayroll = company.CrewCount * company.CrewSalary;
        var payrollsOwed = weeklyPayroll <= 0m
            ? 0
            : (int)decimal.Floor(company.CrewWagesOwed / weeklyPayroll);
        CrewText.Text = $"Crew: {company.CrewCount} × {company.CrewSalary:N0} = " +
                        $"{company.CrewWagesOwed:N0} kubars currently owed ({payrollsOwed} weeks)";
        TaxText.Text = $"Accrued taxes: {company.TaxesOwed:N0}    Tariffs: {company.TariffsOwed:N0} kubars " +
                       $"({company.TaxUnpaidWeeks} weeks overdue)";
        CrewText.Foreground = company.CrewWagesOwed >= weeklyPayroll * 4m
            ? Brush.Parse("#F05555") : Brush.Parse("#7DDAFF");
        TaxText.Foreground = company.IsTaxAuditRisk
            ? Brush.Parse("#F05555") : Brush.Parse("#FFCB70");
    }

    private void PayCrewButton_Click(object? sender, RoutedEventArgs e) => Show(_company?.PayCrew());
    private void PayTaxesButton_Click(object? sender, RoutedEventArgs e) => Show(_company?.PayTaxes());
    private void Show(TradeResult? result)
    {
        if (result is null) return;
        StatusText.Text = result.Message;
        Refresh();
    }
    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
