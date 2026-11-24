# Enterprise Accounting ERP System | نظام تخطيط موارد المؤسسات المحاسبي

نظام محاسبي مؤسسي متكامل للشركات المتعددة (Multi-Company & Multi-Branch Enterprise ERP System) مبني بأحدث التقنيات:
- **Backend**: ASP.NET Core Web API (.NET 10) على معمارية Clean Architecture النظيفة.
- **Database**: Microsoft SQL Server مع Entity Framework Core 10 و Sequential Concurrency-Safe Guid/Gapless Numbering.
- **Frontend**: Angular 21+ مع Angular Material، واجهة عربية RTL وإنجليزية LTR، وتقنية Standalone Components و Lazy Loading.
- **ZATCA e-Invoicing**: متوافق مع متطلبات هيئة الزكاة والضريبة والجمارك (الفاتورة الإلكترونية مع تشفير TLV Base64 QR Code).
- **Docker**: جاهز للتشغيل والإنتاج بضغطة واحدة عبر Docker Compose.

---

## 🏛️ المعمارية الهندسية (Clean Architecture)

```
AccountingERP1/
├── Backend/
│   ├── src/
│   │   ├── AccountingERP.Domain/          # الطبقة الجوهرية: الكيانات (Entities)، كائنات القيمة، والحالات المحاسبية
│   │   ├── AccountingERP.Application/     # حالات الاستخدام، واجهات الخدمات (Interfaces)، و DTOs
│   │   ├── AccountingERP.Infrastructure/  # EF Core DbContext، الترحيل، الأمان JWT، وتكامل SQL Server
│   │   └── AccountingERP.API/             # Controllers، وسيط التعددية TenantMiddleware، والتوثيق
│   ├── tests/
│   │   ├── AccountingERP.UnitTests/       # اختبارات الوحدة (12 Tests Passed)
│   │   └── AccountingERP.IntegrationTests/# اختبارات التكامل الفعلية على SQL Server (47 Tests Passed)
│   ├── AccountingERP.slnx                 # ملف Solution الموحد
│   └── Dockerfile                         # ملف بناء خادم الـ API
├── Frontend/
│   └── accounting-erp-ui/                 # واجهة المستخدم Angular 21+، Angular Material، و RTL/LTR
├── docs/                                  # كافة وثائق المشروع والتقارير الفنية
├── docker-compose.yml                     # ملف النشر الموحد (SQL Server + API + Nginx Frontend)
├── .gitignore                             # ملف استثناءات Git
└── ROADMAP_PHASES.md                      # سجل المراحل الـ 16 وإنجازها بالكامل
```

---

## 🚀 تشغيل النظام (Quick Start)

