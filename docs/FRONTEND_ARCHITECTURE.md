# معمارية الواجهة الأمامية (Frontend Architecture Documentation)
## Angular 21+ Enterprise Architecture - Accounting ERP UI

يوثق هذا المستند الهيكل البرمجي المعتمد لواجهة المستخدم **accounting-erp-ui** المبنية بأحدث تقنيات إطار العمل **Angular 21+** ومكتبة **Angular Material**.

---

## 1. فلسفة التصميم والمعمارية القائمة على الميزات (Feature-Based Architecture)

تعتمد الواجهة الأمامية تصميماً معمارياً مقسماً إلى 4 طبقات رئيسية واضحة المعالم:

```
src/app/
├── core/       # النواة المركزية وخدمات الـ Singleton المحمية
├── shared/     # المكونات والأنابيب المشتركة القابلة لإعادة الاستخدام
├── layout/     # إطار التطبيق الخارجي وهيكل العرض Sidenav & Header
└── features/   # موديولات وشاشات النظام الوظيفية المستقلة
```

### أ. طبقة النواة (`Core Layer`)
- **`guards/auth.guard.ts`**: حماية المسارات والتحقق من مصادقة المستخدم وتوجيهه لشاشة الدخول عند الحاجة.
- **`interceptors/jwt.interceptor.ts`**: حقن التوكن الأمني `Bearer JWT` وترويسات التعددية `X-Company-Id` و `X-Branch-Id` تلقائياً مع كل طلب HTTP.
- **`interceptors/error.interceptor.ts`**: معالجة مركزية للأخطاء وتقديم رسائل تنبيه واضحة عبر `MatSnackBar`.
- **`i18n/`**: إدارة قواميس اللغتين العربية (`ar.ts`) والإنجليزية (`en.ts`) في ملفات بيانات مستقلة وسهلة التعديل.
- **`services/`**: إدارة حالة التطبيق باستخدام **Angular Signals** الحديثة:
  - `AuthService`: إدارة بيانات الدخول والملف الشخصي والتوكن والصلاحيات.
  - `TenantService`: إدارة واختيار الشركة والفرع وتحديث الحالة لحظياً.
  - `LanguageService`: تبديل اللغة وتعديل اتجاه الصفحة (`rtl`/`ltr`) وخصائص الـ DOM.
  - `ExportService`: محرك تصدير ملفات Excel/CSV بترميز UTF-8 BOM والطباعة.
  - خدمات الموديولات المحاسبية: `GeneralLedgerService`, `CommercialService`, `InventoryService`, `BankingService`, `FixedAssetsService`, `FinancialReportsService`, `SettingsService`.

### ب. الطبقة المشتركة (`Shared Layer`)
- **`components/page-header/`**: مكون مستقل موحد لترويسة الصفحات مع العناوين ومساحة للأزرار والإجراءات السريعة.
- **`components/status-badge/`**: شارة الحالة الملونة الموحدة (مرحل، مسودة، نشط، معطل) تضمن اتساق الواجهات.
- **`pipes/currency-format.pipe.ts`**: أنبوب تنسيق الأرقام والعملات مع دعم الفواصل العشرية ورمز العملة (SAR).
- **`index.ts`**: Barrel Export لتسهيل الاستيراد من مسار موحد عبر المسار المستعار `@shared/*`.

### ج. طبقة الهيكل العام (`Layout Layer`)
- **`layout/main-layout/`**:
  - الشريط العلوي (Toolbar): يحوي محول الشركات المنسدل (Company Switcher)، زر التبديل الفوري للغة (AR / EN)، وبيانات المستخدم مع خيار تسجيل الخروج.
  - القائمة الجانبية (Sidenav Drawer): قائمة ملاحة متجاوبة تضم روابط كافة الموديولات الـ 15 مع أيقونات Material المتناسقة.

