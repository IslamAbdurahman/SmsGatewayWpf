using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;

namespace SmsGatewayApp.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly ExcelService _excelService;
        private readonly SmsService _smsService;

        public MainViewModel()
        {
            _dbService = new DatabaseService();
            _excelService = new ExcelService();
            _smsService = new SmsService();

            // Commands
            ImportExcelCommand = new RelayCommand(async _ => await ImportExcel());
            StartSendingCommand = new RelayCommand(async _ => await StartSending(), _ => CanStartSending());
            RefreshPortsCommand = new RelayCommand(_ => LoadPorts());
            SaveTemplateCommand = new RelayCommand(async _ => await SaveTemplate(), _ => CanSaveTemplate());
            NavigateCommand = new RelayCommand(p => CurrentView = p?.ToString() ?? "Dashboard");
            CreateGroupCommand = new RelayCommand(async _ => await CreateGroup(), _ => !string.IsNullOrWhiteSpace(NewGroupName));
            DeleteGroupCommand = new RelayCommand(async p => await DeleteGroup(p as ExcelGroup));
            AddContactCommand = new RelayCommand(async _ => await AddContact(), _ => SelectedGroup != null && !string.IsNullOrWhiteSpace(NewContactPhone));
            DeleteContactCommand = new RelayCommand(async p => await DeleteContact(p as SmsContact));
            TestConnectionCommand = new RelayCommand(async _ => await TestConnection(), _ => SelectedPort != null);
            ClearModemMemoryCommand = new RelayCommand(async _ => await ClearModemMemory(), _ => SelectedPort != null && !IsBusy);
            DeleteTemplateCommand = new RelayCommand(async p => await DeleteTemplate(p as SmsTemplate));
            EditGroupCommand = new RelayCommand(p => StartEditGroup(p as ExcelGroup));
            EditContactCommand = new RelayCommand(p => StartEditContact(p as SmsContact));
            EditTemplateCommand = new RelayCommand(p => StartEditTemplate(p as SmsTemplate));
            ViewHistoryCommand = new RelayCommand(async p => await ViewHistory(p as SmsContact));
            DeleteHistoryCommand = new RelayCommand(async p => await DeleteHistory(p as SmsHistoryEntry));
            CloseHistoryCommand = new RelayCommand(_ => ShowHistory = false);

            // Initial Data
            LoadData();
            LoadPorts();
            CurrentView = "Dashboard";
        }

        #region Properties

        private string _currentView = "Dashboard";
        public string CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ObservableCollection<ExcelGroup> Groups { get; } = new();
        public ObservableCollection<SmsTemplate> Templates { get; } = new();
        public ObservableCollection<SerialPortInfo> AvailablePorts { get; } = new();

        private ExcelGroup? _selectedGroup;
        public ExcelGroup? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                {
                    _ = LoadSelectedGroupContacts();
                }
            }
        }

        private SmsTemplate? _selectedTemplate;
        public SmsTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set => SetProperty(ref _selectedTemplate, value);
        }

        private SmsContact? _selectedContact;
        public SmsContact? SelectedContact
        {
            get => _selectedContact;
            set => SetProperty(ref _selectedContact, value);
        }

        private bool _showHistory;
        public bool ShowHistory
        {
            get => _showHistory;
            set => SetProperty(ref _showHistory, value);
        }

        public ObservableCollection<SmsHistoryEntry> ContactHistory { get; } = new();

        private SerialPortInfo? _selectedPort;
        public SerialPortInfo? SelectedPort
        {
            get => _selectedPort;
            set => SetProperty(ref _selectedPort, value);
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private int _totalSms;
        public int TotalSms
        {
            get => _totalSms;
            set => SetProperty(ref _totalSms, value);
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set => SetProperty(ref _isSending, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _newTemplateTitle = string.Empty;
        public string NewTemplateTitle
        {
            get => _newTemplateTitle;
            set => SetProperty(ref _newTemplateTitle, value);
        }

        private string _newTemplateBody = string.Empty;
        public string NewTemplateBody
        {
            get => _newTemplateBody;
            set => SetProperty(ref _newTemplateBody, value);
        }

        private string _newGroupName = string.Empty;
        public string NewGroupName
        {
            get => _newGroupName;
            set => SetProperty(ref _newGroupName, value);
        }

        private string _newContactPhone = string.Empty;
        public string NewContactPhone
        {
            get => _newContactPhone;
            set => SetProperty(ref _newContactPhone, value);
        }

        private ExcelGroup? _editingGroup;
        public ExcelGroup? EditingGroup
        {
            get => _editingGroup;
            set => SetProperty(ref _editingGroup, value);
        }

        private SmsContact? _editingContact;
        public SmsContact? EditingContact
        {
            get => _editingContact;
            set => SetProperty(ref _editingContact, value);
        }

        private SmsTemplate? _editingTemplate;
        public SmsTemplate? EditingTemplate
        {
            get => _editingTemplate;
            set => SetProperty(ref _editingTemplate, value);
        }

        private string? _newContactName;
        public string? NewContactName
        {
            get => _newContactName;
            set => SetProperty(ref _newContactName, value);
        }

        public ObservableCollection<SmsContact> SelectedGroupContacts { get; } = new();

        // Statistics
        private int _totalSent;
        public int TotalSent { get => _totalSent; set => SetProperty(ref _totalSent, value); }

        private int _totalFailed;
        public int TotalFailed { get => _totalFailed; set => SetProperty(ref _totalFailed, value); }

        #endregion

        #region Commands
        public RelayCommand ImportExcelCommand { get; }
        public RelayCommand StartSendingCommand { get; }
        public RelayCommand RefreshPortsCommand { get; }
        public RelayCommand SaveTemplateCommand { get; }
        public RelayCommand NavigateCommand { get; }
        public RelayCommand CreateGroupCommand { get; }
        public RelayCommand DeleteGroupCommand { get; }
        public RelayCommand AddContactCommand { get; }
        public RelayCommand DeleteContactCommand { get; }
        public RelayCommand TestConnectionCommand { get; }
        public RelayCommand ClearModemMemoryCommand { get; }
        public RelayCommand DeleteTemplateCommand { get; }
        public RelayCommand EditGroupCommand { get; }
        public RelayCommand EditContactCommand { get; }
        public RelayCommand EditTemplateCommand { get; }
        public RelayCommand ViewHistoryCommand { get; }
        public RelayCommand DeleteHistoryCommand { get; }
        public RelayCommand CloseHistoryCommand { get; }
        #endregion

        #region Methods

        private async void LoadData()
        {
            await _dbService.InitializeDatabaseAsync();
            var groups = await _dbService.GetGroupsAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);

            await LoadTemplates();
            
            TotalSent = 0;
            TotalFailed = 0;
        }

        private async Task LoadTemplates()
        {
            var templates = await _dbService.GetTemplatesAsync();
            Templates.Clear();
            foreach (var t in templates) Templates.Add(t);
        }

        private void LoadPorts()
        {
            AvailablePorts.Clear();
            foreach (var p in _smsService.GetAvailablePorts()) AvailablePorts.Add(p);
            if (AvailablePorts.Any()) SelectedPort = AvailablePorts[0];
        }

        private async Task ImportExcel()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                StatusMessage = "Reading Excel...";
                var phones = await Task.Run(() => _excelService.ReadPhoneNumbers(openFileDialog.FileName));
                
                if (phones.Any())
                {
                    StatusMessage = "Saving to database...";
                    
                    int groupId;
                    if (SelectedGroup != null)
                    {
                        groupId = SelectedGroup.Id;
                    }
                    else
                    {
                        groupId = await _dbService.InsertExcelGroupAsync(System.IO.Path.GetFileName(openFileDialog.FileName));
                    }

                    await _dbService.BulkInsertContactsAsync(groupId, phones);
                    
                    if (SelectedGroup == null)
                    {
                        LoadData();
                    }
                    else
                    {
                        await LoadSelectedGroupContacts();
                    }

                    StatusMessage = $"Imported {phones.Count} contacts.";
                    MessageBox.Show($"Successfully imported {phones.Count} contacts.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async Task CreateGroup()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName)) return;

            if (EditingGroup != null)
            {
                await _dbService.UpdateGroupAsync(EditingGroup.Id, NewGroupName);
                EditingGroup = null;
                StatusMessage = "Group updated.";
            }
            else
            {
                await _dbService.InsertExcelGroupAsync(NewGroupName);
                StatusMessage = "Group created.";
            }
            
            LoadData();
            NewGroupName = string.Empty;
        }

        private void StartEditGroup(ExcelGroup? group)
        {
            if (group == null) return;
            EditingGroup = group;
            NewGroupName = group.Name;
        }

        private async Task DeleteGroup(ExcelGroup? group)
        {
            try
            {
                if (group == null) return;
                if (MessageBox.Show($"Are you sure you want to delete group '{group.Name}' and all its contacts?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await _dbService.DeleteGroupAsync(group.Id);
                    LoadData();
                    if (SelectedGroup?.Id == group.Id) SelectedGroup = null;
                    StatusMessage = "Group deleted.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddContact()
        {
            if (SelectedGroup == null || string.IsNullOrWhiteSpace(NewContactPhone)) return;

            try
            {
                if (EditingContact != null)
                {
                    await _dbService.UpdateContactAsync(EditingContact.Id, NewContactPhone, NewContactName);
                    EditingContact = null;
                    StatusMessage = "Contact updated.";
                }
                else
                {
                    await _dbService.InsertContactAsync(SelectedGroup.Id, NewContactPhone, NewContactName);
                    StatusMessage = "Contact added.";
                }

                await LoadSelectedGroupContacts();
                NewContactPhone = string.Empty;
                NewContactName = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving contact: {ex.Message}. Make sure the phone number is unique.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartEditContact(SmsContact? contact)
        {
            if (contact == null) return;
            EditingContact = contact;
            NewContactPhone = contact.Phone;
            NewContactName = contact.Name;
        }

        private async Task DeleteContact(SmsContact? contact)
        {
            try
            {
                if (contact == null) return;
                if (MessageBox.Show($"Are you sure you want to delete contact '{contact.Phone}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _dbService.DeleteContactAsync(contact.Id);
                    await LoadSelectedGroupContacts();
                    StatusMessage = "Contact deleted.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting contact: {ex.Message}\n\nNote: If this contact has history, it cannot be deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveTemplate()
        {
            if (string.IsNullOrWhiteSpace(NewTemplateTitle) || string.IsNullOrWhiteSpace(NewTemplateBody)) return;

            if (EditingTemplate != null)
            {
                await _dbService.UpdateTemplateAsync(EditingTemplate.Id, NewTemplateTitle, NewTemplateBody);
                EditingTemplate = null;
                StatusMessage = "Template updated.";
            }
            else
            {
                await _dbService.SaveTemplateAsync(NewTemplateTitle, NewTemplateBody);
                StatusMessage = "Template saved.";
            }

            NewTemplateTitle = string.Empty;
            NewTemplateBody = string.Empty;
            await LoadTemplates();
        }

        private void StartEditTemplate(SmsTemplate? template)
        {
            if (template == null) return;
            EditingTemplate = template;
            NewTemplateTitle = template.Title;
            NewTemplateBody = template.MessageBody;
        }

        private async Task DeleteTemplate(SmsTemplate? template)
        {
            try
            {
                if (template == null) return;
                if (MessageBox.Show($"Are you sure you want to delete template '{template.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _dbService.DeleteTemplateAsync(template.Id);
                    await LoadTemplates();
                    StatusMessage = "Template deleted.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSelectedGroupContacts()
        {
            try
            {
                if (SelectedGroup == null)
                {
                    SelectedGroupContacts.Clear();
                    return;
                }

                var contacts = await _dbService.GetContactsByGroupAsync(SelectedGroup.Id);
                SelectedGroupContacts.Clear();
                foreach (var c in contacts) SelectedGroupContacts.Add(c);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading contacts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TestConnection()
        {
            if (SelectedPort == null || IsBusy) return;

            IsBusy = true;
            StatusMessage = $"Testing {SelectedPort.PortName}...";
            var result = await _smsService.TestConnectionAsync(SelectedPort.PortName);
            
            if (result.Success)
            {
                StatusMessage = $"{SelectedPort.PortName} is READY.";
                MessageBox.Show($"{SelectedPort.PortName} connection successful!\n\n{result.Message}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"{SelectedPort.PortName} test FAILED.";
                MessageBox.Show($"{SelectedPort.PortName} test failed.\n\nDetails: {result.Message}\n\nTip: Make sure no other modem software is running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            IsBusy = false;
        }

        private async Task ClearModemMemory()
        {
            if (SelectedPort == null) return;

            IsBusy = true;
            StatusMessage = "Clearing modem memory...";
            
            var result = await _smsService.ClearModemMemoryAsync(SelectedPort.PortName);
            
            StatusMessage = result.Message;
            IsBusy = false;
        }

        private async Task ViewHistory(SmsContact? contact)
        {
            if (contact == null) return;
            
            var history = await _dbService.GetHistoryByContactAsync(contact.Id);
            
            // Open separate window for history
            var historyWin = new HistoryWindow(contact.Phone, history);
            historyWin.Owner = Application.Current.MainWindow;
            historyWin.ShowDialog();
        }

        private bool CanStartSending()
        {
            return SelectedGroup != null && SelectedTemplate != null && SelectedPort != null && !IsSending;
        }

        private async Task StartSending()
        {
            if (SelectedGroup == null || SelectedTemplate == null || SelectedPort == null) return;

            IsSending = true;
            ProgressValue = 0;
            StatusMessage = "Fetching contacts...";

            var contacts = await _dbService.GetContactsByGroupAsync(SelectedGroup.Id);
            TotalSms = contacts.Count;
            int current = 0;

            foreach (var contact in contacts)
            {
                string phoneNumber = contact.Phone;
                if (!phoneNumber.StartsWith("+"))
                {
                    phoneNumber = "+" + phoneNumber;
                }

                StatusMessage = $"Sending to {phoneNumber}...";
                bool success = await _smsService.SendSmsAsync(SelectedPort.PortName, phoneNumber, SelectedTemplate.MessageBody);
                
                string newStatus = success ? "Sent" : "Failed";
                await _dbService.AddHistoryAsync(contact.Id, SelectedTemplate.MessageBody, newStatus);

                if (success) TotalSent++; else TotalFailed++;

                current++;
                ProgressValue = (int)((double)current / TotalSms * 100);
            }

            IsSending = false;
            StatusMessage = "Process completed.";
            MessageBox.Show("SMS sending process completed.", "Finished", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task DeleteHistory(SmsHistoryEntry? entry)
        {
            if (entry == null || SelectedContact == null) return;
            
            if (MessageBox.Show("Delete this history entry?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _dbService.DeleteHistoryItemAsync(entry.Id);
                // Refresh history if showing? 
                // Since history window is separate, maybe I should pass the collection to it.
            }
        }

        private bool CanSaveTemplate()
        {
            return !string.IsNullOrWhiteSpace(NewTemplateTitle) && !string.IsNullOrWhiteSpace(NewTemplateBody);
        }

        #endregion
    }
}
