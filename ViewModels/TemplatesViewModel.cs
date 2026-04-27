using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;

using MessageBox = System.Windows.MessageBox;

namespace SmsGatewayApp.ViewModels
{
    public class TemplatesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public TemplatesViewModel(DatabaseService db)
        {
            _db = db;

            SaveTemplateCommand = new AsyncRelayCommand(async _ => await SaveTemplateAsync(), _ => CanSaveTemplate());
            DeleteTemplateCommand = new AsyncRelayCommand(async p => await DeleteTemplateAsync(p as SmsTemplate));
            EditTemplateCommand = new RelayCommand(p => StartEditTemplate(p as SmsTemplate));

            _ = LoadTemplatesAsync();
        }

        #region Properties

        public ObservableCollection<SmsTemplate> Templates { get; } = new();

        private string _newTemplateTitle = string.Empty;
        public string NewTemplateTitle { get => _newTemplateTitle; set => SetProperty(ref _newTemplateTitle, value); }

        private string _newTemplateBody = string.Empty;
        public string NewTemplateBody 
        { 
            get => _newTemplateBody; 
            set 
            {
                if (SetProperty(ref _newTemplateBody, value))
                {
                    OnPropertyChanged(nameof(TemplateBodyLength));
                    OnPropertyChanged(nameof(SmsParts));
                }
            }
        }

        public int TemplateBodyLength => NewTemplateBody?.Length ?? 0;
        public int SmsParts => TemplateBodyLength == 0 ? 0 : (int)Math.Ceiling(TemplateBodyLength / 160.0);

        private SmsTemplate? _editingTemplate;
        public SmsTemplate? EditingTemplate { get => _editingTemplate; set => SetProperty(ref _editingTemplate, value); }

        #endregion

        #region Commands

        public ICommand SaveTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand EditTemplateCommand { get; }

        #endregion

        #region Methods

        private async Task LoadTemplatesAsync()
        {
            var templates = await _db.GetTemplatesAsync();
            Templates.Clear();
            foreach (var t in templates) Templates.Add(t);
        }

        private async Task SaveTemplateAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTemplateTitle) || string.IsNullOrWhiteSpace(NewTemplateBody)) return;
            if (EditingTemplate != null)
            {
                await _db.UpdateTemplateAsync(EditingTemplate.Id, NewTemplateTitle, NewTemplateBody);
                EditingTemplate = null;
            }
            else
            {
                await _db.SaveTemplateAsync(NewTemplateTitle, NewTemplateBody);
            }
            NewTemplateTitle = string.Empty;
            NewTemplateBody = string.Empty;
            await LoadTemplatesAsync();
        }

        private void StartEditTemplate(SmsTemplate? t)
        {
            if (t == null) return;
            EditingTemplate = t;
            NewTemplateTitle = t.Title;
            NewTemplateBody = t.MessageBody;
        }

        private async Task DeleteTemplateAsync(SmsTemplate? t)
        {
            if (t == null) return;
            if (MessageBox.Show($"'{t.Title}' shablonini o'chirishni tasdiqlaysizmi?", "O'chirish", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _db.DeleteTemplateAsync(t.Id);
            await LoadTemplatesAsync();
        }

        private bool CanSaveTemplate() => !string.IsNullOrWhiteSpace(NewTemplateTitle) && !string.IsNullOrWhiteSpace(NewTemplateBody);

        #endregion
    }
}
