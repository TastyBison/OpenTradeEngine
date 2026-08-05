using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTradeEngine;

/// <summary>
/// Optional, bounded, plain-text audit trail for gameplay and debugging.
/// The active file is rotated before it reaches ten megabytes and the oldest
/// completed segments are removed when the configured directory limit is met.
/// </summary>
public static class GameplayLogger
{
    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8 = new(false);
    private static StreamWriter? _writer;
    private static GameSession? _session;
    private static string _directory = DefaultDirectory;
    private static string _sessionPrefix = string.Empty;
    private static string _currentPath = string.Empty;
    private static long _maximumBytes = 100L * 1024L * 1024L;
    private static long _segmentBytes = 10L * 1024L * 1024L;
    private static long _currentBytes;
    private static long _sequence;
    private static int _segment;
    private static readonly AsyncLocal<GameActionContext?> ActiveGameAction = new();

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenTradeEngine", "logs");

    public static bool Enabled { get; private set; }
    public static string? CurrentPath => string.IsNullOrWhiteSpace(_currentPath) ? null : _currentPath;

    public static void Configure(bool enabled, int maximumMegabytes = 100, string? directory = null)
    {
        lock (Gate)
        {
            CloseWriter();
            Enabled = enabled;
            _directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
            var clampedMegabytes = Math.Clamp(maximumMegabytes, 1, 1_024);
            _maximumBytes = clampedMegabytes * 1024L * 1024L;
            _segmentBytes = Math.Min(10L * 1024L * 1024L, _maximumBytes);
            _session = null;
            _sessionPrefix = string.Empty;
            _currentPath = string.Empty;
            _currentBytes = 0;
            _sequence = 0;
            _segment = 0;
            if (Enabled)
            {
                Directory.CreateDirectory(_directory);
                EnforceDirectoryLimit();
            }
        }
    }

    public static void StartSession(GameSession session, string source)
    {
        if (!Enabled) return;
        lock (Gate)
        {
            CloseWriter();
            _session = session;
            _sequence = 0;
            _segment = 0;
            _sessionPrefix = $"game-{DateTime.Now:yyyyMMdd-HHmmss}-{session.Seed}";
            OpenNextSegment();
            WriteCore("SESSION", "SYSTEM",
                $"Started {source}; engine={typeof(GameplayLogger).Assembly.GetName().Version}; " +
                $"seed={session.Seed}; week={session.Week}; difficulty={session.Level}; " +
                $"planets=[{string.Join(',', session.Planets)}]; mods={ModCatalog.Enabled}; " +
                $"logLimitMb={_maximumBytes / 1024L / 1024L}");
            LogAllCompanyStates("SESSION START");
        }
    }

    public static void Log(string category, string actor, string message)
    {
        if (!Enabled || _writer is null) return;
        lock (Gate) WriteCore(category, actor, message);
    }

