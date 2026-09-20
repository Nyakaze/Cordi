using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cordi.Services;

public sealed class AudioService
{
    private const string LogSource = "Audio";
    private const int PollIntervalMs = 100;

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService Log;
    private readonly NotificationManager _notifications;
    private bool missingDeviceReported;

    public AudioService(Configuration.Configuration config, CordiLogService log, NotificationManager notifications)
    {
        _config = config;
        Log = log;
        _notifications = notifications;
    }

    public static IReadOnlyList<DirectSoundDeviceInfo> GetOutputDevices()
    {
        try
        {
            return DirectSoundOut.Devices.Where(device => device.Guid != Guid.Empty).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<DirectSoundDeviceInfo>();
        }
    }

    public static string DefaultDeviceLabel => "Primary Sound Driver";

    public string DescribeConfiguredDevice()
    {
        var configured = _config.Audio.OutputDevice;
        if (configured == Guid.Empty)
            return DefaultDeviceLabel;

        var match = GetOutputDevices().FirstOrDefault(device => device.Guid == configured);
        return match?.Description ?? DefaultDeviceLabel;
    }

    public bool IsConfiguredDeviceAvailable()
    {
        var configured = _config.Audio.OutputDevice;
        return configured == Guid.Empty || GetOutputDevices().Any(device => device.Guid == configured);
    }

    public string ResolveSoundPath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        return Path.Join(Service.PluginInterface.AssemblyLocation.Directory!.FullName, "target.wav");
    }

    public void Play(string configuredPath, float volume)
    {
        var path = ResolveSoundPath(configuredPath);
        if (!File.Exists(path))
        {
            Log.Warning(LogSource, $"Sound file not found: {path}");
            return;
        }

        var deviceId = ResolveOutputDevice();

        Task.Run(() =>
        {
            try
            {
                using var reader = OpenReader(path);
                var sampleProvider = new VolumeSampleProvider(reader.ToSampleProvider())
                {
                    Volume = Math.Clamp(volume, 0f, 1f),
                };

                using var output = new DirectSoundOut(deviceId);
                output.Init(sampleProvider);
                output.Play();

                while (output.PlaybackState == PlaybackState.Playing)
                    Thread.Sleep(PollIntervalMs);
            }
            catch (Exception ex)
            {
                Log.Error(LogSource, $"Sound playback failed for {path}", ex);
            }
        });
    }

    private Guid ResolveOutputDevice()
    {
        var configured = _config.Audio.OutputDevice;
        if (configured == Guid.Empty)
            return Guid.Empty;

        if (GetOutputDevices().Any(device => device.Guid == configured))
        {
            missingDeviceReported = false;
            return configured;
        }

        ReportMissingDevice();
        return Guid.Empty;
    }

    private void ReportMissingDevice()
    {
        if (missingDeviceReported)
            return;

        missingDeviceReported = true;
        Log.Warning(LogSource, "Configured output device is unavailable, falling back to the primary sound driver");
        _notifications.Add(
            "Audio device unavailable",
            "The selected output device was not found. Falling back to the primary sound driver.",
            CordiNotificationType.Warning);
    }

    private static WaveStream OpenReader(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".mp3" => new Mp3Reader(path),
            ".aif" or ".aiff" => new AiffFileReader(path),
            _ => new WaveFileReader(path),
        };
    }

    private sealed class Mp3Reader : Mp3FileReaderBase
    {
        public Mp3Reader(string path)
            : base(path, waveFormat => new AcmMp3FrameDecompressor(waveFormat))
        {
        }
    }
}
