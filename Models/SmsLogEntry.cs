using System;

namespace SmsGatewayApp.Models
{
    public enum SmsLogStatus
    {
        Sending,
        Sent,
        Failed,
        Retrying,
        Cancelled,
        Skipped
    }

    public class SmsLogEntry
    {
        public string Phone { get; set; } = string.Empty;
        public string? Name { get; set; }
        public SmsLogStatus Status { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public string? ErrorMessage { get; set; }
        public int AttemptNumber { get; set; } = 1;

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Phone : $"{Name} ({Phone})";

        public string StatusIcon => Status switch
        {
            SmsLogStatus.Sent => "✅",
            SmsLogStatus.Failed => "❌",
            SmsLogStatus.Sending => "⏳",
            SmsLogStatus.Retrying => "🔄",
            SmsLogStatus.Cancelled => "⛔",
            SmsLogStatus.Skipped => "⏭",
            _ => "❓"
        };

        public string StatusText => Status switch
        {
            SmsLogStatus.Sent => "Yuborildi",
            SmsLogStatus.Failed => "Xatolik",
            SmsLogStatus.Sending => "Yuborilmoqda...",
            SmsLogStatus.Retrying => $"Qayta urinish #{AttemptNumber}",
            SmsLogStatus.Cancelled => "Bekor qilindi",
            SmsLogStatus.Skipped => "O'tkazib yuborildi (Blacklist)",
            _ => "Noma'lum"
        };
    }
}
