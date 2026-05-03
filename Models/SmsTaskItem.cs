using System;
using SmsGatewayApp.Helpers;

namespace SmsGatewayApp.Models
{
    public class SmsTaskItem : ObservableObject
    {
        private int _id;
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        private int _taskId;
        public int TaskId { get => _taskId; set => SetProperty(ref _taskId, value); }

        private string _phoneNumber = string.Empty;
        public string PhoneNumber { get => _phoneNumber; set => SetProperty(ref _phoneNumber, value); }

        private string _message = string.Empty;
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        private string _status = "Pending";
        public string Status { get => _status; set => SetProperty(ref _status, value); } // Pending, Sent, Failed

        private int _retryCount;
        public int RetryCount { get => _retryCount; set => SetProperty(ref _retryCount, value); }

        private DateTime? _lastAttempt;
        public DateTime? LastAttempt { get => _lastAttempt; set => SetProperty(ref _lastAttempt, value); }

        private string? _portName;
        public string? PortName { get => _portName; set => SetProperty(ref _portName, value); }

        private string? _audioPath;
        public string? AudioPath { get => _audioPath; set => SetProperty(ref _audioPath, value); }
    }
}
