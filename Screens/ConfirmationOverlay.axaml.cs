using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenTradeEngine.Screens;

public partial class ConfirmationOverlay : UserControl
{
    private Action? _yes;
    private Action? _no;

    public ConfirmationOverlay() => InitializeComponent();

    public void Show(string heading, string message, string yesText, string noText, Action yes, Action no)
    {
        HeadingText.Text = heading;
        MessageText.Text = message;
        YesButton.Content = yesText;
        NoButton.Content = noText;
        _yes = yes;
        _no = no;
        IsVisible = true;
        YesButton.Focus();
    }

    private void YesButton_Click(object? sender, RoutedEventArgs e)
    {
        GameplayLogger.Log("PLAYER CHOICE", "USER",
            $"heading={HeadingText.Text}; choice={YesButton.Content}; message={MessageText.Text}");
        var action = _yes;
        Close();
        action?.Invoke();
    }

    private void NoButton_Click(object? sender, RoutedEventArgs e)
    {
        GameplayLogger.Log("PLAYER CHOICE", "USER",
            $"heading={HeadingText.Text}; choice={NoButton.Content}; message={MessageText.Text}");
        var action = _no;
        Close();
        action?.Invoke();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void Close()
    {
        IsVisible = false;
        _yes = null;
        _no = null;
    }
}
