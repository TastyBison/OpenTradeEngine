using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine;

public static class GameBitmapCache
{
    private static readonly ConcurrentDictionary<string, Bitmap> Bitmaps =
        new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Bitmaps.GetOrAdd(fullPath, static fileName => new Bitmap(fileName));
    }
}
