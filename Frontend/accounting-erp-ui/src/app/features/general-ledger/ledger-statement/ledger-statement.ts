import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FinancialReportsService, GeneralLedgerReportDto } from '../../../core/services/financial-reports.service';
import { GeneralLedgerService, AccountDto } from '../../../core/services/general-ledger.service';
import { ExportService } from '../../../core/services/export.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-ledger-statement',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './ledger-statement.html',
  styleUrls: ['./ledger-statement.scss']
})
export class LedgerStatementComponent implements OnInit {
  private reportService = inject(FinancialReportsService);
  private glService = inject(GeneralLedgerService);
  private exportService = inject(ExportService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(false);
  accounts = signal<AccountDto[]>([]);
  report = signal<GeneralLedgerReportDto | null>(null);

  selectedAccountId = signal<string>('');
  fromDate = signal<string>('');
  toDate = signal<string>('');

  ngOnInit(): void {
    this.glService.getAccounts().subscribe({
      next: (data) => {
        const posting = data.filter(a => a.isPosting);
        this.accounts.set(posting);
        if (posting.length > 0) {
          this.selectedAccountId.set(posting[0].id);
          this.loadStatement();
        }
      }
    });
  }

  loadStatement(): void {
    this.isLoading.set(true);
    this.reportService.getGeneralLedger({
      accountId: this.selectedAccountId() || undefined,
      fromDate: this.fromDate() || undefined,
      toDate: this.toDate() || undefined
    }).subscribe({
      next: (data) => {
        this.report.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  exportToExcel(): void {
    const rep = this.report();
    if (!rep || !rep.accounts) return;
    const headers = [
      'رمز الحساب',
      'التاريخ',
      'رقم القيد',
      'البيان',
      'المرجع',
      'مدين',
      'دائن',
      'الرصيد التراكمي'
    ];
    const rows: (string | number)[][] = [];
    rep.accounts.forEach(acc => {
      rows.push([acc.accountCode, 'افتتاحي', '--', 'رصيد أول المدة', '', 0, 0, acc.openingBalance]);
      acc.lines.forEach(l => {
        rows.push([
          acc.accountCode,
          l.date,
          l.entryNumber,
          l.description,
          l.referenceNumber || '',
          l.debit,
          l.credit,
          l.runningBalance
        ]);
      });
      rows.push([acc.accountCode, 'ختامي', '--', 'مجموع الحركات ورصيد الإغلاق', '', acc.totalDebit, acc.totalCredit, acc.closingBalance]);
    });
    this.exportService.exportToCsv('General_Ledger_Statement', headers, rows);
  }
}
