import {
  Component, inject, signal, OnInit,
  ViewChild, ElementRef, OnDestroy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SurveyService } from '../../core/services/survey.service';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AuthService } from '../../core/services/auth.service';
import { SurveyListItem } from '../../shared/models';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly surveyService   = inject(SurveyService);
  private readonly analyticsService = inject(AnalyticsService);
  readonly auth = inject(AuthService);

  @ViewChild('lineChart') lineChartRef!: ElementRef<HTMLCanvasElement>;
  private chart: Chart | null = null;
  private animFrame: number | null = null;

  readonly loading       = signal(true);
  readonly recentSurveys = signal<SurveyListItem[]>([]);
  readonly topSurvey     = signal<SurveyListItem | null>(null);
  readonly hasChartData  = signal(false);
  readonly displayValues = signal<string[]>(['0', '0', '0', '0', '0']);
  readonly chartSurveyTitle = signal('');

  private targetValues = [0, 0, 0, 0];
  private dateWiseCounts: { date: string; count: number }[] = [];

  greeting(): string {
    const h = new Date().getHours();
    if (h < 12) return 'morning';
    if (h < 17) return 'afternoon';
    return 'evening';
  }

  ngOnInit(): void  { this.loadData(); }
  ngOnDestroy(): void {
    this.chart?.destroy();
    if (this.animFrame) cancelAnimationFrame(this.animFrame);
  }

  private loadData(): void {
    this.surveyService.getAll({ pageNumber: 1, pageSize: 100 }).subscribe({
      next: (page) => {
        const surveys  = page.items;
        const active   = surveys.filter(s => s.state === 'Active').length;
        const inactive = surveys.filter(s => s.state === 'Inactive').length;
        const closed   = surveys.filter(s => s.state === 'Closed').length;
        const totalRes = surveys.reduce((sum, s) => sum + s.responseCount, 0);

        // [totalSurveys, active, inactive, closed, totalResponses]
        this.targetValues = [page.totalCount, active, inactive, closed, totalRes];
        this.animateCounters();

        const sorted = [...surveys].sort(
          (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.recentSurveys.set(sorted.slice(0, 6));

        // Top performing = most responses
        const top = [...surveys].sort((a, b) => b.responseCount - a.responseCount)[0] ?? null;
        this.topSurvey.set(top);

        const withResponses = surveys.filter(s => s.responseCount > 0);
        if (withResponses.length > 0) {
          const best = withResponses.sort((a, b) => b.responseCount - a.responseCount)[0];
          this.chartSurveyTitle.set(best.title);
          this.loadChartData(best.id);
        } else {
          this.loading.set(false);
        }
      },
      error: () => this.loading.set(false)
    });
  }

  private animateCounters(): void {
    const duration = 1000;
    const start = performance.now();
    const step = (now: number) => {
      const p = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - p, 3);
      this.displayValues.set(
        this.targetValues.map(t => Math.round(t * eased).toLocaleString())
      );
      if (p < 1) this.animFrame = requestAnimationFrame(step);
    };
    this.loading.set(false);
    this.animFrame = requestAnimationFrame(step);
  }

  private loadChartData(surveyId: number): void {
    this.analyticsService.get(surveyId, {}).subscribe({
      next: (a) => {
        this.dateWiseCounts = a.dateWiseCounts ?? [];
        // Show chart even with 1 data point
        this.hasChartData.set(this.dateWiseCounts.length > 0);
        if (this.hasChartData()) {
          setTimeout(() => this.buildChart(), 50);
        }
      },
      error: () => {}
    });
  }

  private buildChart(): void {
    if (!this.lineChartRef) return;
    const ctx = this.lineChartRef.nativeElement.getContext('2d');
    if (!ctx) return;
    this.chart?.destroy();

    this.chart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: this.dateWiseCounts.map(d =>
          new Date(d.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
        ),
        datasets: [{
          label: 'Responses',
          data: this.dateWiseCounts.map(d => d.count),
          backgroundColor: 'rgba(99,102,241,0.7)',
          borderColor: '#6366f1',
          borderWidth: 0,
          borderRadius: 6,
          borderSkipped: false
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: '#0f172a',
            titleColor: '#94a3b8',
            bodyColor: '#f1f5f9',
            padding: 12,
            cornerRadius: 8,
            displayColors: false,
            callbacks: {
              title: (items) => items[0].label,
              label: (item) => `${item.raw} response${Number(item.raw) !== 1 ? 's' : ''}`
            }
          }
        },
        scales: {
          x: { grid: { display: false }, border: { display: false }, ticks: { font: { size: 11 }, color: '#94a3b8' } },
          y: { beginAtZero: true, border: { display: false, dash: [4,4] }, grid: { color: 'rgba(0,0,0,0.04)' }, ticks: { font: { size: 11 }, color: '#94a3b8', precision: 0, stepSize: 1 } }
        }
      }
    });
  }

  statusColor(s: SurveyListItem): string {
    if (s.state === 'Active')   return '#6366f1';
    if (s.state === 'Closed')   return '#ef4444';
    return '#94a3b8'; // Inactive
  }

  statusLabel(s: SurveyListItem): string {
    if (s.state === 'Active')   return 'Live';
    if (s.state === 'Closed')   return 'Closed';
    return 'Editing';  // Inactive
  }

  /** Returns the right CTA link target per survey state */
  surveyRoute(s: SurveyListItem): string[] {
    if (s.state === 'Inactive') return ['/surveys', String(s.id), 'builder'];
    return ['/surveys', String(s.id), 'analytics'];
  }
}
