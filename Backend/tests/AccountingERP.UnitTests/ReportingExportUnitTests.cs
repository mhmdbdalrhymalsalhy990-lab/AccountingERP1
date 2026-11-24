using System;
using System.Text;
using AccountingERP.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountingERP.UnitTests;

[TestClass]
public class ReportingExportUnitTests
{
    [TestMethod]
    public void GenerateZatcaQrCode_ShouldProduceValidBase64TlvString()
    {
        // Arrange
        var sellerName = "شركة تجريبية للحلول البرمجية";
        var vatNumber = "310123456700003";
        var timestamp = new DateTime(2026, 11, 23, 10, 30, 0, DateTimeKind.Utc);
        var total = 1150.00m;
        var vat = 150.00m;

        var service = new ReportingExportService(null!, null!, null!, null!);

        // Act
        var qrBase64 = service.GenerateZatcaQrCode(sellerName, vatNumber, timestamp, total, vat);

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(qrBase64));

        var decodedBytes = Convert.FromBase64String(qrBase64);
        Assert.IsTrue(decodedBytes.Length > 20);

        // Check Tag 1: Seller Name
        Assert.AreEqual((byte)1, decodedBytes[0]);
        var tag1Len = decodedBytes[1];
        var tag1Val = Encoding.UTF8.GetString(decodedBytes, 2, tag1Len);
        Assert.AreEqual(sellerName, tag1Val);

        // Check Tag 2: VAT Number
        var tag2Idx = 2 + tag1Len;
        Assert.AreEqual((byte)2, decodedBytes[tag2Idx]);
        var tag2Len = decodedBytes[tag2Idx + 1];
        var tag2Val = Encoding.UTF8.GetString(decodedBytes, tag2Idx + 2, tag2Len);
        Assert.AreEqual(vatNumber, tag2Val);

        // Check Tag 3: Timestamp
        var tag3Idx = tag2Idx + 2 + tag2Len;
        Assert.AreEqual((byte)3, decodedBytes[tag3Idx]);
        var tag3Len = decodedBytes[tag3Idx + 1];
        var tag3Val = Encoding.UTF8.GetString(decodedBytes, tag3Idx + 2, tag3Len);
        Assert.AreEqual("2026-11-23T10:30:00Z", tag3Val);

        // Check Tag 4: Total Amount
        var tag4Idx = tag3Idx + 2 + tag3Len;
        Assert.AreEqual((byte)4, decodedBytes[tag4Idx]);
        var tag4Len = decodedBytes[tag4Idx + 1];
        var tag4Val = Encoding.UTF8.GetString(decodedBytes, tag4Idx + 2, tag4Len);
        Assert.AreEqual("1150.00", tag4Val);

        // Check Tag 5: VAT Amount
        var tag5Idx = tag4Idx + 2 + tag4Len;
        Assert.AreEqual((byte)5, decodedBytes[tag5Idx]);
        var tag5Len = decodedBytes[tag5Idx + 1];
        var tag5Val = Encoding.UTF8.GetString(decodedBytes, tag5Idx + 2, tag5Len);
        Assert.AreEqual("150.00", tag5Val);
    }
}
