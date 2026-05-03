using System.Collections.Generic;
using SmsGatewayApp.Helpers;

namespace SmsGatewayApp.Models
{
    public class SerialPortInfo : ObservableObject
    {
        public string PortName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string? _audioDeviceName;
        public string? AudioDeviceName { get => _audioDeviceName; set => SetProperty(ref _audioDeviceName, value); }

        public List<string> AvailableAudioDevices { get; set; } = new();
    }
}
