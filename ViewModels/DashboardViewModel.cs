using System.Collections.Generic;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
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

        private ISeries[] _smsStatusSeries = new ISeries[0];
        public ISeries[] SmsStatusSeries { get => _smsStatusSeries; set => SetProperty(ref _smsStatusSeries, value); }

        private async Task LoadStatsAsync()
        {
            var stats = await _db.GetStatsAsync();
            
            GroupsCount = stats.ContainsKey("Groups") ? stats["Groups"] : 0;
            TotalSms = stats.ContainsKey("Contacts") ? stats["Contacts"] : 0;
            TotalSent = stats.ContainsKey("Sent") ? stats["Sent"] : 0;
            TotalFailed = stats.ContainsKey("Failed") ? stats["Failed"] : 0;
            
            int pending = TotalSms - (TotalSent + TotalFailed);
            if (pending < 0) pending = 0;

            SmsStatusSeries = new ISeries[]
            {
                new PieSeries<int> { Values = new[] { TotalSent }, Name = "Sent", Fill = new SolidColorPaint(SKColors.SpringGreen) },
                new PieSeries<int> { Values = new[] { TotalFailed }, Name = "Failed", Fill = new SolidColorPaint(SKColors.Tomato) },
                new PieSeries<int> { Values = new[] { pending }, Name = "Pending", Fill = new SolidColorPaint(SKColors.Gray) }
            };
        }
    }
}
