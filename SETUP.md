# ⚙️ O'rnatish va Sozlash Qo'llanmasi

**SMS Gateway Pro** dasturini kompyuteringizga o'rnatish va ishga tushirish uchun quyidagi bosqichlarni bajaring.

## 📋 Talablar

- **Operatsion tizim**: Windows 10/11
- **SDK**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Uskuna (Hardware)**: GSM Modem (USB Dongle yoki Serial Modem)
- **Drayverlar**: Modem drayverlari o'rnatilgan bo'lishi va COM port tayinlangan bo'lishi shart.

## 🚀 O'rnatish bosqichlari

1. **Loyihani yuklab olish**:
   ```bash
   git clone https://github.com/IslamAbdurahman/SmsGatewayWpf.git
   cd SmsGatewayApp
   ```

2. **Kutubxonalarni tiklash**:
   ```bash
   dotnet restore
   ```

3. **Ma'lumotlar bazasi**:
   Dastur ma'lumotlar bazasini `%LOCALAPPDATA%\SmsGatewayApp\sms_gateway.db` manzilida saqlaydi. 
   - **Default holatga qaytarish**: Agar bazani tozalab, "toza" holatga keltirmoqchi bo'lsangiz, yuqoridagi manzildagi faylni o'chirib tashlang. Dastur keyingi ishga tushishda uni yangidan (bo'sh holda) yaratadi.
   - **Build uchun eslatma**: Loyihani tarqatishdan oldin (publish qilishda), bazaning default (bo'sh) holatda bo'lishini ta'minlash uchun `%LOCALAPPDATA%` dagi faylni o'chirib, keyin yig'ish tavsiya etiladi.

4. **Loyihani yig'ish (Build)**:
   ```bash
   dotnet build
   ```

5. **Ishga tushirish**:
   ```bash
   dotnet run
   ```

6. **📦 EXE (Publish) qilish**:
   Dasturni bitta mustaqil `.exe` fayl holatiga keltirish uchun quyidagi buyruqni ishlating:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true --output ./publish
   ```
   Tayyor fayl `./publish/SmsGatewayApp.exe` manzilida bo'ladi.

## 📱 Uskunani (Modemni) sozlash

1. GSM Modemni USB portga ulang.
2. **Device Manager** -> **Ports (COM & LPT)** bo'limidan modemingiz qaysi portga ulanganini aniqlang (masalan, COM3).
3. Dasturda **SMS Sending** sahifasiga o'ting.
4. **Refresh** () tugmasini bosing va o'z portingizni tanlang.
5. **Test Connection** tugmasi orqali ulanishni tekshiring.
6. Agar SIM-karta xotirasi to'lib qolsa, **Clear Memory** tugmasidan foydalaning.

## 📊 Excel Import formati

Kontaktlarni import qilish uchun Excel faylingiz quyidagi ustunlarga ega bo'lishi kerak:
- **A ustuni**: Telefon raqami (masalan, +998901234567)
- **B ustuni**: Ism (ixtiyoriy)

## 🔧 Muammolarni bartaraf etish

- **Port Busy**: Port boshqa dastur tomonidan band emasligiga ishonch hosil qiling.
- **Access Denied**: Agar Serial Portga ulanishda xatolik bo'lsa, dasturni Administrator nomidan ishga tushiring.
- **DB Lock**: Agar ma'lumotlar bazasi band bo'lsa, dasturning faqat bitta nusxasi ishlayotganini tekshiring.

## 🛠 Yordam

Texnik yordam yoki yangi imkoniyatlar bo'yicha takliflar bo'lsa, repository-da "Issue" qoldiring.
