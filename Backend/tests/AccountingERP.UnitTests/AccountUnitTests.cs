using AccountingERP.Application.Common.Models;
using AccountingERP.Domain.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.UnitTests;

[TestClass]
public class AccountUnitTests
{
    [TestMethod]
    public void PagedResult_ShouldCalculatePagingPropertiesCorrectly()
    {
        var items = new List<string> { "Account1", "Account2", "Account3" };
        var pagedResult = new PagedResult<string>(items, totalCount: 25, pageNumber: 2, pageSize: 10);

        Assert.AreEqual(25, pagedResult.TotalCount);
        Assert.AreEqual(2, pagedResult.PageNumber);
        Assert.AreEqual(10, pagedResult.PageSize);
        Assert.AreEqual(3, pagedResult.TotalPages); // 25 / 10 = 2.5 => ceiling = 3
        Assert.IsTrue(pagedResult.HasPreviousPage);
        Assert.IsTrue(pagedResult.HasNextPage);
    }

    [TestMethod]
    public void AccountType_AccountingNatures_ShouldMatchStandardConventions()
    {
        // Standard normal accounting balances:
        // Assets & Expenses: Debit
        // Liabilities, Equity, Revenues: Credit

        Assert.AreEqual(AccountNature.Debit, GetDefaultNature(AccountType.Asset));
        Assert.AreEqual(AccountNature.Debit, GetDefaultNature(AccountType.Expense));
        Assert.AreEqual(AccountNature.Credit, GetDefaultNature(AccountType.Liability));
        Assert.AreEqual(AccountNature.Credit, GetDefaultNature(AccountType.Equity));
        Assert.AreEqual(AccountNature.Credit, GetDefaultNature(AccountType.Revenue));
    }

    private static AccountNature GetDefaultNature(AccountType type) =>
        type is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit;
}
