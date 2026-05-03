using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmsGatewayApp.Models;

namespace SmsGatewayApp.Services
{
    public class SmsService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<SmsService> _logger;
        private readonly VoiceService _voiceService;

        public SmsService(DatabaseService db, ILogger<SmsService> logger, VoiceService voiceService)
        {
            _db = db;
            _logger = logger;
            _voiceService = voiceService;
        }

        public List<SerialPortInfo> GetAvailablePorts()
        {
            var ports = new List<SerialPortInfo>();
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%)'"))
                {
                    foreach (var port in searcher.Get())
                    {
                        var caption = port["Caption"]?.ToString();
                        if (caption != null)
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(caption, @"\((COM\d+)\)");
                            if (match.Success)
                            {
                                var info = new SerialPortInfo
                                {
                                    PortName = match.Groups[1].Value,
                                    DisplayName = caption
                                };
                                info.AvailableAudioDevices.AddRange(_voiceService.GetAudioDevices());
                                ports.Add(info);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to simple port names if WMI fails
                foreach (var name in SerialPort.GetPortNames())
                {
                    ports.Add(new SerialPortInfo { PortName = name, DisplayName = "Unknown Device" });
                }
            }
            
            // If no ports found via WMI but some exist
            if (ports.Count == 0)
            {
                foreach (var name in SerialPort.GetPortNames())
                {
                    ports.Add(new SerialPortInfo { PortName = name, DisplayName = "Serial Port" });
                }
            }

            // COM port raqami bo'yicha tartiblash (COM2, COM10 dan oldin chiqishi uchun)
            ports = ports.OrderBy(p => 
            {
                if (p.PortName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && 
                    int.TryParse(p.PortName.Substring(3), out int num))
                {
                    return num;
                }
                return int.MaxValue;
            }).ToList();

            return ports;
        }

        public async Task<bool> SendSmsAsync(string portName, string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            phoneNumber = NormalizePhoneNumber(phoneNumber);
            var baudRates = new[] { 115200, 9600 };
            foreach (var baud in baudRates)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                try
                {
                    return await Task.Run(() =>
                    {
                        using (var port = new SerialPort(portName))
                        {
                            port.BaudRate = baud;
                            port.DtrEnable = true;
                            port.RtsEnable = true;
                            port.Handshake = Handshake.RequestToSend;
                            port.ReadTimeout = 5000;
                            port.WriteTimeout = 5000;

                            port.Open();
                            
                            // Wake up
                            port.Write("\r\r");
                            Thread.Sleep(500);
                            if (cancellationToken.IsCancellationRequested) return false;
                            port.DiscardInBuffer();

                            // Set to Text Mode
                            port.Write("AT+CMGF=1\r");
                            Thread.Sleep(500);
                            if (cancellationToken.IsCancellationRequested) return false;
                            
                            // Set Recipient
                            port.Write($"AT+CMGS=\"{phoneNumber}\"\r");
                            Thread.Sleep(500);
                            if (cancellationToken.IsCancellationRequested) return false;

                            // Send Message Body and Ctrl+Z (ASCII 26)
                            port.Write(message + (char)26);
                            Thread.Sleep(3000);
                            if (cancellationToken.IsCancellationRequested) return false;

                            string response = port.ReadExisting();
                            port.Close();

                            return response.Contains("OK") || response.Contains("+CMGS:");
                        }
                    }, cancellationToken);
                }
                catch
                {
                    if (cancellationToken.IsCancellationRequested) return false;
                    continue; // Try next baud rate
                }
            }
            return false;
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(string portName)
        {
            var baudRates = new[] { 115200, 9600 };
            string lastError = "";

            foreach (var baud in baudRates)
            {
                try
                {
                    return await Task.Run(() =>
                    {
                        using (var port = new SerialPort(portName))
                        {
                            port.BaudRate = baud;
                            port.DtrEnable = true;
                            port.RtsEnable = true;
                            port.ReadTimeout = 3000;
                            port.WriteTimeout = 3000;

                            Thread.Sleep(500); // Small pause before opening
                            port.Open();
                            port.DiscardInBuffer();
                            
                            // Wake up sequence
                            port.Write("\r\r");
                            Thread.Sleep(500);
                            port.DiscardInBuffer();

                            // Try multiple times with different line endings
                            var endings = new[] { "\r", "\r\n" };
                            string allResponses = "";

                            foreach (var end in endings)
                            {
                                for (int i = 0; i < 2; i++)
                                {
                                    port.Write("AT" + end);
                                    Thread.Sleep(1000); // Increased delay
                                    string response = port.ReadExisting();
                                    allResponses += $"[{end.Replace("\r","\\r").Replace("\n","\\n")}]: " + (string.IsNullOrWhiteSpace(response) ? "(empty)" : response.Trim()) + "\n";
                                    
                                    if (response.ToUpper().Contains("OK"))
                                    {
                                        port.Close();
                                        return (true, $"Success at {baud} baud with {end.Replace("\r","\\r").Replace("\n","\\n")}.");
                                    }
                                }
                            }
                            
                            port.Close();
                            return (false, "Modem responses:\n" + allResponses);
                        }
                    });
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (ex.Message.Contains("denied")) break; // Don't retry other bauds if access is denied
                }
            }

            return (false, string.IsNullOrEmpty(lastError) ? "Modem did not respond." : "Error: " + lastError);
        }

        private string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            string cleaned = "";
            for (int i = 0; i < phone.Length; i++)
            {
                if (char.IsDigit(phone[i]) || (i == 0 && phone[i] == '+'))
                {
                    cleaned += phone[i];
                }
            }
            return cleaned;
        }

