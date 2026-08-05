using System.Collections.Generic;
using System.IO;

namespace OpenTradeEngine;

public sealed record GameInstallation(
    string RootPath,
    string MainSwfPath,
    string SwfDirectory,
    string ResourcesDirectory,
    string PngDirectory,
    string Mp3Directory)
{
    public static GameInstallationResult TryOpen(string rootPath)
    {
        var requiredFile = Path.Combine(rootPath, "Gazillionaire.swf");
        var requiredDirectories = new Dictionary<string, string>
        {
            ["SWF"] = Path.Combine(rootPath, "SWF"),
            ["resources"] = Path.Combine(rootPath, "resources"),
            ["PNG"] = Path.Combine(rootPath, "PNG"),
            ["MP3"] = Path.Combine(rootPath, "MP3")
        };

        var missing = new List<string>();

        if (!File.Exists(requiredFile))
        {
            missing.Add("Gazillionaire.swf");
        }

        foreach (var directory in requiredDirectories)
        {
            if (!Directory.Exists(directory.Value))
            {
                missing.Add(directory.Key + " folder");
            }
        }

        if (missing.Count > 0)
        {
            return GameInstallationResult.Invalid(
                "This does not appear to be a complete Gazillionaire installation. Missing: "
                + string.Join(", ", missing)
                + ".");
        }

        return GameInstallationResult.Valid(
            new GameInstallation(
                Path.GetFullPath(rootPath),
                requiredFile,
                requiredDirectories["SWF"],
                requiredDirectories["resources"],
                requiredDirectories["PNG"],
                requiredDirectories["MP3"]));
    }
}

public sealed record GameInstallationResult(
    bool IsValid,
    GameInstallation? Installation,
    string ErrorMessage)
{
    public static GameInstallationResult Valid(GameInstallation installation) =>
        new(true, installation, string.Empty);

    public static GameInstallationResult Invalid(string errorMessage) =>
        new(false, null, errorMessage);
}
