import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GeneralLedgerService, AccountDto } from '../../../core/services/general-ledger.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';
import { PageHeaderComponent } from '@shared';

@Component({
  selector: 'app-chart-of-accounts',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    PageHeaderComponent
  ],
  templateUrl: './chart-of-accounts.html',
  styleUrls: ['./chart-of-accounts.scss']
})
export class ChartOfAccountsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private glService = inject(GeneralLedgerService);
  private snackBar = inject(MatSnackBar);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  isSubmitting = signal(false);
  showNewAccountForm = signal(false);

  searchTerm = signal('');
  selectedType = signal('all');
  selectedPostingFilter = signal<'all' | 'header' | 'posting'>('all');

  accounts = signal<AccountDto[]>([]);

  // Header accounts that can be parents (usually non-posting or level < 4)
  headerAccounts = computed(() => {
    return this.accounts().filter(a => !a.isPosting || a.level < 4);
  });

  filteredAccounts = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const typeFilter = this.selectedType();
    const postFilter = this.selectedPostingFilter();
    let list = this.accounts();

    // Type filter
    if (typeFilter !== 'all') {
      list = list.filter(acc => acc.accountType.toLowerCase() === typeFilter.toLowerCase());
    }

    // Posting filter
    if (postFilter === 'header') {
      list = list.filter(acc => !acc.isPosting);
    } else if (postFilter === 'posting') {
      list = list.filter(acc => acc.isPosting);
    }

    // Search filter
    if (!term) return list;
    return list.filter(acc => 
      acc.code.toLowerCase().includes(term) ||
      acc.nameAr.toLowerCase().includes(term) ||
      acc.nameEn.toLowerCase().includes(term) ||
      acc.accountType.toLowerCase().includes(term)
    );
  });

  accountTypesList = [
    { value: 'all', labelAr: 'جميع الأنواع', labelEn: 'All Types' },
    { value: 'Asset', labelAr: 'الأصول (1)', labelEn: 'Assets (1)' },
    { value: 'Liability', labelAr: 'الالتزامات (2)', labelEn: 'Liabilities (2)' },
    { value: 'Equity', labelAr: 'حقوق الملكية (3)', labelEn: 'Equity (3)' },
    { value: 'Revenue', labelAr: 'الإيرادات (4)', labelEn: 'Revenues (4)' },
    { value: 'Expense', labelAr: 'المصروفات (5)', labelEn: 'Expenses (5)' }
  ];

  newAccountForm = this.fb.group({
    parentAccountId: [''],
    accountCode: [''],
    nameAr: ['', [Validators.required, Validators.minLength(2)]],
    nameEn: [''],
    isPosting: [true],
    accountType: ['Asset'],
    nature: ['Debit']
  });

  ngOnInit(): void {
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.isLoading.set(true);
    this.glService.getAccounts(500).subscribe({
      next: (data) => {
        this.accounts.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = err.error?.message || err.message || (this.langService.isRtl() ? 'فشل تحميل دليل الحسابات' : 'Failed to load chart of accounts');
        this.snackBar.open(msg, 'إغلاق', { duration: 4000 });
      }
    });
  }

  toggleNewAccount(): void {
    this.showNewAccountForm.set(!this.showNewAccountForm());
    if (this.showNewAccountForm()) {
      this.resetForm();
    }
  }

  resetForm(): void {
    this.newAccountForm.reset({
      parentAccountId: '',
      accountCode: '',
      nameAr: '',
      nameEn: '',
      isPosting: true,
      accountType: 'Asset',
      nature: 'Debit'
    });
  }

  onParentChange(parentIdVal: string): void {
    if (!parentIdVal) {
      this.newAccountForm.patchValue({ accountCode: '' });
      return;
    }

    const parent = this.accounts().find(a => a.id === parentIdVal);
    if (parent) {
      this.newAccountForm.patchValue({
        accountType: parent.accountType || 'Asset',
        nature: parent.nature || (parent.accountType === 'Asset' || parent.accountType === 'Expense' ? 'Debit' : 'Credit')
      });
    }

    this.glService.getNextAccountCode(parentIdVal).subscribe({
      next: (res) => {
        if (res && res.nextCode) {
          this.newAccountForm.patchValue({ accountCode: res.nextCode });
        }
      }
    });
  }

  openAddChildAccount(parent: AccountDto): void {
    this.showNewAccountForm.set(true);
    this.newAccountForm.patchValue({
      parentAccountId: parent.id,
      nameAr: '',
      nameEn: '',
      isPosting: true,
      accountType: parent.accountType || 'Asset',
      nature: parent.nature || (parent.accountType === 'Asset' || parent.accountType === 'Expense' ? 'Debit' : 'Credit')
    });
    this.onParentChange(parent.id);
  }

  onSubmitNewAccount(): void {
    if (this.newAccountForm.invalid) {
      this.snackBar.open(
        this.langService.isRtl() ? 'يرجى إدخال اسم الحساب بشكل صحيح' : 'Please provide a valid account name',
        'إغلاق', { duration: 3000 }
      );
      return;
    }

    this.isSubmitting.set(true);
    const formVal = this.newAccountForm.value;

    const payload: any = {
      nameAr: formVal.nameAr!.trim(),
      nameEn: formVal.nameEn?.trim() || '',
      isPosting: !!formVal.isPosting
    };

    if (formVal.parentAccountId) {
      payload.parentAccountId = Number(formVal.parentAccountId);
    }
    if (formVal.accountCode) {
      payload.accountCode = formVal.accountCode.trim();
    }
    if (formVal.accountType) {
      payload.accountType = formVal.accountType;
    }
    if (formVal.nature) {
      payload.nature = formVal.nature;
    }

    this.glService.createAccount(payload).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.snackBar.open(
          this.langService.isRtl() ? `تم إنشاء الحساب ${created.code || created.accountCode} بنجاح` : `Account ${created.code || created.accountCode} created successfully`,
          'إغلاق', { duration: 3500 }
        );
        this.showNewAccountForm.set(false);
        this.resetForm();
        this.loadAccounts();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = err.error?.message || err.message || (this.langService.isRtl() ? 'فشل إنشاء الحساب' : 'Failed to create account');
        this.snackBar.open(msg, 'إغلاق', { duration: 5000 });
      }
    });
  }

  toggleAccountStatus(acc: AccountDto): void {
    this.glService.toggleAccountStatus(acc.id).subscribe({
      next: (res) => {
        this.snackBar.open(
          res.message || (this.langService.isRtl() ? 'تم تغيير حالة الحساب بنجاح' : 'Status updated'),
          'إغلاق', { duration: 3000 }
        );
        this.loadAccounts();
      },
      error: (err) => {
        const msg = err.error?.message || err.message || (this.langService.isRtl() ? 'فشل تغيير حالة الحساب' : 'Failed to toggle status');
        this.snackBar.open(msg, 'إغلاق', { duration: 4000 });
      }
    });
  }

  deleteAccount(acc: AccountDto): void {
    const confirmMsg = this.langService.isRtl() 
      ? `هل أنت متأكد من رغبتك في حذف الحساب (${acc.code} - ${acc.nameAr})؟`
      : `Are you sure you want to delete account (${acc.code} - ${acc.nameEn || acc.nameAr})?`;

    if (!confirm(confirmMsg)) return;

    this.glService.deleteAccount(acc.id).subscribe({
      next: () => {
        this.snackBar.open(
          this.langService.isRtl() ? `تم حذف الحساب ${acc.code} بنجاح` : `Account ${acc.code} deleted successfully`,
          'إغلاق', { duration: 3000 }
        );
        this.loadAccounts();
      },
      error: (err) => {
        const msg = err.error?.message || err.message || (this.langService.isRtl() ? 'تعذر حذف الحساب (قد يحتوي على حركات محاسبية أو حسابات فرعية)' : 'Cannot delete account');
        this.snackBar.open(msg, 'إغلاق', { duration: 5000 });
      }
    });
  }

  getAccountTypeClass(type: string): string {
    switch (type?.toLowerCase()) {
      case 'asset': return 'type-asset';
      case 'liability': return 'type-liability';
      case 'equity': return 'type-equity';
      case 'revenue': return 'type-revenue';
      case 'expense': return 'type-expense';
      default: return '';
    }
  }

  getAccountTypeLabel(type: string): string {
    switch (type?.toLowerCase()) {
      case 'asset': return this.langService.isRtl() ? 'أصول' : 'Asset';
      case 'liability': return this.langService.isRtl() ? 'التزامات' : 'Liability';
      case 'equity': return this.langService.isRtl() ? 'حقوق ملكية' : 'Equity';
      case 'revenue': return this.langService.isRtl() ? 'إيرادات' : 'Revenue';
      case 'expense': return this.langService.isRtl() ? 'مصروفات' : 'Expense';
      default: return type || '-';
    }
  }
}
