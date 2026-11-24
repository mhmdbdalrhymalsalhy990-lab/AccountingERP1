import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface ProductDto {
  id: string;
  itemCode: string;
  nameAr: string;
  nameEn: string;
  category?: string;
  unitName: string;
  currentStock: number;
  averageCost: number;
  sellingPrice: number;
  reorderLevel: number;
  isActive: boolean;
}

export interface WarehouseDto {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  location?: string;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class InventoryService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getProducts(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/products`);
  }

  createProduct(dto: any): Observable<ProductDto> {
    return this.http.post<ProductDto>(`${this.baseUrl}/products`, dto);
  }

  getWarehouses(): Observable<WarehouseDto[]> {
    return this.http.get<WarehouseDto[]>(`${this.baseUrl}/warehouses`);
  }
}
