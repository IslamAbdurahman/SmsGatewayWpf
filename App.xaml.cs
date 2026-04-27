using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmsGatewayApp.Services;
using SmsGatewayApp.ViewModels;

namespace SmsGatewayApp
{
    public partial class App : System.Windows.Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddSingleton<DatabaseService>(DatabaseService.Instance); // Keeping existing singleton for now
                    services.AddTransient<ExcelService>();
                    services.AddTransient<SmsService>();

                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<SendingViewModel>();
                    services.AddTransient<ContactsViewModel>();
                    services.AddTransient<TemplatesViewModel>();
                    services.AddTransient<BlacklistViewModel>();
                    services.AddTransient<BackupViewModel>();
                    services.AddTransient<HistoryViewModel>();

                    // Main Window
                    services.AddSingleton(s => new MainWindow
                    {
                        DataContext = s.GetRequiredService<MainViewModel>()
                    });
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            await _host.StartAsync();

            var dbService = _host.Services.GetRequiredService<DatabaseService>();
            await dbService.InitializeDatabaseAsync();

            var templates = await dbService.GetTemplatesAsync();
            if (templates.Count == 0)
            {
                await dbService.SaveTemplateAsync("Tabriknoma", "Assalomu alaykum, {name}! Sizni bayram bilan tabriklaymiz.");
                await dbService.SaveTemplateAsync("Eslatma", "Hurmatli mijoz, sizning raqamingiz ({phone}) guruhda ({group}) faol.");
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}