### د. طبقة الشاشات الوظيفية (`Features Layer`)
تضم 10 موديولات فرعية مستقلة، كل منها يحتوي على مكونات مستقلة (Standalone Components) بنظام التحميل الكسول:
1. `auth/login`: تسجيل الدخول مع التحقق وتعبئة الحساب التجريبي.
2. `dashboard`: لوحة القيادة والمؤشرات المالية KPI.
3. `general-ledger`: دليل الحسابات الشجري، القيود اليومية مع نموذج التوازن الآلي، وكشف الأستاذ العام.
4. `commercial`: العملاء، الموردين، فواتير المبيعات، فواتير المشتريات، ونموذج طباعة الفاتورة الضريبية ZATCA.
5. `inventory`: المنتجات والمخزون ومتوسط التكلفة WAC.
6. `treasury`: الحسابات البنكية والخزائن والمطابقات البنكية.
7. `assets`: الأصول الثابتة وحساب الإهلاك الخطي.
8. `reports`: القوائم المالية الختامية (قائمة الدخل، الميزانية العمومية، وميزان المراجعة).
9. `security`: المستخدمين والأدوار والشركات المصرح بها وصلاحيات RBAC.
10. `settings`: إعدادات الضرائب والعملات وأسعار الصرف.

---

## 2. المسارات المستعارة للاستيراد النظيف (Path Aliases)

تم تكوين مسارات TypeScript المستعارة في `tsconfig.json` و `tsconfig.app.json` لضمان استيراد نظيف ومستقر مع دعم ملفات الـ Barrel Exports المباشرة (`@shared`, `@core`):
```json
"paths": {
  "@core": ["src/app/core/index.ts"],
  "@core/*": ["src/app/core/*"],
  "@shared": ["src/app/shared/index.ts"],
  "@shared/*": ["src/app/shared/*"],
  "@features/*": ["src/app/features/*"],
  "@layout/*": ["src/app/layout/*"],
  "@environments/*": ["src/environments/*"]
}
```

### استخدام المكونات المشتركة في شاشات الميزات:
- **`PageHeaderComponent`**: مستخدم فعلياً في شاشات القيود اليومية، فواتير المبيعات، فواتير المشتريات، العملاء، والموردين.
- **`StatusBadgeComponent`**: مستخدم لعرض حالات القيود والفواتير (مرحل، مسودة).
- **`CurrencyFormatPipe`**: مستخدم لتنسيق المبالغ المالية بدقة وإضافة رمز العملة (SAR).

---

## 3. التحميل الكسول للمسارات (Lazy Loading Route Strategy)

كافة المسارات في `app.routes.ts` تُحمّل عند الطلب فقط عبر الدالة `loadComponent`:
```typescript
{
  path: 'accounts',
  loadComponent: () => import('./features/general-ledger/chart-of-accounts/chart-of-accounts').then(m => m.ChartOfAccountsComponent)
}
```
هذا الأسلوب يُنتج حزماً صغيرة الحجم تضمن أقصى سرعة تحميل وتجربة مستخدم فورية (Instant User Experience).

---

## 4. نتائج فحص البناء الإنتاجي (Production Build Verification)

```
Initial chunk files:
main.js:    738.57 kB (159.16 kB gzip)
styles.css: 105.98 kB (8.31 kB gzip)

Lazy chunk files (Generated Independently):
✔ journal-entries.js (14.45 kB)
✔ invoice-print.js (12.63 kB)
✔ trial-balance.js (8.87 kB)
✔ ledger-statement.js (8.45 kB)
✔ financial-statements.js (8.24 kB)
✔ dashboard.js (7.88 kB)
✔ chart-of-accounts.js (7.51 kB)
✔ login.js (7.29 kB)
✔ suppliers.js (6.51 kB)
✔ customers.js (6.51 kB)
✔ users-permissions.js (5.93 kB)
✔ purchase-invoices.js (5.64 kB)
------------------------------------------------------------
Application bundle generation complete: 0 errors, 0 warnings!
```
