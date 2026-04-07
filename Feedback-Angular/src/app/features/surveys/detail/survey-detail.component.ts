import { Component, inject, signal, OnInit, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { SurveyService } from '../../../core/services/survey.service';
import { InviteService } from '../../../core/services/invite.service';
import { ToastService } from '../../../core/services/toast.service';
import { Survey, SurveyInviteItem } from '../../../shared/models';

@Component({
  selector: 'app-survey-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink],
  templateUrl: './survey-detail.component.html'
})
export class SurveyDetailComponent implements OnInit {
  private readonly fb            = inject(FormBuilder);
  private readonly surveyService = inject(SurveyService);
  private readonly inviteService = inject(InviteService);
  private readonly toast         = inject(ToastService);
  private readonly router        = inject(Router);

  readonly id          = input<string>('');
  readonly loading     = signal(false);
  readonly loadingData = signal(false);
  readonly isEdit      = signal(false);
  readonly survey      = signal<Survey | null>(null);

  // ── Stepper (create only) ─────────────────────────────────────────────────
  readonly step = signal<1 | 2 | 3>(1);

  // Step 2 state (access control — local, applied on Step 3 create)
  inviteOnly  = false;
  inviteEmails = '';          // raw textarea value for step 2 preview

  // Step 3 / edit invite management
  savedInviteEmails  = '';
  inviteEmailError   = '';
  sendingInvites     = false;
  inviteSent         = false;
  togglingInviteOnly = false;
  readonly inviteList = signal<SurveyInviteItem[]>([]);

  get parsedEmails(): string[] {
    return this.savedInviteEmails
      .split(/[\n,;]/)
      .map(e => e.trim().toLowerCase())
      .filter(Boolean);
  }

  get invalidEmails(): string[] {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return this.parsedEmails.filter(e => !re.test(e));
  }

  // created survey id (after step 3 submit)
  createdSurveyId = 0;
  get surveyId(): number { return this.createdSurveyId || Number(this.id()); }

  // Step 2 email preview list
  get emailPreviewList(): string[] {
    return this.inviteEmails.split('\n').map(e => e.trim()).filter(Boolean);
  }

  // ── Form ──────────────────────────────────────────────────────────────────
  readonly form = this.fb.group({
    title:          ['', Validators.required],
    description:    [''],
    allowAnonymous: [false],
    startDate:      [''],
    endDate:        ['']
  });

  readonly scheduleLoading = signal(false);

  // ── Helpers ───────────────────────────────────────────────────────────────
  isDraft(): boolean { return !this.isEdit() || this.survey()?.state === 'Inactive'; }
  canEditSchedule(): boolean { return this.isEdit() && !this.isDraft(); }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  private toLocalDatetime(iso: string): string {
    const d = new Date(iso);
    return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
  }

  minDateTime(): string { return this.toLocalDatetime(new Date().toISOString()); }

  minEndDateTime(): string {
    const start = this.form.get('startDate')?.value;
    return start ? start : this.minDateTime();
  }

  setNow(field: 'startDate' | 'endDate'): void {
    this.form.get(field)?.setValue(
      this.toLocalDatetime(new Date(Date.now() + 60_000).toISOString())
    );
  }

  private toUtcIso(localDatetime: string): string {
    return new Date(localDatetime).toISOString();
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    const idVal = this.id();
    if (idVal) {
      this.isEdit.set(true);
      this.loadSurvey(Number(idVal));
      this.loadInvites(Number(idVal));
    }
  }

  private loadSurvey(id: number): void {
    this.loadingData.set(true);
    this.surveyService.getById(id).subscribe({
      next: (s) => {
        this.survey.set(s);
        this.form.patchValue({
          title:          s.title,
          description:    s.description,
          allowAnonymous: s.allowAnonymous,
          startDate:      s.startDate ? this.toLocalDatetime(s.startDate) : '',
          endDate:        s.endDate   ? this.toLocalDatetime(s.endDate)   : ''
        });
        if (s.state !== 'Inactive') {
          this.form.get('title')?.disable();
          this.form.get('description')?.disable();
          this.form.get('allowAnonymous')?.disable();
        }
        this.loadingData.set(false);
      },
      error: () => { this.loadingData.set(false); this.router.navigate(['/surveys']); }
    });
  }

  // ── Stepper navigation ────────────────────────────────────────────────────
  nextStep(): void {
    if (this.step() === 1) {
      if (this.form.invalid) { this.form.markAllAsTouched(); return; }
      const { startDate, endDate } = this.form.value;
      if (startDate && endDate && new Date(endDate) <= new Date(startDate)) {
        this.toast.error('End date must be after start date.'); return;
      }
      if (startDate && new Date(startDate) < new Date()) {
        this.toast.error('Start date cannot be in the past.'); return;
      }
    }
    this.step.update(s => (s + 1) as 1 | 2 | 3);
  }

  prevStep(): void { this.step.update(s => (s - 1) as 1 | 2 | 3); }

  // ── Create (step 3 submit) ────────────────────────────────────────────────
  createSurvey(): void {
    const { title, description, allowAnonymous, startDate, endDate } = this.form.value;
    const payload = {
      title:          title!,
      description:    description ?? '',
      allowAnonymous: !!allowAnonymous,
      startDate:      startDate ? this.toUtcIso(startDate) : undefined,
      endDate:        endDate   ? this.toUtcIso(endDate)   : undefined
    };

    this.loading.set(true);
    this.surveyService.create(payload).subscribe({
      next: async (s) => {
        this.createdSurveyId = s.id;
        this.survey.set(s);

        // Apply invite-only if toggled
        if (this.inviteOnly) {
          await this.inviteService.setInviteOnly(s.id, true).toPromise().catch(() => {});
        }

        // Send invites if any emails entered
        const emails = this.emailPreviewList;
        if (emails.length) {
          await this.inviteService.sendInvites(s.id, emails).toPromise().catch(() => {});
        }

        this.loading.set(false);
        this.toast.success('Survey created!');
        this.router.navigate(['/surveys', s.id, 'builder']);
      },
      error: () => this.loading.set(false)
    });
  }

  // ── Edit submit ───────────────────────────────────────────────────────────
  onSubmit(): void {
    if (!this.isDraft()) return;
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const { title, description, allowAnonymous, startDate, endDate } = this.form.value;
    if (startDate && endDate && new Date(endDate) <= new Date(startDate)) {
      this.toast.error('End date must be after start date.'); return;
    }
    if (startDate && new Date(startDate) < new Date()) {
      this.toast.error('Start date cannot be in the past.'); return;
    }

    this.loading.set(true);
    this.surveyService.update(Number(this.id()), {
      title: title!, description: description ?? '',
      allowAnonymous: !!allowAnonymous,
      startDate: startDate ? this.toUtcIso(startDate) : undefined,
      endDate:   endDate   ? this.toUtcIso(endDate)   : undefined
    }).subscribe({
      next: () => { this.toast.success('Survey updated.'); this.router.navigate(['/surveys']); },
      error: () => this.loading.set(false),
      complete: () => this.loading.set(false)
    });
  }

  onUpdateSchedule(): void {
    const { startDate, endDate } = this.form.value;
    if (startDate && endDate && new Date(endDate) <= new Date(startDate)) {
      this.toast.error('End date must be after start date.'); return;
    }
    if (startDate && new Date(startDate) < new Date()) {
      this.toast.error('Start date cannot be in the past.'); return;
    }
    this.scheduleLoading.set(true);
    this.surveyService.updateSchedule(
      Number(this.id()),
      startDate ? this.toUtcIso(startDate) : undefined,
      endDate   ? this.toUtcIso(endDate)   : undefined
    ).subscribe({
      next: (s) => { this.survey.set(s); this.toast.success('Schedule updated.'); this.scheduleLoading.set(false); },
      error: () => this.scheduleLoading.set(false)
    });
  }

  // ── Invite management (edit mode) ─────────────────────────────────────────
  loadInvites(id: number): void {
    this.inviteService.getInvites(id).subscribe({
      next: list => this.inviteList.set(list),
      error: () => {}
    });
  }

  toggleInviteOnly(): void {
    const current = this.survey()?.isInviteOnly ?? false;
    this.togglingInviteOnly = true;
    this.inviteService.setInviteOnly(this.surveyId, !current).subscribe({
      next: () => {
        this.survey.update(s => s ? { ...s, isInviteOnly: !current } : s);
        this.togglingInviteOnly = false;
        this.toast.success(!current ? 'Survey set to invite-only.' : 'Invite-only disabled.');
      },
      error: () => { this.togglingInviteOnly = false; }
    });
  }

  sendInvites(): void {
    const emails = this.parsedEmails;
    if (!emails.length) { this.inviteEmailError = 'Please enter at least one email address.'; return; }
    if (emails.length > 100) { this.inviteEmailError = 'Cannot send more than 100 invites at once.'; return; }
    if (this.invalidEmails.length) { this.inviteEmailError = `Invalid emails: ${this.invalidEmails.join(', ')}`; return; }
    this.inviteEmailError = '';
    this.sendingInvites = true;
    this.inviteSent = false;
    this.inviteService.sendInvites(this.surveyId, emails).subscribe({
      next: () => {
        this.inviteSent = true;
        this.sendingInvites = false;
        this.savedInviteEmails = '';
        this.loadInvites(this.surveyId);
        this.toast.success('Invites sent.');
      },
      error: (e) => { this.inviteEmailError = e.error?.message ?? 'Failed to send invites.'; this.sendingInvites = false; }
    });
  }
}
