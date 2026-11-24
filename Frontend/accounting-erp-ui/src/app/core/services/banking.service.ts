import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface BankAccountDto {
  id: string;
  accountNumber: string;
  bankName: string;
  branchName?: string;
  iban?: string;
  swiftCode?: string;
  isCash: boolean;
  bookBalance: number;
  currencyId: string;
  currencySymbol?: string;
  glAccountId: string;
  glAccountNameAr?: string;
  isActive: boolean;
}

export interface BankReconciliationDto {
  id: string;
  bankAccountId: string;
  statementDate: string;
  statementEndingBalance: number;
  bookBalance: number;
  difference: number;
  isReconciled: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class BankingService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getBankAccounts(isCash?: boolean): Observable<BankAccountDto[]> {
    const url = isCash !== undefined 
      ? `${this.baseUrl}/bankaccounts?isCash=${isCash}` 
      : `${this.baseUrl}/bankaccounts`;
    return this.http.get<BankAccountDto[]>(url);
  }

  getBookBalance(id: string): Observable<{ bankAccountId: string; bookBalance: number }> {
    return this.http.get<{ bankAccountId: string; bookBalance: number }>(`${this.baseUrl}/bankaccounts/${id}/balance`);
  }

  getReconciliations(bankAccountId?: string): Observable<BankReconciliationDto[]> {
    const url = bankAccountId 
      ? `${this.baseUrl}/bankreconciliations?bankAccountId=${bankAccountId}` 
      : `${this.baseUrl}/bankreconciliations`;
    return this.http.get<BankReconciliationDto[]>(url);
  }
}
