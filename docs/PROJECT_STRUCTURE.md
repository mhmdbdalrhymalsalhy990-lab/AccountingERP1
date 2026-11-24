# هيكل مشروع نظام تخطيط موارد المؤسسات المحاسبي
## Project Structure Guide - Accounting ERP

يوثق هذا الدليل الهيكل الشجري الكامل والمنظم لمشروع **Accounting ERP** بعد اكتمال عملية إعادة الهيكلة والتنظيم القياسية، مع شرح مسؤولية كل مجلد وموقع كل نوع من الملفات.

---

## 🌳 الهيكل الشجري العام (Root Tree)

```
AccountingERP1/
│
├── Frontend/                                  # 1. المجلد الرئيسي للواجهة الأمامية
│   └── accounting-erp-ui/                     # تطبيق Angular 21+ ومكتبة Angular Material
│       ├── src/
│       │   ├── app/
│       │   │   ├── core/                      # النواة المركزية (Singleton Services, Interceptors, Guards, i18n, Models)
│       │   │   ├── shared/                    # المكونات والأنابيب المشتركة (PageHeader, StatusBadge, CurrencyFormat)
│       │   │   ├── layout/                    # إطار التطبيق الخارجي (Toolbar, Sidenav, Shell)
│       │   │   └── features/                  # شاشات وموديولات النظام الـ 10 المستقلة
│       │   ├── environments/                  # إعدادات البيئات والتطوير والإنتاج
│       │   ├── index.html                     # الصفحة الرئيسية مع خطوط Cairo/Roboto و Material Symbols
│       │   ├── styles.scss                    # الأنماط والتنسيقات العامة وقواعد RTL/LTR
│       │   └── main.ts                        # نقطة تشغيل التطبيق المستقلة (Standalone Bootstrap)
│       ├── Dockerfile                         # بناء وتشغيل الفرونت إند عبر Nginx
│       ├── nginx.conf                         # إعدادات خادم Nginx وتوجيه مسارات SPA والـ Proxy
│       └── tsconfig.json                      # مسارات TypeScript المستعارة (@core, @shared, @features, @layout)
│
├── Backend/                                   # 2. المجلد الرئيسي للباك إند وقاعدة البيانات
│   ├── src/                                   # مشاريع الكود المصدري للباك إند (ASP.NET Core Web API)
│   │   ├── AccountingERP.Domain/              # 1. طبقة النطاق والكيانات الجوهرية (Clean Architecture Core)
│   │   ├── AccountingERP.Application/         # 2. طبقة حالات الاستخدام والعقود والـ DTOs
│   │   ├── AccountingERP.Infrastructure/      # 3. طبقة البنية التحتية و EF Core وتكامل SQL Server
│   │   └── AccountingERP.API/                 # 4. طبقة واجهات البرمجة Controllers ومسار الطلب HTTP
│   ├── tests/                                 # مشاريع الاختبارات الآلية (59 اختبار ناجح)
│   │   ├── AccountingERP.UnitTests/           # اختبارات الوحدة (12 اختبار: Domain, PasswordHasher, ZATCA TLV)
│   │   └── AccountingERP.IntegrationTests/    # اختبارات التكامل الفعلية (47 اختبار تشمل MultiTenancyIsolationTests)
│   ├── AccountingERP.slnx                     # ملف Solution الموحد لـ Visual Studio و VS Code
│   ├── Dockerfile                             # بناء خادم الباك إند (.NET 10 Web API)
│   ├── nuget.config                           # إعدادات التغذية لـ NuGet
│   └── dotnet-tools.json                      # أدوات دوت نت
│
├── docs/                                      # 3. مجلد التوثيق المعماري والتقني وتقارير الجودة
│   ├── ARCHITECTURE.md                        # التصميم المعماري الشامل
│   ├── DATABASE_DESIGN_ERD.md                 # تصميم قاعدة البيانات والـ 35 جدولاً
│   ├── ACCOUNTING_AUDIT_REVIEW.md             # التدقيق المحاسبي للقيود والتوازن
│   ├── INITIAL_DIAGNOSTIC_REPORT.md           # تقرير الفحص والتشخيص الأولي
│   ├── BACKEND_ARCHITECTURE.md                # التوثيق المعماري للباك إند
│   ├── FRONTEND_ARCHITECTURE.md               # التوثيق المعماري للفرونت إند
│   ├── PROJECT_STRUCTURE.md                  # دليل هيكل المشروع المنظم المحدث
│   ├── REFACTORING_REPORT.md                  # التقرير التفصيلي لعملية إعادة الهيكلة
│   └── ROADMAP_PHASES.md                      # سجل المراحل الـ 16 وإنجازها بالكامل
│
├── docker-compose.yml                         # التشغيل الموحد للإنتاج (SQL Server + API + Nginx Frontend)
├── .gitignore                                 # استثناءات Git الموحدة
├── .dockerignore                              # استثناءات بناء الحاويات
└── README.md                                  # دليل البدء السريع والتشغيل
```

