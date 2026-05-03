using System;
using System.Collections.Generic;
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
    public class TasksViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly SmsService _smsService;
        private readonly VoiceService _voiceService;
        private CancellationTokenSource? _cts;

        public TasksViewModel(DatabaseService db, SmsService smsService, VoiceService voiceService)
        {
            _db = db;
            _smsService = smsService;
            _voiceService = voiceService;

            LoadTasksCommand = new AsyncRelayCommand(async _ => await LoadTasksAsync());
            SelectTaskCommand = new AsyncRelayCommand(async p => { if (p is SmsTask t) await SelectTaskAsync(t); });
            StartProcessingCommand = new AsyncRelayCommand(async _ => await StartProcessingAsync(), _ => SelectedTask != null && !IsProcessing);
            CancelProcessingCommand = new RelayCommand(_ => CancelProcessing(), _ => IsProcessing);
            TestAudioCommand = new AsyncRelayCommand(async p => await TestAudioAsync(p as SerialPortInfo));
            
            LoadPorts();
            _ = LoadTasksAsync();
        }

        #region Properties

        private bool _isProcessing;
        public bool IsProcessing 
        { 
            get => _isProcessing; 
            set 
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(IsSelectedTaskProcessing));
                }
            } 
        }

        private int? _processingTaskId;
        public int? ProcessingTaskId 
        { 
            get => _processingTaskId; 
            set 
            {
                if (SetProperty(ref _processingTaskId, value))
                {
                    OnPropertyChanged(nameof(IsSelectedTaskProcessing));
                }
            } 
        }

        public bool IsSelectedTaskProcessing => IsProcessing && SelectedTask != null && SelectedTask.Id == ProcessingTaskId;

        private ObservableCollection<SmsTask> _tasks = new();
        public ObservableCollection<SmsTask> Tasks { get => _tasks; set => SetProperty(ref _tasks, value); }

        private SmsTask? _selectedTask;
        public SmsTask? SelectedTask 
        { 
            get => _selectedTask; 
            set 
            {
                if (SetProperty(ref _selectedTask, value))
                {
                    OnPropertyChanged(nameof(IsSelectedTaskProcessing));
                    _ = RefreshItemsAsync();
                }
            } 
        }

        private ObservableCollection<SmsTaskItem> _taskItems = new();
        public ObservableCollection<SmsTaskItem> TaskItems { get => _taskItems; set => SetProperty(ref _taskItems, value); }

        private string _statusFilter = "All";
        public string StatusFilter 
        { 
            get => _statusFilter; 
            set 
            { 
                if (value != null && SetProperty(ref _statusFilter, value))
                    _ = RefreshItemsAsync();
            } 
        }

        public ObservableCollection<SerialPortInfo> AvailablePorts { get; } = new();

        private string _statusMessage = "Tayyor";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        #endregion

        #region Commands

        public ICommand LoadTasksCommand { get; }
        public ICommand SelectTaskCommand { get; }
        public ICommand StartProcessingCommand { get; }
        public ICommand CancelProcessingCommand { get; }
        public ICommand TestAudioCommand { get; }

        #endregion

        #region Methods

        private async Task LoadTasksAsync()
        {
            var oldSelectedId = SelectedTask?.Id;

            var tasks = await _db.GetSmsTasksAsync();
            Tasks.Clear();
            foreach (var t in tasks) Tasks.Add(t);

            if (oldSelectedId.HasValue)
            {
                SelectedTask = Tasks.FirstOrDefault(t => t.Id == oldSelectedId.Value);
            }
        }

        private void LoadPorts()
        {
            AvailablePorts.Clear();
            foreach (var p in _smsService.GetAvailablePorts()) AvailablePorts.Add(p);
            if (AvailablePorts.Any()) AvailablePorts[0].IsSelected = true;
        }

        private async Task SelectTaskAsync(SmsTask task)
        {
            SelectedTask = task;
            await RefreshItemsAsync();
        }

        private async Task RefreshItemsAsync()
        {
            if (SelectedTask == null) return;
            string? filter = StatusFilter == "All" ? null : StatusFilter;
            var items = await _db.GetSmsTaskItemsAsync(SelectedTask.Id, filter);
            TaskItems.Clear();
            foreach (var item in items) TaskItems.Add(item);
        }

        private async Task StartProcessingAsync()
        {
            if (SelectedTask == null) return;
            
            var selectedPortsMap = AvailablePorts
                .Where(p => p.IsSelected)
                .ToDictionary(p => p.PortName, p => p.DisplayName);

            if (!selectedPortsMap.Any())
            {
                MessageBox.Show("Iltimos, SMS yuborish uchun kamida bitta portni tanlang!", "Port tanlanmagan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProcessingTaskId = SelectedTask.Id;
            IsProcessing = true;
            _cts = new CancellationTokenSource();
            StatusMessage = "Yuborilmoqda...";

            var itemsToProcess = TaskItems.Where(i => i.Status != "Sent").ToList();
            if (!itemsToProcess.Any())
            {
                MessageBox.Show("Yuborish uchun xabarlar yo'q (yoki barchasi yuborilgan).", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
                IsProcessing = false;
                ProcessingTaskId = null;
                return;
            }

            var portMap = AvailablePorts
                .Where(p => p.IsSelected)
                .ToDictionary(p => p.PortName, p => (p.DisplayName, p.AudioDeviceName));

            await _smsService.ProcessTaskItemsAsync(portMap, itemsToProcess, (itemId, status, port) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var item = TaskItems.FirstOrDefault(i => i.Id == itemId);
                    if (item != null)
                    {
                        item.Status = status;
                        item.PortName = port;
                    }
                });
            }, _cts.Token);

            await _db.UpdateTaskStatusAsync(SelectedTask.Id, "Completed"); // Or check if all items are sent
            
            IsProcessing = false;
            ProcessingTaskId = null;
            StatusMessage = _cts.Token.IsCancellationRequested ? "Bekor qilindi" : "Yakunlandi";
            await RefreshItemsAsync();
            await LoadTasksAsync();
        }

        private async Task TestAudioAsync(SerialPortInfo? port)
        {
            if (port == null || string.IsNullOrEmpty(port.AudioDeviceName))
            {
                MessageBox.Show("Iltimos, avval audio qurilmani tanlang.");
                return;
            }

            // Get a sample audio file from templates or a dummy one
            var templates = await _db.GetTemplatesAsync();
            var audioPath = templates.FirstOrDefault(t => !string.IsNullOrEmpty(t.AudioPath))?.AudioPath;

            if (string.IsNullOrEmpty(audioPath) || !System.IO.File.Exists(audioPath))
            {
                MessageBox.Show("Sinab ko'rish uchun kamida bitta shablonga audio yuklangan bo'lishi kerak.");
                return;
            }

            StatusMessage = $"Audio tekshirilmoqda: {port.AudioDeviceName}...";
            try
            {
                await _voiceService.PlayAudioToDeviceAsync(audioPath, port.AudioDeviceName);
                MessageBox.Show("Audio ijro etildi. Agar ovoz eshitilmagan bo'lsa, boshqa qurilmani tanlab ko'ring.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xatolik: {ex.Message}");
            }
            StatusMessage = "Tayyor";
        }

        private void CancelProcessing()
        {
            _cts?.Cancel();
        }

        #endregion
    }
}
