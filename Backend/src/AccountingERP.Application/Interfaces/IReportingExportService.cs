using System;
using System.Threading;
using System.Threading.Tasks;
using AccountingERP.Application.DTOs.Accounting;
using AccountingERP.Application.DTOs.Export;

namespace AccountingERP.Application.Interfaces;

public interface IReportingExportService
{
    /// <summary>
    /// Generates ZATCA (Fatoora) compliant TLV Base64 QR Code string
    /// </summary>
    string GenerateZatcaQrCode(string sellerName, string vatNumber, DateTime timestamp, decimal totalAmount, decimal vatAmount);

    /// <summary>
    /// Retrieves comprehensive tax invoice data ready for printing / PDF rendering including ZATCA QR code
    /// </summary>
    Task<TaxInvoicePrintDto> GetTaxInvoicePrintDataAsync(long invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports Trial Balance report to Excel-compatible UTF-8 CSV with Byte Order Mark
    /// </summary>
    Task<CsvExportResultDto> ExportTrialBalanceCsvAsync(TrialBalanceRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports General Ledger report to Excel-compatible UTF-8 CSV with Byte Order Mark
    /// </summary>
    Task<CsvExportResultDto> ExportGeneralLedgerCsvAsync(GeneralLedgerRequestDto request, CancellationToken cancellationToken = default);
}
