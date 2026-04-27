using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;

using MessageBox = System.Windows.MessageBox;

namespace SmsGatewayApp.ViewModels
{
    public class BlacklistViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public BlacklistViewModel(DatabaseService db)
        {
            _db = db;
            LoadBlacklistCommand = new AsyncRelayCommand(async _ => await LoadBlacklistAsync());
            RemoveBlacklistCommand = new AsyncRelayCommand(async p => await RemoveFromBlacklistAsync(p as BlacklistEntry));
            AddPhoneToBlacklistCommand = new AsyncRelayCommand(async _ => await AddPhoneToBlacklistAsync(), _ => !string.IsNullOrWhiteSpace(BlacklistPhone));

            _ = LoadBlacklistAsync();
        }

        public ObservableCollection<BlacklistEntry> Blacklist { get; } = new();

        private string _blacklistPhone = string.Empty;
        public string BlacklistPhone { get => _blacklistPhone; set => SetProperty(ref _blacklistPhone, value); }

        private string _blacklistReason = string.Empty;
        public string BlacklistReason { get => _blacklistReason; set => SetProperty(ref _blacklistReason, value); }

        public ICommand LoadBlacklistCommand { get; }
        public ICommand RemoveBlacklistCommand { get; }
        public ICommand AddPhoneToBlacklistCommand { get; }

        private async Task LoadBlacklistAsync()
        {
            var list = await _db.GetBlacklistAsync();
            Blacklist.Clear();
            foreach (var b in list) Blacklist.Add(b);
        }

        private async Task AddPhoneToBlacklistAsync()
        {
            if (string.IsNullOrWhiteSpace(BlacklistPhone)) return;
            await _db.AddToBlacklistAsync(BlacklistPhone, BlacklistReason);
            BlacklistPhone = string.Empty;
            BlacklistReason = string.Empty;
            await LoadBlacklistAsync();
        }

        private async Task RemoveFromBlacklistAsync(BlacklistEntry? entry)
        {
            if (entry == null) return;
            await _db.RemoveFromBlacklistAsync(entry.Id);
            await LoadBlacklistAsync();
        }
    }
}
