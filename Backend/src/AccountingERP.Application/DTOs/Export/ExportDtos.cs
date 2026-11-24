using System;
using System.Collections.Generic;

namespace AccountingERP.Application.DTOs.Export;

public class TaxInvoicePrintDto
{
    public long InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string InvoiceType { get; set; } = "Tax Invoice"; // فاتورة ضريبية
    
    // Seller Information
    public string SellerNameAr { get; set; } = string.Empty;
    public string SellerNameEn { get; set; } = string.Empty;
    public string SellerVatNumber { get; set; } = string.Empty;
    public string? SellerCrNumber { get; set; }
    public string? SellerAddress { get; set; }

    // Buyer Information
    public string BuyerNameAr { get; set; } = string.Empty;
    public string BuyerNameEn { get; set; } = string.Empty;
    public string? BuyerVatNumber { get; set; }
    public string? BuyerAddress { get; set; }

    // Amounts
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "SAR";

    // ZATCA Base64 TLV Encoded QR Code
    public string ZatcaQrCodeBase64 { get; set; } = string.Empty;

    // Line items
    public List<TaxInvoiceItemPrintDto> Items { get; set; } = new();
}

public class TaxInvoiceItemPrintDto
{
    public int LineNumber { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameAr { get; set; } = string.Empty;
    public string ItemNameEn { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetTotal { get; set; }
}

public class CsvExportResultDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv; charset=utf-8";
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
