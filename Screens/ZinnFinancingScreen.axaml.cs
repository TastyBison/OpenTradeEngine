using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class ZinnFinancingScreen : UserControl
{
    public event EventHandler? ContinueRequested;

    public ZinnFinancingScreen() => InitializeComponent();

    public void Load(GameInstallation installation, decimal loan)
    {
        FinancingText.Text =
            $"Mr. Zinn, a wealthy and somewhat fickle financier, generously loans you the {loan:N0} kubars necessary to get your company going.\n\n"
            + "However, you must pay Mr. Zinn 4% interest per week.\n\n"
            + "Mr. Zinn wishes you the best of luck on your new venture and hopes he will never have to repossess your ship.";

        var result = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "ZINN_N.SWF"), "ZINN_N");
        if (result.IsSuccessful) ZinnImage.Source = new Bitmap(result.ImagePath!);
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
