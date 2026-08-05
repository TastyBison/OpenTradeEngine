using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace OpenTradeEngine.Screens;

public partial class ShipSelectionScreen : UserControl
{
    public event EventHandler<ShipSelectedEventArgs>? ShipSelected;

    public ShipSelectionScreen() => InitializeComponent();

    public void LoadShips(
        GameInstallation installation,
        string suggestedCompanyName,
        bool requestCompanyName = true,
        IReadOnlySet<int>? unavailableShipNumbers = null)
    {
        CompanyHeading.Text = suggestedCompanyName;
        CompanyNameTextBox.Text = suggestedCompanyName;
        CompanyNamePanel.IsVisible = requestCompanyName;
        CompanyNameBackdrop.IsVisible = requestCompanyName;
        if (requestCompanyName)
        {
            Dispatcher.UIThread.Post(() =>
            {
                CompanyNameTextBox.Focus();
                CompanyNameTextBox.SelectAll();
            }, DispatcherPriority.Loaded);
        }

        for (var index = 0; index < 12; index++)
        {
            var shipNumber = index + 1;
            if (unavailableShipNumbers?.Contains(shipNumber) == true)
                continue;

            var result = SwfImageExtractor.TryExtractFirstEmbeddedImage(
                Path.Combine(installation.SwfDirectory, $"SHIP{shipNumber}.SWF"),
                $"SHIP{shipNumber}");
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            };
            if (result.IsSuccessful) image.Source = GameBitmapCache.Load(result.ImagePath!);

            var button = new Button
            {
                Tag = shipNumber.ToString(),
                Content = image,
                Padding = new Thickness(8),
                Background = Brushes.Black,
                BorderBrush = Brush.Parse("#626278"),
                BorderThickness = new Thickness(7),
                CornerRadius = new CornerRadius(16),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                Focusable = false
            };
            button.Classes.Add("ship-choice");
            button.Click += ShipButton_Click;
            Grid.SetColumn(button, (index % 4) * 2);
            Grid.SetRow(button, (index / 4) * 2);
            ShipGrid.Children.Add(button);
        }
    }

    private void CompanyNameOkButton_Click(object? sender, RoutedEventArgs e)
        => AcceptCompanyName();

    private void CompanyNameTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        AcceptCompanyName();
        e.Handled = true;
    }

    private void AcceptCompanyName()
    {
        var name = CompanyNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        CompanyHeading.Text = name;
        CompanyNamePanel.IsVisible = false;
        CompanyNameBackdrop.IsVisible = false;
    }

    private void ShipButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var shipNumber))
            ShipSelected?.Invoke(this, new ShipSelectedEventArgs(CompanyHeading.Text ?? "Player 1 Inc.", shipNumber));
    }
}

public sealed record ShipSelectedEventArgs(string CompanyName, int ShipNumber);
