using System;

namespace SmsGatewayApp.Models
{
    public class BlacklistEntry
    {
        public int Id { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
