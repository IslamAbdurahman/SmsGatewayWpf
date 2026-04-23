using System;

namespace SmsGatewayApp.Models
{
    public class SmsHistoryEntry
    {
        public int Id { get; set; }
        public int ContactId { get; set; }
        public string MessageBody { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }
}
