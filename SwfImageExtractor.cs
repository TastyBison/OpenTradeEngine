using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;

namespace OpenTradeEngine;

public static class SwfImageExtractor
{
    private sealed record MemoryCacheEntry(long SourceTimestamp, SwfImageExtractionResult Result);
    private static readonly ConcurrentDictionary<string, MemoryCacheEntry> MemoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static SwfImageExtractionResult TryExtractFirstEmbeddedImage(string swfPath, string cacheName)
        => GetOrCreate("first", swfPath, cacheName,
            () => ExtractFirstEmbeddedImage(swfPath, cacheName));

    private static SwfImageExtractionResult ExtractFirstEmbeddedImage(string swfPath, string cacheName)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenTradeEngine", "AssetCache", "Embedded", cacheName);
        var cachedImagePath = Directory.Exists(cacheDirectory)
            ? EnumerateRasterImages(cacheDirectory).OrderBy(ImageExportOrder)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            : null;
        if (cachedImagePath is not null &&
            File.GetLastWriteTimeUtc(cachedImagePath) >= File.GetLastWriteTimeUtc(swfPath))
            return SwfImageExtractionResult.Success(cachedImagePath);

        var ffdecPath = FindFfdec();
        if (ffdecPath is null)
            return SwfImageExtractionResult.Failure(
                "OpenTradeEngine needs JPEXS Free Flash Decompiler (FFDec) to prepare the original embedded artwork.");
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffdecPath,
                Arguments = $"-export image \"{cacheDirectory}\" \"{swfPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null) return SwfImageExtractionResult.Failure("FFDec could not be started.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            cachedImagePath = EnumerateRasterImages(cacheDirectory).OrderBy(ImageExportOrder)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (process.ExitCode != 0 || cachedImagePath is null)
                return SwfImageExtractionResult.Failure(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
            return SwfImageExtractionResult.Success(cachedImagePath);
        }
        catch (Exception exception)
        {
            return SwfImageExtractionResult.Failure(
                "The embedded SWF image could not be prepared: " + exception.Message);
        }
    }

    public static SwfImageExtractionResult TryExtractLargestEmbeddedImage(string swfPath, string cacheName)
        => GetOrCreate("largest", swfPath, cacheName,
            () => ExtractLargestEmbeddedImage(swfPath, cacheName));

    public static SwfImageExtractionResult TryExtractLargestVectorShape(string swfPath, string cacheName)
        => GetOrCreate("largest-shape", swfPath, cacheName,
            () => ExtractLargestVectorShape(swfPath, cacheName));

    private static SwfImageExtractionResult ExtractLargestVectorShape(string swfPath, string cacheName)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenTradeEngine", "AssetCache", "Shapes", cacheName);
        var cachedImagePath = Directory.Exists(cacheDirectory)
            ? EnumerateRasterImages(cacheDirectory)
                .OrderByDescending(path => new FileInfo(path).Length).FirstOrDefault()
            : null;
        if (cachedImagePath is not null &&
            File.GetLastWriteTimeUtc(cachedImagePath) >= File.GetLastWriteTimeUtc(swfPath))
            return SwfImageExtractionResult.Success(cachedImagePath);

        var ffdecPath = FindFfdec();
        if (ffdecPath is null)
            return SwfImageExtractionResult.Failure(
                "OpenTradeEngine needs JPEXS Free Flash Decompiler (FFDec) to prepare the original vector artwork.");
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffdecPath,
                Arguments = $"-format shape:png -export shape \"{cacheDirectory}\" \"{swfPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null) return SwfImageExtractionResult.Failure("FFDec could not be started.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            cachedImagePath = EnumerateRasterImages(cacheDirectory)
                .OrderByDescending(path => new FileInfo(path).Length).FirstOrDefault();
            if (process.ExitCode != 0 || cachedImagePath is null)
                return SwfImageExtractionResult.Failure(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
            return SwfImageExtractionResult.Success(cachedImagePath);
        }
        catch (Exception exception)
        {
            return SwfImageExtractionResult.Failure(
                "The embedded SWF vector shape could not be prepared: " + exception.Message);
        }
    }

    private static SwfImageExtractionResult ExtractLargestEmbeddedImage(string swfPath, string cacheName)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenTradeEngine", "AssetCache", "Embedded", cacheName);
        var cachedImagePath = Directory.Exists(cacheDirectory)
            ? EnumerateRasterImages(cacheDirectory)
                .OrderByDescending(path => new FileInfo(path).Length).FirstOrDefault()
            : null;
        if (cachedImagePath is not null &&
            File.GetLastWriteTimeUtc(cachedImagePath) >= File.GetLastWriteTimeUtc(swfPath))
            return SwfImageExtractionResult.Success(cachedImagePath);

        var ffdecPath = FindFfdec();
        if (ffdecPath is null)
            return SwfImageExtractionResult.Failure(
                "OpenTradeEngine needs JPEXS Free Flash Decompiler (FFDec) to prepare the original embedded artwork.");
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffdecPath,
                Arguments = $"-export image \"{cacheDirectory}\" \"{swfPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null) return SwfImageExtractionResult.Failure("FFDec could not be started.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            cachedImagePath = EnumerateRasterImages(cacheDirectory)
                .OrderByDescending(path => new FileInfo(path).Length).FirstOrDefault();
            if (process.ExitCode != 0 || cachedImagePath is null)
                return SwfImageExtractionResult.Failure(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
            return SwfImageExtractionResult.Success(cachedImagePath);
        }
        catch (Exception exception)
        {
            return SwfImageExtractionResult.Failure(
                "The embedded SWF image could not be prepared: " + exception.Message);
        }
    }

    public static SwfImageExtractionResult TryExtractEmbeddedImage(
        string swfPath,
        string imageIdentifier)
        => GetOrCreate("identified", swfPath, imageIdentifier,
            () => ExtractEmbeddedImage(swfPath, imageIdentifier));

    private static SwfImageExtractionResult ExtractEmbeddedImage(
        string swfPath,
        string imageIdentifier)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenTradeEngine",
            "AssetCache",
            "GazillionaireEmbeddedImages");

        var cachedImagePath = FindEmbeddedImage(cacheDirectory, imageIdentifier);
        if (cachedImagePath is not null
            && File.GetLastWriteTimeUtc(cachedImagePath) >= File.GetLastWriteTimeUtc(swfPath))
        {
            return SwfImageExtractionResult.Success(cachedImagePath);
        }

        var ffdecPath = FindFfdec();
        if (ffdecPath is null)
        {
            return SwfImageExtractionResult.Failure(
                "OpenTradeEngine needs JPEXS Free Flash Decompiler (FFDec) to prepare the original embedded artwork.");
        }

        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffdecPath,
                Arguments = $"-export image \"{cacheDirectory}\" \"{swfPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
            {
                return SwfImageExtractionResult.Failure("FFDec could not be started.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            cachedImagePath = FindEmbeddedImage(cacheDirectory, imageIdentifier);
            if (process.ExitCode != 0 || cachedImagePath is null)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                return SwfImageExtractionResult.Failure(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"FFDec did not find the embedded image '{imageIdentifier}'."
                        : "FFDec could not extract the embedded image: " + detail.Trim());
            }

            return SwfImageExtractionResult.Success(cachedImagePath);
        }
        catch (Exception exception)
        {
            return SwfImageExtractionResult.Failure(
                "The embedded SWF image could not be prepared: " + exception.Message);
        }
    }

    private static string? FindFfdec()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "FFDec",
                "ffdec-cli.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "FFDec",
                "ffdec-cli.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindEmbeddedImage(string directory, string identifier)
    {
        if (!Directory.Exists(directory)) return null;
        foreach (var file in EnumerateRasterImages(directory))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Equals(identifier, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(identifier + "_", StringComparison.OrdinalIgnoreCase)
                || name.Contains(identifier, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateRasterImages(string directory) =>
        Directory.EnumerateFiles(directory).Where(path =>
            Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase));

    private static int ImageExportOrder(string path) =>
        int.TryParse(Path.GetFileNameWithoutExtension(path), out var id) ? id : int.MaxValue;

    private static SwfImageExtractionResult GetOrCreate(
        string mode,
        string swfPath,
        string identifier,
        Func<SwfImageExtractionResult> factory)
    {
        var fullPath = Path.GetFullPath(swfPath);
        var timestamp = File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath).Ticks : 0L;
        var key = $"{mode}|{fullPath}|{identifier}";
        if (MemoryCache.TryGetValue(key, out var cached) &&
            cached.SourceTimestamp == timestamp &&
            (!cached.Result.IsSuccessful || File.Exists(cached.Result.ImagePath)))
        {
            return cached.Result;
        }

        var result = factory();
        MemoryCache[key] = new MemoryCacheEntry(timestamp, result);
        return result;
    }
}

public sealed record SwfImageExtractionResult(
    bool IsSuccessful,
    string? ImagePath,
    string ErrorMessage)
{
    public static SwfImageExtractionResult Success(string imagePath) =>
        new(true, imagePath, string.Empty);

    public static SwfImageExtractionResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
