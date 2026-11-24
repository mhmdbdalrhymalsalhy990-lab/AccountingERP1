# تقرير المراجعة التصحيحية والتدقيق المحاسبي النهائي (Accounting ERP v3.0)
**مشروع: Accounting ERP**  
**التاريخ: 2026-11-22**  
**الحالة: النسخة النهائية المعتمدة للتصميم الهندسي والمحاسبي**

---

## 1. ملخص المعالجات التصحيحية للنقاط الـ 12

تمت معالجة كافة الملاحظات الهندسية والمحاسبية الدقيقة وتحويلها إلى قيود صلبة على مستوى قاعدة البيانات (Database Constraints) ومعمارية النظام:

---

### النقطة 1: ربط سطر القيد برأس القيد بنفس الشركة (`JournalEntryLines` -> `JournalEntries`)
* **المشكلة:** في العلاقات العادية، يربط السطر برأس القيد عبر `JournalEntryId` فقط، مما يترك احتمالاً نظرياً أن يحمل السطر `CompanyId = 1` بينما رأس القيد `CompanyId = 2`.
* **الحل الهندسي:**
  - إضافة قيد مركب فريد في جدول `JournalEntries`: `UNIQUE (CompanyId, Id)`.
  - إنشاء **Composite Foreign Key** في جدول `JournalEntryLines`:
    ```sql
    FOREIGN KEY (CompanyId, JournalEntryId) 
    REFERENCES JournalEntries(CompanyId, Id) 
    ON DELETE NO ACTION;
    ```
* **الأثر:** يستحيل فيزيائياً في SQL Server حفظ سطر قيد برأس قيد تابع لشركة أخرى.

---

### النقطة 2: شجرة الحسابات والعلاقة الذاتية المركبة (`Accounts`)
* **المشكلة:** في شجرة الحسابات، كان `ParentAccountId` يشير إلى `Accounts(Id)`، مما قد يسمح بحساب أب من شركة أخرى.
* **الحل الهندسي:**
  - إنشاء **Composite Foreign Key** ذاتي (Self-Referencing):
    ```sql
    FOREIGN KEY (CompanyId, ParentAccountId) 
    REFERENCES Accounts(CompanyId, Id) 
    ON DELETE NO ACTION;
    ```
* **الأثر:** محرك قاعدة البيانات يضمن أن الحساب الابن والحساب الأب يتبعان نفس الشركة بنسبة 100%. (وإذا كان الحساب رئيسياً في المستوى الأول، تكون قيمة `ParentAccountId` فارغة `NULL`).

---

### النقطة 3: مراكز التكلفة والعلاقة الذاتية المركبة (`CostCenters`)
* **الحل الهندسي:**
  - تطبيق نفس القيد الذاتي المركب:
    ```sql
    FOREIGN KEY (CompanyId, ParentCostCenterId) 
    REFERENCES CostCenters(CompanyId, Id) 
    ON DELETE NO ACTION;
    ```
* **الأثر:** استحالة ربط مركز تكلفة بمركز أب تابع لشركة أخرى.

---

### النقطة 4: ربط شركاء الأعمال بحسابات نفس الشركة (`BusinessPartners`)
* **الحل الهندسي:**
  - ربط حساب العميل أو المورد بحساب في الدليل عبر مفتاح مركب:
    ```sql
    FOREIGN KEY (CompanyId, AccountId) 
    REFERENCES Accounts(CompanyId, Id) 
    ON DELETE NO ACTION;
    ```
* **الأثر:** يستحيل ربط عميل أو مورد بحساب محاسبي لشركة أخرى.

---

### النقطة 5: إعدادات الحسابات الافتراضية للنظام (`CompanyFinancialSettings`)
* **الحل الهندسي:**
  - فرض مفاتيح خارجية مركبة لجميع الحسابات النظامية الأربعة:
    ```sql
    FOREIGN KEY (CompanyId, RetainedEarningsAccountId) REFERENCES Accounts(CompanyId, Id) ON DELETE NO ACTION,
    FOREIGN KEY (CompanyId, CurrentYearEarningsAccountId) REFERENCES Accounts(CompanyId, Id) ON DELETE NO ACTION,
    FOREIGN KEY (CompanyId, ForexGainLossAccountId) REFERENCES Accounts(CompanyId, Id) ON DELETE NO ACTION,
    FOREIGN KEY (CompanyId, DefaultVatAccountId) REFERENCES Accounts(CompanyId, Id) ON DELETE NO ACTION;
    ```
* **الأثر:** ضمان أن حسابات إقفال الأرباح وفروق العملات والضريبة تتبع نفس الشركة حصراً.

---

