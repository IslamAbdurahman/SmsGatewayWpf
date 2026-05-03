using System;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Services;

namespace SmsGatewayApp.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private object? _currentViewModel;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            // Navigation Command
            NavigateCommand = new RelayCommand(p => Navigate(p?.ToString() ?? Constants.ViewNames.Dashboard));

            // Default View
            Navigate(Constants.ViewNames.Dashboard);
        }

        #region Properties

        public object? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        private string _statusMessage = "Tayyor";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region Commands

        public ICommand NavigateCommand { get; }

        #endregion

        #region Methods

        public void Navigate(string viewName)
        {
            StatusMessage = $"O'tish: {viewName}...";

            CurrentViewModel = viewName switch
            {
                Constants.ViewNames.Dashboard => _serviceProvider.GetService(typeof(DashboardViewModel)),
                Constants.ViewNames.Sending => _serviceProvider.GetService(typeof(SendingViewModel)),
                Constants.ViewNames.Contacts => _serviceProvider.GetService(typeof(ContactsViewModel)),
                Constants.ViewNames.Templates => _serviceProvider.GetService(typeof(TemplatesViewModel)),
                Constants.ViewNames.Blacklist => _serviceProvider.GetService(typeof(BlacklistViewModel)),
                Constants.ViewNames.Backup => _serviceProvider.GetService(typeof(BackupViewModel)),
                Constants.ViewNames.History => _serviceProvider.GetService(typeof(HistoryViewModel)),
                Constants.ViewNames.Tasks => _serviceProvider.GetService(typeof(TasksViewModel)),
                _ => CurrentViewModel
            };

            StatusMessage = "Tayyor";
        }

        #endregion
    }
}
