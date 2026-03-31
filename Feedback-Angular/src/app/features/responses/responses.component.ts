import {
  ChangeDetectionStrategy, Component, inject, signal,
  OnInit, OnDestroy, input
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { Subject, merge, EMPTY } from 'rxjs';
import { catchError, map, startWith, switchMap, takeUntil, tap } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';

import { ResponseService } from '../../core/services/response.service';
import { SurveyService } from '../../core/services/survey.service';
import { AnalyticsService } from '../../core/services/analytics.service';
import { SurveyResponseRecord, Survey } from '../../shared/models';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-responses',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, EmptyStateComponent],
  templateUrl: './responses.component.html'
})
export class ResponsesComponent implements OnInit, OnDestroy {
  private readonly responseService  = inject(ResponseService);
  private readonly surveyService    = inject(SurveyService);
  private readonly analyticsService = inject(AnalyticsService);
  private readonly fb               = inject(FormBuilder);

  readonly id = input<string>('');

  // ─── Filter form ─────────────────────────────────────────────────────────
  readonly filterForm = this.fb.nonNullable.group({
    dateFrom: [''],
    dateTo:   [''],
    userId:   ['']
  });

  // Template reads filter values reactively under OnPush
  readonly formSnap = toSignal(
    this.filterForm.valueChanges,
    { initialValue: this.filterForm.value }
  );

  // ─── Pagination ───────────────────────────────────────────────────────────
  private readonly pageSubject$ = new Subject<number>();
  private _page = 1;
  get currentPage(): number { return this._page; }
  readonly PAGE_SIZE = 20;

  // ─── Data signals ─────────────────────────────────────────────────────────
  readonly responses   = signal<SurveyResponseRecord[]>([]);
  readonly survey      = signal<Survey | null>(null);
  readonly totalPages  = signal(1);
  readonly totalCount  = signal(0);
  readonly loading     = signal(true);
  readonly initialLoad = signal(true);   // skeleton only on first paint
  readonly exporting   = signal(false);
  readonly expandedId  = signal<number | null>(null);

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.surveyService.getById(Number(this.id())).subscribe({
      next: s => this.survey.set(s)
    });
    this.setupPipeline();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ─── Reactive pipeline ────────────────────────────────────────────────────
  // Filter changes reset to page 1; page nav keeps current filters.
  // switchMap cancels any in-flight request when a new trigger arrives.
  private setupPipeline(): void {
    // Filter change → always jump to page 1
    const filter$ = this.filterForm.valueChanges.pipe(map(() => 1));

    // Explicit page navigation
    const page$ = this.pageSubject$;

    merge(filter$, page$).pipe(
      startWith(1),
      tap(page => { this._page = page; }),
      switchMap(() => {
        this.loading.set(true);
        const f = this.filterForm.value;
        return this.responseService.getAll(Number(this.id()), {
          pageNumber:    this._page,
          pageSize:      this.PAGE_SIZE,
          submittedFrom: f.dateFrom || undefined,
          submittedTo:   f.dateTo   || undefined,
          userId:        f.userId   ? Number(f.userId) : undefined
        }).pipe(
          catchError(() => { this.loading.set(false); return EMPTY; })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(page => {
      this.responses.set(page.items);
      this.totalCount.set(page.totalCount);
      this.totalPages.set(page.totalPages || 1);
      this.loading.set(false);
      this.initialLoad.set(false);
    });
  }

  // ─── Helpers ──────────────────────────────────────────────────────────────

  toggleExpand(id: number): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  clearFilters(): void {
    this.filterForm.reset({ dateFrom: '', dateTo: '', userId: '' });
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.pageSubject$.next(page);
  }

  getOptionText(_response: SurveyResponseRecord, _questionId: number, optionId: number): string {
    return `#${optionId}`;
  }

  exportExcel(): void {
    this.exporting.set(true);
    this.analyticsService.exportExcel(Number(this.id())).subscribe({
      next: (blob) => { this.downloadBlob(blob, 'responses.xlsx'); this.exporting.set(false); },
      error: () => this.exporting.set(false)
    });
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename; a.click();
    URL.revokeObjectURL(url);
  }
}
