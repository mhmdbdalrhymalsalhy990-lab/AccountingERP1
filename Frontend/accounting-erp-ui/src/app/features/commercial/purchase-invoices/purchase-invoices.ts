import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommercialService, PurchaseInvoiceDto } from '../../../core/services/commercial.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';
import { PageHeaderComponent, StatusBadgeComponent, CurrencyFormatPipe } from '@shared';

@Component({
  selector: 'app-purchase-invoices',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    CurrencyFormatPipe
  ],
  templateUrl: './purchase-invoices.html',
  styleUrls: ['./purchase-invoices.scss']
})
export class PurchaseInvoicesComponent implements OnInit {
  private commercialService = inject(CommercialService);
  private snackBar = inject(MatSnackBar);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  invoices = signal<PurchaseInvoiceDto[]>([]);

  ngOnInit(): void {
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.isLoading.set(true);
    this.commercialService.getPurchaseInvoices().subscribe({
      next: (data) => {
        const items = data.items || data;
        this.invoices.set(Array.isArray(items) ? items : []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  postInvoice(invoice: PurchaseInvoiceDto): void {
    this.commercialService.postPurchaseInvoice(invoice.id).subscribe({
      next: () => {
        this.snackBar.open(
          this.langService.isRtl() 
            ? `تم ترحيل فاتورة الشراء ${invoice.invoiceNumber} للأستاذ العام` 
            : `Purchase invoice ${invoice.invoiceNumber} posted`,
          'إغلاق', { duration: 3000 }
        );
        this.loadInvoices();
      }
    });
  }
}
