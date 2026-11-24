import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface FixedAssetDto {
  id: string;
  assetCode: string;
  nameAr: string;
  nameEn: string;
  assetCategory: string;
  acquisitionDate: string;
  acquisitionCost: number;
  salvageValue: number;
  usefulLifeMonths: number;
  accumulatedDepreciation: number;
  bookValue: number;
  monthlyDepreciationAmount: number;
  status: string;
}

@Injectable({
  providedIn: 'root'
})
export class FixedAssetsService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getFixedAssets(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/fixedassets`);
  }

  calculateMonthlyDepreciation(id: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fixedassets/${id}/depreciate`, {});
  }
}
