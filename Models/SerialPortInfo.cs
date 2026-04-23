namespace SmsGatewayApp.Models
{
    public class SerialPortInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DisplayName => $"{PortName} - {Description}";
    }
}
