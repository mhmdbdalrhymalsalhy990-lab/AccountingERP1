using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.Export;
using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingERP.Infrastructure.Services;

public class ReportingExportService : IReportingExportService
{
    private readonly ApplicationDbContext _context;
    private readonly IFinancialReportService _reportService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<ReportingExportService> _logger;

    public ReportingExportService(
        ApplicationDbContext context,
        IFinancialReportService reportService,
        ICurrentTenantService tenantService,
        ILogger<ReportingExportService> logger)
    {
        _context = context;
        _reportService = reportService;
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Encodes seller and invoice metadata into ZATCA standard TLV Base64 format
    /// </summary>
    public string GenerateZatcaQrCode(string sellerName, string vatNumber, DateTime timestamp, decimal totalAmount, decimal vatAmount)
    {
        var timestampStr = timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var totalStr = totalAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var vatStr = vatAmount.ToString("0.00", CultureInfo.InvariantCulture);

        using var memoryStream = new MemoryStream();

        WriteTlvTag(memoryStream, 1, sellerName);
        WriteTlvTag(memoryStream, 2, vatNumber);
        WriteTlvTag(memoryStream, 3, timestampStr);
        WriteTlvTag(memoryStream, 4, totalStr);
        WriteTlvTag(memoryStream, 5, vatStr);

        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private static void WriteTlvTag(MemoryStream ms, byte tag, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        ms.WriteByte(tag);
        ms.WriteByte((byte)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    public async Task<TaxInvoicePrintDto> GetTaxInvoicePrintDataAsync(long invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.SalesInvoices
            .Include(i => i.Items)
                .ThenInclude(item => item.Product)
            .Include(i => i.Customer)
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
        {
            throw new InvalidOperationException($"فاتورة المبيعات بالرقم {invoiceId} غير موجودة.");
        }

        var sellerName = invoice.Company?.NameAr ?? "شركة الأعمال التجارية";
        var sellerVat = invoice.Company?.TaxNumber ?? "300000000000003";

        var qrBase64 = GenerateZatcaQrCode(
            sellerName,
            sellerVat,
            invoice.InvoiceDate,
            invoice.GrandTotal,
            invoice.TaxTotal
        );

        int lineIdx = 1;
        var dto = new TaxInvoicePrintDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            InvoiceType = "فاتورة ضريبية (Tax Invoice)",
            SellerNameAr = invoice.Company?.NameAr ?? string.Empty,
            SellerNameEn = invoice.Company?.NameEn ?? string.Empty,
            SellerVatNumber = sellerVat,
            SellerCrNumber = invoice.Company?.CommercialRegistration,
            SellerAddress = invoice.Company?.Address,
            BuyerNameAr = invoice.Customer?.NameAr ?? "عميل نقدي",
            BuyerNameEn = invoice.Customer?.NameEn ?? "Cash Customer",
            BuyerVatNumber = invoice.Customer?.TaxNumber,
            BuyerAddress = invoice.Customer?.Address,
            SubTotal = invoice.SubTotal,
            DiscountAmount = invoice.DiscountTotal,
            TaxableAmount = invoice.SubTotal - invoice.DiscountTotal,
            TaxAmount = invoice.TaxTotal,
            TotalAmount = invoice.GrandTotal,
            CurrencyCode = "SAR",
            ZatcaQrCodeBase64 = qrBase64,
            Items = invoice.Items.Select(item => new TaxInvoiceItemPrintDto
            {
                LineNumber = lineIdx++,
                ItemCode = item.Product?.Code ?? $"ITEM-{item.ProductId}",
                ItemNameAr = item.Product?.NameAr ?? "بضاعة / خدمة",
                ItemNameEn = item.Product?.NameEn ?? "Goods / Services",
                Quantity = item.Quantity,
                UnitName = item.Product?.UnitName ?? "حبة",
                UnitPrice = item.UnitPrice,
                Discount = 0,
                TaxRatePercent = item.TaxRate,
                TaxAmount = item.TaxAmount,
                NetTotal = item.LineTotal
            }).ToList()
        };

        return dto;
    }

    public async Task<CsvExportResultDto> ExportTrialBalanceCsvAsync(TrialBalanceRequestDto request, CancellationToken cancellationToken = default)
    {
        var report = await _reportService.GetTrialBalanceAsync(request, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("رمز الحساب (Account Code),اسم الحساب (Account Name),رصيد افتتاح مدين (Opening Debit),رصيد افتتاح دائن (Opening Credit),حركة الفترة مدين (Period Debit),حركة الفترة دائن (Period Credit),رصيد ختامي مدين (Ending Debit),رصيد ختامي دائن (Ending Credit)");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2}",
                EscapeCsv(item.AccountCode),
                EscapeCsv(item.AccountNameAr),
                item.OpeningDebit,
                item.OpeningCredit,
                item.PeriodDebit,
                item.PeriodCredit,
                item.EndingDebit,
                item.EndingCredit));
        }

        // Totals Row
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "\"الإجمالي العام (Total)\",\"\",{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2}",
            report.TotalOpeningDebit,
            report.TotalOpeningCredit,
            report.TotalPeriodDebit,
            report.TotalPeriodCredit,
            report.TotalEndingDebit,
            report.TotalEndingCredit));

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var textBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var combined = new byte[bom.Length + textBytes.Length];
        Buffer.BlockCopy(bom, 0, combined, 0, bom.Length);
        Buffer.BlockCopy(textBytes, 0, combined, bom.Length, textBytes.Length);

        var fileName = $"TrialBalance_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return new CsvExportResultDto
        {
            FileName = fileName,
            Data = combined
        };
    }

    public async Task<CsvExportResultDto> ExportGeneralLedgerCsvAsync(GeneralLedgerRequestDto request, CancellationToken cancellationToken = default)
    {
        var report = await _reportService.GetGeneralLedgerAsync(request, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("رمز الحساب,اسم الحساب,التاريخ,رقم القيد,البيان / الشرح,المرجع,مدين,دائن,الرصيد التراكمي");

        // Account header row
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "\"{0}\",\"{1}\",\"افتتاحي\",\"--\",\"رصيد أول المدة\",\"\",0.00,0.00,{2:F2}",
            EscapeCsv(report.AccountCode),
            EscapeCsv(report.AccountNameAr),
            report.OpeningBalance));

        foreach (var line in report.Transactions)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2:yyyy-MM-dd}\",\"{3}\",\"{4}\",\"{5}\",{6:F2},{7:F2},{8:F2}",
                EscapeCsv(report.AccountCode),
                EscapeCsv(report.AccountNameAr),
                line.EntryDate,
                EscapeCsv(line.EntryNumber),
                EscapeCsv(line.Description),
                EscapeCsv(line.ReferenceNumber ?? ""),
                line.Debit,
                line.Credit,
                line.RunningBalance));
        }

        // Account closing row
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "\"{0}\",\"{1}\",\"إجمالي\",\"--\",\"مجموع الحركات ورصيد الإغلاق\",\"\",{2:F2},{3:F2},{4:F2}",
            EscapeCsv(report.AccountCode),
            EscapeCsv(report.AccountNameAr),
            report.TotalDebit,
            report.TotalCredit,
            report.ClosingBalance));

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var textBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var combined = new byte[bom.Length + textBytes.Length];
        Buffer.BlockCopy(bom, 0, combined, 0, bom.Length);
        Buffer.BlockCopy(textBytes, 0, combined, bom.Length, textBytes.Length);

        var fileName = $"GeneralLedger_{report.AccountCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return new CsvExportResultDto
        {
            FileName = fileName,
            Data = combined
        };
    }

    private static string EscapeCsv(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}
