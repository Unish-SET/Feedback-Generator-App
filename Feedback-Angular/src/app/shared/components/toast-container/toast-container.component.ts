import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../../core/services/toast.service';
import { Toast } from '../../models';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast-container.component.html'
})
export class ToastContainerComponent {
  readonly toastService = inject(ToastService);

  toastClass(toast: Toast): string {
    const map: Record<string, string> = {
      success: 'bg-emerald-50 text-emerald-800 border-emerald-200',
      error: 'bg-red-50 text-red-800 border-red-200',
      warning: 'bg-amber-50 text-amber-800 border-amber-200',
      info: 'bg-blue-50 text-blue-800 border-blue-200'
    };
    return map[toast.type] ?? map['info'];
  }

  toastIcon(toast: Toast): string {
    const map: Record<string, string> = {
      success: '✓', error: '✕', warning: '⚠', info: 'ℹ'
    };
    return map[toast.type] ?? 'ℹ';
  }
}
