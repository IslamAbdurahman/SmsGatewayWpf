# ⚙️ Setup & Installation Guide

Follow these steps to set up and run **SMS Gateway Pro** on your machine.

## 📋 Prerequisites

- **Operating System**: Windows 10/11
- **SDK**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Hardware**: GSM Modem (USB Dongle or Serial Modem)
- **Drivers**: Ensure your modem drivers are installed and a COM port is assigned.

## 🚀 Installation Steps

1. **Clone the Project**:
   ```bash
   git clone https://github.com/IslamAbdurahman/SmsGatewayWpf.git
   cd SmsGatewayApp
   ```

2. **Restore Dependencies**:
   ```bash
   dotnet restore
   ```

3. **Database Initialization**:
   The application automatically creates and initializes the `sms_gateway.db` file on its first run. No manual SQL execution is required.

4. **Build the Application**:
   ```bash
   dotnet build
   ```

5. **Run the App**:
   ```bash
   dotnet run
   ```

## 📱 Hardware Configuration

1. Connect your GSM Modem to a USB port.
2. Open **Device Manager** -> **Ports (COM & LPT)** to find your modem's COM port (e.g., COM3).
3. In the app, navigate to the **SMS Sending** page.
4. Click the **Refresh** button () to see available ports.
5. Select your port and click **Test Connection**.
6. Use **Clear Memory** if your modem's SIM storage is full.

## 📊 Excel Import Format

To import contacts, your Excel file should have the following column structure (headers are optional):
- **Column A**: Phone Number (e.g., +998901234567)
- **Column B**: Contact Name (Optional)

## 🔧 Troubleshooting

- **Port Busy**: Ensure no other software (like another SMS app or hyperterminal) is using the COM port.
- **Access Denied**: Run the application as Administrator if you encounter permission issues with Serial Ports.
- **DB Lock**: If the app crashes with a database lock, ensure only one instance of the app is running.

## 🛠 Support

For technical support or feature requests, please open an issue in the repository.
