# معمارية الخادم الخلفي (Backend Architecture Documentation)
## Accounting ERP - Clean Architecture & Engineering Design

يوثق هذا المستند التفاصيل المعمارية الدقيقة للطبقات الخلفية وخادم **ASP.NET Core Web API (.NET 10)** وقاعدة بيانات **Microsoft SQL Server**.

---

## 1. أسلوب المعمارية النظيفة (Clean Architecture Layers)

يتبع النظام مبادئ Clean Architecture مع فصل صارم للاتجاهات وتبعية داخلية أحادية الاتجاه (Dependency Rule):

```mermaid
graph TD
    API["AccountingERP.API (Presentation Layer)"] --> Application["AccountingERP.Application (Use Cases)"]
    API --> Infrastructure["AccountingERP.Infrastructure (Data & External Services)"]
    Infrastructure --> Application
    Infrastructure --> Domain["AccountingERP.Domain (Enterprise Business Rules)"]
    Application --> Domain
```

### أ. Domain Layer (نواة الأعمال الصافية)
- لا تعتمد على أي مكتبة أو طبقة خارجية (Zero External Dependencies).
- تحوي الكيانات الأساسية الـ 35، محددات الحالات (Enums)، واستثناءات النطاق المالي (`Domain Exceptions`).
- تفرض قواعد الأعمال الحتمية (مثل: منع إنشاء قيد محاسبي غير متوازن).

### ب. Application Layer (حالات الاستخدام والعقود)
- تحوي كائنات نقل البيانات (DTOs) المعزولة عن كيانات قاعدة البيانات.
- تحوي واجهات الخدمات (Interfaces) التي تحدد العمليات المتاحة دون معرفة تفاصيل تنفيذها في قاعدة البيانات.
- تحوي النماذج المساعدة ومحددات التصفية (`Pagination & Filtering`).

### ج. Infrastructure Layer (البنية التحتية والتنفيذ الفعلي)
- تنفيذ واجهات التطبيق بالكامل باستخدام **Entity Framework Core 10**.
- إدارة سياق قاعدة البيانات `ApplicationDbContext` وتطبيق فلاتر الاستعلام الشاملة (Global Query Filters).
- معترض تدقيق الحركات `AuditingAndTenantInterceptor` لحقن معرفات التدقيق وحماية القيود المرحلة من الحذف أو التعديل.
- خدمات الأمان والتشفير (JWT Token Generation, PBKDF2 Password Hashing).
- محرك تشفير الفاتورة الضريبية ZATCA TLV Base64 وتصدير CSV/Excel بترميز BOM UTF-8.

### د. API Layer (طبقة العرض والتحكم)
- 20 متحكم RESTful API تغطي كافة متطلبات النظام.
- تطبيق وسيط التعددية `TenantMiddleware` لقراءة وتأكيد معرفات الشركات والفروع.
- فحص الصلاحيات الصارم عبر سمة `[HasPermission("...")]` على مستوى كل Endpoint.

---

## 2. استراتيجية تعدد المستأجرين ومبدأ الانغلاق الآمن (Multi-Tenancy & Fail-Closed Strategy)

يعتمد النظام أسلوب **قاعدة البيانات المشتركة والمخطط المشترك مع العزل المنطقي الصارم وتطبيق مبدأ الانغلاق الآمن (Fail-Closed Architecture)**:

1. **الكيانات متعددة المستأجرين (`ITenantEntity`)**:
   - كل كيان يتبع لشركة معينة يحمل خاصية `int CompanyId { get; set; }` غير قابلة لأن تكون `null`.
   - في بعض الكيانات التشغيلية، يضاف `int BranchId { get; set; }` لتحديد الفرع.

2. **فلاتر الاستعلام العامة الصارمة (Fail-Closed Global Query Filters)**:
   - يتم تطبيق فلتر استعلام تلقائي وإلزامي في `ApplicationDbContext.OnModelCreating`:
     ```csharp
     entity.HasQueryFilter(e => !e.IsDeleted && e.CompanyId == CurrentCompanyId);
     ```
   - **مبدأ Fail-Closed**: إذا كانت قيمة `CurrentCompanyId == null` (مثل الطلبات غير الموثقة أو قبل اختيار الشركة)، فإن الاستعلام يترجم في SQL Server إلى `WHERE CompanyId = NULL` مما يعيد **0 سجلات حتماً**. لا يمكن لأي طلب بدون سياق شركة موثق الوصول إلى أي سجلات لأي شركة نهائياً.

3. **حماية التعديل والإضافة والحذف في المعترض (`AuditingAndTenantInterceptor`)**:
   - **عند الإضافة (`Added`)**: يُمنع منعاً باتاً إضافة سجلات لشركة غير مصرح بها. إذا كان الكيان يحمل `CompanyId` مختلف عن `CurrentCompanyId`، يرمي النظام فوراً `UnauthorizedAccessException`.
   - **عند التعديل أو الحذف (`Modified / Deleted`)**: يمنع النظام تعديل أو حذف أي كيان تابع لشركة أخرى حتى لو كان الكيان محملاً في الـ ChangeTracker.
   - إذا لم يكن هناك سياق شركة موثق (`CurrentCompanyId == null`)، تُرفض كافة عمليات الإضافة والتعديل والحذف لحماية سلامة البيانات.

