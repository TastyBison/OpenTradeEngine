using System;
using System.Diagnostics;
using System.IO;

namespace OpenTradeEngine;

public static class SwfImageExtractor
{
    public static SwfImageExtractionResult TryExtractFirstFrame(
        string swfPath,
        string cacheName)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenTradeEngine",
            "AssetCache",
            cacheName);
        var cachedImagePath = Path.Combine(cacheDirectory, "1.png");

        if (File.Exists(cachedImagePath)
            && File.GetLastWriteTimeUtc(cachedImagePath) >= File.GetLastWriteTimeUtc(swfPath))
        {
            return SwfImageExtractionResult.Success(cachedImagePath);
        }

        var ffdecPath = FindFfdec();
        if (ffdecPath is null)
        {
            return SwfImageExtractionResult.Failure(
                "OpenTradeEngine needs JPEXS Free Flash Decompiler (FFDec) to prepare the original static SWF artwork. FFDec was not found in either Program Files folder.");
        }

        try
        {
            Directory.CreateDirectory(cacheDirectory);

            var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = ffdecPath,
                    Arguments = $"-format frame:png -export frame \"{cacheDirectory}\" \"{swfPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

            if (process is null)
            {
                return SwfImageExtractionResult.Failure("FFDec could not be started.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(cachedImagePath))
            {
                var error = process.StandardError.ReadToEnd().Trim();
                return SwfImageExtractionResult.Failure(
                    string.IsNullOrEmpty(error)
                        ? "FFDec did not produce the expected static image."
                        : "FFDec could not extract the static image: " + error);
            }

            return SwfImageExtractionResult.Success(cachedImagePath);
        }
        catch (Exception exception)
        {
            return SwfImageExtractionResult.Failure(
                "The static SWF image could not be prepared: " + exception.Message);
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