### النقطة 6: ربط حساب الضريبة بالشركة المالكة للإعداد (`TaxSettings`)
* **الحل الهندسي:**
  - إضافة المفتاح المركب:
    ```sql
    FOREIGN KEY (CompanyId, AccountId) 
    REFERENCES Accounts(CompanyId, Id) 
    ON DELETE NO ACTION;
    ```
* **الأثر:** منع استخدام حساب ضريبي يتبع شركة أخرى.

---

### النقطة 7: إلغاء الحذف التتابعي (`ON DELETE CASCADE`) من أسطر القيود
* **المشكلة:** الحذف التتابعي على أسطر القيود قد يؤدي لحذف كارثي غير مقصود لبيانات تاريخية.
* **الحل الهندسي والمحاسبي:**
  - تحويل علاقة `JournalEntryLines` برأس القيد إلى:
    `ON DELETE RESTRICT` / `NO ACTION`.
  - **آلية حذف القيود المسودة (Draft):** تتم حصراً عبر كود الخدمة (Application Service) داخل **Explicit Database Transaction**:
    1. التأكد الصارم من أن القيد بحالة `Status == Draft`.
    2. حذف الأسطر أولاً برمجياً: `DELETE FROM JournalEntryLines WHERE JournalEntryId = @Id`.
    3. حذف رأس القيد: `DELETE FROM JournalEntries WHERE Id = @Id`.
    4. تثبيت المعاملة (`Commit`).
  - **الحماية:** القيود المرحلة `Status == Posted` مستحيل حذفها فيزيائياً لأن محرك قاعدة البيانات سيرفض حذف الرأس لوجود أسطر تابعة له (`Restrict`)، والـ Interceptor يمنع حذف الأسطر والرأس مرحلاً.

---

### النقطة 8: إعادة تصميم علاقة القيد الأصلي بالقيد العكسي (Reversal Engine)
* **الحل الهندسي:**
  1. **تطابق الشركة:** إضافة مفتاح خارجي مركب:
     ```sql
     FOREIGN KEY (CompanyId, ReversedEntryId) 
     REFERENCES JournalEntries(CompanyId, Id) 
     ON DELETE NO ACTION;
     ```
  2. **منع عكس القيد أكثر من مرة:** إنشاء فهرس فريد مفلتر:
     ```sql
     CREATE UNIQUE INDEX UQ_JournalEntries_ReversedEntryId 
     ON JournalEntries(CompanyId, ReversedEntryId) 
     WHERE ReversedEntryId IS NOT NULL;
     ```
     هذا الفهرس يجعل من المستحيل رياضياً إنشاء أكثر من قيد عكسي لنفس القيد الأصلي.
  3. **منع عكس القيد العكسي نفسه:**
     إضافة قيد فحص `Check Constraint`:
     ```sql
     CHECK (NOT (EntryType = 4 AND ReversedEntryId IS NULL))
     ```
     وفي كود الترحيل: يُمنع عكس أي قيد إذا كان `EntryType == Reversal` أو `IsReversed == true`.

---

### النقطة 9: آلية الترقيم التسلسلي المحاسبي (Sequential Numbering & Gapless Guarantee)
* **المقارنة والتحليل:**
  * **SQL Sequence:** يضمن التفرد العالي والأداء الفائق تحت الضغط المتزامن، لكنه **ليس عديم الفجوات (Not Gapless)**؛ لأنه عند حدوث `Rollback` أو إعادة تشغيل الخادم، تُفقد الأرقام المحجوزة في الذاكرة مما يولد فجوات في الترقيم (مثلاً: 1, 2, 5, 6).
  * **الترقيم المحاسبي الصارم غير المنقطع (Gapless Sequential Numbering):** تشترطه المعايير الضريبية والقانونية لدفتر اليومية.
* **التصميم المعتمد:**
  1. لا يتم توليد رقم القيد عند إنشاء المسودة (المسودة تأخذ رقماً مؤقتاً: `DRAFT-XXXX`).
  2. يتم توليد الرقم التسلسلي الصارم (`JV-2026-00001`) **حصراً عند لحظة الترحيل الناجح (`Status -> Posted`)**.
  3. إنشاء جدول ترقيم: `CompanyJournalSequences` يحتوي على `(CompanyId, FiscalYearId, LastNumber)`.
  4. داخل الـ Transaction الخاصة بالترحيل، يتم قفل الصف ذرّياً:
     ```sql
     SELECT LastNumber 
     FROM CompanyJournalSequences WITH (UPDLOCK, ROWLOCK, HOLDLOCK) 
     WHERE CompanyId = @CompanyId AND FiscalYearId = @FiscalYearId;
     
     UPDATE CompanyJournalSequences 
     SET LastNumber = LastNumber + 1 
     WHERE CompanyId = @CompanyId AND FiscalYearId = @FiscalYearId;
     ```
  5. إذا فشل الترحيل أو حدث `Rollback`، لا يزيد الرقم، مما يضمن **عدم وجود أي فجوات نهائياً (Gapless)** وتفرد الترقيم بنسبة 100%.

