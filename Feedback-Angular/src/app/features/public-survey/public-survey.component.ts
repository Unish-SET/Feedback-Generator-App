import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SurveyService } from '../../core/services/survey.service';
import { ResponseService } from '../../core/services/response.service';
import { AuthService } from '../../core/services/auth.service';
import { OtpService } from '../../core/services/otp.service';
import { PublicSurvey, PublicQuestion, SubmitResponseRequest } from '../../shared/models';

@Component({
  selector: 'app-public-survey',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './public-survey.component.html'
})
export class PublicSurveyComponent implements OnInit {
  private readonly route           = inject(ActivatedRoute);
  private readonly router          = inject(Router);
  private readonly surveyService   = inject(SurveyService);
  private readonly responseService = inject(ResponseService);
  private readonly authService     = inject(AuthService);
  private readonly otpService      = inject(OtpService);
  private readonly fb              = inject(FormBuilder);

  readonly loading          = signal(true);
  readonly submitting       = signal(false);
  readonly submitted        = signal(false);
  readonly alreadySubmitted = signal(false);
  readonly error            = signal(false);
  readonly errorMessage     = signal('');
  readonly survey           = signal<PublicSurvey | null>(null);
  readonly currentQuestion  = signal(0);
  readonly showErrors       = signal(false);

  // ── OTP gate ──────────────────────────────────────────────────────────────
  readonly otpStep       = signal<'email' | 'otp' | 'verified'>('email');
  readonly otpEmail      = signal('');
  readonly otpCode       = signal('');
  readonly otpSending    = signal(false);
  readonly otpVerifying  = signal(false);
  readonly otpError      = signal('');
  readonly otpEmailError = signal('');
  readonly otpCodeError  = signal('');
  readonly otpAttempts   = signal(0);
  readonly isInviteOnly  = signal(false);

  readonly responseForm: FormGroup = this.fb.group({});
  private checkboxAnswers = new Map<number, Set<number>>();
  private ratingAnswers   = new Map<number, number>();

  private get publicToken(): string {
    return this.route.snapshot.paramMap.get('publicToken')!;
  }

  private submissionKey(publicToken: string): string {
    const userId = this.authService.user()?.userId?.toString() ?? this.authService.getAnonId();
    return `survey_submitted_${userId}_${publicToken}`;
  }
  private markSubmitted(publicToken: string): void {
    localStorage.setItem(this.submissionKey(publicToken), '1');
  }
  private hasSubmitted(publicToken: string): boolean {
    return !!localStorage.getItem(this.submissionKey(publicToken));
  }

  ngOnInit(): void {
    this.loadSurvey();
  }

  private loadSurvey(): void {
    this.loading.set(true);
    this.surveyService.getPublic(this.publicToken).subscribe({
      next: (s) => {
        if (this.hasSubmitted(this.publicToken)) {
          this.alreadySubmitted.set(true);
          this.loading.set(false);
          return;
        }

        // Handle invite-only OTP gate
        if (s.isInviteOnly) {
          this.isInviteOnly.set(true);
          const existing = sessionStorage.getItem(`otp_session_${this.publicToken}`);
          if (existing) {
            this.otpStep.set('verified');
          }
          // Fall through — HTML will show OTP gate if not verified
        } else if (!s.allowAnonymous && !this.authService.isAuthenticated()) {
          this.router.navigate(['/auth/login'], {
            queryParams: { returnUrl: this.router.url }
          });
          return;
        }

        this.survey.set(s);
        this.buildForm(s);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        const msg: string = err.error?.message ?? '';
        this.error.set(true);
        if      (msg === 'SURVEY_PAUSED')       this.errorMessage.set('This survey is currently paused.');
        else if (msg === 'SURVEY_CLOSED')       this.errorMessage.set('This survey is closed.');
        else if (msg === 'SURVEY_NOT_STARTED')  this.errorMessage.set('This survey has not started yet.');
        else if (msg === 'SURVEY_EXPIRED')      this.errorMessage.set('This survey has expired.');
        else if (msg === 'SURVEY_NO_QUESTIONS') this.errorMessage.set('This survey has no questions yet.');
        else if (err.status === 404)            this.errorMessage.set('Survey not found.');
        else                                    this.errorMessage.set('Unable to load survey.');
      }
    });
  }

