using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Models;
using SmsGatewayApp.Services;

using MessageBox = System.Windows.MessageBox;

using System.IO;
using NAudio.Wave;

namespace SmsGatewayApp.ViewModels
{
    public class TemplatesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        private IWavePlayer? _previewPlayer;

        public TemplatesViewModel(DatabaseService db)
        {
            _db = db;

            SaveTemplateCommand = new AsyncRelayCommand(async _ => await SaveTemplateAsync(), _ => CanSaveTemplate());
            DeleteTemplateCommand = new AsyncRelayCommand(async p => await DeleteTemplateAsync(p as SmsTemplate));
            EditTemplateCommand = new RelayCommand(p => StartEditTemplate(p as SmsTemplate));
            BrowseAudioCommand = new RelayCommand(_ => BrowseAudio());
            PlayAudioCommand = new RelayCommand(p => PlayPreview(p as string ?? NewAudioPath));

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

        private string? _newAudioPath;
        public string? NewAudioPath { get => _newAudioPath; set => SetProperty(ref _newAudioPath, value); }

        public int TemplateBodyLength => NewAudioPath != null && string.IsNullOrEmpty(NewTemplateBody) ? 0 : (NewTemplateBody?.Length ?? 0);
        public int SmsParts => TemplateBodyLength == 0 ? 0 : (int)Math.Ceiling(TemplateBodyLength / 160.0);

        private SmsTemplate? _editingTemplate;
        public SmsTemplate? EditingTemplate { get => _editingTemplate; set => SetProperty(ref _editingTemplate, value); }

        #endregion

        #region Commands

        public ICommand SaveTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand EditTemplateCommand { get; }
        public ICommand BrowseAudioCommand { get; }
        public ICommand PlayAudioCommand { get; }

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
            if (string.IsNullOrWhiteSpace(NewTemplateTitle)) return;
            if (string.IsNullOrWhiteSpace(NewTemplateBody) && string.IsNullOrWhiteSpace(NewAudioPath)) return;

            if (EditingTemplate != null)
            {
                await _db.UpdateTemplateAsync(EditingTemplate.Id, NewTemplateTitle, NewTemplateBody, NewAudioPath);
                EditingTemplate = null;
            }
            else
            {
                await _db.SaveTemplateAsync(NewTemplateTitle, NewTemplateBody, NewAudioPath);
            }
            NewTemplateTitle = string.Empty;
            NewTemplateBody = string.Empty;
            NewAudioPath = null;
            await LoadTemplatesAsync();
        }

        private void BrowseAudio()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3|All Files (*.*)|*.*",
                Title = "Audio faylni tanlang"
            };
            if (dialog.ShowDialog() == true)
            {
                NewAudioPath = dialog.FileName;
            }
        }

        private void PlayPreview(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                if (_previewPlayer != null)
                {
                    _previewPlayer.Stop();
                    _previewPlayer.Dispose();
                    _previewPlayer = null;
                    return; // Toggle stop
                }

                _previewPlayer = new WaveOutEvent();
                var audioFile = new AudioFileReader(path);
                _previewPlayer.Init(audioFile);
                _previewPlayer.PlaybackStopped += (s, e) => { _previewPlayer?.Dispose(); _previewPlayer = null; };
                _previewPlayer.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audioni eshitishda xatolik: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartEditTemplate(SmsTemplate? t)
        {
            if (t == null) return;
            EditingTemplate = t;
            NewTemplateTitle = t.Title;
            NewTemplateBody = t.MessageBody;
            NewAudioPath = t.AudioPath;
        }

        private async Task DeleteTemplateAsync(SmsTemplate? t)
        {
            if (t == null) return;
            if (MessageBox.Show($"'{t.Title}' shablonini o'chirishni tasdiqlaysizmi?", "O'chirish", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _db.DeleteTemplateAsync(t.Id);
            await LoadTemplatesAsync();
        }

        private bool CanSaveTemplate() => !string.IsNullOrWhiteSpace(NewTemplateTitle) && (!string.IsNullOrWhiteSpace(NewTemplateBody) || !string.IsNullOrWhiteSpace(NewAudioPath));

        #endregion
    }
}