### 1. التشغيل السريع بنقرة واحدة (One-Click Launch)
في المجلد الرئيسي للمشروع:
- **تشغيل كل من Backend و Frontend معاً:** انقر نقراً مزدوجاً على [run-all.bat](file:///c:/Users/ALBAHA/Desktop/AccountingERP1/run-all.bat).
- **تشغيل الباك إند فقط:** انقر نقراً مزدوجاً على [run-backend.bat](file:///c:/Users/ALBAHA/Desktop/AccountingERP1/run-backend.bat).
- **تشغيل الواجهة الأمامية فقط:** انقر نقراً مزدوجاً على [run-frontend.bat](file:///c:/Users/ALBAHA/Desktop/AccountingERP1/run-frontend.bat).

---

### 2. التشغيل عبر سطر الأوامر (Terminal / PowerShell / CMD)

#### أ. تشغيل الباك إند (.NET 10 API):
من المجلد الرئيسي للمشروع:
```powershell
dotnet run --project Backend/src/AccountingERP.API/AccountingERP.API.csproj --no-restore --urls "http://localhost:5000"
```
أو بالدخول إلى مجلد الباك إند:
```powershell
cd Backend/src/AccountingERP.API
dotnet run --no-restore --urls "http://localhost:5000"
```
- الرابط: `http://localhost:5000`
- فحص الحالة (Health Check): `http://localhost:5000/health`

#### ب. تشغيل الواجهة الأمامية (Angular):
من المجلد الرئيسي للمشروع:
```powershell
npm start --prefix Frontend/accounting-erp-ui
```
أو بالدخول إلى مجلد الواجهة:
```powershell
cd Frontend/accounting-erp-ui
npm start
```
- الرابط: `http://localhost:4200`

---

### 2. التشغيل عبر حاويات Docker (One-Click Production Deployment)

```powershell
docker compose up -d --build
```
سيبدأ تلقائياً:
1. **قاعدة البيانات**: `sqlserver` (SQL Server 2022 Developer Edition) على المنفذ `1433`.
2. **الخادم الخلفي**: `backend` (ASP.NET Core Web API) على المنفذ `5000`.
3. **الواجهة الأمامية**: `frontend` (Angular 21+ مع Nginx) على المنفذ `80` و `4200`.

---

## 🔑 بيانات تسجيل الدخول الافتراضية (Default Credentials)

تم إعداد بيانات أولية (Seeded Data) تلقائياً عند التشغيل الأول:
- **البريد الإلكتروني**: `admin@erp.com`
- **كلمة المرور**: `Admin@123456`
- **الشركة الافتراضية**: شركة الأعمال المتقدمة للتجارة (الرقم الضريبي: `300000000000003`)
- **الفرع الافتراضي**: الفرع الرئيسي (Main Branch)

---

## 💼 الموديولات والوظائف المنفذة (Implemented Modules)

### 1. دليل وشجرة الحسابات (Chart of Accounts)
- شجرة حسابات هرمية ديناميكية من 5 مستويات (الأصول، الخصوم، حقوق الملكية، الإيرادات، المصروفات).
- حساب تلقائي للأرصدة ومستويات الترحيل (Posting vs Header Accounts).

### 2. القيود اليومية والأستاذ العام (General Ledger & Journal Entries)
- ترقيم متسلسل بدون فجوات (Gapless Numbering) `JE-YYYY-XXXXXX`.
- تحقق لحظي من التوازن المحاسبي المزدوج $(\text{Total Debit} = \text{Total Credit})$.
- ترحيل آلي، إقفال فترات مالية، وكشف حساب أستاذ تفصيلي مع رصيد تراكمي متحرك.

### 3. المبيعات والمشتريات والعملاء والموردين (Commercial Module)
- فواتير المبيعات وفواتير المشتريات مع ترحيل آلي للحسابات والمخزون والضريبة.
- سندات القبض والصرف وتخصيص الدفعات على الفواتير.

### 4. الفاتورة الضريبية وترميز ZATCA e-Invoicing
- إصدار وطباعة فواتير ضريبية رسمية بمقاس A4.
- محرك تشفير **TLV Base64** متوافق 100% مع هيئة الزكاة والضريبة والجمارك لترميز رمز الاستجابة السريع (QR Code).

### 5. المستودعات والمخزون (Inventory & Warehouses)
- محرك متوسط التكلفة المرجح المتحرك (Moving Weighted Average Cost - WAC) على كل وارد مخزني.
- تحويلات مخزنية وتسويات جردية مع قيود يومية آلية.

### 6. الخزائن والبنوك (Treasury & Banking)
- حساب الأرصدة الدفترية اللحظية من واقع القيود المرحلة.
- نظام مطابقات بنكية مع حساب الفروقات آلياً.

### 7. الأصول الثابتة (Fixed Assets)
- سجل الأصول الثابتة، تصنيفاتها ومواقعها.
- محرك احتساب الإهلاك الخطي الشهري الآلي والترحيل للأستاذ العام.
- معالجة استبعاد وبيع الأصول مع احتساب أرباح/خسائر بيع الأصول الرأسمالية.

### 8. التقارير والقوائم المالية (Financial Reports)
- **ميزان المراجعة (Trial Balance)**: بالأرصدة الافتتاحية، حركات الفترة، والأرصدة الختامية.
- **قائمة الدخل (Income Statement / P&L)**: إجمالي الربح، المصروفات التشغيلية، وصافي الدخل.
- **قائمة المركز المالي (Balance Sheet)**: الأصول = الالتزامات + حقوق الملكية مع تحقق التوازن المحاسبي.
- **تصدير إكسل (Excel / CSV)**: مزود بترميز **UTF-8 BOM** لضمان فتح النصوص العربية بدقة في برنامج Excel.

---

## 🧪 نتائج الاختبارات البرمجية والتحقق (Tests & Verification)

- **Backend Tests**: **52 اختبار ناجح بنسبة 100%** (12 Unit Tests + 40 Integration Tests على SQL Server).
- **Frontend Build**: **0 أخطاء و 0 تحذيرات** مع تقسيم الحزم المستقلة بالتحميل الكسول (Lazy Loaded Chunks).
- **Conformity**: Clean Architecture, SOLID Principles, Multi-Tenant Isolation, Audit Trail.
