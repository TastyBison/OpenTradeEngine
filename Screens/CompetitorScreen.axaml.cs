using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class CompetitorScreen : UserControl
{
    public event EventHandler? ContinueRequested;

    public CompetitorScreen() => InitializeComponent();

    public void LoadCompetitors(GameInstallation installation, IReadOnlyList<AiOpponentProfile> competitors)
    {
        for (var index = 0; index < competitors.Count; index++)
        {
            var competitor = competitors[index];
            var heading = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = competitor.Name, Foreground = Brushes.White, FontSize = 19, FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center },
                    new TextBlock { Text = competitor.Personality, Foreground = Brushes.White, FontSize = 15, TextAlignment = TextAlignment.Center }
                }
            };
            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#3C3C61")),
                CornerRadius = new CornerRadius(12),
                Padding = new Avalonia.Thickness(10),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*"),
                    Children =
                    {
                        heading,
                        new Image { Source = new Bitmap(Path.Combine(installation.PngDirectory, $"OP{competitor.Number}.PNG")), Stretch = Stretch.Uniform, Margin = new Avalonia.Thickness(0,8,0,0) }
                    }
                }
            };
            Grid.SetRow(((Grid)card.Child).Children[1], 1);
            Grid.SetColumn(card, (index % 3) * 2);
            Grid.SetRow(card, (index / 3) * 2);
            CompetitorGrid.Children.Add(card);
        }
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
