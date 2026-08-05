using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OpenTradeEngine.Screens;

public partial class NetWorthScreen : UserControl
{
    private static readonly string[] Colors =
        ["#2F80ED", "#14918D", "#54AD00", "#A4A400", "#FF9D0A", "#F06D72", "#8D78EA"];
    private IReadOnlyList<CompanyState>? _actualCompanies;
    private GameSession? _session;
    private decimal _historyTop;
    private decimal _historyBottom;

    public event EventHandler? ContinueRequested;

    public NetWorthScreen() => InitializeComponent();

    public void LoadCompanies(IReadOnlyList<string> companies, decimal initialDebt)
    {
        var count = Math.Min(companies.Count, Colors.Length);
        BarGrid.ColumnDefinitions.Clear();
        BarGrid.Children.Clear();
        LegendPanel.Children.Clear();
        var barHeight = Math.Clamp((double)(initialDebt / 200_000m) * 500d, 0d, 500d);
        SetAxis(0m, -50_000m, -100_000m, -150_000m, -200_000m);

        for (var index = 0; index < count; index++)
        {
            BarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var color = new SolidColorBrush(Color.Parse(Colors[index]));
            var bar = new Border
            {
                Background = color,
                Margin = new Thickness(13, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Width = 120,
                Height = barHeight
            };
            Grid.SetColumn(bar, index);
            BarGrid.Children.Add(bar);

            var legend = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*") };
            legend.Children.Add(new Ellipse
            {
                Width = 22,
                Height = 22,
                Fill = color,
                Stroke = Brushes.White,
                StrokeThickness = 1
            });
            var label = new TextBlock
            {
                Text = companies[index],
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            legend.Children.Add(label);
            LegendPanel.Children.Add(legend);
        }
    }

    public void LoadCompanies(GameSession session)
    {
        _session = session;
        _actualCompanies = session.Companies.Where(company => !company.IsBankrupt).ToArray();
        RenderHistory();
    }

    public void LoadCompanies(GameInstallation installation, GameSession session)
    {
        LoadCompanies(session);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = GameBitmapCache.Load(help.ImagePath!);
    }

    private void RenderActual(bool marketStrength)
    {
        if (_actualCompanies is null) return;
        BarGrid.IsVisible = true;
        HistoryCanvas.IsVisible = false;
        var companies = _actualCompanies;
        var count = Math.Min(companies.Count, Colors.Length);
        BarGrid.ColumnDefinitions.Clear();
        BarGrid.Children.Clear();
        LegendPanel.Children.Clear();
        var rawMaximum = Math.Max(1m, companies.Take(count).Max(company =>
            marketStrength ? company.MarketStrength : Math.Max(0m, _session?.NetWorthOf(company) ?? company.NetWorth)));
        var maximum = NiceMaximum(rawMaximum);
        ChartHeading.Text = marketStrength ? "Market Strength by Ship Size" : "Net Worth";
        SetAxis(maximum, maximum * 0.75m, maximum * 0.5m, maximum * 0.25m, 0m);

        for (var index = 0; index < count; index++)
        {
            var company = companies[index];
            BarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var color = new SolidColorBrush(Color.Parse(Colors[index]));
            var bar = new Border { Background = color, Margin = new Thickness(13, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 120,
                Height = Math.Max(8d, (double)((marketStrength ? company.MarketStrength :
                    Math.Max(0m, _session?.NetWorthOf(company) ?? company.NetWorth)) / maximum) * 480d) };
            Grid.SetColumn(bar, index); BarGrid.Children.Add(bar);
            var legend = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*") };
            legend.Children.Add(new Ellipse { Width = 22, Height = 22, Fill = color, Stroke = Brushes.White, StrokeThickness = 1 });
            var value = marketStrength ? $"{company.MarketStrength:N0} tons" : $"{(_session?.NetWorthOf(company) ?? company.NetWorth):N0}";
            var label = new TextBlock { Text = $"{company.Name}  {value}", Foreground = Brushes.White,
                FontSize = 17, FontWeight = FontWeight.Bold, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            Grid.SetColumn(label, 1); legend.Children.Add(label); LegendPanel.Children.Add(legend);
        }
    }

    private void RenderHistory()
    {
        if (_actualCompanies is null || _session is null) return;
        BarGrid.IsVisible = false;
        HistoryCanvas.IsVisible = true;
        BarGrid.Children.Clear();
        LegendPanel.Children.Clear();
        ChartHeading.Text = $"Company History - Week {_session.Week}";

        var values = _actualCompanies.SelectMany(company => company.NetWorthHistory.Count > 0
            ? company.NetWorthHistory
            : [NetWorthOrCurrent(company)]).ToArray();
        var minimum = Math.Min(0m, values.DefaultIfEmpty(0m).Min());
        var maximum = Math.Max(0m, values.DefaultIfEmpty(0m).Max());
        _historyTop = maximum > 0m ? NiceMaximum(maximum) : 0m;
        _historyBottom = minimum < 0m ? -NiceMaximum(Math.Abs(minimum)) : 0m;
        if (_historyTop == _historyBottom) _historyTop = 100_000m;
        var interval = (_historyTop - _historyBottom) / 4m;
        SetAxis(_historyTop, _historyTop - interval, _historyTop - interval * 2m,
            _historyTop - interval * 3m, _historyBottom);

        for (var index = 0; index < Math.Min(_actualCompanies.Count, Colors.Length); index++)
        {
            var company = _actualCompanies[index];
            var color = new SolidColorBrush(Color.Parse(Colors[index]));
            var legend = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*") };
            legend.Children.Add(new Ellipse
            {
                Width = 22, Height = 22, Fill = color, Stroke = Brushes.White, StrokeThickness = 1
            });
            var label = new TextBlock
            {
                Text = company.Name, Foreground = Brushes.White, FontSize = 17,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            legend.Children.Add(label);
            LegendPanel.Children.Add(legend);
        }
        DrawHistory();
    }

    private decimal NetWorthOrCurrent(CompanyState company) =>
        _session?.NetWorthOf(company) ?? company.NetWorth;

    private void DrawHistory()
    {
        HistoryCanvas.Children.Clear();
        if (_actualCompanies is null || HistoryCanvas.Bounds.Width <= 0 || HistoryCanvas.Bounds.Height <= 0) return;
        var width = HistoryCanvas.Bounds.Width;
        var height = HistoryCanvas.Bounds.Height;
        var range = _historyTop - _historyBottom;
        if (range <= 0m) return;
        for (var companyIndex = 0; companyIndex < Math.Min(_actualCompanies.Count, Colors.Length); companyIndex++)
        {
            var company = _actualCompanies[companyIndex];
            var history = company.NetWorthHistory.Count > 0
                ? company.NetWorthHistory
                : [NetWorthOrCurrent(company)];
            var points = history.Count == 1 ? new[] { history[0], history[0] } : history.ToArray();
            for (var index = 1; index < points.Length; index++)
            {
                var x1 = width * (index - 1) / (points.Length - 1d);
                var x2 = width * index / (points.Length - 1d);
                var y1 = (double)((_historyTop - points[index - 1]) / range) * height;
                var y2 = (double)((_historyTop - points[index]) / range) * height;
                HistoryCanvas.Children.Add(new Line
                {
                    StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2),
                    Stroke = new SolidColorBrush(Color.Parse(Colors[companyIndex])), StrokeThickness = 3
                });
            }
        }
    }

    private void SetAxis(decimal top, decimal upper, decimal middle, decimal lower, decimal bottom)
    {
        AxisTopText.Text = FormatAxis(top);
        AxisUpperText.Text = FormatAxis(upper);
        AxisMiddleText.Text = FormatAxis(middle);
        AxisLowerText.Text = FormatAxis(lower);
        AxisBottomText.Text = FormatAxis(bottom);
    }

    private static string FormatAxis(decimal value)
    {
        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000m) return $"{value / 1_000_000m:0.#}M";
        if (absolute >= 1_000m) return $"{value / 1_000m:0.#}K";
        return value.ToString("0");
    }

    private static decimal NiceMaximum(decimal value)
    {
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)value)));
        foreach (var multiplier in new[] { 1m, 2m, 5m, 10m })
        {
            var candidate = magnitude * multiplier;
            if (candidate >= value) return candidate;
        }
        return value;
    }

    private void HistoryButton_Click(object? sender, RoutedEventArgs e) => RenderHistory();
    private void NetWorthButton_Click(object? sender, RoutedEventArgs e) => RenderActual(false);
    private void MarketStrengthButton_Click(object? sender, RoutedEventArgs e) => RenderActual(true);
    private void HistoryCanvas_SizeChanged(object? sender, SizeChangedEventArgs e) => DrawHistory();
    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Graphs Help", OriginalHelpCatalog.Graph(GameSession.StandardWinTarget));

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) =>
        ContinueRequested?.Invoke(this, EventArgs.Empty);
}
