using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenTradeEngine.Screens;

public partial class InformationOverlay : UserControl
{
    public InformationOverlay() => InitializeComponent();

    public void Show(string title, string body)
    {
        TitleText.Text = title;
        BodyText.Text = body;
        IsVisible = true;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => IsVisible = false;
}
