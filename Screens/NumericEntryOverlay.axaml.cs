using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace OpenTradeEngine.Screens;

public partial class NumericEntryOverlay : UserControl
{
    private decimal _maximum;
    private decimal _minimum;
    private decimal _lower;
    private decimal _middle;
    private decimal _upper;
    private Action<decimal>? _confirmed;

    public NumericEntryOverlay() => InitializeComponent();

    public void Show(string heading, string summary, string prompt, decimal maximum, Action<decimal> confirmed,
        decimal initial = 0m, decimal? lowerPreset = null, decimal? middlePreset = null,
        decimal? upperPreset = null, decimal minimum = 0m)
    {
        _maximum = Math.Max(0m, decimal.Floor(maximum));
        _minimum = Math.Clamp(decimal.Floor(minimum), 0m, _maximum);
        _lower = Math.Clamp(decimal.Floor(lowerPreset ?? Preset(_maximum, 0.25m)), _minimum, _maximum);
        _middle = Math.Clamp(decimal.Floor(middlePreset ?? Preset(_maximum, 0.50m)), _minimum, _maximum);
        // Decompiled showAmountNew callers pass floor(max / 4), floor(max / 2),
        // and max for the Lower, Middle, and Upper buttons respectively.
        _upper = Math.Clamp(decimal.Floor(upperPreset ?? _maximum), _minimum, _maximum);
        _confirmed = confirmed;
        HeadingText.Text = heading;
        SummaryText.Text = summary;
        PromptText.Text = prompt;
        ValueBox.Text = Math.Clamp(decimal.Floor(initial), _minimum, _maximum).ToString("0", CultureInfo.CurrentCulture);
        ErrorText.Text = string.Empty;
        IsVisible = true;
        Dispatcher.UIThread.Post(() => { ValueBox.Focus(); ValueBox.SelectAll(); });
    }

    private static decimal Preset(decimal maximum, decimal fraction) =>
        maximum <= 0m ? 0m : Math.Max(1m, decimal.Floor(maximum * fraction));

    private void LowerButton_Click(object? sender, RoutedEventArgs e) => SetPreset(_lower);
    private void MiddleButton_Click(object? sender, RoutedEventArgs e) => SetPreset(_middle);
    private void UpperButton_Click(object? sender, RoutedEventArgs e) => SetPreset(_upper);
    private void SetPreset(decimal value) { ValueBox.Text = value.ToString("0", CultureInfo.CurrentCulture); ValueBox.Focus(); ValueBox.SelectAll(); }

    private void ValueBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Confirm();
        else if (e.Key == Key.Escape) Close();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Confirm();
    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void Confirm()
    {
        if (!decimal.TryParse(ValueBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value))
        {
            GameplayLogger.Log("PLAYER INPUT", "USER",
                $"numericHeading={HeadingText.Text}; entered={ValueBox.Text}; result=invalid number");
            ErrorText.Text = "Enter a valid whole number.";
            ValueBox.Focus(); ValueBox.SelectAll();
            return;
        }
        value = decimal.Floor(value);
        if (value < _minimum || value > _maximum)
        {
            GameplayLogger.Log("PLAYER INPUT", "USER",
                $"numericHeading={HeadingText.Text}; entered={value:0}; allowed={_minimum:0}-{_maximum:0}; result=out of range");
            ErrorText.Text = $"Enter an amount from {_minimum:N0} to {_maximum:N0}.";
            ValueBox.Focus(); ValueBox.SelectAll();
            return;
        }
        var callback = _confirmed;
        GameplayLogger.Log("PLAYER INPUT", "USER",
            $"numericHeading={HeadingText.Text}; entered={value:0}; result=confirmed");
        Close();
        callback?.Invoke(value);
    }

    private void Close() { IsVisible = false; _confirmed = null; ErrorText.Text = string.Empty; }
}
