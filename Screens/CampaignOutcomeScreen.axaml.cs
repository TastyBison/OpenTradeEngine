using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenTradeEngine.Screens;

public partial class CampaignOutcomeScreen : UserControl
{
    public event EventHandler? MainMenuRequested;
    public event EventHandler? ContinueGameRequested;
    public CampaignOutcomeScreen() => InitializeComponent();
    public void Load(string heading, string body, bool canContinue)
    {
        Heading.Text = heading;
        Body.Text = body;
        ContinueGameButton.IsVisible = canContinue;
    }
    private void ContinueGameButton_Click(object? sender, RoutedEventArgs e) => ContinueGameRequested?.Invoke(this, EventArgs.Empty);
    private void MainMenuButton_Click(object? sender, RoutedEventArgs e) => MainMenuRequested?.Invoke(this, EventArgs.Empty);
}
