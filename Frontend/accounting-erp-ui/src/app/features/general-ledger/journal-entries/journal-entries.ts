import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';
import { GeneralLedgerService, JournalEntryDto, AccountLookupDto } from '../../../core/services/general-ledger.service';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';
import { PageHeaderComponent, StatusBadgeComponent, CurrencyFormatPipe } from '@shared';

@Component({
  selector: 'app-journal-entries',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    CurrencyFormatPipe
  ],
  templateUrl: './journal-entries.html',
  styleUrls: ['./journal-entries.scss']
})
export class JournalEntriesComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private glService = inject(GeneralLedgerService);
  private snackBar = inject(MatSnackBar);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  isSubmitting = signal(false);
  showNewEntryForm = signal(false);

  // Entries List & Pagination
  entries = signal<JournalEntryDto[]>([]);
  totalCount = signal(0);
  pageNumber = signal(1);
  pageSize = signal(20);

  // Search & Filter state
  searchTerm = signal('');
  selectedStatus = signal<number | undefined>(undefined);
  fromDate = signal('');
  toDate = signal('');

  // Expandable row details
  expandedEntryId = signal<string | null>(null);

  // Reversal dialog state
  showReverseModal = signal(false);
  entryToReverse = signal<JournalEntryDto | null>(null);
  reverseReason = signal('');
  isReversing = signal(false);

  // Posting accounts for dropdown
  postingAccounts = signal<AccountLookupDto[]>([]);

  // Totals & Balance
  totalDebit = signal(0);
  totalCredit = signal(0);

  isBalanced = computed(() => {
    const d = Math.round(this.totalDebit() * 100);
    const c = Math.round(this.totalCredit() * 100);
    return d > 0 && d === c;
  });

  difference = computed(() => {
    return Math.abs(this.totalDebit() - this.totalCredit());
  });

  newEntryForm = this.fb.group({
    date: [new Date().toISOString().substring(0, 10), Validators.required],
    entryType: ['Manual', Validators.required],
    description: ['', [Validators.required, Validators.minLength(3)]],
    referenceNumber: [''],
    lines: this.fb.array([])
  });

  private formSub?: Subscription;

  get lines(): FormArray {
    return this.newEntryForm.get('lines') as FormArray;
  }

  ngOnInit(): void {
    this.loadData();
    this.loadAccountsLookup();
    this.initDefaultLines();

    this.formSub = this.newEntryForm.valueChanges.subscribe(() => {
      this.recalculateTotals();
    });
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.glService.getJournalEntriesPaged({
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
      searchTerm: this.searchTerm() || undefined,
      status: this.selectedStatus(),
      fromDate: this.fromDate() || undefined,
      toDate: this.toDate() || undefined
    }).subscribe({
      next: (res) => {
        this.entries.set(res.items);
        this.totalCount.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  loadAccountsLookup(): void {
    this.glService.getPostingAccountsLookup().subscribe({
      next: (accs) => this.postingAccounts.set(accs),
      error: () => {
        // Fallback to getAccounts if lookup endpoint fails
        this.glService.getAccounts(500).subscribe({
          next: (all) => {
            const mapped: AccountLookupDto[] = all.filter(a => a.isPosting).map(a => ({
              id: Number(a.id),
              accountCode: a.code || a.accountCode || '',
              nameAr: a.nameAr,
              nameEn: a.nameEn,
              displayText: `${a.code || a.accountCode} - ${a.nameAr}`
            }));
            this.postingAccounts.set(mapped);
          }
        });
      }
    });
  }

  initDefaultLines(): void {
    this.lines.clear();
    this.addLine();
    this.addLine();
    this.recalculateTotals();
  }

  addLine(): void {
    const lineGroup = this.fb.group({
      accountId: ['', Validators.required],
      description: [''],
      debit: [0, [Validators.required, Validators.min(0)]],
      credit: [0, [Validators.required, Validators.min(0)]]
    });
    this.lines.push(lineGroup);
    this.recalculateTotals();
  }

  removeLine(index: number): void {
    if (this.lines.length > 2) {
      this.lines.removeAt(index);
      this.recalculateTotals();
    }
  }

  recalculateTotals(): void {
    let d = 0;
    let c = 0;
    for (const ctrl of this.lines.controls) {
      d += Number(ctrl.get('debit')?.value) || 0;
      c += Number(ctrl.get('credit')?.value) || 0;
    }
    this.totalDebit.set(d);
    this.totalCredit.set(c);
  }

  toggleNewEntry(): void {
    this.showNewEntryForm.set(!this.showNewEntryForm());
    if (this.showNewEntryForm() && this.lines.length === 0) {
      this.initDefaultLines();
    }
  }

  toggleExpandEntry(entry: JournalEntryDto): void {
    const entryId = String(entry.id);
    if (this.expandedEntryId() === entryId) {
      this.expandedEntryId.set(null);
    } else {
      this.expandedEntryId.set(entryId);
      // If entry has no lines loaded, load full details
      if (!entry.lines || entry.lines.length === 0) {
        this.glService.getJournalEntryById(entry.id).subscribe({
          next: (full) => {
            const current = this.entries();
            const idx = current.findIndex(e => String(e.id) === entryId);
            if (idx !== -1) {
              current[idx] = full;
              this.entries.set([...current]);
            }
          }
        });
      }
    }
  }

  // Filter actions
  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageNumber.set(1);
    this.loadData();
  }

  setStatusFilter(status?: number): void {
    this.selectedStatus.set(status);
    this.pageNumber.set(1);
    this.loadData();
  }

  onDateFilter(): void {
    this.pageNumber.set(1);
    this.loadData();
  }

  resetFilters(): void {
    this.searchTerm.set('');
    this.selectedStatus.set(undefined);
    this.fromDate.set('');
    this.toDate.set('');
    this.pageNumber.set(1);
    this.loadData();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.pageNumber.set(page);
      this.loadData();
    }
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
  }

  // Submission (Draft or Direct Post)
  onSubmitNewEntry(autoPost: boolean = false): void {
    this.recalculateTotals();

    if (this.newEntryForm.invalid) {
      this.newEntryForm.markAllAsTouched();
      this.snackBar.open(
        this.langService.isRtl() ? 'يرجى استكمال الحقول المطلوبة (الوصف، اختيار الحساب في كل سطر)' : 'Please complete all required fields',
        'إغلاق', { duration: 3500 }
      );
      return;
    }

    if (!this.isBalanced()) {
      this.snackBar.open(
        this.langService.isRtl() ? 'القيد غير متوازن! يجب أن يتساوى إجمالي المدين مع إجمالي الدائن' : 'Entry is unbalanced! Debits must equal Credits',
        'إغلاق', { duration: 3500 }
      );
      return;
    }

    this.isSubmitting.set(true);
    const formVal = this.newEntryForm.value;

    const request = {
      entryDate: formVal.date ? new Date(formVal.date).toISOString() : new Date().toISOString(),
      date: formVal.date ? new Date(formVal.date).toISOString() : new Date().toISOString(),
      entryType: 1, // Manual
      description: formVal.description!.trim(),
      referenceNumber: formVal.referenceNumber?.trim() || undefined,
      lines: (formVal.lines as any[]).map((l, idx) => ({
        accountId: Number(l.accountId),
        description: l.description?.trim() || formVal.description!.trim(),
        debit: Number(l.debit) || 0,
        credit: Number(l.credit) || 0,
        lineNumber: idx + 1
      }))
    };

    const action$ = autoPost 
      ? this.glService.createAndPostJournalEntry(request)
      : this.glService.createJournalEntry(request);

    action$.subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        const successMsg = autoPost
          ? (this.langService.isRtl() ? `تم إنشاء وترحيل القيد ${res.entryNumber} بنجاح` : `Entry ${res.entryNumber} created and posted successfully`)
          : (this.langService.isRtl() ? `تم حفظ القيد المسودة ${res.entryNumber} بنجاح` : `Draft entry ${res.entryNumber} saved successfully`);
        
        this.snackBar.open(successMsg, 'إغلاق', { duration: 3500 });
        this.showNewEntryForm.set(false);
        this.newEntryForm.reset({
          date: new Date().toISOString().substring(0, 10),
          entryType: 'Manual'
        });
        this.initDefaultLines();
        this.loadData();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const errorMsg = err.error?.message || err.message || (this.langService.isRtl() ? 'حدث خطأ أثناء حفظ القيد' : 'Error saving entry');
        this.snackBar.open(errorMsg, 'إغلاق', { duration: 5000 });
      }
    });
  }

  // Post Draft Entry
  postEntry(entry: JournalEntryDto): void {
    this.glService.postJournalEntry(entry.id).subscribe({
      next: (res) => {
        this.snackBar.open(
          this.langService.isRtl() ? `تم تترحيل القيد برقم متسلسل: ${res.entryNumber || entry.entryNumber}` : `Entry posted as ${res.entryNumber || entry.entryNumber}`,
          'إغلاق', { duration: 3500 }
        );
        this.loadData();
      },
      error: (err) => {
        const msg = err.error?.message || err.message || (this.langService.isRtl() ? 'فشل ترحيل القيد' : 'Failed to post entry');
        this.snackBar.open(msg, 'إغلاق', { duration: 5000 });
      }
    });
  }

  // Reverse Entry Modal & Action
  openReverseModal(entry: JournalEntryDto): void {
    this.entryToReverse.set(entry);
    this.reverseReason.set('');
    this.showReverseModal.set(true);
  }

  closeReverseModal(): void {
    this.showReverseModal.set(false);
    this.entryToReverse.set(null);
    this.reverseReason.set('');
  }

  confirmReverse(): void {
    const entry = this.entryToReverse();
    const reason = this.reverseReason().trim();

    if (!entry) return;
    if (!reason || reason.length < 3) {
      this.snackBar.open(
        this.langService.isRtl() ? 'يرجى إدخال سبب عكس القيد بشكل واضح (3 أحرف على الأقل)' : 'Please provide a valid reversal reason',
        'إغلاق', { duration: 3000 }
      );
      return;
    }

    this.isReversing.set(true);
    this.glService.reverseJournalEntry(entry.id, reason).subscribe({
      next: (rev) => {
        this.isReversing.set(false);
        this.closeReverseModal();
        this.snackBar.open(
          this.langService.isRtl() ? `تم إنشاء القيد العكسي برقم ${rev.entryNumber} بنجاح` : `Reversal entry ${rev.entryNumber} created successfully`,
          'إغلاق', { duration: 4000 }
        );
        this.loadData();
      },
      error: (err) => {
        this.isReversing.set(false);
        const msg = err.error?.message || err.message || (this.langService.isRtl() ? 'فشل عكس القيد' : 'Failed to reverse entry');
        this.snackBar.open(msg, 'إغلاق', { duration: 5000 });
      }
    });
  }
}
