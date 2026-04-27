using System.Windows;

namespace SmsGatewayApp.Helpers
{
    /// <summary>
    /// Windows balloon tooltip notification (works on all Windows versions without extra packages).
    /// </summary>
    public static class WindowsNotificationHelper
    {
        private static System.Windows.Forms.NotifyIcon? _trayIcon;

        public static void Initialize(System.Windows.Forms.NotifyIcon trayIcon)
        {
            _trayIcon = trayIcon;
        }

        /// <summary>
        /// Shows a balloon notification from the system tray icon.
        /// Falls back to a MessageBox if the tray icon is not initialized.
        /// </summary>
        public static void Show(string title, string message,
            System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info,
            int durationMs = 4000)
        {
            if (_trayIcon != null)
            {
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = message;
                _trayIcon.BalloonTipIcon = icon;
                _trayIcon.ShowBalloonTip(durationMs);
            }
            // No fallback MessageBox — notification is optional/non-blocking
        }

        public static void ShowSuccess(string message)
            => Show("SMS Gateway Pro", message, System.Windows.Forms.ToolTipIcon.Info);

        public static void ShowWarning(string message)
            => Show("SMS Gateway Pro", message, System.Windows.Forms.ToolTipIcon.Warning);

        public static void ShowError(string message)
            => Show("SMS Gateway Pro", message, System.Windows.Forms.ToolTipIcon.Error);
    }
}
