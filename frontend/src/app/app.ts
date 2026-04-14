import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { EMPTY, catchError, finalize, switchMap, tap } from 'rxjs';
import { DashboardData, FailureModeState, Invoice, Product } from './models/api.models';
import { NfeApiService } from './services/nfe-api.service';

type FeedbackTone = 'success' | 'error' | 'info';

interface FeedbackBanner {
  tone: FeedbackTone;
  title: string;
  message: string;
}

@Component({
  selector: 'app-root',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly apiService = inject(NfeApiService);
  private readonly destroyRef = inject(DestroyRef);
  private feedbackTimeoutId: ReturnType<typeof setTimeout> | null = null;

  readonly productForm = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    description: ['', [Validators.required, Validators.maxLength(200)]],
    balance: [10, [Validators.required, Validators.min(0)]],
  });

  readonly failureForm = this.formBuilder.nonNullable.group({
    enabled: [false],
    message: ['Falha simulada manualmente no servico de estoque para demonstracao do tratamento entre microsservicos.'],
  });

  readonly invoiceForm = this.formBuilder.group({
    items: this.formBuilder.array([this.createInvoiceItemGroup()]),
  });

  products: Product[] = [];
  invoices: Invoice[] = [];
  failureMode: FailureModeState | null = null;

  dashboardLoading = true;
  savingFailureMode = false;
  submittingProduct = false;
  submittingInvoice = false;
  printingInvoiceId: string | null = null;

  feedback: FeedbackBanner | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.clearFeedbackTimer();
    });
  }

  ngOnInit(): void {
    this.loadDashboard();
  }

  get invoiceItems(): FormArray {
    return this.invoiceForm.get('items') as FormArray;
  }

  get openInvoicesCount(): number {
    return this.invoices.filter((invoice) => invoice.status === 'Aberta').length;
  }

  get closedInvoicesCount(): number {
    return this.invoices.filter((invoice) => invoice.status === 'Fechada').length;
  }

  get lowStockProductsCount(): number {
    return this.products.filter((product) => product.balance <= 3).length;
  }

  addInvoiceItem(): void {
    this.invoiceItems.push(this.createInvoiceItemGroup());
  }

  removeInvoiceItem(index: number): void {
    if (this.invoiceItems.length === 1) {
      return;
    }

    this.invoiceItems.removeAt(index);
  }

  submitProduct(): void {
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      return;
    }

    this.submittingProduct = true;
    const productPayload = this.productForm.getRawValue();

    this.apiService
      .createProduct({
        ...productPayload,
        code: productPayload.code.trim().toUpperCase(),
        description: this.formatProductDescription(productPayload.description),
      })
      .pipe(
        switchMap(() => this.apiService.getDashboard()),
        tap((dashboard) => {
          this.applyDashboard(dashboard);
          this.productForm.reset({
            code: '',
            description: '',
            balance: 10,
          });
          this.setFeedback('success', 'Produto cadastrado', 'O item ja esta disponivel para novas notas fiscais.');
        }),
        catchError((error) => this.handleError(error, 'Nao foi possivel cadastrar o produto.')),
        finalize(() => {
          this.submittingProduct = false;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  submitInvoice(): void {
    if (this.invoiceForm.invalid) {
      this.invoiceForm.markAllAsTouched();
      return;
    }

    const items = this.invoiceItems.controls
      .map((control) => {
        const rawValue = control.getRawValue() as { productId: string; quantity: number };
        const product = this.products.find((current) => current.id === rawValue.productId);

        if (!product) {
          return null;
        }

        return {
          productId: product.id,
          productCode: product.code,
          productDescription: product.description,
          quantity: Number(rawValue.quantity),
        };
      })
      .filter((item): item is NonNullable<typeof item> => item !== null);

    if (items.length !== this.invoiceItems.length) {
      this.setFeedback('error', 'Produto invalido', 'Selecione apenas produtos existentes para montar a nota.');
      return;
    }

    this.submittingInvoice = true;

    this.apiService
      .createInvoice({ items })
      .pipe(
        switchMap(() => this.apiService.getDashboard()),
        tap((dashboard) => {
          this.applyDashboard(dashboard);
          this.resetInvoiceForm();
          this.setFeedback('success', 'Nota criada', 'A nota fiscal foi aberta e recebeu numeracao sequencial automaticamente.');
        }),
        catchError((error) => this.handleError(error, 'Nao foi possivel criar a nota fiscal.')),
        finalize(() => {
          this.submittingInvoice = false;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  saveFailureMode(): void {
    this.savingFailureMode = true;

    this.apiService
      .updateFailureMode(this.failureForm.getRawValue())
      .pipe(
        switchMap(() => this.apiService.getDashboard()),
        tap((dashboard) => {
          this.applyDashboard(dashboard);
          this.setFeedback(
            'info',
            dashboard.failureMode.enabled ? 'Falha simulada ativa' : 'Falha simulada desativada',
            dashboard.failureMode.enabled
              ? 'Agora qualquer tentativa de impressao vai receber erro tratado do servico de estoque.'
              : 'O servico de estoque voltou ao estado normal e a impressao pode ser tentada novamente.',
          );
        }),
        catchError((error) => this.handleError(error, 'Nao foi possivel atualizar o modo de falha.')),
        finalize(() => {
          this.savingFailureMode = false;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  printInvoice(invoice: Invoice): void {
    if (invoice.status !== 'Aberta') {
      return;
    }

    this.printingInvoiceId = invoice.id;

    this.apiService
      .printInvoice(invoice.id)
      .pipe(
        switchMap(() => this.apiService.getDashboard()),
        tap((dashboard) => {
          this.applyDashboard(dashboard);
          this.setFeedback(
            'success',
            'Nota impressa',
            'A nota foi fechada com sucesso e o saldo do estoque foi atualizado.',
          );
        }),
        catchError((error) => this.handleError(error, 'Nao foi possivel concluir a impressao da nota fiscal.')),
        finalize(() => {
          this.printingInvoiceId = null;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  formatInvoiceNumber(invoiceNumber: number): string {
    return invoiceNumber.toString().padStart(6, '0');
  }

  formatProductDescription(description: string): string {
    return description
      .trim()
      .replace(/\s+/g, ' ')
      .split(' ')
      .map((word) => {
        if (word.length === 0 || word === word.toUpperCase()) {
          return word;
        }

        return `${word.charAt(0).toUpperCase()}${word.slice(1).toLowerCase()}`;
      })
      .join(' ');
  }

  trackById(index: number, item: { id: string }): string {
    return item.id;
  }

  dismissFeedback(): void {
    this.clearFeedbackTimer();
    this.feedback = null;
  }

  private loadDashboard(): void {
    this.dashboardLoading = true;

    this.apiService
      .getDashboard()
      .pipe(
        tap((dashboard) => {
          this.applyDashboard(dashboard);
        }),
        catchError((error) => this.handleError(error, 'Nao foi possivel carregar os dados iniciais do sistema.')),
        finalize(() => {
          this.dashboardLoading = false;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  private applyDashboard(dashboard: DashboardData): void {
    this.products = dashboard.products;
    this.invoices = dashboard.invoices;
    this.failureMode = dashboard.failureMode;
    this.failureForm.patchValue(
      {
        enabled: dashboard.failureMode.enabled,
        message:
          dashboard.failureMode.message ??
          'Falha simulada manualmente no servico de estoque para demonstracao do tratamento entre microsservicos.',
      },
      { emitEvent: false },
    );
  }

  private createInvoiceItemGroup(): FormGroup {
    return this.formBuilder.nonNullable.group({
      productId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(1)]],
    });
  }

  private resetInvoiceForm(): void {
    while (this.invoiceItems.length > 1) {
      this.invoiceItems.removeAt(this.invoiceItems.length - 1);
    }

    const firstItem = this.invoiceItems.at(0);
    firstItem.reset({
      productId: '',
      quantity: 1,
    });
  }

  private setFeedback(tone: FeedbackTone, title: string, message: string): void {
    this.clearFeedbackTimer();

    this.feedback = {
      tone,
      title,
      message,
    };

    this.feedbackTimeoutId = setTimeout(() => {
      this.feedback = null;
      this.feedbackTimeoutId = null;
    }, 5000);
  }

  private handleError(error: unknown, fallbackMessage: string) {
    const message = this.extractErrorMessage(error) ?? fallbackMessage;
    this.setFeedback('error', 'Operacao interrompida', message);
    return EMPTY;
  }

  private extractErrorMessage(error: unknown): string | null {
    if (typeof error !== 'object' || error === null) {
      return null;
    }

    const candidate = error as {
      error?: { detail?: string; title?: string; errors?: Record<string, string[]> };
      message?: string;
    };

    if (candidate.error?.detail) {
      return candidate.error.detail;
    }

    if (candidate.error?.title) {
      return candidate.error.title;
    }

    if (candidate.error?.errors) {
      return Object.values(candidate.error.errors)
        .flat()
        .join(' ');
    }

    return candidate.message ?? null;
  }

  private clearFeedbackTimer(): void {
    if (this.feedbackTimeoutId !== null) {
      clearTimeout(this.feedbackTimeoutId);
      this.feedbackTimeoutId = null;
    }
  }
}
