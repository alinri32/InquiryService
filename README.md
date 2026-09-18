# مستندات پیاده‌سازی سرویس وب استعلام (Inquiry Service API)

---

### ۱. روش اجرای پروژه

#### پیش‌نیازها
* دات‌نت نسخه **.NET 8.0** یا بالاتر
* پایگاه‌داده **Microsoft SQL Server** (نسخه ۲۰۱۹ به بالا یا Azure SQL)

#### مراحل راه‌اندازی و اجرا
1. **پایگاه‌داده و اجرای اسکریپت‌ها:**
   * ابتدا اسکریپت ساخت دیتابیس و جداول موجود در مسیر `sql/01_InitDatabase.sql` را در SQL Server اجرا کنید.
   * این اسکریپت دیتابیس `InquiryDb`، کاربر و دسترسی‌های `Ayan` و جداول `Inquiries` و `InquiryProviderAttempts` را به همراه فشرده‌سازی صفحه‌ای (`PAGE Compression`) ایجاد می‌کند.

2. **تنظیم Connection String:**
   * در فایل `src/InquiryService.Api/appsettings.json`، مقدار کانکشن‌استرینگ `InquiryDb` را متناسب با محیط خود بررسی و تنظیم کنید:
     ```json
     "ConnectionStrings": {
       "InquiryDb": "Server=localhost;Database=InquiryDb;User Id=Ayan;Password=StrongP@ssw0rd!;TrustServerCertificate=True;"
     }
     ```

3. **اجرای تست‌های واحد (Unit Tests):**
   * برای اطمینان از صحت عملکرد منطق Failover، بیزینس ارور، Idempotency و کش:
     ```bash
     dotnet test
     ```

4. **اجرای سرویس:**
   * دستور زیر را در خط فرمان اجرا کرده و به آدرس Swagger سرویس مراجعه کنید:
     ```bash
     dotnet run --project src/InquiryService.Api
     ```
   * آدرس پیش‌فرض مستندات: `https://localhost:<port>/swagger`

---

### ۲. نمونه سناریوهای تستی برای Swagger و Postman

برای بررسی کامل رفتار سیستم در شرایط گوناگون (موفقیت، خطای سرور پرووایدر، قطعی شبکه و خطای تجاری)، مقادیر زیر را در هدر و بدنه درخواست ارسال کنید:

```json
{
  "Success": {
    "Headers": {
      "Idempotency-Key": "req-success-100"
    },
    "Body": {
      "identityIdentifier": "4311515545",
      "inquiryType": "NationalCode",
      "bypassCache": false
    }
  },
  "Failover - Timeout": {
    "Headers": {
      "Idempotency-Key": "req-timeout-100"
    },
    "Body": {
      "identityIdentifier": "9991234567",
      "inquiryType": "NationalCode",
      "bypassCache": false
    }
  },
  "Failover - TechnicalError": {
    "Headers": {
      "Idempotency-Key": "req-techerr-100"
    },
    "Body": {
      "identityIdentifier": "8881234567",
      "inquiryType": "NationalCode",
      "bypassCache": false
    }
  },
  "Business Error": {
    "Headers": {
      "Idempotency-Key": "req-buserr-100"
    },
    "Body": {
      "identityIdentifier": "7771234567",
      "inquiryType": "NationalCode",
      "bypassCache": false
    }
  }
}