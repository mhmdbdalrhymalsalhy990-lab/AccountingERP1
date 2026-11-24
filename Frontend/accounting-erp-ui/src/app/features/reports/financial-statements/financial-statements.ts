import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { 
  FinancialReportsService, 
  IncomeStatementReportDto, 
  BalanceSheetReportDto 
} from '../../../core/services/financial-reports.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-financial-statements',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './financial-statements.html',
  styleUrls: ['./financial-statements.scss']
})
export class FinancialStatementsComponent implements OnInit {
  private reportService = inject(FinancialReportsService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  incomeStatement = signal<IncomeStatementReportDto | null>(null);
  balanceSheet = signal<BalanceSheetReportDto | null>(null);

  ngOnInit(): void {
    this.loadReports();
  }

  loadReports(): void {
    this.isLoading.set(true);

    this.reportService.getIncomeStatement({}).subscribe({
      next: (res) => {
        this.incomeStatement.set(res);
        this.loadBalanceSheet();
      },
      error: () => this.loadBalanceSheet()
    });
  }

  private loadBalanceSheet(): void {
    this.reportService.getBalanceSheet({}).subscribe({
      next: (res) => {
        this.balanceSheet.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
