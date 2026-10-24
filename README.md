# چقدر منو می‌شناسی؟

بازی اجتماعی فارسی با ASP.NET Core 8، Razor Pages، EF Core و SQL Server.
صاحب بازی با نام کاربری و رمز وارد می‌شود؛ دوستان فقط با نام نمایشی بازی می‌کنند.

راهنمای توسعه و تحویل در [docs/DELIVERY.fa.md](docs/DELIVERY.fa.md) و بررسی اولیه در [docs/PROJECT-AUDIT.fa.md](docs/PROJECT-AUDIT.fa.md) قرار دارد.

قابلیت‌های کشف علایق، سؤال تطبیقی، MBTI اختیاری، فکت مستند و مقایسهٔ دوطرفه در [docs/INTELLIGENCE.fa.md](docs/INTELLIGENCE.fa.md) مستند شده‌اند. پس از ورود، از «کشف من» شروع کنید؛ تحلیل به‌صورت پیش‌فرض خاموش است و برای فعال‌شدن رضایت می‌خواهد. موتور فعلی بدون API خارجی کار می‌کند.

بازطراحی جدید: «کشف من» یک معرفی کوتاه و حداکثر شش فکت با لایک/دیسلایک نشان می‌دهد. استوری شخصیت فقط از فکت‌های تأییدشده ساخته می‌شود؛ استوری دوستان صرفاً جمع‌بندی تجمیعی است. مسیرها، قرارداد تصاویر آینده، فایل‌های تغییرکرده و محدودیت‌های بررسی بصری در [راهنمای شخصیت و استوری](docs/PERSONALITY-EXPERIENCE.fa.md) آمده‌اند. تنظیمات و MBTI به «تنظیمات و پاسخ‌هام» منتقل شده‌اند.

## اجرای محلی

SQL Server باید در دسترس باشد. اتصال توسعه در `FriendShip.web/appsettings.Development.json` تعریف شده است؛ برای سرور متفاوت، مقدار `ConnectionStrings__DefaultConnection` را در محیط تنظیم کنید.

```powershell
dotnet restore FriendShip.slnx
dotnet tool restore --tool-manifest dotnet-tools.json
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project FriendShip.Infrastructure --startup-project FriendShip.web --configuration Release
dotnet run --project FriendShip.web/FriendShip.Web.csproj --configuration Release --launch-profile https
```

نشانی توسعه: `https://localhost:7238`. HTTPS برای کوکی‌های امن بازی ضروری است؛ در صورت نیاز گواهی توسعه معتبر را با ابزار استاندارد .NET تنظیم کنید. بانک سؤال هنگام شروع، با `SeedQuestionBank=true` به‌صورت تکرارپذیر تکمیل می‌شود. برنامه migration را خودکار اجرا نمی‌کند.

## تست‌ها

```powershell
dotnet test FriendShip.slnx --configuration Release
node scripts/check-frontend.cjs
node scripts/check-story-export.cjs
```

تست مهاجرت به SQL Server محلی و اجازه ساخت دیتابیس آزمایشی نیاز دارد. فقط دیتابیس تصادفی `FriendShipTests_<guid>` خودش را می‌سازد و حذف می‌کند. اتصال سرور آزمایشی را می‌توان با `FRIENDSHIP_TEST_SQL` تنظیم کرد؛ نام دیتابیس همیشه توسط تست جایگزین می‌شود.

اجرای تست‌های مستقل از SQL Server:

```powershell
dotnet test FriendShip.slnx --configuration Release --filter 'Category!=SqlServer'
```
