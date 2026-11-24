using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Starting database foundational seeding...");

            // 1. Countries
            if (!await context.Countries.AnyAsync())
            {
                logger.LogInformation("Seeding Countries...");
                var countries = new List<Country>
                {
                    new() { Code = "SA", NameAr = "المملكة العربية السعودية", NameEn = "Saudi Arabia", PhoneCode = "+966" },
                    new() { Code = "YE", NameAr = "الجمهورية اليمنية", NameEn = "Yemen", PhoneCode = "+967" },
                    new() { Code = "AE", NameAr = "الإمارات العربية المتحدة", NameEn = "United Arab Emirates", PhoneCode = "+971" },
                    new() { Code = "US", NameAr = "الولايات المتحدة الأمريكية", NameEn = "United States", PhoneCode = "+1" }
                };
                await context.Countries.AddRangeAsync(countries);
                await context.SaveChangesAsync();
            }
            else
            {
                // Ensure migration/replacement from EG to YE if present
                var egCountry = await context.Countries.FirstOrDefaultAsync(c => c.Code == "EG");
                if (egCountry != null)
                {
                    egCountry.Code = "YE";
                    egCountry.NameAr = "الجمهورية اليمنية";
                    egCountry.NameEn = "Yemen";
                    egCountry.PhoneCode = "+967";
                    await context.SaveChangesAsync();
                }
            }

            // 2. Currencies
            if (!await context.Currencies.AnyAsync())
            {
                logger.LogInformation("Seeding Currencies...");
                var currencies = new List<Currency>
                {
                    new() { Code = "SAR", NameAr = "ريال سعودي", NameEn = "Saudi Riyal", Symbol = "ر.س", DecimalPlaces = 2, IsActive = true },
                    new() { Code = "USD", NameAr = "دولار أمريكي", NameEn = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true },
                    new() { Code = "EUR", NameAr = "يورو", NameEn = "Euro", Symbol = "€", DecimalPlaces = 2, IsActive = true },
                    new() { Code = "YER", NameAr = "ريال يمني", NameEn = "Yemeni Rial", Symbol = "ر.ي", DecimalPlaces = 2, IsActive = true }
                };
                await context.Currencies.AddRangeAsync(currencies);
                await context.SaveChangesAsync();
            }
            else
            {
                // Ensure migration/replacement from EGP to YER if present
                var egpCurrency = await context.Currencies.FirstOrDefaultAsync(c => c.Code == "EGP");
                if (egpCurrency != null)
                {
                    egpCurrency.Code = "YER";
                    egpCurrency.NameAr = "ريال يمني";
                    egpCurrency.NameEn = "Yemeni Rial";
                    egpCurrency.Symbol = "ر.ي";
                    await context.SaveChangesAsync();
                }
            }

            var saCountry = await context.Countries.FirstAsync(c => c.Code == "SA");
            var sarCurrency = await context.Currencies.FirstAsync(c => c.Code == "SAR");

            // 3. Default Company
            Company company;
            if (!await context.Companies.IgnoreQueryFilters().AnyAsync())
            {
                logger.LogInformation("Seeding Default Company...");
                company = new Company
                {
                    Code = "COMP01",
                    NameAr = "شركة الأفق للتجارة والخدمات المحدودة",
                    NameEn = "Al-Ofuq Trading & Services Co. Ltd.",
                    TaxNumber = "300012345600003",
                    CommercialRegistration = "1010123456",
                    CountryId = saCountry.Id,
                    BaseCurrencyId = sarCurrency.Id,
                    Address = "الرياض، المملكة العربية السعودية",
                    Phone = "+966112345678",
                    Email = "info@alofuq.com",
                    IsActive = true
                };
                await context.Companies.AddAsync(company);
                await context.SaveChangesAsync();
            }
            else
            {
                company = await context.Companies.IgnoreQueryFilters().FirstAsync();
            }

            // 4. Main Branch
            Branch branch;
            if (!await context.Branches.IgnoreQueryFilters().AnyAsync(b => b.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Main Branch...");
                branch = new Branch
                {
                    CompanyId = company.Id,
                    Code = "BR01",
                    NameAr = "الفرع الرئيسي - الرياض",
                    NameEn = "Main Branch - Riyadh",
                    IsMainBranch = true,
                    Address = "طريق الملك فهد، الرياض",
                    Phone = "+966112345679",
                    IsActive = true
                };
                await context.Branches.AddAsync(branch);
                await context.SaveChangesAsync();
            }
            else
            {
                branch = await context.Branches.IgnoreQueryFilters().FirstAsync(b => b.CompanyId == company.Id);
            }

            // 5. Chart of Accounts (Tree Hierarchy Levels 1 to 4)
            if (!await context.Accounts.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Standard Chart of Accounts (Levels 1 to 4)...");

                // --- Level 1 ---
                var assets = new Account { CompanyId = company.Id, AccountCode = "1", NameAr = "الأصول", NameEn = "Assets", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 1, IsPosting = false };
                var liabilities = new Account { CompanyId = company.Id, AccountCode = "2", NameAr = "الالتزامات", NameEn = "Liabilities", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 1, IsPosting = false };
                var equity = new Account { CompanyId = company.Id, AccountCode = "3", NameAr = "حقوق الملكية", NameEn = "Equity", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 1, IsPosting = false };
                var revenue = new Account { CompanyId = company.Id, AccountCode = "4", NameAr = "الإيرادات", NameEn = "Revenues", AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Level = 1, IsPosting = false };
                var expense = new Account { CompanyId = company.Id, AccountCode = "5", NameAr = "المصروفات", NameEn = "Expenses", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 1, IsPosting = false };

                await context.Accounts.AddRangeAsync(assets, liabilities, equity, revenue, expense);
                await context.SaveChangesAsync();

                // --- Level 2 ---
                var currentAssets = new Account { CompanyId = company.Id, ParentAccountId = assets.Id, AccountCode = "11", NameAr = "الأصول المتداولة", NameEn = "Current Assets", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 2, IsPosting = false };
                var nonCurrentAssets = new Account { CompanyId = company.Id, ParentAccountId = assets.Id, AccountCode = "12", NameAr = "الأصول غير المتداولة", NameEn = "Non-Current Assets", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 2, IsPosting = false };
                var currentLiabilities = new Account { CompanyId = company.Id, ParentAccountId = liabilities.Id, AccountCode = "21", NameAr = "الالتزامات المتداولة", NameEn = "Current Liabilities", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 2, IsPosting = false };
                var nonCurrentLiabilities = new Account { CompanyId = company.Id, ParentAccountId = liabilities.Id, AccountCode = "22", NameAr = "الالتزامات غير المتداولة", NameEn = "Non-Current Liabilities", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 2, IsPosting = false };
                var capitalAndReserves = new Account { CompanyId = company.Id, ParentAccountId = equity.Id, AccountCode = "31", NameAr = "رأس المال والاحتياطيات", NameEn = "Capital & Reserves", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 2, IsPosting = false };
                var operatingRevenue = new Account { CompanyId = company.Id, ParentAccountId = revenue.Id, AccountCode = "41", NameAr = "إيرادات النشاط التشغيلي", NameEn = "Operating Revenues", AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Level = 2, IsPosting = false };
                var cogs = new Account { CompanyId = company.Id, ParentAccountId = expense.Id, AccountCode = "51", NameAr = "تكلفة النشاط والمبيعات", NameEn = "Cost of Goods Sold", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 2, IsPosting = false };
                var adminExpenses = new Account { CompanyId = company.Id, ParentAccountId = expense.Id, AccountCode = "52", NameAr = "المصروفات العمومية والإدارية", NameEn = "General & Administrative Expenses", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 2, IsPosting = false };

                await context.Accounts.AddRangeAsync(currentAssets, nonCurrentAssets, currentLiabilities, nonCurrentLiabilities, capitalAndReserves, operatingRevenue, cogs, adminExpenses);
                await context.SaveChangesAsync();

                // --- Level 3 ---
                var cashAndEquivalents = new Account { CompanyId = company.Id, ParentAccountId = currentAssets.Id, AccountCode = "1101", NameAr = "النقدية وما في حكمها", NameEn = "Cash and Cash Equivalents", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 3, IsPosting = false };
                var accountsReceivable = new Account { CompanyId = company.Id, ParentAccountId = currentAssets.Id, AccountCode = "1102", NameAr = "المدينون والعملاء", NameEn = "Accounts Receivable", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 3, IsPosting = false };
                var inventory = new Account { CompanyId = company.Id, ParentAccountId = currentAssets.Id, AccountCode = "1103", NameAr = "المخزون السلعي", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 3, IsPosting = false };
                var ppe = new Account { CompanyId = company.Id, ParentAccountId = nonCurrentAssets.Id, AccountCode = "1201", NameAr = "الأصول الثابتة", NameEn = "Property, Plant & Equipment", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 3, IsPosting = false };

                var accountsPayable = new Account { CompanyId = company.Id, ParentAccountId = currentLiabilities.Id, AccountCode = "2101", NameAr = "الموردون والدائنون", NameEn = "Accounts Payable", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 3, IsPosting = false };
                var taxesPayable = new Account { CompanyId = company.Id, ParentAccountId = currentLiabilities.Id, AccountCode = "2102", NameAr = "الضرائب المستحقة", NameEn = "Taxes Payable", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 3, IsPosting = false };

                var paidCapital = new Account { CompanyId = company.Id, ParentAccountId = capitalAndReserves.Id, AccountCode = "3101", NameAr = "رأس المال المدفوع", NameEn = "Paid-in Capital", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 3, IsPosting = false };
                var retainedEarnings = new Account { CompanyId = company.Id, ParentAccountId = capitalAndReserves.Id, AccountCode = "3102", NameAr = "الأرباح المحتجزة / المبقاة", NameEn = "Retained Earnings", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 3, IsPosting = false };
                var currentYearEarnings = new Account { CompanyId = company.Id, ParentAccountId = capitalAndReserves.Id, AccountCode = "3103", NameAr = "أرباح وخسائر العام الحالي", NameEn = "Current Year Earnings", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 3, IsPosting = false };

                var salesRevenues = new Account { CompanyId = company.Id, ParentAccountId = operatingRevenue.Id, AccountCode = "4101", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenues", AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Level = 3, IsPosting = false };
                var cogsGroup = new Account { CompanyId = company.Id, ParentAccountId = cogs.Id, AccountCode = "5101", NameAr = "تكلفة البضاعة المباعة", NameEn = "Cost of Goods Sold Group", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 3, IsPosting = false };

                var salaries = new Account { CompanyId = company.Id, ParentAccountId = adminExpenses.Id, AccountCode = "5201", NameAr = "الرواتب والأجور", NameEn = "Salaries & Wages", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 3, IsPosting = false };
                var rentExpenses = new Account { CompanyId = company.Id, ParentAccountId = adminExpenses.Id, AccountCode = "5202", NameAr = "الإيجارات والمرافق", NameEn = "Rent & Utilities", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 3, IsPosting = false };
                var depreciationExpense = new Account { CompanyId = company.Id, ParentAccountId = adminExpenses.Id, AccountCode = "5203", NameAr = "إهلاك الأصول الثابتة", NameEn = "Depreciation Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 3, IsPosting = false };
                var forexDifferences = new Account { CompanyId = company.Id, ParentAccountId = adminExpenses.Id, AccountCode = "5204", NameAr = "فروق أسعار صرف العملات", NameEn = "Foreign Exchange Differences", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 3, IsPosting = false };

                await context.Accounts.AddRangeAsync(
                    cashAndEquivalents, accountsReceivable, inventory, ppe,
                    accountsPayable, taxesPayable,
                    paidCapital, retainedEarnings, currentYearEarnings,
                    salesRevenues, cogsGroup,
                    salaries, rentExpenses, depreciationExpense, forexDifferences
                );
                await context.SaveChangesAsync();

                // --- Level 4 (Transactional Posting Accounts) ---
                var postingAccounts = new List<Account>
                {
                    new() { CompanyId = company.Id, ParentAccountId = cashAndEquivalents.Id, AccountCode = "110101", NameAr = "الصندوق الرئيسي - كاش", NameEn = "Main Cash Box", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = cashAndEquivalents.Id, AccountCode = "110102", NameAr = "بنك الراجحي - الحساب الجاري", NameEn = "Al Rajhi Bank - Current Account", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = accountsReceivable.Id, AccountCode = "110201", NameAr = "حساب مراقبة العملاء", NameEn = "Customers Control Account", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = inventory.Id, AccountCode = "110301", NameAr = "مخزون البضائع بالمستودع الرئيسي", NameEn = "General Inventory Account", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = ppe.Id, AccountCode = "120101", NameAr = "الأجهزة والمعدات", NameEn = "Equipment & Hardware", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = ppe.Id, AccountCode = "120102", NameAr = "مجمع إهلاك الأجهزة والمعدات", NameEn = "Accumulated Depreciation - Equipment", AccountType = AccountType.Asset, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },

                    new() { CompanyId = company.Id, ParentAccountId = accountsPayable.Id, AccountCode = "210101", NameAr = "حساب مراقبة الموردين", NameEn = "Suppliers Control Account", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = taxesPayable.Id, AccountCode = "210201", NameAr = "ضريبة القيمة المضافة المستحقة (المخرجات)", NameEn = "VAT Output / Payable", AccountType = AccountType.Liability, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = taxesPayable.Id, AccountCode = "210202", NameAr = "ضريبة القيمة المضافة القابلة للاسترداد (المدخلات)", NameEn = "VAT Input / Recoverable", AccountType = AccountType.Asset, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },

                    new() { CompanyId = company.Id, ParentAccountId = paidCapital.Id, AccountCode = "310101", NameAr = "رأس مال الشركة", NameEn = "Shareholders Capital", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = retainedEarnings.Id, AccountCode = "310201", NameAr = "الأرباح المبقاة", NameEn = "Retained Earnings", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = currentYearEarnings.Id, AccountCode = "310301", NameAr = "صافي أرباح / خسائر العام", NameEn = "Current Year Profit/Loss", AccountType = AccountType.Equity, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },

                    new() { CompanyId = company.Id, ParentAccountId = salesRevenues.Id, AccountCode = "410101", NameAr = "مبيعات البضائع والمنتجات", NameEn = "Product Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = salesRevenues.Id, AccountCode = "410102", NameAr = "مبيعات الخدمات والاستشارات", NameEn = "Service Revenues", AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },

                    new() { CompanyId = company.Id, ParentAccountId = cogsGroup.Id, AccountCode = "510101", NameAr = "تكلفة البضاعة المباعة", NameEn = "Cost of Goods Sold (COGS)", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = salaries.Id, AccountCode = "520101", NameAr = "رواتب الموظفين", NameEn = "Employee Salaries", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = rentExpenses.Id, AccountCode = "520201", NameAr = "مصروف إيجار المقرات", NameEn = "Rent Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = depreciationExpense.Id, AccountCode = "520301", NameAr = "مصروف إهلاك المعدات والأجهزة", NameEn = "Depreciation Expense - Equipment", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id },
                    new() { CompanyId = company.Id, ParentAccountId = forexDifferences.Id, AccountCode = "520401", NameAr = "أرباح وخسائر فروق أسعار العملات", NameEn = "Forex Gain / Loss", AccountType = AccountType.Expense, Nature = AccountNature.Debit, Level = 4, IsPosting = true, CurrencyId = sarCurrency.Id }
                };

                await context.Accounts.AddRangeAsync(postingAccounts);
                await context.SaveChangesAsync();
            }

            // 6. Company Financial Settings Mapping
            if (!await context.CompanySettings.IgnoreQueryFilters().AnyAsync(s => s.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Company Financial Settings Mapping...");
                var accounts = await context.Accounts.IgnoreQueryFilters().Where(a => a.CompanyId == company.Id).ToListAsync();
                var retainedAcc = accounts.First(a => a.AccountCode == "310201");
                var currentYearAcc = accounts.First(a => a.AccountCode == "310301");
                var forexAcc = accounts.First(a => a.AccountCode == "520401");
                var vatAcc = accounts.First(a => a.AccountCode == "210201");
                var salesAcc = accounts.First(a => a.AccountCode == "410101");
                var cogsAcc = accounts.First(a => a.AccountCode == "510101");
                var invAcc = accounts.First(a => a.AccountCode == "110301");

                var settings = new CompanySettings
                {
                    CompanyId = company.Id,
                    RetainedEarningsAccountId = retainedAcc.Id,
                    CurrentYearEarningsAccountId = currentYearAcc.Id,
                    ForexGainLossAccountId = forexAcc.Id,
                    DefaultVatAccountId = vatAcc.Id,
                    SalesRevenueAccountId = salesAcc.Id,
                    PurchaseExpenseAccountId = cogsAcc.Id,
                    InventoryAssetAccountId = invAcc.Id,
                    CogsAccountId = cogsAcc.Id
                };

                await context.CompanySettings.AddAsync(settings);
                await context.SaveChangesAsync();
            }

            // 7. Tax Types & Tax Rates
            if (!await context.TaxTypes.IgnoreQueryFilters().AnyAsync(t => t.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Tax Types and Rates...");
                var vatAccount = await context.Accounts.IgnoreQueryFilters().FirstAsync(a => a.CompanyId == company.Id && a.AccountCode == "210201");

                var vatTax = new TaxType
                {
                    CompanyId = company.Id,
                    Code = "VAT15",
                    NameAr = "ضريبة القيمة المضافة 15%",
                    NameEn = "Value Added Tax 15%",
                    Category = TaxCategory.Sales,
                    AccountId = vatAccount.Id,
                    IsActive = true
                };
                await context.TaxTypes.AddAsync(vatTax);
                await context.SaveChangesAsync();

                var vatRate = new TaxRate
                {
                    CompanyId = company.Id,
                    TaxTypeId = vatTax.Id,
                    Rate = 15.00m,
                    EffectiveFrom = new DateTime(2020, 7, 1)
                };
                await context.TaxRates.AddAsync(vatRate);
                await context.SaveChangesAsync();
            }

            // 8. Fiscal Year & 13 Periods (12 Monthly + Period 13 Closing)
            FiscalYear fiscalYear;
            if (!await context.FiscalYears.IgnoreQueryFilters().AnyAsync(fy => fy.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Fiscal Year 2026 and 13 Periods...");
                fiscalYear = new FiscalYear
                {
                    CompanyId = company.Id,
                    Name = "السنة المالية 2026",
                    StartDate = new DateTime(2026, 1, 1),
                    EndDate = new DateTime(2026, 12, 31),
                    IsClosed = false
                };
                await context.FiscalYears.AddAsync(fiscalYear);
                await context.SaveChangesAsync();

                var periods = new List<FiscalPeriod>();
                for (int i = 1; i <= 12; i++)
                {
                    periods.Add(new FiscalPeriod
                    {
                        CompanyId = company.Id,
                        FiscalYearId = fiscalYear.Id,
                        PeriodNumber = i,
                        PeriodType = FiscalPeriodType.Regular,
                        NameAr = $"فترة شهر {i} - 2026",
                        NameEn = $"Period Month {i} - 2026",
                        StartDate = new DateTime(2026, i, 1),
                        EndDate = new DateTime(2026, i, DateTime.DaysInMonth(2026, i)),
                        Status = FiscalPeriodStatus.Open
                    });
                }

                // Period 13 - Year End Adjustments
                periods.Add(new FiscalPeriod
                {
                    CompanyId = company.Id,
                    FiscalYearId = fiscalYear.Id,
                    PeriodNumber = 13,
                    PeriodType = FiscalPeriodType.YearEndAdjustment,
                    NameAr = "فترة التسويات الختامية 2026",
                    NameEn = "Year-End Adjustments Period 2026",
                    StartDate = new DateTime(2026, 12, 31),
                    EndDate = new DateTime(2026, 12, 31),
                    Status = FiscalPeriodStatus.Open
                });

                await context.FiscalPeriods.AddRangeAsync(periods);
                await context.SaveChangesAsync();
            }
            else
            {
                fiscalYear = await context.FiscalYears.IgnoreQueryFilters().FirstAsync(fy => fy.CompanyId == company.Id);
            }

            // 9. Document Sequence for Journal Entries
            if (!await context.DocumentSequences.IgnoreQueryFilters().AnyAsync(ds => ds.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Document Sequence for Journal Entries...");
                var sequence = new DocumentSequence
                {
                    CompanyId = company.Id,
                    FiscalYearId = fiscalYear.Id,
                    DocumentType = 1, // JournalEntry
                    Prefix = "JV",
                    LastNumber = 0
                };
                await context.DocumentSequences.AddAsync(sequence);
                await context.SaveChangesAsync();
            }

            // 10. Cost Centers
            if (!await context.CostCenters.IgnoreQueryFilters().AnyAsync(cc => cc.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Cost Centers...");
                var costCenters = new List<CostCenter>
                {
                    new() { CompanyId = company.Id, Code = "CC01", NameAr = "الإدارة العامة", NameEn = "General Administration", IsActive = true },
                    new() { CompanyId = company.Id, Code = "CC02", NameAr = "قسم المبيعات والتسويق", NameEn = "Sales & Marketing", IsActive = true },
                    new() { CompanyId = company.Id, Code = "CC03", NameAr = "قسم العمليات والخدمات", NameEn = "Operations & Services", IsActive = true }
                };
                await context.CostCenters.AddRangeAsync(costCenters);
                await context.SaveChangesAsync();
            }

            // 11. Bank Account, Cash Box, and Central Warehouse
            if (!await context.BankAccounts.IgnoreQueryFilters().AnyAsync(ba => ba.CompanyId == company.Id))
            {
                logger.LogInformation("Seeding Bank Accounts and Warehouses...");
                var accounts = await context.Accounts.IgnoreQueryFilters().Where(a => a.CompanyId == company.Id).ToListAsync();
                var cashAccount = accounts.First(a => a.AccountCode == "110101");
                var bankAccount = accounts.First(a => a.AccountCode == "110102");

                var bank = new BankAccount
                {
                    CompanyId = company.Id,
                    BranchId = branch.Id,
                    Code = "BA01",
                    NameAr = "حساب مصرف الراجحي",
                    NameEn = "Al Rajhi Bank Account",
                    AccountNumber = "SA0380000000001234567890",
                    CurrencyId = sarCurrency.Id,
                    AccountId = bankAccount.Id,
                    IsCash = false,
                    IsActive = true
                };

                var cashBox = new BankAccount
                {
                    CompanyId = company.Id,
                    BranchId = branch.Id,
                    Code = "CSH01",
                    NameAr = "الصندوق النقدي الرئيسي",
                    NameEn = "Main Cash Box",
                    AccountNumber = "CASH-MAIN-01",
                    CurrencyId = sarCurrency.Id,
                    AccountId = cashAccount.Id,
                    IsCash = true,
                    IsActive = true
                };

                await context.BankAccounts.AddRangeAsync(bank, cashBox);

                var warehouse = new Warehouse
                {
                    CompanyId = company.Id,
                    BranchId = branch.Id,
                    Code = "WH01",
                    NameAr = "المستودع المركزي - الرياض",
                    NameEn = "Central Warehouse - Riyadh",
                    Location = "المنطقة الصناعية الثانية، الرياض",
                    IsActive = true
                };
                await context.Warehouses.AddAsync(warehouse);
                await context.SaveChangesAsync();
            }

            // 12. Identity: Permissions, Roles, Admin User, and CompanyUser Association
            if (!await context.Permissions.AnyAsync())
            {
                logger.LogInformation("Seeding Permissions...");
                var permissions = new List<Permission>
                {
                    new() { Code = "Auth.ManageUsers", NameAr = "إدارة المستخدمين", NameEn = "Manage Users", Module = "Identity" },
                    new() { Code = "Auth.ManageRoles", NameAr = "إدارة الأدوار والصلاحيات", NameEn = "Manage Roles & Permissions", Module = "Identity" },
                    new() { Code = "Companies.Manage", NameAr = "إدارة الشركات والفروع", NameEn = "Manage Companies & Branches", Module = "Organization" },
                    new() { Code = "COA.Manage", NameAr = "إدارة دليل الحسابات", NameEn = "Manage Chart of Accounts", Module = "GeneralLedger" },
                    new() { Code = "JournalEntries.View", NameAr = "عرض قيود اليومية", NameEn = "View Journal Entries", Module = "GeneralLedger" },
                    new() { Code = "JournalEntries.Create", NameAr = "إنشاء وتعديل قيود اليومية", NameEn = "Create & Edit Journal Entries", Module = "GeneralLedger" },
                    new() { Code = "JournalEntries.Post", NameAr = "ترحيل قيود اليومية", NameEn = "Post Journal Entries", Module = "GeneralLedger" },
                    new() { Code = "JournalEntries.Reverse", NameAr = "عكس قيود اليومية", NameEn = "Reverse Journal Entries", Module = "GeneralLedger" },
                    new() { Code = "FinancialReports.View", NameAr = "عرض القوائم والتقارير المالية", NameEn = "View Financial Reports", Module = "Reports" },
                    new() { Code = "FiscalYears.Manage", NameAr = "إدارة السنوات والفترات المالية والإقفال", NameEn = "Manage Fiscal Years & Closing", Module = "GeneralLedger" },
                    new() { Code = "Customers.Manage", NameAr = "إدارة العملاء", NameEn = "Manage Customers", Module = "Sales" },
                    new() { Code = "Suppliers.Manage", NameAr = "إدارة الموردين", NameEn = "Manage Suppliers", Module = "Purchases" },
                    new() { Code = "SalesInvoices.Manage", NameAr = "إدارة فواتير المبيعات", NameEn = "Manage Sales Invoices", Module = "Sales" },
                    new() { Code = "PurchaseInvoices.Manage", NameAr = "إدارة فواتير المشتريات", NameEn = "Manage Purchase Invoices", Module = "Purchases" },
                    new() { Code = "Payments.Manage", NameAr = "إدارة سندات القبض والصرف", NameEn = "Manage Payments & Receipts", Module = "Treasury" },
                    new() { Code = "Treasury.Manage", NameAr = "إدارة البنوك والخزائن والتسويات", NameEn = "Manage Treasury & Reconciliations", Module = "Treasury" },
                    new() { Code = "Inventory.Manage", NameAr = "إدارة المستودعات وحركات المخزون", NameEn = "Manage Inventory & Warehouses", Module = "Inventory" },
                    new() { Code = "FixedAssets.Manage", NameAr = "إدارة الأصول الثابتة والإهلاك", NameEn = "Manage Fixed Assets & Depreciation", Module = "FixedAssets" },
                    new() { Code = "TaxAndCurrencies.Manage", NameAr = "إدارة الضرائب وأسعار الصرف", NameEn = "Manage Taxes & Currencies", Module = "MasterData" },
                    new() { Code = "AuditLogs.View", NameAr = "عرض سجل التدقيق والمراقبة", NameEn = "View System Audit Logs", Module = "System" }
                };
                await context.Permissions.AddRangeAsync(permissions);
                await context.SaveChangesAsync();
            }

            ApplicationRole superAdminRole;
            if (!await context.Roles.AnyAsync())
            {
                logger.LogInformation("Seeding Roles & Role Permissions...");
                superAdminRole = new ApplicationRole { Name = "SuperAdmin", Description = "مدير عام النظام مع كامل الصلاحيات" };
                var accountantRole = new ApplicationRole { Name = "Accountant", Description = "محاسب مالي - إدخال وترحيل العمليات المالية" };
                var auditorRole = new ApplicationRole { Name = "FinancialAuditor", Description = "مدقق ومراجع مالي داخلي / خارجي" };

                await context.Roles.AddRangeAsync(superAdminRole, accountantRole, auditorRole);
                await context.SaveChangesAsync();

                // Grant all permissions to SuperAdmin
                var allPermissions = await context.Permissions.ToListAsync();
                var rolePermissions = allPermissions.Select(p => new RolePermission
                {
                    RoleId = superAdminRole.Id,
                    PermissionId = p.Id
                }).ToList();

                await context.RolePermissions.AddRangeAsync(rolePermissions);
                await context.SaveChangesAsync();
            }
            else
            {
                superAdminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
            }

            ApplicationUser adminUser;
            var hasher = new Services.PasswordHasher();
            var adminPasswordHash = hasher.HashPassword("Admin@123456");

            if (!await context.Users.AnyAsync())
            {
                logger.LogInformation("Seeding SuperAdmin User...");
                adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@erp.com",
                    FullName = "المدير العام للنظام",
                    PhoneNumber = "+966500000001",
                    PasswordHash = adminPasswordHash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();
            }
            else
            {
                adminUser = await context.Users.FirstAsync(u => u.UserName == "admin");
                if (string.IsNullOrEmpty(adminUser.PasswordHash))
                {
                    adminUser.PasswordHash = adminPasswordHash;
                    await context.SaveChangesAsync();
                }
            }

            if (!await context.CompanyUsers.IgnoreQueryFilters().AnyAsync(cu => cu.CompanyId == company.Id && cu.UserId == adminUser.Id))
            {
                logger.LogInformation("Associating Admin with Default Company...");
                var companyUser = new CompanyUser
                {
                    UserId = adminUser.Id,
                    CompanyId = company.Id,
                    DefaultBranchId = branch.Id,
                    RoleId = superAdminRole.Id,
                    IsActive = true
                };
                await context.CompanyUsers.AddAsync(companyUser);
                await context.SaveChangesAsync();
            }

            logger.LogInformation("Database foundational seeding completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database: {Message}", ex.Message);
            throw;
        }
    }
}
