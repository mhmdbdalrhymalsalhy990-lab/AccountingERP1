import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { InventoryService, ProductDto } from '../../../core/services/inventory.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-products-inventory',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './products-inventory.html',
  styleUrls: ['./products-inventory.scss']
})
export class ProductsInventoryComponent implements OnInit {
  private inventoryService = inject(InventoryService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  products = signal<ProductDto[]>([]);

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.isLoading.set(true);
    this.inventoryService.getProducts().subscribe({
      next: (data) => {
        const items = data.items || data;
        this.products.set(Array.isArray(items) ? items : []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