---

## 📁 تفصيل طبقات الـ Backend

### 1. `AccountingERP.Domain`
- **`Common/`**: الكيانات الأساسية المجردة والعقود المشتركة:
  - `BaseEntity<T>`: المعرف الأساسي ومقارنة الكيانات.
  - `AuditableEntity<T>`: حقول التدقيق (`CreatedAt`, `CreatedByUserId`, `LastModifiedAt`, `LastModifiedByUserId`).
  - `ITenantEntity`: خاصية `CompanyId` لعزل بيانات الشركات.
  - `IBranchEntity`: خاصية `BranchId` لتحديد الفروع.
  - `ISoftDelete`: خاصية `IsDeleted` للحذف الآمن.
  - `IConcurrencyAware`: خاصية `RowVersion` لحماية القيود من التعديل المتزامن.
- **`Entities/`**: الكيانات الـ 35 مقسمة حسب الموديولات:
  - `Company.cs`: الشركة والفروع.
  - `AccountingEntities.cs`: الحسابات، السنوات المالية، الفترات المالية، القيود، وأسطر القيود.
  - `CommercialEntities.cs`: العملاء، الموردين، فواتير المبيعات، فواتير المشتريات، الدفعات، وتخصيص الدفعات.
  - `TreasuryAndInventoryEntities.cs`: الحسابات البنكية، الخزائن، المطابقات، المنتجات، المستودعات، الحركات المخزنية، والتسويات.
  - `AssetAndSystemEntities.cs`: الأصول الثابتة، سجل الإهلاك، مراكز التكلفة، سجلات التدقيق، والمرفقات.
  - `IdentityEntities.cs`: المستخدمين، الأدوار، الصلاحيات، ورموز التحديث.
  - `MasterEntities.cs`: البلدان، العملات، أسعار الصرف، والضرائب.
- **`Enums/`**: الحالات والأنواع المحاسبية (`AccountType`, `AccountNature`, `InvoiceStatus`, إلخ).
- **`Exceptions/`**: الاستثناءات المالية الخاصة (`UnbalancedJournalEntryException`, إلخ).

### 2. `AccountingERP.Application`
- **`DTOs/`**: كائنات نقل البيانات مصنفة حسب الموديولات:
  - `Accounting/`: DTOs شجرة الحسابات، القيود، والقوائم المالية.
  - `Commercial/`: DTOs العملاء، الموردين، وفواتير المبيعات والمشتريات.
  - `TreasuryAndInventory/`: DTOs البنوك والمخزون والمنتجات.
  - `AssetAndSystem/`: DTOs الأصول الثابتة ومراكز التكلفة وسجلات التدقيق.
  - `Export/`: DTOs الفاتورة الضريبية ZATCA وتصدير ملفات CSV/Excel.
  - `Auth/`: DTOs الدخول، التوكن، واختيار الشركة.
  - `Master/`: DTOs الضرائب والعملات.
- **`Interfaces/`**: عقود الخدمات المحاسبية والتجارية:
  - `IAccountingServices.cs` (`IAccountService`, `IJournalEntryService`, `IFinancialReportService`).
  - `ICommercialServices.cs` (`ICustomerService`, `ISupplierService`, `ISalesInvoiceService`, `IPurchaseInvoiceService`, `IPaymentService`).
  - `IInventoryServices.cs`, `ITreasuryServices.cs`, `IFixedAssetService.cs`, `ICostCenterService.cs`, `ITaxService.cs`, `ICurrencyService.cs`, `ISystemServices.cs`, `IReportingExportService.cs`.
  - `IApplicationDbContext.cs`, `ICurrentTenantService.cs`, `ICurrentUserService.cs`, `IAuthService.cs`, `IJwtTokenService.cs`, `IPasswordHasher.cs`.

