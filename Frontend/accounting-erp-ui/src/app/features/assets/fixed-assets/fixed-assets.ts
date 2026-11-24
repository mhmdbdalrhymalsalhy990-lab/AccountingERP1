import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FixedAssetsService, FixedAssetDto } from '../../../core/services/fixed-assets.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-fixed-assets',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './fixed-assets.html',
  styleUrls: ['./fixed-assets.scss']
})
export class FixedAssetsComponent implements OnInit {
  private assetService = inject(FixedAssetsService);
  private snackBar = inject(MatSnackBar);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  assets = signal<FixedAssetDto[]>([]);

  ngOnInit(): void {
    this.loadAssets();
  }

  loadAssets(): void {
    this.isLoading.set(true);
    this.assetService.getFixedAssets().subscribe({
      next: (data) => {
        const items = data.items || data;
        this.assets.set(Array.isArray(items) ? items : []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  runDepreciation(asset: FixedAssetDto): void {
    this.assetService.calculateMonthlyDepreciation(asset.id).subscribe({
      next: () => {
        this.snackBar.open(
          this.langService.isRtl() 
            ? `تم احتساب وترحيل إهلاك الأصل ${asset.assetCode} بنجاح` 
            : `Depreciation calculated and posted for ${asset.assetCode}`,
          'إغلاق', { duration: 3000 }
        );
        this.loadAssets();
      }
    });
  }
}