        public async Task<(bool Success, string Message)> ClearModemMemoryAsync(string portName)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (var port = new SerialPort(portName))
                    {
                        port.BaudRate = 115200;
                        port.DtrEnable = true;
                        port.RtsEnable = true;
                        port.ReadTimeout = 5000;
                        port.WriteTimeout = 5000;
                        port.Open();

                        // Select SIM storage
                        port.Write("AT+CPMS=\"SM\",\"SM\",\"SM\"\r");
                        Thread.Sleep(500);
                        string cpmsResponse = port.ReadExisting();

                        // Delete all messages: 1 is index (ignored with type 4), 4 means all messages
                        port.Write("AT+CMGD=1,4\r");
                        Thread.Sleep(1000);
                        string delResponse = port.ReadExisting();

                        port.Close();

                        if (delResponse.Contains("OK"))
                            return (true, "Memory cleared successfully.");
                        else
                            return (false, $"Failed to clear memory: {delResponse}");
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, $"Error clearing memory: {ex.Message}");
            }
        }
        public async Task ProcessTaskItemsAsync(Dictionary<string, (string DisplayName, string? AudioDevice)> portMap, List<SmsTaskItem> items, Action<int, string, string>? onProgressUpdate = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting ProcessTaskItemsAsync with {PortCount} ports and {ItemCount} items.", portMap.Count, items.Count);

            var queue = new System.Collections.Concurrent.ConcurrentQueue<SmsTaskItem>(items);
            var tasks = new List<Task>();

            foreach (var portName in portMap.Keys)
            {
                var displayName = portMap[portName];
                tasks.Add(Task.Run(async () =>
                {
                    _logger.LogInformation("Worker started for port {DisplayName}", displayName);
                    while (!cancellationToken.IsCancellationRequested && queue.TryDequeue(out var item))
                    {
                        bool success;
                        string message = "";

                        if (!string.IsNullOrEmpty(item.AudioPath) && string.IsNullOrEmpty(item.Message))
                        {
                            // Voice Task
                            var audioDevice = portMap[portName].AudioDevice;
                            var res = await MakeVoiceCallAsync(item.PhoneNumber, portName, item.AudioPath, audioDevice);
                            success = res.success;
                            message = res.status;
                        }
                        else
                        {
                            // SMS Task
                            success = await SendSmsAsync(portName, item.PhoneNumber, item.Message, cancellationToken);
                        }
                        
                        if (success) _logger.LogInformation("Successfully processed item to {PhoneNumber} via {DisplayName}", item.PhoneNumber, displayName);
                        else _logger.LogError("Failed to process item to {PhoneNumber} via {DisplayName}. Error: {Message}", item.PhoneNumber, displayName, message);

                        string status = success ? "Sent" : "Failed";
                        await _db.UpdateTaskItemStatusAsync(item.Id, status, !success, displayName.DisplayName);
                        
                        // History (Tarix) ga qo'shish
                        var contactId = await _db.GetContactIdByPhoneAsync(item.PhoneNumber);
                        if (contactId.HasValue)
                        {
                            await _db.AddHistoryAsync(contactId.Value, item.Message, status);
                        }
                        
                        onProgressUpdate?.Invoke(item.Id, status, displayName.DisplayName);

                        // Wait 10 seconds before next message on this port to prevent hardware spam
                        if (!queue.IsEmpty && !cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(10000, cancellationToken);
                        }
                    }
                    _logger.LogInformation("Worker finished for port {PortName}", portName);
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task<(bool success, string status)> MakeVoiceCallAsync(string phoneNumber, string portName, string audioPath, string? audioDeviceName = null)
        {
            return await Task.Run(() =>
            {
                using (var port = new SerialPort(portName, 115200))
                {
                    try
                    {
                        port.ReadTimeout = 3000;
                        port.WriteTimeout = 3000;
                        port.DtrEnable = true;
                        port.RtsEnable = true;
                        port.Open();

                        // Helper: Send AT command and read response
                        string SendAT(string cmd, int waitMs = 500)
                        {
                            port.DiscardInBuffer();
                            port.Write(cmd + "\r");
                            Thread.Sleep(waitMs);
                            return port.ReadExisting();
                        }

                        // 0. Initialize
                        SendAT("ATE0", 300);
                        SendAT("AT+CVHU=0", 200);
                        port.DiscardInBuffer();

                        // 1. Dial
                        _logger.LogInformation("Dialing {PhoneNumber} via {PortName}...", phoneNumber, portName);
                        port.DiscardInBuffer();
                        port.Write($"ATD{phoneNumber};\r");
                        
                        string dialResp = "";
                        for (int i = 0; i < 50; i++)
                        {
                            Thread.Sleep(100);
                            dialResp += port.ReadExisting();
                            if (dialResp.Contains("OK") || dialResp.Contains("ERROR"))
                                break;
                        }
                        _logger.LogInformation("Dial response: {R}", dialResp.Trim());

                        if (dialResp.Contains("ERROR"))
                        {
                            port.Close();
                            return (false, "Modem qo'ng'iroqni rad etdi.");
                        }

                        // 2. Wait for answer (AT+CLCC polling)
                        _logger.LogInformation("Waiting for answer...");
                        bool answered = false;

                        for (int i = 0; i < 90; i++) // max ~45 sec
                        {
                            Thread.Sleep(500);
                            port.DiscardInBuffer();
                            port.Write("AT+CLCC\r");
                            Thread.Sleep(300);
                            string resp = port.ReadExisting();

                            if (resp.Contains("+CLCC:"))
                            {
                                var clccLine = resp.Split('\n').FirstOrDefault(l => l.Contains("+CLCC:"));
                                if (clccLine != null)
                                {
                                    var parts = clccLine.Split(',');
                                    if (parts.Length > 2)
                                    {
                                        string state = parts[2].Trim();
                                        _logger.LogInformation("Call state: {State}", state);
                                        if (state == "0") { answered = true; break; }
                                        if (state == "6" || state == "7") break;
                                    }
                                }
                            }

                            if (resp.Contains("BUSY") || resp.Contains("NO ANSWER") || resp.Contains("NO CARRIER"))
                            {
                                _logger.LogWarning("Call rejected: {R}", resp.Trim());
                                break;
                            }
                        }

                        if (answered)
                        {
                            _logger.LogInformation("Call answered! Holding for audio duration...");

                            // Calculate audio duration
                            int durationMs = 30000; // default 30s
                            try
                            {
                                using (var reader = new NAudio.Wave.AudioFileReader(audioPath))
                                {
                                    durationMs = (int)reader.TotalTime.TotalMilliseconds;
                                }
                            }
                            catch { }
                            
                            _logger.LogInformation("Audio duration: {Ms}ms", durationMs);

                            // Try to play audio via Windows audio device (if modem has one)
                            bool audioPlayed = false;
                            if (!string.IsNullOrEmpty(audioDeviceName))
                            {
                                try
                                {
                                    _logger.LogInformation("Attempting audio playback via device: {Dev}", audioDeviceName);
                                    _voiceService.PlayAudioToDeviceAsync(audioPath, audioDeviceName).Wait();
                                    audioPlayed = true;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Audio playback failed, holding call for duration instead.");
                                }
                            }

                            if (!audioPlayed)
                            {
                                // No audio device — just hold the call for the audio duration
                                Thread.Sleep(durationMs);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Call was not answered.");
                        }

                        // 3. Hang up
                        _logger.LogInformation("Hanging up...");
                        SendAT("ATH", 1000);
                        port.Close();

                        return (answered, answered ? "Call completed." : "Call not answered.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Voice call error on {PortName}", portName);
                        try { if (port.IsOpen) { port.Write("ATH\r"); Thread.Sleep(500); port.Close(); } } catch { }
                        return (false, ex.Message);
                    }
                }
            });
        }
    }
}