---

### النقطة 10: الفترات المالية وتداخل التواريخ وفترة التسويات (Fiscal Periods & Period 13)
* **الحلول والتعديلات:**
  1. **منع تداخل الفترات المالية:**
     - يتم ضبط الفترات بتواريخ متصلة غير متداخلة:
       `Period(N).StartDate = Period(N-1).EndDate + 1 Day`.
  2. **مطابقة تاريخ القيد للفترة:**
     - قيد فحص أو تحقق نطاق: تاريخ القيد `EntryDate` يجب أن يقع قطعاً بين `StartDate` و `EndDate` للفترة المالية المرتبطة به.
  3. **دعم السنوات غير الميلادية:**
     - الحقول `StartDate` و `EndDate` في `FiscalYears` تقبل أي نطاق زمني (مثلاً: من 1 يوليو إلى 30 يونيو، أو سنوات هجرية)، حيث يعتمد النظام على التواريخ الفعلية دون فرض التقويم الميلادي النمطي.
  4. **دعم فترة التسويات الجردية (Period 13 / Adjustment Period):**
     - إضافة نوع الفترة `PeriodType` في `FiscalPeriods`:
       - `1 = Regular` (فترة عادية 1 إلى 12).
       - `2 = YearEndAdjustment` (فترة تسويات جردية رقم 13، تقع في اليوم الأخير من السنة المالية وتُخصص لقيود مراجعي الحسابات الخارجية وإهلاك الأصول وجرد المخزون السنوي دون المساس بحسابات شهر 12).

---

### النقطة 11: الضوابط الصارمة لإقفال السنة المالية (Year-End Closing Controls)
* **المصفوفة الرقابية قبل تنفيذ الإقفال:**
  1. `CheckUnbalancedEntries`: التحقق من عدم وجود أي قيد غير متوازن في السنة ككل.
  2. `CheckPendingDrafts`: التحقق من خلو السنة من أي قيود في حالة مسودة (`Draft Count == 0`).
  3. `CheckAllPeriodsClosed`: التحقق من أن جميع الفترات من 1 إلى 13 في حالة `Closed`.
  4. `EnforceSingleClosingEntry`: تثبيت معرف قيد الإقفال `ClosingJournalEntryId` في جدول `FiscalYears`، مع فهرس فريد يمنع تكراره.
  5. `EnforceSingleOpeningEntry`: إنشاء قيد الأرصدة الافتتاحية للسنة الجديدة مرة واحدة فقط مع منع التكرار عبر:
     `UNIQUE (CompanyId, FiscalYearId, EntryType) WHERE EntryType = 2 (OpeningBalance)`.
  6. `PermanentAccountsOnly`: ترحيل أرصدة الميزانية العمومية فقط (أصول، خصوم، حقوق ملكية مع الأرباح المبقاة المحدثة)، وتصفير الحسابات الاسمية (إيرادات ومصروفات).
  7. `ValidateOpeningBalance`: التأكد الصارم من توازن قيد الأرصدة الافتتاحية الجديد قبل اعتماده.

---

### النقطة 12: قواعد العملات وأسعار الصرف وتاريخيتها (Currency & Historical Rate Integrity)
* **اتجاه سعر الصرف المعتمد (Direct Quotation):**
  - السعر يُعرّف دائماً: **كم وحدة من عملة الأساس تساوي وحدة واحدة من العملة الأجنبية؟**
  - المعادلة المحاسبية:
    $$\text{المبلغ بعملة الأساس} = \text{المبلغ بالعملة الأجنبية} \times \text{ExchangeRate}$$
    *مثال:* عملة الأساس ريال سعودي (SAR)، العملة الأجنبية دولار (USD). سعر الصرف = `3.750000`. مبلغ 100 دولار = $100 \times 3.750000 = 375.00$ ريال.
* **قاعدة التقريب المحاسبي المعتمدة:**
  - تطبيق تقريب المصرفيين المعتمد دولياً (**Banker's Rounding: MidpointRounding.ToEven**):
    `Math.Round(ForeignAmount * ExchangeRate, currency.DecimalPlaces, MidpointRounding.ToEven)`
* **ثبات سعر الصرف التاريخي في السجل:**
  - يتم تخزين `ExchangeRate` المستخدم لحظة تسجيل القيد في سطر القيد `JournalEntryLines.ExchangeRate` بشكل دائم وغير قابل للتعديل.
  - أي تغيير مستقبلي في جدول أسعار الصرف `CurrencyExchangeRates` لا يمس ولا يغير أي قيد مسجل تاريخياً.
