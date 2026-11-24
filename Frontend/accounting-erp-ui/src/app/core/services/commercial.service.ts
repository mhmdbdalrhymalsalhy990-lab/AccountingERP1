import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface CustomerDto {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  phone?: string;
  email?: string;
  currentBalance: number;
}

export interface SupplierDto {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  phone?: string;
  email?: string;
  currentBalance: number;
}

export interface SalesInvoiceItemDto {
  id?: string;
  productId: string;
  productName?: string;
  quantity: number;
  unitPrice: number;
  subTotal: number;
  discount: number;
  taxAmount: number;
  netTotal: number;
}

export interface SalesInvoiceDto {
  id: string;
  invoiceNumber: string;
  customerId: string;
  customerNameAr?: string;
  customerNameEn?: string;
  invoiceDate: string;
  dueDate?: string;
  status: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  journalEntryId?: string;
  items: SalesInvoiceItemDto[];
}

export interface PurchaseInvoiceDto {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierNameAr?: string;
  supplierNameEn?: string;
  invoiceDate: string;
  dueDate?: string;
  status: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  journalEntryId?: string;
}

export interface CreateCustomerDto {
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  phone?: string;
  email?: string;
}

export interface CreateSupplierDto {
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  phone?: string;
  email?: string;
}

@Injectable({
  providedIn: 'root'
})
export class CommercialService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Customers
  getCustomers(): Observable<CustomerDto[]> {
    return this.http.get<CustomerDto[]>(`${this.baseUrl}/customers`);
  }

  createCustomer(dto: CreateCustomerDto): Observable<CustomerDto> {
    return this.http.post<CustomerDto>(`${this.baseUrl}/customers`, dto);
  }

  // Suppliers
  getSuppliers(): Observable<SupplierDto[]> {
    return this.http.get<SupplierDto[]>(`${this.baseUrl}/suppliers`);
  }

  createSupplier(dto: CreateSupplierDto): Observable<SupplierDto> {
    return this.http.post<SupplierDto>(`${this.baseUrl}/suppliers`, dto);
  }

  // Sales Invoices
  getSalesInvoices(): Observable<SalesInvoiceDto[]> {
    return this.http.get<SalesInvoiceDto[]>(`${this.baseUrl}/salesinvoices`);
  }

  postSalesInvoice(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/salesinvoices/${id}/post`, {});
  }

  // Purchase Invoices
  getPurchaseInvoices(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/purchaseinvoices`);
  }

  postPurchaseInvoice(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/purchaseinvoices/${id}/post`, {});
  }
}
