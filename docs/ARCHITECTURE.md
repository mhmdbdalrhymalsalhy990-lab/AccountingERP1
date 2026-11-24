# معمارية النظام المعتمدة (System Architecture v3.0) - Accounting ERP

نظام تخطيط موارد المؤسسات والمحاسبة المالية (Accounting ERP) مبني باستخدام معمارية البرمجيات النظيفة (**Clean Architecture / Onion Architecture**) لضمان الفصل التام بين منطق الأعمال المحاسبي (Accounting Business Logic) وتفاصيل البنية التحتية وقواعد البيانات وواجهات المستخدم.

---

## 1. مبادئ المعمارية النظيفة (Clean Architecture Principles)

تعتمد المعمارية على قاعدة التبعية الصارمة (Strict Dependency Rule): **الطبقات الخارجية تعتمد على الطبقات الداخلية، ولا تملك الطبقات الداخلية أي اعتماديات على مكتبات البنية التحتية أو العرض.**

```
               +-----------------------------------------+
               |        Presentation (WebApi / Angular)  |
               +--------------------+--------------------+
                                    |
               +--------------------v--------------------+
               |              Infrastructure             |
               |       (Persistence, Identity, JWT)      |
               +--------------------+--------------------+
                                    |
               +--------------------v--------------------+
               |               Application               |
               |        (CQRS, Services, DTOs)           |
               +--------------------+--------------------+
                                    |
               +--------------------v--------------------+
               |                 Domain                  |
               |      (Entities, Enums, Logic)           |
               +-----------------------------------------+
```

---

## 2. استراتيجية تعدد الشركات والعزل الحتمي (Defensive Multi-Tenancy Strategy)

تم تصميم النظام وفق **استراتيجية الدفاع ثلاثي المستويات (3-Layer Multi-Tenancy Defense)** لضمان استحالة تسريب أو اختلاط بيانات أي شركة تحت أي ظرف:

### المستوى 1: مستوى محرك قاعدة البيانات (Database Level - Composite Foreign Keys)
* كل الجداول التي تخص المستأجر تحتوي على عمود `CompanyId NOT NULL`.
* تم تطبيق **مفاتيح خارجية مركبة (Composite Foreign Keys)** تمنع اختلاط بيانات الشركات على مستوى المحرك الفيزيائي:
  1. `JournalEntryLines (CompanyId, JournalEntryId) -> JournalEntries (CompanyId, Id)`.
  2. `JournalEntryLines (CompanyId, AccountId) -> Accounts (CompanyId, Id)`.
  3. `Accounts (CompanyId, ParentAccountId) -> Accounts (CompanyId, Id)`.
  4. `CostCenters (CompanyId, ParentCostCenterId) -> CostCenters (CompanyId, Id)`.
  5. `BusinessPartners (CompanyId, AccountId) -> Accounts (CompanyId, Id)`.
  6. `TaxSettings (CompanyId, AccountId) -> Accounts (CompanyId, Id)`.
  7. `CompanyFinancialSettings (CompanyId, Accounts) -> Accounts (CompanyId, Id)`.
  8. `JournalEntries (CompanyId, BranchId) -> Branches (CompanyId, Id)`.
  9. `JournalEntries (CompanyId, FiscalPeriodId) -> FiscalPeriods (CompanyId, Id)`.
  10. `JournalEntries (CompanyId, ReversedEntryId) -> JournalEntries (CompanyId, Id)`.

### المستوى 2: مستوى الـ ORM (EF Core Global Query Filters)
* يتم حقن فلتر تلقائي في `OnModelCreating` في `ApplicationDbContext` لجميع الجداول التي تطبق `ITenantEntity`:
  ```csharp
  modelBuilder.Entity<T>().HasQueryFilter(e => e.CompanyId == _currentTenantService.CompanyId);
  ```
  هذا يضمن أن جميع استعلامات الـ LINQ تقوم بتوليد كود SQL محتوم بـ `WHERE CompanyId = @TenantId`.

### المستوى 3: مستوى معالجة الطلبات والحفظ (SaveChanges Interceptor)
* يعترض الـ `TenantSaveChangesInterceptor` أي عملية حفظ أو تعديل:
  - التحقق من مطابقة `CompanyId` في الكائنات المضافة مع هوية الشركة المصرح بها للمستخدم الحالي من واقع توكن الـ JWT.
  - حظر أي طلب يحاول تعديل سجل يتبع شركة أخرى.

---

## 3. الترقيم المحاسبي المتسلسل الصارم دون فجوات (Gapless Sequential Numbering)

* **القاعدة:** المعايير المحاسبية والقانونية لدفتر اليومية تشترط ترقيماً متسلسلاً غير منقطع ($1, 2, 3, 4\dots$).
* **آلية التنفيذ:**
  1. لا يتم تخصيص رقم القيد المحاسبي النهائي عند إنشاء المسودة (`Draft`).
  2. يتم توليد الرقم التسلسلي الصارم (`JV-2026-00001`) **حصراً عند لحظة الترحيل الناجح (`Status -> Posted`)** داخل معاملة قاعدة بيانات ذرية (`Atomic Transaction`).
  3. استخدام قفل مستوى الصف الصارم في SQL Server:
     `SELECT LastEntryNumber FROM CompanyJournalSequences WITH (UPDLOCK, ROWLOCK, HOLDLOCK) ...`
  4. إذا فشلت عملية الترحيل لأي سبب، يتم عمل `Rollback` للمعاملة بالكامل، مما يمنع حدوث أي فجوات نهائياً.

---

## 4. الحماية الصارمة من الحذف الخاطئ للقيود المحاسبية

* **إلغاء الحذف التتابعي (`ON DELETE RESTRICT / NO ACTION`):**
  - لا يمكن حذف أسطر القيد تلقائياً عند محاولة حذف رأس القيد.
* **حذف القيود المسودة فقط برمجياً:**
  - يتم حذف أسطر القيود المسودة (`Draft`) حصراً عبر عملية برمجية صريحة داخل كود الـ Application Service تتأكد من أن `Status == Draft`، ثم تحذف الأسطر أولاً يليها رأس القيد.
* **حظر حذف القيود المرحلة:**
  - محرك قاعدة البيانات سيرفض فيزيائياً حذف رأس أي قيد مرحل لوجود أسطر تابعة له (`Restrict`).
  - طبقة الـ Domain والـ Interceptor تمنع حذف أو تعديل أي قيد حالته `Posted`.
