using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using Microsoft.Extensions.Logging;

namespace SmsGatewayApp.Services
{
    public class VoiceService
    {
        private readonly ILogger<VoiceService> _logger;
        private IWavePlayer? _previewPlayer;

        public VoiceService(ILogger<VoiceService> logger)
        {
            _logger = logger;
        }


        public async Task PlayAudioToDeviceAsync(string audioPath, string? deviceNameHint)
        {
            await Task.Run(() =>
            {
                try
                {
                    int deviceNumber = -1;
                    
                    // Priority 0: Check if hint starts with [Index]
                    if (!string.IsNullOrEmpty(deviceNameHint) && deviceNameHint.StartsWith("[") && deviceNameHint.Contains("]"))
                    {
                        var parts = deviceNameHint.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[0], out int index))
                        {
                            deviceNumber = index;
                            _logger.LogInformation("Using audio device by index: {Index}", deviceNumber);
                        }
                    }

                    // Priority 1: Name-based fallback (if index not found)
                    if (deviceNumber == -1)
                    {
                        for (int i = 0; i < WaveOut.DeviceCount; i++)
                        {
                            var caps = WaveOut.GetCapabilities(i);
                            if (!string.IsNullOrEmpty(deviceNameHint) && caps.ProductName.Contains(deviceNameHint, StringComparison.OrdinalIgnoreCase))
                            {
                                deviceNumber = i;
                                break;
                            }
                        }
                    }

                    if (deviceNumber == -1) deviceNumber = -1; // Default

                    _logger.LogInformation("Final audio device number: {Index}", deviceNumber);

                    using (var audioFile = new AudioFileReader(audioPath))
                    {
                        var outFormat = new WaveFormat(8000, 16, 1);
                        using (var resampler = new MediaFoundationResampler(audioFile, outFormat))
                        {
                            resampler.ResamplerQuality = 60;
                            using (var outputDevice = new WaveOutEvent { DeviceNumber = deviceNumber })
                            {
                                outputDevice.Init(resampler);
                                outputDevice.Play();
                                while (outputDevice.PlaybackState == PlaybackState.Playing) Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Audio playback error");
                }
            });
        }
        public List<string> GetAudioDevices()
        {
            var devices = new List<string>();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                devices.Add($"[{i}] {caps.ProductName}");
            }
            return devices;
        }
    }
}
