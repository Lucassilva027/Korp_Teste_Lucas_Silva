import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import {
  CreateInvoicePayload,
  CreateProductPayload,
  DashboardData,
  FailureModeState,
  Invoice,
  PrintInvoiceResponse,
  Product,
  UpdateFailureModePayload,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class NfeApiService {
  private readonly http = inject(HttpClient);

  private readonly stockApiUrl = 'http://localhost:5001/api';
  private readonly billingApiUrl = 'http://localhost:5002/api';

  getDashboard(): Observable<DashboardData> {
    return forkJoin({
      products: this.getProducts(),
      invoices: this.getInvoices(),
      failureMode: this.getFailureMode(),
    });
  }

  getProducts(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.stockApiUrl}/products`);
  }

  createProduct(payload: CreateProductPayload): Observable<Product> {
    return this.http.post<Product>(`${this.stockApiUrl}/products`, payload);
  }

  getInvoices(): Observable<Invoice[]> {
    return this.http.get<Invoice[]>(`${this.billingApiUrl}/invoices`);
  }

  createInvoice(payload: CreateInvoicePayload): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.billingApiUrl}/invoices`, payload);
  }

  printInvoice(invoiceId: string): Observable<PrintInvoiceResponse> {
    return this.http.post<PrintInvoiceResponse>(`${this.billingApiUrl}/invoices/${invoiceId}/print`, {});
  }

  getFailureMode(): Observable<FailureModeState> {
    return this.http.get<FailureModeState>(`${this.stockApiUrl}/failure-mode`);
  }

  updateFailureMode(payload: UpdateFailureModePayload): Observable<FailureModeState> {
    return this.http.post<FailureModeState>(`${this.stockApiUrl}/failure-mode`, payload);
  }
}
