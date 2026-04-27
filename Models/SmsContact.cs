namespace SmsGatewayApp.Models
{
    public class SmsContact
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? Name { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Phone : $"{Name} ({Phone})";
    }
}