### 3. `AccountingERP.Infrastructure`
- **`Persistence/`**:
  - `Context/ApplicationDbContext.cs`: سياق قاعدة البيانات مع تطبيق الـ Global Query Filters والـ DbSets الـ 35.
  - `Configurations/`: ملفات تكوين Fluent API لكل مجموعة كيانات مع دقة الأرقام والمفاتيح المركبة الصارمة.
  - `Interceptors/AuditingAndTenantInterceptor.cs`: المعترض التلقائي لتدقيق التعديلات، منع تعديل القيود المرحلة، وحقن `CompanyId`.
  - `Migrations/`: سجل ترحيلات قاعدة البيانات المطبقة على SQL Server.
  - `Seed/DbInitializer.cs`: بذر البيانات الأساسية (الشركة الافتراضية، شجرة الحسابات، العملة، الضريبة، والمستخدم Admin).
- **`Services/`**: الخدمات البرمجية مصنفة حسب الموديولات الوظيفية:
  - `Accounting/`: `AccountService.cs`, `JournalEntryService.cs`, `FinancialReportService.cs`, `ReportingExportService.cs`.
  - `Commercial/`: `CommercialServices.cs`.
  - `Inventory/`: `InventoryService.cs`.
  - `Treasury/`: `TreasuryService.cs`.
  - `Assets/`: `FixedAssetService.cs`, `CostCenterService.cs`.
  - `Master/`: `CurrencyService.cs`, `TaxService.cs`.
  - `System/`: `SystemServices.cs`.
  - `Identity/`: `AuthService.cs`, `JwtTokenService.cs`, `PasswordHasher.cs`, `CurrentTenantService.cs`, `CurrentUserService.cs`.
- **`Security/`**:
  - `JwtAuthenticationHandler.cs`: معالج مصادقة التوكن المخصص.
  - `PermissionSecurity.cs`: تطبيق نظام الصلاحيات المبني على السياسات (`HasPermissionAttribute`, `PermissionAuthorizationHandler`).
- **`DependencyInjection.cs`**: التسجيل المركزي لكافة الخدمات وقاعدة البيانات في حاوية الاعتماديات.

### 4. `AccountingERP.API`
- **`Controllers/`**: 20 متحكم RESTful API تغطي كافة العمليات مع أوسمة الأمان والتحقق من الصلاحيات.
- **`Middlewares/`**:
  - `TenantMiddleware.cs`: وسيط إدارة سياق الشركات بمبدأ Fail-Closed ومنع التلاعب بترويسات المستأجرين (Anti-Tampering).
- **`Program.cs`**: تهيئة مسار الطلبات، سياسات الـ CORS، التوثيق، وبذر البيانات التلقائي.

---

## 📁 تفصيل طبقات الـ Frontend (`accounting-erp-ui`)

- **`src/app/core/`**:
  - `guards/`: `auth.guard.ts` لحماية المسارات.
  - `interceptors/`: `jwt.interceptor.ts` (حقن التوكن وترويسات الشركة)، و `error.interceptor.ts` (معالجة الأخطاء وإشعارات SnackBar).
  - `i18n/`: قواميس الترجمة المستقلة `ar.ts` (العربية) و `en.ts` (الإنجليزية) مع مفتاح موحد.
  - `models/`: النماذج المصنفة `auth.models.ts` و `erp.models.ts`.
  - `services/`: خدمات الـ HTTP والـ Signals المركزية (`AuthService`, `TenantService`, `LanguageService`, `GeneralLedgerService`, إلخ).
- **`src/app/shared/`**:
  - `components/page-header/`: ترويسة الصفحات الموحدة مع العنوان والوصف وزر الإجراءات.
  - `components/status-badge/`: شارات الحالة الملونة الموحدة (مرحل، مسودة، نشط، معطل).
  - `pipes/currency-format.pipe.ts`: تنسيق المبالغ النقدية بدقة.
  - `index.ts`: Barrel export للاستيراد النظيف.
- **`src/app/layout/`**:
  - `main-layout/`: الشريط العلوي (محول الشركات، تبديل اللغة، بيانات المستخدم) والقائمة الجانبية الشاملة.
- **`src/app/features/`**:
  - 10 موديولات محاسبية وإدارية مستقلة ومحملة بنظام التحميل الكسول (Lazy Loaded Standalone Components).
