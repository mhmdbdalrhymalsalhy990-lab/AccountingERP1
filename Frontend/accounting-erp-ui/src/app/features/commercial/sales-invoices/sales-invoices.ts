import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommercialService, SalesInvoiceDto } from '../../../core/services/commercial.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';
import { PageHeaderComponent, StatusBadgeComponent, CurrencyFormatPipe } from '@shared';

@Component({
  selector: 'app-sales-invoices',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    CurrencyFormatPipe
  ],
  templateUrl: './sales-invoices.html',
  styleUrls: ['./sales-invoices.scss']
})
export class SalesInvoicesComponent implements OnInit {
  private commercialService = inject(CommercialService);
  private snackBar = inject(MatSnackBar);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  invoices = signal<SalesInvoiceDto[]>([]);

  ngOnInit(): void {
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.isLoading.set(true);
    this.commercialService.getSalesInvoices().subscribe({
      next: (data) => {
        this.invoices.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  postInvoice(invoice: SalesInvoiceDto): void {
    this.commercialService.postSalesInvoice(invoice.id).subscribe({
      next: () => {
        this.snackBar.open(
          this.langService.isRtl() 
            ? `تم ترحيل الفاتورة ${invoice.invoiceNumber} إلى الأستاذ العام بنجاح` 
            : `Invoice ${invoice.invoiceNumber} posted to GL`,
          'إغلاق', { duration: 3000 }
        );
        this.loadInvoices();
      }
    });
  }
}
