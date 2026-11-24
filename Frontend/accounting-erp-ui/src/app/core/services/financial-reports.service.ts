import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface GeneralLedgerRequest {
  accountId?: string;
  fromDate?: string;
  toDate?: string;
}

export interface IncomeStatementRequest {
  fromDate?: string;
  toDate?: string;
}

export interface BalanceSheetRequest {
  asOfDate?: string;
}

export interface GeneralLedgerLineDto {
  date: string;
  entryNumber: string;
  description: string;
  referenceNumber?: string;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface GeneralLedgerAccountDto {
  accountId: string;
  accountCode: string;
  accountNameAr: string;
  accountNameEn: string;
  openingBalance: number;
  closingBalance: number;
  totalDebit: number;
  totalCredit: number;
  lines: GeneralLedgerLineDto[];
}

export interface GeneralLedgerReportDto {
  companyName: string;
  fromDate?: string;
  toDate?: string;
  accounts: GeneralLedgerAccountDto[];
}

export interface IncomeStatementReportDto {
  companyName: string;
  fromDate?: string;
  toDate?: string;
  totalRevenue: number;
  costOfGoodsSold: number;
  grossProfit: number;
  operatingExpenses: number;
  operatingIncome: number;
  netIncome: number;
}

export interface BalanceSheetReportDto {
  companyName: string;
  asOfDate: string;
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  isBalanced: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class FinancialReportsService {
  private readonly baseUrl = `${environment.apiUrl}/reports`;

  constructor(private http: HttpClient) {}

  getGeneralLedger(request: GeneralLedgerRequest): Observable<GeneralLedgerReportDto> {
    return this.http.post<GeneralLedgerReportDto>(`${this.baseUrl}/general-ledger`, request);
  }

  getIncomeStatement(request: IncomeStatementRequest): Observable<IncomeStatementReportDto> {
    return this.http.post<IncomeStatementReportDto>(`${this.baseUrl}/income-statement`, request);
  }

  getBalanceSheet(request: BalanceSheetRequest): Observable<BalanceSheetReportDto> {
    return this.http.post<BalanceSheetReportDto>(`${this.baseUrl}/balance-sheet`, request);
  }
}
