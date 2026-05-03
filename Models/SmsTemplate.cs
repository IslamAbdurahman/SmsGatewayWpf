namespace SmsGatewayApp.Models
{
    public class SmsTemplate
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
        public string? AudioPath { get; set; }
    }
}
