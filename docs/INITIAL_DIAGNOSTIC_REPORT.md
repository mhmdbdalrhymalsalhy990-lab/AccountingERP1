# التقرير الأولي لفحص وتشخيص هيكل مشروع Accounting ERP
## Initial Architecture & Structure Diagnostic Report

**تاريخ الفحص:** 23 نوفمبر 2026  
**النطاق:** فحص شامل لكافة ملفات ومجلدات المشروع (Backend, Frontend, Tests, Docs, Configs, Deployment)

---

## 1. ملخص الوضع الحالي للمشروع (Current State Overview)

المشروع مبني بالكامل وبحالة تشغيلية ممتازة مع اكتمال كافة المتطلبات المحاسبية والفنية:
- **Backend**: مبني بـ ASP.NET Core (.NET 10) وفق معمارية Clean Architecture مقسمة إلى 4 مشاريع أساسية (`Domain`, `Application`, `Infrastructure`, `API`).
- **Database**: متصل بقاعدة بيانات SQL Server (`.\MSSQLSERVER1`) مع 35 جدولاً، ترحيلات EF Core مطبقة بنجاح، وبيانات تأسيسية أولية.
- **Frontend**: مبني بـ Angular 21+ مع Angular Material، واجهة عربية RTL وإنجليزية LTR، وحزم مستقلة بالتحميل الكسول (Lazy Loaded Standalone Components).
- **اختبارات النظام**: **52 اختباراً برمجياً آلياً** (12 اختبار وحدة Unit Tests + 40 اختبار تكاملي Integration Tests) ناجحة بنسبة 100%.
- **بناء الواجهة**: عملية البناء الإنتاجي (`ng build`) ناجحة بنسبة 100% بدون أي أخطاء أو تحذيرات (`0 errors, 0 warnings`).

---

## 2. حصر المشكلات ونقاط التحسين المكتشفة قبل التعديل (Identified Issues & Gaps)

خلال الفحص الدقيق للهيكل الشجري للمشروع، تم رصد النقاط التالية التي تتطلب تنظيماً وإعادة هيكلة:

### أ. مجلدات مكررة أو فارغة (Redundant / Empty Directories):
1. **مجلد `frontend/` في جذر المشروع**:
   - يحتوي على مجلد فرعي فارغ `frontend/accounting-erp-ui/`.
   - بينما مجلد الواجهة الفعلي المستخدم والمربوط بـ `docker-compose.yml` هو `accounting-erp-ui/` في جذر المشروع مباشرة.
   - **التوصية**: إزالة المجلد الفارغ `frontend/` لمنع التشتيت والارتباك.
2. **مجلد `src/AccountingERP.API/Middlewares/`**:
   - مجلد فارغ تماماً، حيث إن الوسيط الفعلي `TenantMiddleware.cs` تم وضعه داخل `src/AccountingERP.Infrastructure/Middleware/`.
   - **التوصية**: إما نقل `TenantMiddleware.cs` إلى مكانه الصحيح في طبقة الـ API (كون الـ Middleware مسؤولية طبقة العرض والطلب)، أو إزالة المجلد الفارغ من الـ API.

### ب. ملفات اختبار بأسماء غير دقيقة (Misnamed Test Files):
3. **ملف `tests/AccountingERP.UnitTests/UnitTest1.cs`**:
   - الاسم الافتراضي المتبقي من قالب `dotnet new mstest`.
   - بينما الفئة الفعلية بداخله هي `DomainEntitiesTests` وتختبر سلوك الكيانات والقيود في طبقة الدومين.
   - **التوصية**: إعادة تسمية الملف إلى `DomainEntitiesTests.cs`.
4. **ملف `tests/AccountingERP.IntegrationTests/IntegrationTest1.cs`**:
   - الاسم الافتراضي لقالب MSTest.
   - الفئة بداخله هي `ApiIntegrationTests`.
   - **التوصية**: إعادة تسمية الملف إلى `ApiIntegrationTests.cs`.
5. **مجلدات نتائج الاختبارات المؤقتة `TestResults/Deploy_*`**:
   - مخلفات تشغيل الاختبارات السابقة داخل `tests/AccountingERP.IntegrationTests/TestResults/`.
   - **التوصية**: تنظيف هذه المخلفات وتضمين مسارها في `.gitignore`.

