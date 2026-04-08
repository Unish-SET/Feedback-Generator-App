import {
  ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, AbstractControl } from '@angular/forms';
import { Subject, merge, EMPTY } from 'rxjs';
import {
  catchError, debounceTime, distinctUntilChanged,
  map, startWith, switchMap, takeUntil, tap
} from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';

import { SurveyService } from '../../../core/services/survey.service';
import { ToastService }   from '../../../core/services/toast.service';
import { AuthService }    from '../../../core/services/auth.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SurveyListItem, SurveyState } from '../../../shared/models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-survey-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, EmptyStateComponent],
  templateUrl: './survey-list.component.html'
})
export class SurveyListComponent implements OnInit, OnDestroy {
  private readonly surveyService = inject(SurveyService);
  private readonly toast         = inject(ToastService);
  private readonly route         = inject(ActivatedRoute);
  private readonly fb            = inject(FormBuilder);
  private readonly confirmDialog = inject(ConfirmDialogService);
  readonly auth                  = inject(AuthService);

  readonly today = new Date().toISOString().split('T')[0];

  // ─── Filter form ─────────────────────────────────────────────────────────
  // Single source of truth for all filter state; reactive pipeline reads this.
  readonly filterForm = this.fb.nonNullable.group({
    search:   [''],
    status:   [''],
    sortBy:   ['createdAt'],
    sortDir:  ['desc'],
    dateFrom: [''],
    dateTo:   ['']
  }, { validators: (g: AbstractControl) => this.dateRangeValidator(g) });

  private dateRangeValidator(group: AbstractControl) {
    const from = group.get('dateFrom')?.value;
    const to   = group.get('dateTo')?.value;
    if (from && to && to < from) {
      group.get('dateTo')?.setErrors({ dateOrder: true });
      return { dateOrder: true };
    }
    if (group.get('dateTo')?.hasError('dateOrder')) {
      group.get('dateTo')?.setErrors(null);
    }
    return null;
  }

  // Expose form value as a signal so template expressions stay reactive under OnPush.
  readonly formSnap = toSignal(
    this.filterForm.valueChanges.pipe(startWith(this.filterForm.value)),
    { requireSync: true }
  );

  // ─── Pagination ───────────────────────────────────────────────────────────
  // Page is kept separate from the filter form so changePage() doesn't
  // trigger debounce and doesn't reset to 1.
  private readonly pageSubject$ = new Subject<number>();
  private _page = 1;
  get currentPage(): number { return this._page; }
  readonly PAGE_SIZE = 6;

  // ─── Data signals ─────────────────────────────────────────────────────────
  readonly surveys     = signal<SurveyListItem[]>([]);
  readonly totalCount  = signal(0);
  readonly totalPages  = signal(1);
  readonly loading     = signal(true);
  readonly initialLoad = signal(true);   // skeleton only on first paint

  // ─── Action signals ───────────────────────────────────────────────────────
  readonly deleteTarget  = signal<SurveyListItem | null>(null);
  readonly shareTarget   = signal<SurveyListItem | null>(null);
  readonly copied        = signal(false);
  readonly actionLoading = signal(false);

  // Signal-based in-progress tracking — compatible with OnPush
  readonly actionsInFlight = signal(new Set<number>());
  isActionInProgress(id: number): boolean { return this.actionsInFlight().has(id); }

  private readonly destroy$ = new Subject<void>();

  readonly tabs = [
    { label: 'All',      value: '' },
    { label: 'Active',   value: 'Active' },
    { label: 'Inactive', value: 'Inactive' },
    { label: 'Closed',   value: 'Closed' }
  ];

  // ─── Lifecycle ────────────────────────────────────────────────────────────

