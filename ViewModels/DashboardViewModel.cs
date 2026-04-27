using System.Threading.Tasks;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Services;

namespace SmsGatewayApp.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public DashboardViewModel(DatabaseService db)
        {
            _db = db;
            _ = LoadStatsAsync();
        }

        private int _totalSent;
        public int TotalSent { get => _totalSent; set => SetProperty(ref _totalSent, value); }

        private int _totalFailed;
        public int TotalFailed { get => _totalFailed; set => SetProperty(ref _totalFailed, value); }

        private int _totalSms;
        public int TotalSms { get => _totalSms; set => SetProperty(ref _totalSms, value); }

        private int _groupsCount;
        public int GroupsCount { get => _groupsCount; set => SetProperty(ref _groupsCount, value); }

        private async Task LoadStatsAsync()
        {
            var stats = await _db.GetStatsAsync();
            
            GroupsCount = stats.ContainsKey("Groups") ? stats["Groups"] : 0;
            TotalSms = stats.ContainsKey("Contacts") ? stats["Contacts"] : 0;
            TotalSent = stats.ContainsKey("Sent") ? stats["Sent"] : 0;
            TotalFailed = stats.ContainsKey("Failed") ? stats["Failed"] : 0;
        }
    }
}
