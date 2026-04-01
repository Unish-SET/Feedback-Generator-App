import {
  ChangeDetectionStrategy, Component, inject, signal,
  OnInit, OnDestroy, ViewChild, ElementRef, input
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { Subject, EMPTY } from 'rxjs';
import { catchError, switchMap, startWith, takeUntil } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';
import { Chart, registerables } from 'chart.js';

import { AnalyticsService } from '../../core/services/analytics.service';
import { SurveyService } from '../../core/services/survey.service';
import { SurveyAnalytics, Survey } from '../../shared/models';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

Chart.register(...registerables);

@Component({
  selector: 'app-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, EmptyStateComponent],
  templateUrl: './analytics.component.html'
})
export class AnalyticsComponent implements OnInit, OnDestroy {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly surveyService    = inject(SurveyService);
  private readonly router           = inject(Router);
  private readonly fb               = inject(FormBuilder);

  readonly id = input<string>('');

  // ─── Filter form ─────────────────────────────────────────────────────────
  readonly filterForm = this.fb.nonNullable.group({
    dateFrom: [''],
    dateTo:   ['']
  });

  readonly formSnap = toSignal(
    this.filterForm.valueChanges,
    { initialValue: this.filterForm.value }
  );

  // ─── Data signals ─────────────────────────────────────────────────────────
  readonly loading   = signal(true);
  readonly exporting = signal(false);
  readonly analytics = signal<SurveyAnalytics | null>(null);
  readonly survey    = signal<Survey | null>(null);

  @ViewChild('lineChart') lineChartRef!: ElementRef<HTMLCanvasElement>;

  private charts: Chart[] = [];
  private readonly reload$ = new Subject<void>();
  private readonly destroy$ = new Subject<void>();

  readonly mathRound = Math.round;

  avgRating(): number | null {
    const rqs = this.analytics()?.questions.filter(
      q => q.averageRating !== null && q.averageRating !== undefined
    ) ?? [];
    if (rqs.length === 0) return null;
    return rqs.reduce((s, q) => s + q.averageRating!, 0) / rqs.length;
  }

  ngOnInit(): void {
    this.surveyService.getById(Number(this.id())).subscribe({
      next: s => this.survey.set(s),
      error: () => this.router.navigate(['/surveys'])
    });

    // ─── Reactive pipeline ────────────────────────────────────────────────
    // Key fix: destroyCharts() is called AFTER data arrives (inside next:),
    // not at the start of loading. Old charts stay visible during the fetch,
    // eliminating the blank flash between filter change and new chart render.
    this.reload$.pipe(
      startWith(undefined),
      switchMap(() => {
        this.loading.set(true);
        const f = this.filterForm.value;
        return this.analyticsService.get(Number(this.id()), {
          fromDate: f.dateFrom || undefined,
          toDate:   f.dateTo   || undefined
        }).pipe(
          catchError(() => { this.loading.set(false); return EMPTY; })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(data => {
      // Destroy old charts only now — new data is ready, no blank period
      this.destroyCharts();
      this.analytics.set(data);
      this.loading.set(false);
      setTimeout(() => this.buildCharts(), 100);
    });

    // Filter changes trigger a reload
    this.filterForm.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.reload$.next();
    });
  }

  ngOnDestroy(): void {
    this.destroyCharts();
    this.destroy$.next();
    this.destroy$.complete();
  }

  clearFilters(): void {
    this.filterForm.reset({ dateFrom: '', dateTo: '' });
  }

  // ─── Chart management ─────────────────────────────────────────────────────

  private destroyCharts(): void {
    this.charts.forEach(c => c.destroy());
    this.charts = [];
  }

  private buildCharts(): void {
    const a = this.analytics();
    if (!a) return;

    // ── Line chart (response timeline) ──────────────────────────────────
    if (this.lineChartRef && (a.dateWiseCounts ?? []).length > 0) {
      const ctx = this.lineChartRef.nativeElement.getContext('2d');
      if (ctx) {
        const c = new Chart(ctx, {
          type: 'line',
          data: {
            labels: a.dateWiseCounts.map(d =>
              new Date(d.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
            ),
            datasets: [{
              label: 'Responses',
              data: a.dateWiseCounts.map(d => d.count),
              borderColor: '#6366f1',
              backgroundColor: 'rgba(99,102,241,0.08)',
              borderWidth: 2,
              pointRadius: 4,
              fill: true,
              tension: 0.4
            }]
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
              x: { grid: { display: false }, ticks: { font: { size: 11 }, color: '#94a3b8' } },
              y: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 }, color: '#94a3b8' } }
            }
          }
        });
        this.charts.push(c);
      }
    }

    // ── Doughnut charts per question ─────────────────────────────────────
    const pieCanvases = document.querySelectorAll('canvas.pie-chart');
    pieCanvases.forEach((canvas) => {
      const qi = Number((canvas as HTMLElement).getAttribute('data-qi'));
      const qa = a.questions[qi];
      if (!qa?.optionDistributions?.length) return;
      const ctx = (canvas as HTMLCanvasElement).getContext('2d');
      if (!ctx) return;

      const colors = ['#6366f1','#22c55e','#f59e0b','#ef4444','#8b5cf6','#06b6d4','#f97316','#ec4899'];
      const c = new Chart(ctx, {
        type: 'doughnut',
        data: {
          labels: qa.optionDistributions.map(o => o.optionText),
          datasets: [{
            data: qa.optionDistributions.map(o => o.count),
            backgroundColor: colors.slice(0, qa.optionDistributions.length),
            borderWidth: 2,
            borderColor: '#ffffff'
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { position: 'bottom', labels: { font: { size: 11 }, padding: 10 } }
          }
        }
      });
      this.charts.push(c);
    });
  }

  exportExcel(): void {
    this.exporting.set(true);
    this.analyticsService.exportExcel(Number(this.id())).subscribe({
      next: (blob) => { this.downloadBlob(blob, 'survey-responses.xlsx'); this.exporting.set(false); },
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