4. **وسيط التعددية والحماية من التلاعب (`TenantMiddleware` & Anti-Tampering)**:
   - يقع في طبقة العرض `AccountingERP.API/Middlewares/TenantMiddleware.cs` وفق مبادئ Clean Architecture.
   - لا يقبل ترويسات `X-Company-Id` و `X-Branch-Id` من أي طلب غير موثق.
   - في الطلبات الموثقة، يقارن قيمة `X-Company-Id` مع الـ Claim المعتمد في رمز JWT؛ وإذا وجد أي اختلاف أو محاولة تلاعب بالترويسات، يقوم بقطع الطلب فوراً برمز الاستجابة `403 Forbidden` برسالة واضحة دون استهلاك موارد النظام.

---

## 3. المحركات المحاسبية والمالية الجوهرية (Core Accounting Engines)

### أ. محرك الترقيم المتسلسل غير المتقطع (Gapless Sequential Numbering Engine)
- للامتثال للمعايير المحاسبية والقانونية، يُمنع منعاً باتاً وجود فجوات في أرقام القيود أو الفواتير.
- يتم الترقيم عبر مسار قفل متزامن مخصص:
  - صيغة القيود: `JE-YYYY-XXXXXX` (مثال: `JE-2026-000001`).
  - صيغة فواتير المبيعات: `INV-YYYY-XXXXXX`.
  - صيغة فواتير المشتريات: `PINV-YYYY-XXXXXX`.

### ب. محرك القيد المزدوج والتحقق من التوازن (Double-Entry Invariants)
- كل معاملة مالية تُثبت عبر قيد يومية متوازن:
  $$\sum \text{Debit} = \sum \text{Credit}$$
- عند ترحيل القيد، يتم الانتقال من حالة `Draft` إلى `Posted` مع تثبيت تاريخ الترحيل واسم المستخدم المرحل.
- القيود المرحلة محمية بنسبة 100% ضد أي تعديل أو حذف بواسطة كود حماية حتمي داخل `AuditingAndTenantInterceptor`.

### ج. محرك متوسط التكلفة المرجح المتحرك (Moving Weighted Average Cost - WAC)
- يُعاد احتساب تكلفة الوحدة لكل صنف مخزني عند كل عملية توريد / شراء جديدة وفق المعادلة المحاسبية:
  $$\text{New WAC} = \frac{(\text{Current Qty} \times \text{Current Cost}) + (\text{Incoming Qty} \times \text{Incoming Price})}{\text{Current Qty} + \text{Incoming Qty}}$$
- يُستخدم هذا السعر اللحظي بدقة عند الصرف أو البيع لتحديد تكلفة البضاعة المباعة (COGS) بدقة تامة.

### د. محرك الإهلاك الخطي للأصول الثابتة (Straight-Line Depreciation Engine)
- يُحسب الإهلاك الشهري الثابت وفق الصيغة المعتمدة:
  $$\text{Monthly Depreciation} = \frac{\text{Acquisition Cost} - \text{Salvage Value}}{\text{Useful Life in Months}}$$
- يُنشئ المحرك آلياً قيد يومية متوازن متسلسل:
  - **مدين**: مصروف إهلاك الأصول الثابتة (Depreciation Expense).
  - **دائن**: مجمع إهلاك الأصول الثابتة (Accumulated Depreciation).

### هـ. محرك الفاتورة الإلكترونية السعودية (ZATCA e-Invoicing Engine)
- ترميز مصفوفة TLV (Tag-Length-Value) المعتمدة من هيئة الزكاة والضريبة والجمارك وتحويلها إلى Base64:
  - **Tag 1**: اسم المنشأة (UTF-8).
  - **Tag 2**: الرقم الضريبي (15 خانة).
  - **Tag 3**: الختم الزمني ISO 8601 (`yyyy-MM-ddTHH:mm:ssZ`).
  - **Tag 4**: إجمالي الفاتورة شاملاً الضريبة (`0.00`).
  - **Tag 5**: مبلغ ضريبة القيمة المضافة (`0.00`).

---

## 4. نتائج الجودة والاختبارات الآلية (Verification Matrix)

```
Test Execution Summary:
------------------------------------------------------------
✔ AccountingERP.UnitTests:        12 Passed, 0 Failed, 0 Skipped
✔ AccountingERP.IntegrationTests: 40 Passed, 0 Failed, 0 Skipped
------------------------------------------------------------
Total Automated Tests:            52 Passed (100% Success Rate)
Build Status:                     0 Errors, 0 Warnings (--no-restore verified)
```