  ngOnInit(): void {
    const status = this.route.snapshot.queryParamMap.get('status') ?? '';
    if (status) this.filterForm.patchValue({ status }, { emitEvent: false });

    this.setupPipeline();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ─── Reactive pipeline ────────────────────────────────────────────────────
  // Root cause of flickering:
  //   1. No debounce — every keystroke in search fired a new HTTP request.
  //   2. No switchMap — multiple requests raced and stale responses could overwrite fresh ones.
  //   3. loading.set(true) on every filter showed the skeleton, blanking the grid.
  //
  // Fix:
  //   • search has 300 ms debounce + distinctUntilChanged — collapses burst typing into 1 call.
  //   • switchMap cancels any in-flight HTTP request before issuing the next one — no race.
  //   • skeleton only shows on the very first load; subsequent loads dim the existing grid.

  private setupPipeline(): void {
    // 1. Search: debounce keystrokes so we never fire per-character
    const search$ = this.filterForm.get('search')!.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      map(() => 1)          // filter change → reset to page 1
    );

    // 2. Everything else: immediate, still reset to page 1
    const otherFilters$ = merge(
      this.filterForm.get('status')!.valueChanges,
      this.filterForm.get('sortBy')!.valueChanges,
      this.filterForm.get('sortDir')!.valueChanges,
      this.filterForm.get('dateFrom')!.valueChanges,
      this.filterForm.get('dateTo')!.valueChanges
    ).pipe(map(() => 1));

    // 3. Explicit page navigation: keeps current filters, just changes the page
    const page$ = this.pageSubject$;

    // 4. Merge all triggers → snapshot current form + page → single API call chain
    merge(search$, otherFilters$, page$).pipe(
      startWith(1),                          // fire immediately on component init
      tap(page   => (this._page = page)),    // keep _page in sync for template
      map(page   => this.buildParams(page)),
      switchMap(params => {                  // switchMap cancels previous in-flight request
        this.loading.set(true);
        return this.surveyService.getAll(params).pipe(
          catchError(() => {
            this.loading.set(false);
            return EMPTY;
          })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(result => {
      this.surveys.set(result.items);
      this.totalCount.set(result.totalCount);
      this.totalPages.set(result.totalPages ?? (Math.ceil(result.totalCount / this.PAGE_SIZE) || 1));
      this.loading.set(false);
      this.initialLoad.set(false);
    });
  }

  private buildParams(page: number) {
    const f = this.filterForm.value;
    return {
      pageNumber:    page,
      pageSize:      this.PAGE_SIZE,
      status:        f.status    || undefined,
      search:        f.search?.trim() || undefined,
      sortBy:        f.sortBy    || 'createdAt',
      sortDir:       f.sortDir   || 'desc',
      startDateFrom: f.dateFrom  || undefined,
      startDateTo:   f.dateTo    || undefined
    };
  }

  // ─── Filter helpers ───────────────────────────────────────────────────────

  setTab(value: string): void {
    // patchValue emits on the status control → triggers otherFilters$ → page resets to 1
    this.filterForm.patchValue({ status: value });
  }

  toggleSortDir(): void {
    const dir = this.filterForm.value.sortDir === 'asc' ? 'desc' : 'asc';
    this.filterForm.patchValue({ sortDir: dir });
  }

  clearDates(): void {
    this.filterForm.patchValue({ dateFrom: '', dateTo: '' });
  }

  onDateFromChange(): void {
    const { dateFrom, dateTo } = this.filterForm.value;
    if (dateTo && dateFrom && dateTo < dateFrom) {
      this.filterForm.patchValue({ dateTo: dateFrom });
    }
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.pageSubject$.next(page);
  }

  // ─── Badge / label helpers ────────────────────────────────────────────────

  uiState(s: SurveyListItem): 'inactive' | 'live' | 'closed' {
    if (s.state === 'Active') return 'live';
    if (s.state === 'Closed') return 'closed';
    return 'inactive';
  }

  uiLabel(s: SurveyListItem): string {
    return ({ Inactive: 'Editing', Active: 'Live', Closed: 'Closed' } as Record<string, string>)[s.state] ?? s.state;
  }

  badgeStyle(s: SurveyListItem): string {
    const styles: Record<string, string> = {
      Active:   'background:#eef2ff;color:#4f46e5;border:1px solid #c7d2fe',
      Inactive: 'background:#f1f5f9;color:#64748b;border:1px solid #e2e8f0',
      Closed:   'background:#fef2f2;color:#dc2626;border:1px solid #fecaca'
    };
    return styles[s.state] ?? styles['Inactive'];
  }

  dotStyle(s: SurveyListItem): string {
    const dots: Record<string, string> = {
      Active:   'background:#6366f1',
      Inactive: 'background:#94a3b8',
      Closed:   'background:#ef4444'
    };
    return dots[s.state] ?? dots['Inactive'];
  }

  // ─── Action helpers ───────────────────────────────────────────────────────

  private startAction(id: number): void {
    this.actionsInFlight.update(s => new Set([...s, id]));
  }

  private stopAction(id: number): void {
    this.actionsInFlight.update(s => { const n = new Set(s); n.delete(id); return n; });
  }

  async toggleLive(s: SurveyListItem): Promise<void> {
    const next     = s.state === 'Active' ? 'Inactive' : 'Active';
    const goingLive = next === 'Active';
    const confirmed = await this.confirmDialog.confirm({
      title:        goingLive ? 'Publish Survey?' : 'Pause Survey?',
      message:      goingLive
        ? `"${s.title}" will be live and accepting responses.`
        : `"${s.title}" will be paused — no new responses.`,
      confirmLabel: goingLive ? 'Publish' : 'Pause',
      danger:       false
    });
    if (!confirmed) return;

    const msg = goingLive ? `"${s.title}" is now Live!` : `"${s.title}" paused.`;
    this.startAction(s.id);
    this.surveyService.setState(s.id, next as SurveyState).subscribe({
      next: updated => {
        this.surveys.update(list =>
          list.map(item => item.id === s.id ? { ...item, state: updated.state as SurveyState } : item)
        );
        this.toast.success(msg);
        this.stopAction(s.id);
      },
      error: () => this.stopAction(s.id)
    });
  }

  async reopen(s: SurveyListItem): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title:        'Reopen Survey?',
      message:      `"${s.title}" will move back to Editing (Inactive).`,
      confirmLabel: 'Reopen',
      danger:       false
    });
    if (!confirmed) return;

    this.startAction(s.id);
    this.surveyService.setState(s.id, 'Inactive').subscribe({
      next: updated => {
        this.surveys.update(list =>
          list.map(item => item.id === s.id ? { ...item, state: updated.state as SurveyState } : item)
        );
        this.toast.success(`"${s.title}" reopened — back to editing.`);
        this.stopAction(s.id);
      },
      error: () => this.stopAction(s.id)
    });
  }

  async close(s: SurveyListItem): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title:        'Close Survey?',
      message:      `"${s.title}" will stop accepting responses permanently.`,
      confirmLabel: 'Close Survey',
      danger:       true
    });
    if (!confirmed) return;

    this.startAction(s.id);
    this.surveyService.setState(s.id, 'Closed').subscribe({
      next: updated => {
        this.surveys.update(list =>
          list.map(item => item.id === s.id ? { ...item, state: updated.state as SurveyState } : item)
        );
        this.toast.success(`"${s.title}" closed.`);
        this.stopAction(s.id);
      },
      error: () => this.stopAction(s.id)
    });
  }

  confirmDelete(s: SurveyListItem): void { this.deleteTarget.set(s); }

  doDelete(): void {
    const t = this.deleteTarget();
    if (!t) return;
    this.actionLoading.set(true);
    this.surveyService.delete(t.id).subscribe({
      next: () => {
        // Optimistic removal — no full reload avoids an extra API round-trip
        this.surveys.update(list => list.filter(s => s.id !== t.id));
        this.totalCount.update(c => Math.max(0, c - 1));
        this.toast.success('Survey deleted.');
        this.deleteTarget.set(null);
        this.actionLoading.set(false);
      },
      error: () => this.actionLoading.set(false)
    });
  }

  openShare(s: SurveyListItem): void { this.shareTarget.set(s); this.copied.set(false); }

  shareUrl(): string {
    return `${window.location.origin}/survey/${this.shareTarget()?.publicToken}/respond`;
  }

  copyShareLink(): void {
    const url = this.shareUrl();
    navigator.clipboard?.writeText(url).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    }).catch(() => {
      const el = document.createElement('textarea');
      el.value = url; el.style.position = 'fixed'; el.style.opacity = '0';
      document.body.appendChild(el); el.focus(); el.select();
      document.execCommand('copy'); document.body.removeChild(el);
      this.copied.set(true); setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
