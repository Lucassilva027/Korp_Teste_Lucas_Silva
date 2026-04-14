export interface Product {
  id: string;
  code: string;
  description: string;
  balance: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface FailureModeState {
  enabled: boolean;
  message: string | null;
}

export interface InvoiceItem {
  id: string;
  productId: string;
  productCode: string;
  productDescription: string;
  quantity: number;
}

export interface Invoice {
  id: string;
  number: number;
  status: 'Aberta' | 'Fechada';
  createdAtUtc: string;
  closedAtUtc: string | null;
  lastError: string | null;
  items: InvoiceItem[];
}

export interface PrintInvoiceResponse {
  invoice: Invoice;
  message: string;
}

export interface DashboardData {
  products: Product[];
  invoices: Invoice[];
  failureMode: FailureModeState;
}

export interface CreateProductPayload {
  code: string;
  description: string;
  balance: number;
}

export interface CreateInvoicePayload {
  items: Array<{
    productId: string;
    productCode: string;
    productDescription: string;
    quantity: number;
  }>;
}

export interface UpdateFailureModePayload {
  enabled: boolean;
  message: string;
}
