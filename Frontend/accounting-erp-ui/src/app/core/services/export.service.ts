import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TaxInvoicePrintData {
  invoiceId: number;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate?: string;
  invoiceType: string;
  sellerNameAr: string;
  sellerNameEn: string;
  sellerVatNumber: string;
  sellerCrNumber?: string;
  sellerAddress?: string;
  buyerNameAr: string;
  buyerNameEn: string;
  buyerVatNumber?: string;
  buyerAddress?: string;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  taxAmount: number;
  totalAmount: number;
  currencyCode: string;
  zatcaQrCodeBase64: string;
  items: {
    lineNumber: number;
    itemCode: string;
    itemNameAr: string;
    itemNameEn: string;
    quantity: number;
    unitName: string;
    unitPrice: number;
    discount: number;
    taxRatePercent: number;
    taxAmount: number;
    netTotal: number;
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class ExportService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  getInvoicePrintData(invoiceId: string | number): Observable<TaxInvoicePrintData> {
    return this.http.get<TaxInvoicePrintData>(`${this.baseUrl}/export/invoices/${invoiceId}/print`);
  }

  exportToCsv(filename: string, headers: string[], rows: (string | number)[][]): void {
    let csvContent = '\uFEFF'; // UTF-8 Byte Order Mark (BOM) for Excel

    // Add headers
    csvContent += headers.map(h => `"${this.escapeCsv(h)}"`).join(',') + '\r\n';

    // Add rows
    rows.forEach(row => {
      const line = row.map(val => {
        if (typeof val === 'number') {
          return val.toFixed(2);
        }
        return `"${this.escapeCsv(String(val ?? ''))}"`;
      }).join(',');
      csvContent += line + '\r\n';
    });

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `${filename}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  private escapeCsv(text: string): string {
    return text.replace(/"/g, '""');
  }
}
