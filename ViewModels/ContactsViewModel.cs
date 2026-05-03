using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;
using SmsGatewayApp;

using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Application = System.Windows.Application;

namespace SmsGatewayApp.ViewModels
{
    public class ContactsViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly ExcelService _excelService;

        public ContactsViewModel(DatabaseService db, ExcelService excelService)
        {
            _db = db;
            _excelService = excelService;

            // Commands
            CreateGroupCommand = new AsyncRelayCommand(async _ => await CreateGroupAsync(), _ => !string.IsNullOrWhiteSpace(NewGroupName));
            DeleteGroupCommand = new AsyncRelayCommand(async p => await DeleteGroupAsync(p as ExcelGroup));
            EditGroupCommand = new RelayCommand(p => StartEditGroup(p as ExcelGroup));

            AddContactCommand = new AsyncRelayCommand(async _ => await AddContactAsync(), _ => SelectedGroup != null && !string.IsNullOrWhiteSpace(NewContactPhone));
            DeleteContactCommand = new AsyncRelayCommand(async p => await DeleteContactAsync(p as SmsContact));
            EditContactCommand = new RelayCommand(p => StartEditContact(p as SmsContact));
            AddToBlacklistCommand = new AsyncRelayCommand(async p => await AddContactToBlacklistAsync(p as SmsContact));
            ImportExcelCommand = new AsyncRelayCommand(async _ => await ImportExcelAsync());
            ExportExcelCommand = new AsyncRelayCommand(async _ => await ExportExcelAsync(), _ => SelectedGroup != null && SelectedGroupContacts.Any());
            ViewHistoryCommand = new AsyncRelayCommand(async p => await ViewHistoryAsync(p as SmsContact));

            _ = LoadDataAsync();
        }

        #region Properties

        public ObservableCollection<ExcelGroup> Groups { get; } = new();
        public ObservableCollection<SmsContact> SelectedGroupContacts { get; } = new();
        public ObservableCollection<SmsContact> FilteredContacts { get; } = new();
        public ObservableCollection<SmsHistoryEntry> ContactHistory { get; } = new();

