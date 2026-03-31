import { Injectable, signal } from '@angular/core';

export interface ConfirmDialogData {
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  readonly visible = signal(false);
  readonly data = signal<ConfirmDialogData>({
    message: '',
    title: 'Confirm',
    confirmLabel: 'Delete',
    cancelLabel: 'Cancel',
    danger: true
  });

  private resolvePromise: ((confirmed: boolean) => void) | null = null;

  confirm(data: ConfirmDialogData): Promise<boolean> {
    this.data.set({
      title: data.title ?? 'Confirm',
      message: data.message,
      confirmLabel: data.confirmLabel ?? 'Delete',
      cancelLabel: data.cancelLabel ?? 'Cancel',
      danger: data.danger ?? true
    });
    this.visible.set(true);
    return new Promise(resolve => { this.resolvePromise = resolve; });
  }

  accept(): void {
    this.visible.set(false);
    this.resolvePromise?.(true);
    this.resolvePromise = null;
  }

  decline(): void {
    this.visible.set(false);
    this.resolvePromise?.(false);
    this.resolvePromise = null;
  }
}
