import { Component, inject, signal, OnInit, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { SurveyService } from '../../../core/services/survey.service';
import { ToastService } from '../../../core/services/toast.service';
import { Survey } from '../../../shared/models';

@Component({
  selector: 'app-survey-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './survey-detail.component.html'
})
export class SurveyDetailComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly surveyService = inject(SurveyService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly id = input<string>('');
  readonly loading = signal(false);
  readonly loadingData = signal(false);
  readonly isEdit = signal(false);
  readonly survey = signal<Survey | null>(null);

  readonly form = this.fb.group({
    title: ['', Validators.required],
    description: [''],
    allowAnonymous: [false],
    startDate: [''],
    endDate: ['']
  });

  isDraft(): boolean {
    // New survey is always editable; existing survey only if Inactive
    return !this.isEdit() || this.survey()?.state === 'Inactive';
  }

  ngOnInit(): void {
    const idVal = this.id();
    if (idVal) {
      this.isEdit.set(true);
      this.loadSurvey(Number(idVal));
    }
  }

  private loadSurvey(id: number): void {
    this.loadingData.set(true);
    this.surveyService.getById(id).subscribe({
      next: (s) => {
        this.survey.set(s);
        this.form.patchValue({
          title: s.title,
          description: s.description,
          allowAnonymous: s.allowAnonymous,
          startDate: s.startDate ? this.toLocalDatetime(s.startDate) : '',
          endDate: s.endDate ? this.toLocalDatetime(s.endDate) : ''
        });
        // Disable form controls for non-Inactive surveys
        if (s.state !== 'Inactive') {
          this.form.disable();
        }
        this.loadingData.set(false);
      },
      error: () => {
        this.loadingData.set(false);
        this.router.navigate(['/surveys']);
      }
    });
  }

  private toLocalDatetime(iso: string): string {
    // UX-03 FIX: d.toISOString() always returns UTC, so a UTC date displayed in a
    // datetime-local input appeared shifted for users outside UTC.
    // Fix: subtract the timezone offset so the value shown matches the user's local time.
    const d = new Date(iso);
    const offsetMs = d.getTimezoneOffset() * 60_000;
    return new Date(d.getTime() - offsetMs).toISOString().slice(0, 16);
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onSubmit(): void {
    // Guard: never submit for non-Draft surveys
    if (this.isEdit() && !this.isDraft()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { title, description, allowAnonymous, startDate, endDate } = this.form.value;
    const payload = {
      title: title!,
      description: description ?? '',
      allowAnonymous: !!allowAnonymous,
      startDate: startDate || undefined,
      endDate: endDate || undefined
    };

    this.loading.set(true);
    const req$ = this.isEdit()
      ? this.surveyService.update(Number(this.id()), payload)
      : this.surveyService.create(payload);

    req$.subscribe({
      next: (s) => {
        this.toast.success(this.isEdit() ? 'Survey updated.' : 'Survey created!');
        if (!this.isEdit()) {
          this.router.navigate(['/surveys', s.id, 'builder']);
        } else {
          this.router.navigate(['/surveys']);
        }
      },
      error: () => this.loading.set(false),
      complete: () => this.loading.set(false)
    });
  }
}
