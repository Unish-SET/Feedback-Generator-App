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

  readonly scheduleLoading = signal(false);

  isDraft(): boolean {
    return !this.isEdit() || this.survey()?.state === 'Inactive';
  }

  canEditSchedule(): boolean {
    // Dates are editable on any state — only title/desc/anonymous require Inactive
    return this.isEdit() && !this.isDraft();
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
        // Only lock title/description/allowAnonymous for non-Inactive surveys.
        // startDate and endDate remain editable via the schedule endpoint.
        if (s.state !== 'Inactive') {
          this.form.get('title')?.disable();
          this.form.get('description')?.disable();
          this.form.get('allowAnonymous')?.disable();
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
    const d = new Date(iso);
    const offsetMs = d.getTimezoneOffset() * 60_000;
    return new Date(d.getTime() - offsetMs).toISOString().slice(0, 16);
  }

  // Returns current datetime in local timezone as "YYYY-MM-DDTHH:mm" for the min attribute
  minDateTime(): string {
    return this.toLocalDatetime(new Date().toISOString());
  }

  // End date min = start date if set, otherwise now
  minEndDateTime(): string {
    const start = this.form.get('startDate')?.value;
    return start ? start : this.minDateTime();
  }

  // Sets the given field to now + 1 minute (avoids "cannot be in the past" edge case)
  setNow(field: 'startDate' | 'endDate'): void {
    const nowPlus1 = new Date(Date.now() + 60_000);
    this.form.get(field)?.setValue(this.toLocalDatetime(nowPlus1.toISOString()));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  // Converts a datetime-local string (local time, no TZ) to a proper UTC ISO string
  private toUtcIso(localDatetime: string): string {
    // datetime-local gives "YYYY-MM-DDTHH:mm" in local time — new Date() parses it as local
    return new Date(localDatetime).toISOString();
  }

  onSubmit(): void {
    if (this.isEdit() && !this.isDraft()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { title, description, allowAnonymous, startDate, endDate } = this.form.value;

    if (startDate && endDate && new Date(endDate) <= new Date(startDate)) {
      this.toast.error('End date must be after start date.');
      return;
    }

    if (startDate && new Date(startDate) < new Date()) {
      this.toast.error('Start date cannot be in the past.');
      return;
    }

    const payload = {
      title: title!,
      description: description ?? '',
      allowAnonymous: !!allowAnonymous,
      startDate: startDate ? this.toUtcIso(startDate) : undefined,
      endDate:   endDate   ? this.toUtcIso(endDate)   : undefined
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

  onUpdateSchedule(): void {
    const { startDate, endDate } = this.form.value;

    if (startDate && endDate && new Date(endDate) <= new Date(startDate)) {
      this.toast.error('End date must be after start date.');
      return;
    }

    if (startDate && new Date(startDate) < new Date()) {
      this.toast.error('Start date cannot be in the past.');
      return;
    }

    this.scheduleLoading.set(true);
    this.surveyService.updateSchedule(
      Number(this.id()),
      startDate ? this.toUtcIso(startDate) : undefined,
      endDate   ? this.toUtcIso(endDate)   : undefined
    ).subscribe({
      next: (s) => {
        this.survey.set(s);
        this.toast.success('Survey schedule updated.');
        this.scheduleLoading.set(false);
      },
      error: () => this.scheduleLoading.set(false)
    });
  }
}
