import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface TaxTypeDto {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  taxCategory: string;
  currentRate: number;
  isActive: boolean;
}

export interface CurrencyDto {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  symbol: string;
  isBaseCurrency: boolean;
  exchangeRateToBase: number;
  isActive: boolean;
}

export interface AuditLogDto {
  id: string;
  userName: string;
  action: string;
  entityName: string;
  entityId: string;
  timestamp: string;
  ipAddress?: string;
}

@Injectable({
  providedIn: 'root'
})
export class SettingsService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getTaxes(): Observable<TaxTypeDto[]> {
    return this.http.get<TaxTypeDto[]>(`${this.baseUrl}/taxes`);
  }

  getCurrencies(): Observable<CurrencyDto[]> {
    return this.http.get<CurrencyDto[]>(`${this.baseUrl}/currencies`);
  }

  getAuditLogs(): Observable<AuditLogDto[]> {
    return this.http.get<AuditLogDto[]>(`${this.baseUrl}/auditlogs`);
  }
}
