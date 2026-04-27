using System;

namespace SmsGatewayApp.Models
{
    public class SmsHistoryEntry
    {
        public int Id { get; set; }
        public int ContactId { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        
        public string DisplayName => string.IsNullOrWhiteSpace(ContactName) ? ContactPhone : $"{ContactName} ({ContactPhone})";
    }
}
