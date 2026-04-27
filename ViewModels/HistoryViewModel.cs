using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;

namespace SmsGatewayApp.ViewModels
{
    public class HistoryViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public HistoryViewModel(DatabaseService db)
        {
            _db = db;
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadHistoryAsync());
            _ = LoadHistoryAsync();
        }

        public ObservableCollection<SmsHistoryEntry> History { get; } = new();

        public ICommand RefreshCommand { get; }

        private async Task LoadHistoryAsync()
        {
            var items = await _db.GetAllHistoryAsync();
            History.Clear();
            foreach (var item in items)
                History.Add(item);
        }
    }
}