        private ExcelGroup? _selectedGroup;
        public ExcelGroup? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                    _ = LoadSelectedGroupContactsAsync();
            }
        }

        private SmsContact? _selectedContact;
        public SmsContact? SelectedContact
        {
            get => _selectedContact;
            set => SetProperty(ref _selectedContact, value);
        }

        private string _newGroupName = string.Empty;
        public string NewGroupName { get => _newGroupName; set => SetProperty(ref _newGroupName, value); }

        private string _newContactName = string.Empty;
        public string NewContactName { get => _newContactName; set => SetProperty(ref _newContactName, value); }

        private string _newContactPhone = string.Empty;
        public string NewContactPhone { get => _newContactPhone; set => SetProperty(ref _newContactPhone, value); }

        private string _contactSearchText = string.Empty;
        public string ContactSearchText
        {
            get => _contactSearchText;
            set
            {
                if (SetProperty(ref _contactSearchText, value))
                    ApplyContactFilter();
            }
        }

        private bool _showHistory;
        public bool ShowHistory { get => _showHistory; set => SetProperty(ref _showHistory, value); }

        private ExcelGroup? _editingGroup;
        public ExcelGroup? EditingGroup { get => _editingGroup; set => SetProperty(ref _editingGroup, value); }

        private SmsContact? _editingContact;
        public SmsContact? EditingContact { get => _editingContact; set => SetProperty(ref _editingContact, value); }

        #endregion

        #region Commands

        public ICommand CreateGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }
        public ICommand EditGroupCommand { get; }
        public ICommand AddContactCommand { get; }
        public ICommand DeleteContactCommand { get; }
        public ICommand EditContactCommand { get; }
        public ICommand AddToBlacklistCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ViewHistoryCommand { get; }

        #endregion

        #region Methods

        private async Task LoadDataAsync()
        {
            var groups = await _db.GetGroupsAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);
        }

        private async Task LoadSelectedGroupContactsAsync()
        {
            SelectedGroupContacts.Clear();
            FilteredContacts.Clear();
            if (SelectedGroup == null) return;
            var contacts = await _db.GetContactsByGroupAsync(SelectedGroup.Id);
            foreach (var c in contacts) SelectedGroupContacts.Add(c);
            ApplyContactFilter();
        }

        private void ApplyContactFilter()
        {
            FilteredContacts.Clear();
            var search = ContactSearchText?.Trim().ToLower() ?? string.Empty;
            foreach (var c in SelectedGroupContacts)
            {
                if (string.IsNullOrEmpty(search) || (c.Name?.ToLower().Contains(search) ?? false) || c.Phone.Contains(search))
                    FilteredContacts.Add(c);
            }
        }

        private async Task CreateGroupAsync()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName)) return;
            if (EditingGroup != null)
            {
                await _db.UpdateGroupAsync(EditingGroup.Id, NewGroupName);
                EditingGroup = null;
            }
            else
            {
                await _db.InsertExcelGroupAsync(NewGroupName);
            }
            await LoadDataAsync();
            NewGroupName = string.Empty;
        }

        private void StartEditGroup(ExcelGroup? group)
        {
            if (group == null) return;
            EditingGroup = group;
            NewGroupName = group.Name;
        }

        private async Task DeleteGroupAsync(ExcelGroup? group)
        {
            if (group == null) return;
            if (MessageBox.Show($"'{group.Name}' guruhini o'chirishni tasdiqlaysizmi?", "O'chirish", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _db.DeleteGroupAsync(group.Id);
            await LoadDataAsync();
            if (SelectedGroup?.Id == group.Id) SelectedGroup = null;
        }

        private async Task AddContactAsync()
        {
            if (SelectedGroup == null || string.IsNullOrWhiteSpace(NewContactPhone)) return;
            if (EditingContact != null)
            {
                await _db.UpdateContactAsync(EditingContact.Id, NewContactPhone, NewContactName);
                EditingContact = null;
            }
            else
            {
                await _db.InsertContactAsync(SelectedGroup.Id, NewContactPhone, NewContactName);
            }
            await LoadSelectedGroupContactsAsync();
            NewContactPhone = string.Empty;
            NewContactName = string.Empty;
        }

        private void StartEditContact(SmsContact? contact)
        {
            if (contact == null) return;
            EditingContact = contact;
            NewContactPhone = contact.Phone;
            NewContactName = contact.Name ?? string.Empty;
        }

        private async Task DeleteContactAsync(SmsContact? contact)
        {
            if (contact == null) return;
            if (MessageBox.Show($"'{contact.Phone}' kontaktini o'chirishni tasdiqlaysizmi?", "O'chirish", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _db.DeleteContactAsync(contact.Id);
            await LoadSelectedGroupContactsAsync();
        }

        private async Task AddContactToBlacklistAsync(SmsContact? contact)
        {
            if (contact == null) return;
            await _db.AddToBlacklistAsync(contact.Phone, "Kontaktdan qo'shildi");
        }

        private async Task ImportExcelAsync()
        {
            var dlg = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;

            var contacts = await Task.Run(() => _excelService.ReadContacts(dlg.FileName));
            if (!contacts.Any()) return;

            int groupId = SelectedGroup?.Id ?? await _db.InsertExcelGroupAsync(System.IO.Path.GetFileName(dlg.FileName));
            await _db.BulkInsertContactsAsync(groupId, contacts);

            if (SelectedGroup == null) await LoadDataAsync();
            else await LoadSelectedGroupContactsAsync();
        }

        private async Task ExportExcelAsync()
        {
            if (SelectedGroup == null || !SelectedGroupContacts.Any()) return;

            var dlg = new SaveFileDialog { Filter = "Excel File|*.xlsx", FileName = $"{SelectedGroup.Name}_kontaktlar.xlsx" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                await Task.Run(() => _excelService.WriteContacts(dlg.FileName, SelectedGroupContacts.ToList()));
                MessageBox.Show("Muvaffaqiyatli eksport qilindi!", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eksportda xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ViewHistoryAsync(SmsContact? contact)
        {
            if (contact == null) return;
            var history = await _db.GetHistoryByContactAsync(contact.Id);
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                var win = new HistoryWindow(_db, contact.DisplayName, history);
                win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
            });
        }

        #endregion
    }
}
