using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
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
                .UseSerilog((context, services, configuration) => configuration
                    .WriteTo.Console()
                    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day))
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddSingleton<DatabaseService>();
                    services.AddTransient<ExcelService>();
                    services.AddSingleton<VoiceService>();
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
                    services.AddTransient<TasksViewModel>();

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
