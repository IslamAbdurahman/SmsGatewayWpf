using System;

namespace SmsGatewayApp.Models
{
    public class ExcelGroup
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
