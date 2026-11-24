import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable, map } from 'rxjs';

export interface AccountDto {
  id: string;
  code: string;
  accountCode?: string;
  nameAr: string;
  nameEn: string;
  accountType: string;
  nature?: string;
  parentAccountId?: string;
  level: number;
  isPosting: boolean;
  isActive: boolean;
  openingDebitBalance?: number;
  openingCreditBalance?: number;
  currentDebitBalance?: number;
  currentCreditBalance?: number;
  netBalance?: number;
  children?: AccountDto[];
}

export interface AccountLookupDto {
  id: number;
  accountCode: string;
  nameAr: string;
  nameEn: string;
  displayText?: string;
  accountType?: number;
  nature?: number;
  currencyId?: number;
}

export interface JournalEntryLineDto {
  id?: string;
  accountId: string;
  accountCode?: string;
  accountNameAr?: string;
  accountNameEn?: string;
  costCenterId?: string;
  costCenterName?: string;
  description?: string;
  debit: number;
  credit: number;
  lineNumber: number;
}

export interface JournalEntryDto {
  id: string;
  companyId: string;
  branchId?: string;
  fiscalPeriodId: string;
  entryNumber: string;
  date?: string;
  entryDate?: string;
  entryType: string | number;
  entryTypeName?: string;
  status: string | number;
  statusName?: string;
  referenceNumber?: string;
  description: string;
  totalDebit: number;
  totalCredit: number;
  postedAt?: string;
  postedByUserName?: string;
  isReversed?: boolean;
  reversedEntryId?: number;
  reversedEntryNumber?: string;
  lines: JournalEntryLineDto[];
}

export interface CreateJournalEntryRequest {
  entryDate?: string;
  date?: string;
  entryType?: string | number;
  description: string;
  referenceNumber?: string;
  branchId?: string;
  lines: {
    accountId: number | string;
    costCenterId?: string;
    description?: string;
    debit: number;
    credit: number;
    lineNumber: number;
  }[];
}

export interface TrialBalanceItemDto {
  accountId: string;
  accountCode: string;
  accountNameAr: string;
  accountNameEn: string;
  accountType: string;
  openingDebit: number;
  openingCredit: number;
  periodDebit: number;
  periodCredit: number;
  endingDebit: number;
  endingCredit: number;
}

export interface TrialBalanceReportDto {
  companyName: string;
  fromDate?: string;
  toDate?: string;
  generatedAt: string;
  items: TrialBalanceItemDto[];
  totalOpeningDebit: number;
  totalOpeningCredit: number;
  totalPeriodDebit: number;
  totalPeriodCredit: number;
  totalEndingDebit: number;
  totalEndingCredit: number;
  isBalanced: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class GeneralLedgerService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Chart of Accounts
  getAccounts(pageSize: number = 500): Observable<AccountDto[]> {
    return this.http.get<any>(`${this.baseUrl}/accounts?pageSize=${pageSize}`).pipe(
      map((res: any) => {
        const list = Array.isArray(res) ? res : (res?.items || []);
        return list.map((a: any) => ({
          ...a,
          id: String(a.id),
          code: a.code || a.accountCode || '',
          accountCode: a.accountCode || a.code || '',
          accountType: a.accountTypeName || a.accountType || '',
          nature: a.natureName || a.nature || ''
        }));
      })
    );
  }

  getAccountTree(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/accounts/tree`);
  }

  getNextAccountCode(parentAccountId: number | string): Observable<{ parentAccountId: number; nextCode: string }> {
    return this.http.get<{ parentAccountId: number; nextCode: string }>(`${this.baseUrl}/accounts/next-code/${parentAccountId}`);
  }

  createAccount(dto: any): Observable<AccountDto> {
    return this.http.post<AccountDto>(`${this.baseUrl}/accounts`, dto);
  }

  updateAccount(id: number | string, dto: any): Observable<AccountDto> {
    return this.http.put<AccountDto>(`${this.baseUrl}/accounts/${id}`, dto);
  }

  deleteAccount(id: number | string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/accounts/${id}`);
  }

  toggleAccountStatus(id: number | string): Observable<any> {
    return this.http.patch<any>(`${this.baseUrl}/accounts/${id}/toggle-status`, {});
  }

  getPostingAccountsLookup(): Observable<AccountLookupDto[]> {
    return this.http.get<AccountLookupDto[]>(`${this.baseUrl}/accounts/lookup`);
  }

  // Journal Entries
  getJournalEntries(companyId?: string): Observable<JournalEntryDto[]> {
    const url = companyId
      ? `${this.baseUrl}/journalentries?companyId=${companyId}&pageSize=100`
      : `${this.baseUrl}/journalentries?pageSize=100`;
    return this.http.get<any>(url).pipe(
      map((res: any) => {
        return Array.isArray(res) ? res : (res?.items || []);
      })
    );
  }

  getJournalEntriesPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: number;
    fromDate?: string;
    toDate?: string;
    companyId?: string;
  }): Observable<{ items: JournalEntryDto[]; totalCount: number; pageNumber: number; pageSize: number }> {
    let url = `${this.baseUrl}/journalentries?pageNumber=${params.pageNumber || 1}&pageSize=${params.pageSize || 25}`;
    if (params.searchTerm) url += `&searchTerm=${encodeURIComponent(params.searchTerm)}`;
    if (params.status) url += `&status=${params.status}`;
    if (params.fromDate) url += `&fromDate=${params.fromDate}`;
    if (params.toDate) url += `&toDate=${params.toDate}`;
    if (params.companyId) url += `&companyId=${params.companyId}`;

    return this.http.get<any>(url).pipe(
      map((res: any) => ({
        items: res.items || [],
        totalCount: res.totalCount || 0,
        pageNumber: res.pageNumber || 1,
        pageSize: res.pageSize || 25
      }))
    );
  }

  getJournalEntryById(id: string | number): Observable<JournalEntryDto> {
    return this.http.get<JournalEntryDto>(`${this.baseUrl}/journalentries/${id}`);
  }

  createJournalEntry(request: CreateJournalEntryRequest): Observable<JournalEntryDto> {
    return this.http.post<JournalEntryDto>(`${this.baseUrl}/journalentries/draft`, request);
  }

  createAndPostJournalEntry(request: CreateJournalEntryRequest): Observable<JournalEntryDto> {
    return this.http.post<JournalEntryDto>(`${this.baseUrl}/journalentries/create-and-post`, request);
  }

  postJournalEntry(id: string | number): Observable<JournalEntryDto> {
    return this.http.post<JournalEntryDto>(`${this.baseUrl}/journalentries/${id}/post`, {});
  }

  reverseJournalEntry(id: string | number, reason: string, reversalDate?: string): Observable<JournalEntryDto> {
    return this.http.post<JournalEntryDto>(`${this.baseUrl}/journalentries/${id}/reverse`, {
      reason,
      reversalDate: reversalDate || new Date().toISOString()
    });
  }

  // Reports
  getTrialBalance(fromDate?: string, toDate?: string): Observable<TrialBalanceReportDto> {
    let url = `${this.baseUrl}/financialreports/trial-balance`;
    const params: string[] = [];
    if (fromDate) params.push(`fromDate=${fromDate}`);
    if (toDate) params.push(`toDate=${toDate}`);
    if (params.length > 0) {
      url += `?${params.join('&')}`;
    }
    return this.http.get<TrialBalanceReportDto>(url);
  }
}
