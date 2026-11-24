import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ExportService, TaxInvoicePrintData } from '../../../core/services/export.service';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-invoice-print',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './invoice-print.html',
  styleUrls: ['./invoice-print.scss']
})
export class InvoicePrintComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private exportService = inject(ExportService);
  langService = inject(LanguageService);

  isLoading = signal(true);
  invoice = signal<TaxInvoicePrintData | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') || '1';
    this.exportService.getInvoicePrintData(id).subscribe({
      next: (data) => {
        this.invoice.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        // Fallback demo tax invoice if id not found
        this.invoice.set({
          invoiceId: 1,
          invoiceNumber: 'INV-2026-000001',
          invoiceDate: new Date().toISOString(),
          dueDate: new Date(Date.now() + 30 * 86400000).toISOString(),
          invoiceType: 'فاتورة ضريبية (Tax Invoice)',
          sellerNameAr: 'شركة النظم المحاسبية المتقدمة',
          sellerNameEn: 'Advanced Accounting Systems Co.',
          sellerVatNumber: '300000000000003',
          sellerCrNumber: '1010000000',
          sellerAddress: 'الرياض، المملكة العربية السعودية',
          buyerNameAr: 'شركة الأفق للتجارة والمقاولات',
          buyerNameEn: 'Horizon Trading & Contracting',
          buyerVatNumber: '310999999900003',
          buyerAddress: 'جدة، المملكة العربية السعودية',
          subTotal: 10000.00,
          discountAmount: 0.00,
          taxableAmount: 10000.00,
          taxAmount: 1500.00,
          totalAmount: 11500.00,
          currencyCode: 'SAR',
          zatcaQrCodeBase64: 'AQ3YtNix2YPYqQ==',
          items: [
            {
              lineNumber: 1,
              itemCode: 'SRV-001',
              itemNameAr: 'خدمات استشارية محاسبية وتدقيق',
              itemNameEn: 'Accounting Consulting & Audit',
              quantity: 1,
              unitName: 'خدمة',
              unitPrice: 10000.00,
              discount: 0,
              taxRatePercent: 15,
              taxAmount: 1500.00,
              netTotal: 11500.00
            }
          ]
        });
        this.isLoading.set(false);
      }
    });
  }

  printInvoice(): void {
    window.print();
  }
}
