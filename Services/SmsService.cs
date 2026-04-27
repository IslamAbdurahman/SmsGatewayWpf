using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SmsGatewayApp.Models;

namespace SmsGatewayApp.Services
{
    public class SmsService
    {

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
                                ports.Add(new SerialPortInfo
                                {
                                    PortName = match.Groups[1].Value,
                                    Description = caption.Replace($"({match.Groups[1].Value})", "").Trim()
                                });
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
                    ports.Add(new SerialPortInfo { PortName = name, Description = "Unknown Device" });
                }
            }
            
            // If no ports found via WMI but some exist
            if (ports.Count == 0)
            {
                foreach (var name in SerialPort.GetPortNames())
                {
                    ports.Add(new SerialPortInfo { PortName = name, Description = "Serial Port" });
                }
            }

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
    }
}
