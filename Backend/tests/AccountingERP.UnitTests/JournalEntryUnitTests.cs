using AccountingERP.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.UnitTests;

[TestClass]
public class JournalEntryUnitTests
{
    [TestMethod]
    public void UnbalancedJournalEntryException_ShouldFormatMessageCorrectly()
    {
        var ex = new UnbalancedJournalEntryException(1500.50m, 1200.00m);
        Assert.IsTrue(ex.Message.Contains("1500.5000"));
        Assert.IsTrue(ex.Message.Contains("1200.0000"));
    }

    [TestMethod]
    public void HeaderAccountPostingException_ShouldIncludeAccountCodeInMessage()
    {
        const string headerCode = "1101";
        var ex = new HeaderAccountPostingException(headerCode);
        Assert.IsTrue(ex.Message.Contains(headerCode));
    }

    [TestMethod]
    public void ForeignCurrencyCalculation_ShouldMultiplyByExchangeRateCorrectly()
    {
        const decimal foreignAmount = 100.00m;
        const decimal exchangeRate = 3.750000m; // USD to SAR

        var baseAmount = Math.Round(foreignAmount * exchangeRate, 4);

        Assert.AreEqual(375.0000m, baseAmount);
    }
}
