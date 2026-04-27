using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;

using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace SmsGatewayApp.ViewModels
{
    public class SendingViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly SmsService _smsService;
        private CancellationTokenSource? _cts;

        public SendingViewModel(DatabaseService db, SmsService smsService)
        {
            _db = db;
            _smsService = smsService;

            StartSendingCommand = new AsyncRelayCommand(async _ => await StartSendingAsync(), _ => CanStartSending());
            CancelSendingCommand = new RelayCommand(_ => CancelSending(), _ => IsSending);
            RefreshPortsCommand = new RelayCommand(_ => LoadPorts());
            TestConnectionCommand = new AsyncRelayCommand(async _ => await TestConnectionAsync(), _ => SelectedPort != null);
            ClearModemMemoryCommand = new AsyncRelayCommand(async _ => await ClearModemMemoryAsync(), _ => SelectedPort != null && !IsBusy);

            LoadPorts();
            _ = LoadDataAsync();
        }

        #region Properties

        private bool _isSending;
        public bool IsSending { get => _isSending; set => SetProperty(ref _isSending, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private ExcelGroup? _selectedGroup;
        public ExcelGroup? SelectedGroup { get => _selectedGroup; set => SetProperty(ref _selectedGroup, value); }

        private SmsTemplate? _selectedTemplate;
        public SmsTemplate? SelectedTemplate { get => _selectedTemplate; set => SetProperty(ref _selectedTemplate, value); }

        private SerialPortInfo? _selectedPort;
        public SerialPortInfo? SelectedPort { get => _selectedPort; set => SetProperty(ref _selectedPort, value); }

        private int _maxRetries = 0;
        public int MaxRetries 
        { 
            get => _maxRetries; 
            set 
            {
                if (SetProperty(ref _maxRetries, value))
                    OnPropertyChanged(nameof(IsRetryEnabled));
            }
        }

        public bool IsRetryEnabled
        {
            get => MaxRetries > 0;
            set
            {
                if (value) { if (MaxRetries == 0) MaxRetries = 1; }
                else { MaxRetries = 0; }
                OnPropertyChanged(nameof(IsRetryEnabled));
            }
        }

        private int _progressValue;
        public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }

        private int _totalSent;
        public int TotalSent { get => _totalSent; set => SetProperty(ref _totalSent, value); }

        private int _totalFailed;
        public int TotalFailed { get => _totalFailed; set => SetProperty(ref _totalFailed, value); }

        private int _totalSkipped;
        public int TotalSkipped { get => _totalSkipped; set => SetProperty(ref _totalSkipped, value); }

        private int _totalSms;
        public int TotalSms { get => _totalSms; set => SetProperty(ref _totalSms, value); }

        private string _statusMessage = "Tayyor";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ObservableCollection<ExcelGroup> Groups { get; } = new();
        public ObservableCollection<SmsTemplate> Templates { get; } = new();
        public ObservableCollection<SerialPortInfo> AvailablePorts { get; } = new();
        public ObservableCollection<SmsLogEntry> SendingLog { get; } = new();

        #endregion

        #region Commands

        public ICommand StartSendingCommand { get; }
        public ICommand CancelSendingCommand { get; }
        public ICommand RefreshPortsCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand ClearModemMemoryCommand { get; }

        #endregion

        #region Methods

        private async Task LoadDataAsync()
        {
            var groups = await _db.GetGroupsAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);

            var templates = await _db.GetTemplatesAsync();
            Templates.Clear();
            foreach (var t in templates) Templates.Add(t);
        }

        private void LoadPorts()
        {
            AvailablePorts.Clear();
            foreach (var p in _smsService.GetAvailablePorts()) AvailablePorts.Add(p);
            if (AvailablePorts.Any()) SelectedPort = AvailablePorts[0];
        }

        private bool CanStartSending() => SelectedGroup != null && SelectedTemplate != null && SelectedPort != null && !IsSending;

        private async Task StartSendingAsync()
        {
            if (SelectedGroup == null || SelectedTemplate == null || SelectedPort == null) return;

            IsSending = true;
            ProgressValue = 0; TotalSent = 0; TotalFailed = 0; TotalSkipped = 0;
            SendingLog.Clear();
            StatusMessage = "Kontaktlar yuklanmoqda...";
            _cts = new CancellationTokenSource();

            var contacts = await _db.GetContactsByGroupAsync(SelectedGroup.Id);
            TotalSms = contacts.Count;
            int current = 0;

            foreach (var contact in contacts)
            {
                if (_cts.Token.IsCancellationRequested) break;

                string phone = contact.Phone.StartsWith("+") ? contact.Phone : "+" + contact.Phone;
                string message = InterpolateMessage(SelectedTemplate.MessageBody, contact);

                if (await _db.IsBlacklistedAsync(contact.Phone))
                {
                    Application.Current.Dispatcher.Invoke(() => SendingLog.Insert(0, new SmsLogEntry { Phone = phone, Name = contact.Name, Status = SmsLogStatus.Skipped }));
                    TotalSkipped++; current++;
                    ProgressValue = (int)((double)current / TotalSms * 100);
                    continue;
                }

                bool success = false; int attempt = 0;
                var logEntry = new SmsLogEntry { Phone = phone, Name = contact.Name, Status = SmsLogStatus.Sending };
                Application.Current.Dispatcher.Invoke(() => SendingLog.Insert(0, logEntry));

                while (!success && attempt <= MaxRetries && !_cts.Token.IsCancellationRequested)
                {
                    attempt++;
                    logEntry.AttemptNumber = attempt;
                    logEntry.Status = attempt == 1 ? SmsLogStatus.Sending : SmsLogStatus.Retrying;
                    StatusMessage = attempt == 1 ? $"Yuborilmoqda: {phone}" : $"Qayta urinish #{attempt}: {phone}";
                    success = await _smsService.SendSmsAsync(SelectedPort.PortName, phone, message, _cts.Token);
                    if (!success && attempt <= MaxRetries) await Task.Delay(3000, _cts.Token).ContinueWith(_ => { });
                }

                if (_cts.Token.IsCancellationRequested) logEntry.Status = SmsLogStatus.Cancelled;
                else if (success) { logEntry.Status = SmsLogStatus.Sent; TotalSent++; }
                else { logEntry.Status = SmsLogStatus.Failed; TotalFailed++; }

                await _db.AddHistoryAsync(contact.Id, message, logEntry.Status.ToString());
                current++;
                ProgressValue = (int)((double)current / TotalSms * 100);
            }

            IsSending = false;
            StatusMessage = _cts.Token.IsCancellationRequested ? "Bekor qilindi." : "Yakunlandi.";
            WindowsNotificationHelper.ShowSuccess($"SMS yuborish yakunlandi: {TotalSent} ta yuborildi.");
            _cts.Dispose(); _cts = null;
        }

        private string InterpolateMessage(string template, SmsContact contact)
        {
            return template
                .Replace("{name}", contact.Name ?? contact.Phone)
                .Replace("{phone}", contact.Phone)
                .Replace("{group}", SelectedGroup?.Name ?? string.Empty)
                .Replace("{date}", DateTime.Now.ToString("dd.MM.yyyy"))
                .Replace("{time}", DateTime.Now.ToString("HH:mm"));
        }

        private void CancelSending() { _cts?.Cancel(); IsSending = false; StatusMessage = "Bekor qilinmoqda..."; }

        private async Task TestConnectionAsync()
        {
            if (SelectedPort == null || IsBusy) return;
            IsBusy = true; StatusMessage = $"Test: {SelectedPort.PortName}...";
            var result = await _smsService.TestConnectionAsync(SelectedPort.PortName);
            StatusMessage = result.Success ? $"{SelectedPort.PortName} TAYYOR." : "Test XATO.";
            MessageBox.Show(result.Message, result.Success ? "Muvaffaqiyat" : "Xatolik", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            IsBusy = false;
        }

        private async Task ClearModemMemoryAsync()
        {
            if (SelectedPort == null) return;
            IsBusy = true; StatusMessage = "Tozalanmoqda...";
            var result = await _smsService.ClearModemMemoryAsync(SelectedPort.PortName);
            StatusMessage = result.Message; IsBusy = false;
        }

        #endregion
    }
}
