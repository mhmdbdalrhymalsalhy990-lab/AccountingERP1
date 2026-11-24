import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { GeneralLedgerService, TrialBalanceReportDto } from '../../../core/services/general-ledger.service';
import { ExportService } from '../../../core/services/export.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-trial-balance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './trial-balance.html',
  styleUrls: ['./trial-balance.scss']
})
export class TrialBalanceComponent implements OnInit {
  private glService = inject(GeneralLedgerService);
  private exportService = inject(ExportService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  report = signal<TrialBalanceReportDto | null>(null);

  fromDate = signal('');
  toDate = signal('');

  ngOnInit(): void {
    this.loadTrialBalance();
  }

  loadTrialBalance(): void {
    this.isLoading.set(true);
    this.glService.getTrialBalance(this.fromDate() || undefined, this.toDate() || undefined).subscribe({
      next: (data) => {
        this.report.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  exportToExcel(): void {
    const rep = this.report();
    if (!rep) return;
    const headers = [
      'رمز الحساب (Code)',
      'اسم الحساب (Name)',
      'افتتاحي مدين (Open Debit)',
      'افتتاحي دائن (Open Credit)',
      'حركة مدين (Period Debit)',
      'حركة دائن (Period Credit)',
      'ختامي مدين (End Debit)',
      'ختامي دائن (End Credit)'
    ];
    const rows = rep.items.map(item => [
      item.accountCode,
      this.langService.isRtl() ? item.accountNameAr : item.accountNameEn,
      item.openingDebit,
      item.openingCredit,
      item.periodDebit,
      item.periodCredit,
      item.endingDebit,
      item.endingCredit
    ]);
    rows.push([
      'الإجمالي العام (Total)',
      '',
      rep.totalOpeningDebit,
      rep.totalOpeningCredit,
      rep.totalPeriodDebit,
      rep.totalPeriodCredit,
      rep.totalEndingDebit,
      rep.totalEndingCredit
    ]);
    this.exportService.exportToCsv('Trial_Balance_Report', headers, rows);
  }
}
