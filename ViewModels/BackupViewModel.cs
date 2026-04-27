using System.Threading.Tasks;
using System.Windows.Input;
using SmsGatewayApp.Helpers;
using SmsGatewayApp.Services;

namespace SmsGatewayApp.ViewModels
{
    public class BackupViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public BackupViewModel(DatabaseService db)
        {
            _db = db;
            BackupCommand = new AsyncRelayCommand(async _ => await BackupAsync());
            RestoreCommand = new AsyncRelayCommand(async _ => await RestoreAsync());
        }

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }

        private async Task BackupAsync()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Backup Files|*.zip", FileName = "sms_backup.zip" };
                if (dlg.ShowDialog() == true)
                {
                    await _db.BackupDatabaseAsync(dlg.FileName);
                    System.Windows.MessageBox.Show("Zaxira muvaffaqiyatli yaratildi!", "Muvaffaqiyat", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Zaxira yaratishda xatolik: {ex.Message}", "Xatolik", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task RestoreAsync()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Backup Files|*.zip" };
                if (dlg.ShowDialog() == true)
                {
                    await _db.RestoreDatabaseAsync(dlg.FileName);
                    System.Windows.MessageBox.Show("Ma'lumotlar qayta tiklandi! Dasturni qayta ishga tushiring.", "Muvaffaqiyat", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Tiklashda xatolik: {ex.Message}", "Xatolik", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
