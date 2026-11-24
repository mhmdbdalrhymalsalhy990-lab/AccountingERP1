import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { GeneralLedgerService, JournalEntryDto } from '../../core/services/general-ledger.service';
import { LanguageService } from '../../core/services/language.service';
import { TenantService } from '../../core/services/tenant.service';

export interface DashboardMetrics {
  totalRevenue: number;
  totalExpenses: number;
  netProfit: number;
  cashAndBankBalance: number;
  entriesCount: number;
  unpostedEntriesCount: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class DashboardComponent implements OnInit {
  private glService = inject(GeneralLedgerService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  recentEntries = signal<JournalEntryDto[]>([]);
  metrics = signal<DashboardMetrics>({
    totalRevenue: 250000,
    totalExpenses: 85000,
    netProfit: 165000,
    cashAndBankBalance: 120500,
    entriesCount: 14,
    unpostedEntriesCount: 2
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.glService.getJournalEntries().subscribe({
      next: (entries) => {
        this.recentEntries.set(entries.slice(0, 5));
        const unposted = entries.filter(e => e.status !== 'Posted').length;
        this.metrics.update(m => ({
          ...m,
          entriesCount: entries.length,
          unpostedEntriesCount: unposted
        }));
        this.isLoading.set(false);
      },
      error: () => {
        // Fallback demo entries if API is unreachable
        this.isLoading.set(false);
      }
    });
  }
}
