using System.Configuration;
using System.Data;
using System.Windows;

namespace SmsGatewayApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var dbService = new Services.DatabaseService();
        await dbService.InitializeDatabaseAsync();
        
        // Add initial template if none exist
        var templates = await dbService.GetTemplatesAsync();
        if (templates.Count == 0)
        {
            await dbService.SaveTemplateAsync("Welcome Message", "Welcome to our service! We are glad to have you.");
            await dbService.SaveTemplateAsync("Alert", "Urgent: Please check your account for recent updates.");
        }
    }
}
