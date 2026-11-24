import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SettingsService, TaxTypeDto, CurrencyDto } from '../../../core/services/settings.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-system-settings',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTabsModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './system-settings.html',
  styleUrls: ['./system-settings.scss']
})
export class SystemSettingsComponent implements OnInit {
  private settingsService = inject(SettingsService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  taxes = signal<TaxTypeDto[]>([]);
  currencies = signal<CurrencyDto[]>([]);

  ngOnInit(): void {
    this.loadSettings();
  }

  loadSettings(): void {
    this.isLoading.set(true);
    this.settingsService.getTaxes().subscribe({
      next: (data) => {
        this.taxes.set(data);
        this.loadCurrencies();
      },
      error: () => this.loadCurrencies()
    });
  }

  private loadCurrencies(): void {
    this.settingsService.getCurrencies().subscribe({
      next: (data) => {
        this.currencies.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
