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

        private bool CanStartSending() => SelectedGroup != null && SelectedTemplate != null && (!string.IsNullOrWhiteSpace(SelectedTemplate.MessageBody) || !string.IsNullOrWhiteSpace(SelectedTemplate.AudioPath)) && SelectedPort != null && !IsSending;

        private async Task StartSendingAsync()
        {
            if (SelectedGroup == null || SelectedTemplate == null || SelectedPort == null) return;

            IsSending = true;
            StatusMessage = "Vazifa yaratilmoqda...";

            var contacts = await _db.GetContactsByGroupAsync(SelectedGroup.Id);
            if (!contacts.Any())
            {
                MessageBox.Show("Guruhda kontaktlar mavjud emas.", "Xato", MessageBoxButton.OK, MessageBoxImage.Warning);
                IsSending = false;
                return;
            }

            // Create Task
            string taskTitle = $"{SelectedGroup.Name} - {SelectedTemplate.Title} ({DateTime.Now:g})";
            int taskId = await _db.CreateSmsTaskAsync(taskTitle);

            // Prepare Items
            var items = new List<(string Phone, string Message, string? AudioPath)>();
            foreach (var contact in contacts)
            {
                if (await _db.IsBlacklistedAsync(contact.Phone)) continue;

                string message = string.IsNullOrEmpty(SelectedTemplate.MessageBody) ? string.Empty : InterpolateMessage(SelectedTemplate.MessageBody, contact);
                items.Add((contact.Phone, message, SelectedTemplate.AudioPath));
            }

            if (items.Any())
            {
                await _db.BulkInsertTaskItemsAsync(taskId, items);
                StatusMessage = "Vazifa muvaffaqiyatli yaratildi.";
                
                var result = MessageBox.Show("Vazifa yaratildi. Vazifalar oynasiga o'tishni xohlaysizmi?", "Muvaffaqiyat", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // Find MainViewModel and navigate
                    if (Application.Current.MainWindow.DataContext is MainViewModel mainVm)
                    {
                        mainVm.Navigate(Constants.ViewNames.Tasks);
                    }
                }
            }
            else
            {
                MessageBox.Show("Yuborish uchun yaroqli kontaktlar topilmadi (balki barchasi qora ro'yxatdadir).", "Xato", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            IsSending = false;
            StatusMessage = "Tayyor";
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

        private void CancelSending() { IsSending = false; StatusMessage = "Bekor qilinmoqda..."; }

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
