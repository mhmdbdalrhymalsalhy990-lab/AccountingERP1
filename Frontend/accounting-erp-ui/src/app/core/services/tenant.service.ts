import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable, tap } from 'rxjs';

export interface CompanySummary {
  id: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  commercialRegistrationNumber?: string;
  currencyId: string;
  currencySymbol?: string;
  isActive: boolean;
}

export interface BranchSummary {
  id: string;
  companyId: string;
  nameAr: string;
  nameEn: string;
  code: string;
  isMain: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  private readonly baseUrl = `${environment.apiUrl}/companies`;

  // Signals for state
  private companiesSignal = signal<CompanySummary[]>([]);
  private selectedCompanyIdSignal = signal<string | null>(localStorage.getItem('erp_company_id'));
  private selectedBranchIdSignal = signal<string | null>(localStorage.getItem('erp_branch_id'));

  readonly companies = this.companiesSignal.asReadonly();
  readonly selectedCompanyId = this.selectedCompanyIdSignal.asReadonly();
  readonly selectedBranchId = this.selectedBranchIdSignal.asReadonly();

  readonly currentCompany = computed(() => {
    const list = this.companiesSignal();
    const id = this.selectedCompanyIdSignal();
    return list.find(c => c.id === id) || (list.length > 0 ? list[0] : null);
  });

  constructor(private http: HttpClient) {
    // If no company selected or empty, initialize from storage or API
    this.loadCompanies();
  }

  loadCompanies(): Observable<CompanySummary[]> {
    return this.http.get<CompanySummary[]>(this.baseUrl).pipe(
      tap(companies => {
        this.companiesSignal.set(companies);
        if (companies.length > 0 && !this.selectedCompanyIdSignal()) {
          this.setCompany(companies[0].id);
        }
      })
    );
  }

  setCompany(companyId: string): void {
    this.selectedCompanyIdSignal.set(companyId);
    localStorage.setItem('erp_company_id', companyId);
  }

  setBranch(branchId: string | null): void {
    this.selectedBranchIdSignal.set(branchId);
    if (branchId) {
      localStorage.setItem('erp_branch_id', branchId);
    } else {
      localStorage.removeItem('erp_branch_id');
    }
  }

  clear(): void {
    this.selectedCompanyIdSignal.set(null);
    this.selectedBranchIdSignal.set(null);
    localStorage.removeItem('erp_company_id');
    localStorage.removeItem('erp_branch_id');
  }
}