  private validateEmail(email: string): string {
    if (!email.trim()) return 'Email is required.';
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!re.test(email.trim())) return 'Enter a valid email address.';
    return '';
  }

  sendOtp(): void {
    const err = this.validateEmail(this.otpEmail());
    if (err) { this.otpEmailError.set(err); return; }
    this.otpEmailError.set('');
    this.otpError.set('');
    this.otpSending.set(true);
    this.otpService.sendOtp(this.otpEmail().trim(), this.publicToken).subscribe({
      next: () => { this.otpStep.set('otp'); this.otpSending.set(false); this.otpAttempts.set(0); },
      error: (e) => { this.otpError.set(e.error?.message ?? 'Failed to send OTP. Try again.'); this.otpSending.set(false); }
    });
  }

  verifyOtp(): void {
    const code = this.otpCode().trim();
    if (!code)                 { this.otpCodeError.set('Please enter the OTP code.'); return; }
    if (code.length !== 6)     { this.otpCodeError.set('OTP must be exactly 6 digits.'); return; }
    if (!/^\d{6}$/.test(code)) { this.otpCodeError.set('OTP must contain numbers only.'); return; }
    this.otpCodeError.set('');
    this.otpError.set('');
    this.otpVerifying.set(true);
    this.otpService.verifyOtp(this.otpEmail().trim(), this.publicToken, code).subscribe({
      next: (res) => {
        sessionStorage.setItem(`otp_session_${this.publicToken}`, res.sessionToken);
        this.otpStep.set('verified');
        this.otpVerifying.set(false);
      },
      error: (e) => {
        this.otpAttempts.update(n => n + 1);
        if (this.otpAttempts() >= 5) {
          this.otpError.set('Too many failed attempts. Please request a new OTP.');
          this.otpStep.set('email');
          this.otpCode.set('');
          this.otpAttempts.set(0);
        } else {
          this.otpError.set(e.error?.message ?? 'Invalid OTP.');
        }
        this.otpVerifying.set(false);
      }
    });
  }

  onOtpInput(event: Event): void {
    const input  = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').slice(0, 6);
    input.value  = digits;
    this.otpCode.set(digits);
    if (this.otpCodeError()) this.otpCodeError.set('');
  }

  private buildForm(s: PublicSurvey): void {
    s.questions.forEach(q => {
      if (q.type !== 'MultipleChoice' && q.type !== 'RatingScale') {
        this.responseForm.addControl(
          'q_' + q.id,
          this.fb.control('', q.isRequired ? Validators.required : null)
        );
      }
      if (q.type === 'MultipleChoice') this.checkboxAnswers.set(q.id, new Set());
    });
  }

  progressPercent(): number {
    const total = this.survey()?.questions.length ?? 1;
    return ((this.currentQuestion() + 1) / total) * 100;
  }

  isChecked(qId: number, optId: number): boolean {
    return this.checkboxAnswers.get(qId)?.has(optId) ?? false;
  }

  toggleCheckbox(qId: number, optId: number): void {
    const set = this.checkboxAnswers.get(qId) ?? new Set<number>();
    if (set.has(optId)) set.delete(optId); else set.add(optId);
    this.checkboxAnswers.set(qId, set);
    this.clearError();
  }

  setRating(qId: number, value: number): void { this.ratingAnswers.set(qId, value); this.clearError(); }

  getRatingClass(qId: number, value: number): string {
    const selected = this.ratingAnswers.get(qId) ?? 0;
    return selected >= value
      ? 'bg-brand-600 text-white border-brand-600'
      : 'bg-white text-surface-500 border-surface-200 hover:border-brand-400';
  }

  isAnswered(q: PublicQuestion): boolean {
    if (!q.isRequired) return true;
    if (q.type === 'MultipleChoice') return (this.checkboxAnswers.get(q.id)?.size ?? 0) > 0;
    if (q.type === 'RatingScale')    return this.ratingAnswers.has(q.id);
    return !!this.responseForm.get('q_' + q.id)?.value;
  }

  clearError(): void {
    if (this.showErrors()) this.showErrors.set(false);
  }

  nextQuestion(): void {
    const s = this.survey();
    if (!s) return;
    const q = s.questions[this.currentQuestion()];
    if (!this.isAnswered(q)) { this.showErrors.set(true); return; }
    this.showErrors.set(false);
    this.currentQuestion.update(n => n + 1);
  }

  prevQuestion(): void {
    this.showErrors.set(false);
    this.currentQuestion.update(n => n - 1);
  }

  submit(): void {
    const s = this.survey();
    if (!s) return;
    const invalid = s.questions.find(q => !this.isAnswered(q));
    if (invalid) { this.showErrors.set(true); return; }

    const answers = s.questions.map(q => {
      if (q.type === 'MultipleChoice')
        return { questionId: q.id, selectedOptionIds: [...(this.checkboxAnswers.get(q.id) ?? [])] };
      if (q.type === 'RatingScale')
        return { questionId: q.id, ratingValue: this.ratingAnswers.get(q.id) };
      if (q.type === 'SingleChoice') {
        const val = this.responseForm.get('q_' + q.id)?.value;
        return { questionId: q.id, selectedOptionId: val ? Number(val) : undefined };
      }
      return { questionId: q.id, textValue: this.responseForm.get('q_' + q.id)?.value };
    });

    const payload: SubmitResponseRequest = { answers };

    this.submitting.set(true);
    this.responseService.submit(this.publicToken, payload).subscribe({
      next: () => {
        this.markSubmitted(this.publicToken);
        this.submitted.set(true);
        this.submitting.set(false);
      },
      error: (err) => {
        if (err.status === 409) {
          this.markSubmitted(this.publicToken);
          this.alreadySubmitted.set(true);
        }
        this.submitting.set(false);
      }
    });
  }
}
