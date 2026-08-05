using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace OpenTradeEngine.Screens;

public partial class StockMarketScreen : UserControl
{
    private static readonly Color[] ExchangeColors =
    [
        Color.Parse("#1685F8"), Color.Parse("#4FA40C"), Color.Parse("#FF9708"),
        Color.Parse("#EA5C59"), Color.Parse("#E45AC8"), Color.Parse("#8965EF"),
        Color.Parse("#777777")
    ];

    private GameSession? _session;
    private CompanyState? _company;
    private ChartMode _chartMode = ChartMode.All;
    public event EventHandler? ContinueRequested;
    public event EventHandler<string>? SoundRequested;

    public StockMarketScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        _session = session;
        _company = company;
        session.InitializeStocks();
        _chartMode = ChartMode.All;
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);
        Refresh();
    }

    private string LocalExchange => _company?.Planet ?? string.Empty;

    private void Trade(bool buying)
    {
        if (_session is null || _company is null || string.IsNullOrWhiteSpace(LocalExchange)) return;
        var exchange = LocalExchange;
        var price = _session.SharePrices[exchange];
        if (!_session.IsExchangeOpen(exchange))
        {
            StatusText.Text = $"The {exchange} Exchange has crashed and is closed this week.";
            StatusText.Foreground = Brushes.OrangeRed;
            return;
        }
        if (buying && _company.StockSpentThisWeek > 0m)
        {
            StatusText.Text = "Stock-market regulations allow only one share purchase per week. You may still sell shares.";
            StatusText.Foreground = Brushes.OrangeRed;
            return;
        }

        var owned = _company.Shares.GetValueOrDefault(exchange);
        var maximum = buying
            ? _company.MaximumStockPurchaseShares(price)
            : owned;
        if (maximum <= 0)
        {
            StatusText.Text = buying
                ? "You cannot buy one share under this week's investment limit."
                : $"You do not own any shares on the {exchange} Exchange.";
            StatusText.Foreground = Brushes.OrangeRed;
            return;
        }

        var average = _company.ShareAverageCosts.GetValueOrDefault(exchange);
        var projected = decimal.Floor((price - average) * owned);
        var tradeDetails = buying
            ? $"Investment limit this week:  {_company.MaximumStockInvestment:N0}\nMaximum shares:  {maximum:N0}"
            : $"Price you paid:  {average:N0}\n" +
              $"Gross result if all shares are sold:  {FormatProfit(projected)}";
        AmountEntry.Show(buying ? "Buy Shares" : "Sell Shares",
            $"Cash:  {_company.Cash:N0}\nSavings:  {_company.Bank:N0}\nLoan:  {_company.Loan:N0}\n" +
            $"Shares owned:  {owned:N0}\nPrice per share:  {price:N0}\n{tradeDetails}\nBroker commission:  1%",
            $"Enter the number of {exchange} shares you wish to {(buying ? "buy" : "sell")}:", maximum,
            amount => ApplyTrade(buying, exchange, price, (int)amount));
    }

    private void ApplyTrade(bool buying, string exchange, decimal price, int quantity)
    {
        if (_company is null) return;
        var average = _company.ShareAverageCosts.GetValueOrDefault(exchange);
        var result = buying
            ? _company.BuyShares(exchange, price, quantity)
            : _company.SellShares(exchange, price, quantity);
        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful ? Brushes.LightGreen : Brushes.OrangeRed;
        if (result.IsSuccessful)
        {
            var sound = buying ? "STOCK.MP3" :
                price > average ? "GAMBLE.MP3" :
                price < average ? "BAD7.MP3" : "NEUTRAL.MP3";
            SoundRequested?.Invoke(this, sound);
        }
        Refresh();
    }

    private void Refresh()
    {
        if (_session is null || _company is null) return;
        var exchange = LocalExchange;
        ExchangeNameText.Text = $"{exchange}\nExchange";
        ShowLocalButton.Content = $"Show {exchange}";
        CurrentPriceText.Text = _session.SharePrices.GetValueOrDefault(exchange).ToString("N0");
        PaidPriceText.Text = _company.ShareAverageCosts.GetValueOrDefault(exchange).ToString("N0");
        SharesOwnedText.Text = _company.Shares.GetValueOrDefault(exchange).ToString("N0");
        CashText.Text = _company.Cash.ToString("N0");
        BankText.Text = _company.Bank.ToString("N0");
        LoanText.Text = _company.Loan.ToString("N0");
        LoanText.Foreground = _company.WouldExceedUnionCreditLimit
            ? Brush.Parse("#F05555") : Brushes.White;
        var open = _session.IsExchangeOpen(exchange);
        BuyButton.IsEnabled = open && _company.StockSpentThisWeek <= 0m &&
                              _company.MaximumStockPurchaseShares(
                                  _session.SharePrices.GetValueOrDefault(exchange)) > 0;
        SellButton.IsEnabled = open && _company.Shares.GetValueOrDefault(exchange) > 0;
        ExchangeStateText.Text = open
            ? _company.StockSpentThisWeek > 0m
                ? "Purchase made this week — selling remains open"
                : $"Weekly investment limit: {_company.MaximumStockInvestment:N0} kubars"
            : "EXCHANGE CLOSED — market crash";
        ExchangeStateText.Foreground = open ? Brushes.LightBlue : Brushes.OrangeRed;
        ChartTitle.Text = _chartMode switch
        {
            ChartMode.Local => $"Planetary Stock Market: {exchange} Exchange",
            ChartMode.Shares => "Planetary Stock Market: Your Shares",
            _ => "Planetary Stock Market: All Exchanges"
        };
        Dispatcher.UIThread.Post(DrawChart);
    }

    private IReadOnlyList<string> DisplayedExchanges()
    {
        if (_session is null || _company is null) return [];
        return _chartMode switch
        {
            ChartMode.Local => _session.Planets.Where(name =>
                name.Equals(LocalExchange, StringComparison.OrdinalIgnoreCase)).ToArray(),
            ChartMode.Shares => _session.Planets.Where(name =>
                _company.Shares.GetValueOrDefault(name) > 0).ToArray(),
            _ => _session.Planets.ToArray()
        };
    }

    private void DrawChart()
    {
        if (_session is null || ChartCanvas.Bounds.Width < 80 || ChartCanvas.Bounds.Height < 80) return;
        ChartCanvas.Children.Clear();
        var names = DisplayedExchanges();
        var width = ChartCanvas.Bounds.Width;
        var height = ChartCanvas.Bounds.Height;
        const double left = 82;
        const double right = 86;
        const double top = 16;
        const double bottom = 18;
        var graphWidth = Math.Max(1, width - left - right);
        var graphHeight = Math.Max(1, height - top - bottom);
        var highest = names.SelectMany(name => _session.SharePriceHistory.GetValueOrDefault(name) ?? [])
            .DefaultIfEmpty(2_000m).Max();
        var maximum = Math.Max(2_000m, decimal.Ceiling(highest / 500m) * 500m);

        for (var tick = 0; tick <= 4; tick++)
        {
            var value = maximum * tick / 4m;
            var y = top + graphHeight - graphHeight * tick / 4d;
            AddLine(left, y, left + graphWidth, y, "#D5E5F7", tick == 0 ? 3 : 1.5);
            var label = value switch
            {
                >= 1_000m when value % 1_000m == 0m => $"{value / 1_000m:0}K",
                >= 1_000m => $"{value / 1_000m:0.#}K",
                _ => value.ToString("0")
            };
            AddLabel(label, 6, y - 15, 64, "#777777", 18, TextAlignment.Right, FontWeight.Bold);
        }
        AddLine(left, top, left, top + graphHeight, "#C8DCF6", 3);

        for (var planetIndex = 0; planetIndex < _session.Planets.Count; planetIndex++)
        {
            var name = _session.Planets[planetIndex];
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            var history = _session.SharePriceHistory.GetValueOrDefault(name) ?? [];
            if (history.Count == 0) continue;
            var color = ExchangeColors[planetIndex % ExchangeColors.Length];
            var points = history.Count == 1 ? new[] { history[0], history[0] } : history.ToArray();
            for (var index = 1; index < points.Length; index++)
            {
                var x1 = left + graphWidth * (index - 1) / (points.Length - 1d);
                var x2 = left + graphWidth * index / (points.Length - 1d);
                var y1 = top + graphHeight * (1d - (double)(points[index - 1] / maximum));
                var y2 = top + graphHeight * (1d - (double)(points[index] / maximum));
                AddLine(x1, y1, x2, y2, color.ToString(), 2.5);
            }
            var currentY = top + graphHeight * (1d - (double)(points[^1] / maximum));
            AddLabel(name, left + graphWidth + 14, currentY - 14, right - 12,
                color.ToString(), 18, TextAlignment.Left, FontWeight.Bold);
        }

        if (_chartMode == ChartMode.Shares && names.Count == 0)
            AddLabel("You do not own any shares.", left + 40, top + graphHeight / 2 - 18,
                graphWidth - 80, "#666666", 20, TextAlignment.Center, FontWeight.SemiBold);
    }

    private void AddLine(double x1, double y1, double x2, double y2, string color, double thickness)
    {
        ChartCanvas.Children.Add(new Line
        {
            StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2),
            Stroke = new SolidColorBrush(Color.Parse(color)), StrokeThickness = thickness
        });
    }

    private void AddLabel(string text, double x, double y, double width, string color, double size,
        TextAlignment alignment, FontWeight weight)
    {
        var label = new TextBlock
        {
            Text = text, Width = Math.Max(1, width), Foreground = new SolidColorBrush(Color.Parse(color)),
            FontSize = size, FontWeight = weight, TextAlignment = alignment
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        ChartCanvas.Children.Add(label);
    }

    private void ChartCanvas_SizeChanged(object? sender, SizeChangedEventArgs e) => DrawChart();
    private void BuyButton_Click(object? sender, RoutedEventArgs e) => Trade(true);
    private void SellButton_Click(object? sender, RoutedEventArgs e) => Trade(false);
    private void ShowLocalButton_Click(object? sender, RoutedEventArgs e) { _chartMode = ChartMode.Local; Refresh(); }
    private void ShowAllButton_Click(object? sender, RoutedEventArgs e) { _chartMode = ChartMode.All; Refresh(); }
    private void ShowSharesButton_Click(object? sender, RoutedEventArgs e) { _chartMode = ChartMode.Shares; Refresh(); }
    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        StatusText.Text = "You may trade only on the exchange where you are docked. You may buy once per week, up to the greater of 10,000 kubars or 1% of cash and savings. Selling is unrestricted. Every trade carries 1% commission.";
    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);

    private static string FormatProfit(decimal amount) => amount switch
    {
        > 0m => $"{amount:N0} kubars profit",
        < 0m => $"{Math.Abs(amount):N0} kubars loss",
        _ => "break even"
    };

    private enum ChartMode { Local, All, Shares }
}