    public static void LogCompanyState(string category, CompanyState company, string message = "")
    {
        if (!Enabled || _writer is null) return;
        var cargo = string.Join(',', company.Cargo.OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key}:{pair.Value.Quantity}@{pair.Value.AverageCost:0}"));
        var shares = string.Join(',', company.Shares.OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key}:{pair.Value}"));
        var warehouses = string.Join(';', company.Warehouses.OrderBy(pair => pair.Key)
            .Select(warehouse => $"{warehouse.Key}=[{string.Join(',', warehouse.Value.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{pair.Value.Quantity}@{pair.Value.AverageCost:0}"))}]"));
        Log(category, company.Name,
            $"{message}; human={company.IsHuman}; bankrupt={company.IsBankrupt}; ship={company.ShipNumber}:{company.ShipModel}; " +
            $"planet={company.Planet}; lastPlanet={company.LastPlanet}; destination={company.PlannedDestination}; " +
            $"cash={company.Cash:0}; bank={company.Bank:0}; loan={company.Loan:0}; zinn={company.ZinnLoan:0}; " +
            $"taxes={company.TaxesOwed:0}; tariffs={company.TariffsOwed:0}; wages={company.CrewWagesOwed:0}; " +
            $"fuel={company.Fuel:0.###}/{company.FuelCapacity}; passengers={company.Passengers}/{company.PassengerCapacity}; " +
            $"fare={company.TicketPrice:0}; nextFare={company.NextTicketPrice:0}; " +
            $"passengerAds={company.PassengerAdvertising}; commodityAds={company.CommodityAdvertising}; " +
            $"insurance={company.InsuranceLevel}; luck={company.Luck}; cargo=[{cargo}]; shares=[{shares}]; warehouses=[{warehouses}]");
    }

    public static void LogAllCompanyStates(string reason)
    {
        if (!Enabled || _session is null || _writer is null) return;
        foreach (var company in _session.Companies)
            LogCompanyState("STATE", company, reason);
    }

    public static IDisposable BeginCompanyAction(CompanyState company, string action, string details = "")
    {
        if (!Enabled || _writer is null) return EmptyScope.Instance;
        var previous = ActiveGameAction.Value;
        ActiveGameAction.Value = new GameActionContext(company, action, details, previous);
        return new GameActionScope(previous);
    }

    public static void RecordTradeResult(TradeResult result)
    {
        var context = ActiveGameAction.Value;
        if (context is null || !Enabled || _writer is null) return;
        Log("GAME ACTION", context.Company.Name,
            $"action={context.Action}; details={context.Details}; success={result.IsSuccessful}; result={result.Message}");
        LogCompanyState("GAME STATE", context.Company, $"after action={context.Action}");
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            if (_writer is not null) WriteCore("SESSION", "SYSTEM", "Logger closed normally.");
            CloseWriter();
            _currentPath = string.Empty;
            EnforceDirectoryLimit();
            _session = null;
        }
    }

    private static void WriteCore(string category, string actor, string message)
    {
        if (_writer is null) return;
        var sanitized = (message ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Replace("\n", " | ", StringComparison.Ordinal);
        var line = $"[{DateTimeOffset.Now:O}] [Action {++_sequence}] [Week {_session?.Week ?? 0}] " +
                   $"[{category}] [{actor}] {sanitized}";
        var bytes = Utf8.GetByteCount(line + Environment.NewLine);
        if (_currentBytes > 0 && _currentBytes + bytes > _segmentBytes)
        {
            CloseWriter();
            OpenNextSegment();
        }
        _writer.WriteLine(line);
        _writer.Flush();
        _currentBytes += bytes;
        EnforceDirectoryLimit();
    }

    private static void OpenNextSegment()
    {
        Directory.CreateDirectory(_directory);
        _segment++;
        _currentPath = Path.Combine(_directory,
            $"{_sessionPrefix}-{_segment.ToString("000", CultureInfo.InvariantCulture)}.log");
        _writer = new StreamWriter(new FileStream(_currentPath, FileMode.Create, FileAccess.Write,
            FileShare.Read), Utf8) { AutoFlush = true };
        _currentBytes = 0;
        EnforceDirectoryLimit();
    }

    private static void EnforceDirectoryLimit()
    {
        if (!Directory.Exists(_directory)) return;
        var files = new DirectoryInfo(_directory).GetFiles("*.log")
            .OrderBy(file => file.LastWriteTimeUtc).ToList();
        foreach (var file in files) file.Refresh();
        var total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= _maximumBytes) break;
            if (file.FullName.Equals(_currentPath, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var length = file.Length;
                File.Delete(file.FullName);
                total -= length;
            }
            catch
            {
                // Logging must never interrupt gameplay because an old file is locked.
            }
        }
    }

    private static void CloseWriter()
    {
        _writer?.Dispose();
        _writer = null;
        _currentBytes = 0;
    }

    private sealed record GameActionContext(
        CompanyState Company, string Action, string Details, GameActionContext? Previous);

    private sealed class GameActionScope(GameActionContext? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            ActiveGameAction.Value = previous;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}