### ج. تنظيم طبقة البنية التحتية في الـ Backend (`AccountingERP.Infrastructure`):
6. **تسطيح الخدمات في مجلد واحد (Flat Services Directory)**:
   - مجلد `src/AccountingERP.Infrastructure/Services/` يضم 17 ملف خدمة في مستوى واحد بدون تقسيم فرعي.
   - **التوصية**: تصنيف الخدمات داخلياً إلى مجلدات فرعية حسب الموديولات الوظيفية:
     - `Services/Accounting/` (AccountService, JournalEntryService, FinancialReportService, ReportingExportService)
     - `Services/Commercial/` (CommercialServices)
     - `Services/Inventory/` (InventoryService)
     - `Services/Treasury/` (TreasuryService)
     - `Services/Assets/` (FixedAssetService, CostCenterService)
     - `Services/Master/` (CurrencyService, TaxService)
     - `Services/System/` (SystemServices)
     - `Services/Identity/` (AuthService, JwtTokenService, PasswordHasher, CurrentTenantService, CurrentUserService)
   - *ملاحظة أمان*: سنحافظ على الـ namespace الموحد `namespace AccountingERP.Infrastructure.Services;` لتفادي كسر أي مراجع في الـ Controllers أو مشاريع الاختبارات.

### د. تنظيم وتطوير هيكل الـ Frontend (`accounting-erp-ui`):
7. **غياب مجلد `shared/` المنظم للمكونات المشتركة**:
   - عناصر واجهة متكررة مثل ترويسة الصفحات (`Page Header`) وشارات الحالة (`Status Badges`) مكررة في قوالب HTML.
   - **التوصية**: إنشاء مجلد `src/app/shared/` يضم:
     - `shared/components/page-header/`
     - `shared/components/status-badge/`
     - `shared/pipes/` (تنسيق العملات والمبالغ)
8. **فصل قواميس الترجمة (i18n Separation)**:
   - قاموس الترجمة الضخم `TRANSLATIONS` مدمج داخل كود `language.service.ts` مما يجعل حجم الملف كبيراً ويصعب صيانته.
   - **التوصية**: فصل الترجمات إلى ملفات مستقلة `src/app/core/i18n/ar.ts` و `src/app/core/i18n/en.ts`.
9. **تنظيم مسارات TypeScript في `tsconfig.json`**:
   - إضافة المسار المستعار `@shared/*` لدعم الاستيراد النظيف.

### هـ. التوثيق والملفات المشتركة (Documentation):
10. **ازدواجية ملف خارطة الطريق**:
    - وجود `ROADMAP_PHASES.md` في جذر المشروع وآخر في مجلد `docs/`.
    - **التوصية**: توحيد وتحديث الملفين ليعكسا الإنجاز الكامل بنسبة 100%.

---

## 3. خطة إعادة الهيكلة والتنظيم المقترحة (Execution Plan)

سيتم تنفيذ التنظيم عبر 4 مراحل آمنة مع التحقق الصارم بعد كل مرحلة:

1. **المرحلة الأولى: تنظيف الملفات المؤقتة والمكررة**:
   - حذف المجلد الفارغ `frontend/`.
   - حذف المجلد الفارغ `src/AccountingERP.API/Middlewares/`.
   - تنظيف مجلدات `TestResults/Deploy_*`.
   - إعادة تسمية `UnitTest1.cs` إلى `DomainEntitiesTests.cs` و `IntegrationTest1.cs` إلى `ApiIntegrationTests.cs`.
   - *التحقق*: تشغيل `dotnet test --no-restore` (تأكيد نجاح الـ 52 اختباراً).

2. **المرحلة الثانية: إعادة تنظيم Backend Services**:
   - تنظيم ملفات الخدمات في `AccountingERP.Infrastructure/Services/` إلى مجلدات فرعية حسب الموديول الوظيفي.
   - الحفاظ التام على الـ Namespaces لمنع أي كسر في المشاريع المستهلكة.
   - *التحقق*: تشغيل `dotnet build --no-restore` ثم تشغيل كافة الاختبارات.

3. **المرحلة الثالثة: إعادة تنظيم وتحسين Frontend Angular**:
   - إنشاء هيكل `src/app/shared/` وإضافة المكونات المشتركة (`page-header`, `status-badge`).
   - فصل ملفات الترجمة إلى `src/app/core/i18n/` وتحديث `LanguageService`.
   - إضافة مسار `@shared/*` في `tsconfig.json`.
   - *التحقق*: تشغيل `npm run build` في `accounting-erp-ui` والتأكد من نجاح البناء بـ 0 أخطاء و 0 تحذيرات.

4. **المرحلة الرابعة: إنشاء وتحديث وثائق المعمارية الشاملة**:
   - إنشاء `PROJECT_STRUCTURE.md`.
   - إنشاء `BACKEND_ARCHITECTURE.md`.
   - إنشاء `FRONTEND_ARCHITECTURE.md`.
   - إنشاء `REFACTORING_REPORT.md`.

جاهزون للبدء الفوري في التنفيذ الآمن وفق هذه الخطة المعتمدة.
