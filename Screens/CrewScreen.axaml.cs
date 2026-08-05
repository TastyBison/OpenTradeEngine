using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class CrewScreen : UserControl
{
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;
    public CrewScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var art = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "CREW_N.SWF"), "CREW_N_STATIC");
        if (art.IsSuccessful) CrewImage.Source = new Bitmap(art.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
        Refresh();
    }

    private void Refresh()
    {
        if (_company is null) return;
        CountText.Text = _company.CrewCount.ToString();
        SalaryText.Text = $"{_company.CrewSalary:N0}";
        OwedText.Text = $"{_company.CrewWagesOwed:N0}";
        var weeklyPayroll = _company.CrewCount * _company.CrewSalary;
        OwedText.Foreground = _company.CrewWagesOwed >= weeklyPayroll * 4m
            ? Brush.Parse("#F05555") : Brush.Parse("#303030");
        CashText.Text = $"{_company.Cash:N0}";
        MoraleText.Text = _company.CrewWagesOwed switch
        {
            var owed when owed >= weeklyPayroll * 4m =>
                "Your crew demands that you pay them their salary!",
            var owed when owed >= weeklyPayroll * 3m =>
                "Your crew appears to be upset over the fact that you haven't paid them.",
            var owed when owed >= weeklyPayroll * 2m =>
                "Your crew seems mildly annoyed over the fact that you haven't paid them.",
            _ => "Your crew's morale is high and everyone seems happy."
        };
    }

    private void PayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        var result = _company.PayCrew();
        StatusText.Text = result.Message;
        if (result.IsSuccessful) SoundRequested?.Invoke(this, "CREW.MP3");
        Refresh();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);

    private void HelpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is not null) HelpOverlay.Show("Crew Help", OriginalHelpCatalog.Crew(_company));
    }
}
