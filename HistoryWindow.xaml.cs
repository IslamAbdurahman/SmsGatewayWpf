using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SmsGatewayApp.Models;

using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace SmsGatewayApp
{
    public partial class HistoryWindow : Window
    {
        private readonly Services.DatabaseService _db;

        public HistoryWindow(Services.DatabaseService db, string contactName, List<SmsHistoryEntry> history)
        {
            InitializeComponent();
            _db = db;
            ContactInfo.Text = $"History for {contactName}";
            HistoryGrid.ItemsSource = history;
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SmsHistoryEntry entry)
            {
                if (MessageBox.Show("Delete this history entry?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _db.DeleteHistoryItemAsync(entry.Id);
                    
                    var items = (List<SmsHistoryEntry>)HistoryGrid.ItemsSource;
                    items.Remove(entry);
                    HistoryGrid.ItemsSource = null;
                    HistoryGrid.ItemsSource = items;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
