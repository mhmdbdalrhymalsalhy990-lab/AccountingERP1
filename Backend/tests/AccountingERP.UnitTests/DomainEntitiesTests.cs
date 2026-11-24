using AccountingERP.Domain.Common;
using AccountingERP.Domain.Entities;
using AccountingERP.Domain.Enums;
using AccountingERP.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.UnitTests;

[TestClass]
public class DomainEntitiesTests
{
    [TestMethod]
    public void Company_DefaultValues_ShouldBeConfiguredCorrectly()
    {
        var company = new Company
        {
            Code = "COMP01",
            NameAr = "شركة الأمل للتجارة",
            NameEn = "Al-Amal Trading Co"
        };

        Assert.IsTrue(company.IsActive);
        Assert.IsFalse(company.IsDeleted);
        Assert.IsNotNull(company.Branches);
        Assert.IsNotNull(company.Accounts);
    }

    [TestMethod]
    public void JournalEntry_UnbalancedException_ShouldFormatMessageCorrectly()
    {
        decimal debit = 1000m;
        decimal credit = 850m;

        var exception = new UnbalancedJournalEntryException(debit, credit);

        Assert.IsTrue(exception.Message.Contains("1000.0000"));
        Assert.IsTrue(exception.Message.Contains("850.0000"));
    }

    [TestMethod]
    public void Account_Properties_ShouldReflectHierarchyAndNature()
    {
        var account = new Account
        {
            AccountCode = "1101",
            NameAr = "الصندوق",
            NameEn = "Cash on Hand",
            AccountType = AccountType.Asset,
            Nature = AccountNature.Debit,
            IsPosting = true,
            Level = 3
        };

        Assert.AreEqual(AccountType.Asset, account.AccountType);
        Assert.AreEqual(AccountNature.Debit, account.Nature);
        Assert.IsTrue(account.IsPosting);
        Assert.AreEqual(3, account.Level);
    }
}
