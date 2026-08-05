using System;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace OpenTradeEngine;

public sealed class GameAudioPlayer : IDisposable
{
    private string? _mp3Directory;
    private string? _menuClickWavePath;
    private AudioFileReader? _voiceReader;
    private WaveOutEvent? _voiceOutput;

    public bool Enabled { get; private set; } = true;

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled)
        {
            StopAll();
            return;
        }

        PrepareMenuClick();
    }

    public void SetInstallation(GameInstallation installation)
    {
        StopAll();
        _mp3Directory = installation.Mp3Directory;
        if (Enabled) PrepareMenuClick();
    }

    public void PlayMenuClick()
    {
        if (!Enabled) return;
        if (_menuClickWavePath is null) PrepareMenuClick();
        if (_menuClickWavePath is null) return;

        // Cache the tiny MP3 as a WAV and let Windows play it directly. This avoids
        // opening or priming an MP3 output stream on each press and prevents overlap distortion.
        PlaySound(_menuClickWavePath, IntPtr.Zero, SoundAsync | SoundFileName | SoundNoDefault);
    }

    public void PlayVoice(string fileName)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(_mp3Directory)) return;

        var path = Path.Combine(_mp3Directory, fileName);
        if (!File.Exists(path)) return;

        StopVoice();
        try
        {
            _voiceReader = new AudioFileReader(path);
            _voiceOutput = new WaveOutEvent { DesiredLatency = 60, NumberOfBuffers = 2 };
            _voiceOutput.Init(_voiceReader);
            _voiceOutput.Play();
        }
        catch
        {
            StopVoice();
        }
    }

    public void StopVoice()
    {
        _voiceOutput?.Stop();
        _voiceOutput?.Dispose();
        _voiceReader?.Dispose();
        _voiceOutput = null;
        _voiceReader = null;
    }

    public void StopAll()
    {
        StopVoice();
        if (OperatingSystem.IsWindows()) PlaySound(null, IntPtr.Zero, 0);
        _menuClickWavePath = null;
    }

    public void Dispose() => StopAll();

    private void PrepareMenuClick()
    {
        if (!Enabled || !OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(_mp3Directory)) return;

        var path = Path.Combine(_mp3Directory, "PING1.MP3");
        if (!File.Exists(path)) return;

        try
        {
            var cacheDirectory = Path.Combine(Path.GetTempPath(), "OpenTradeEngine", "audio");
            Directory.CreateDirectory(cacheDirectory);
            var wavePath = Path.Combine(cacheDirectory, "PING1.wav");
            if (!File.Exists(wavePath) || File.GetLastWriteTimeUtc(wavePath) < File.GetLastWriteTimeUtc(path))
            {
                using var reader = new AudioFileReader(path);
                WaveFileWriter.CreateWaveFile16(wavePath, reader);
            }
            _menuClickWavePath = wavePath;
        }
        catch
        {
            _menuClickWavePath = null;
        }
    }

    private const uint SoundAsync = 0x0001;
    private const uint SoundNoDefault = 0x0002;
    private const uint SoundFileName = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? soundName, IntPtr module, uint flags);
}
