import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommercialService, SupplierDto } from '../../../core/services/commercial.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';
import { PageHeaderComponent } from '@shared';

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageHeaderComponent
  ],
  templateUrl: './suppliers.html',
  styleUrls: ['./suppliers.scss']
})
export class SuppliersComponent implements OnInit {
  private fb = inject(FormBuilder);
  private commercialService = inject(CommercialService);
  private snackBar = inject(MatSnackBar);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  showForm = signal(false);
  suppliers = signal<SupplierDto[]>([]);

  supplierForm = this.fb.group({
    code: ['', Validators.required],
    nameAr: ['', Validators.required],
    nameEn: ['', Validators.required],
    taxNumber: [''],
    phone: [''],
    email: ['', Validators.email]
  });

  ngOnInit(): void {
    this.loadSuppliers();
  }

  loadSuppliers(): void {
    this.isLoading.set(true);
    this.commercialService.getSuppliers().subscribe({
      next: (data) => {
        this.suppliers.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  toggleForm(): void {
    this.showForm.set(!this.showForm());
  }

  saveSupplier(): void {
    if (this.supplierForm.invalid) return;

    const val = this.supplierForm.value;
    this.commercialService.createSupplier({
      code: val.code!,
      nameAr: val.nameAr!,
      nameEn: val.nameEn!,
      taxNumber: val.taxNumber || undefined,
      phone: val.phone || undefined,
      email: val.email || undefined
    }).subscribe({
      next: (res) => {
        this.snackBar.open(
          this.langService.isRtl() ? `تم إضافة المورد ${res.nameAr} بنجاح` : `Supplier ${res.nameEn} added`,
          'إغلاق', { duration: 3000 }
        );
        this.showForm.set(false);
        this.supplierForm.reset();
        this.loadSuppliers();
      }
    });
  }
}
