import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink, Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly showPwd = signal(false);
  readonly returnUrl = signal<string | null>(null);

  constructor() {
    this.returnUrl.set(this.route.snapshot.queryParamMap.get('returnUrl'));
  }

  readonly form = this.fb.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [
      Validators.required, 
      Validators.email,
      Validators.pattern(/^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/)  // ← ADD THIS LINE
    ]],
    password: ['', [Validators.required, Validators.minLength(6)]]  // ← CHANGE 8 to 6 (backend requirement)
  });

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onSubmit(): void {
    if (this.form.invalid) { 
      this.form.markAllAsTouched(); 
      this.toast.error('Please fix validation errors');  // ← ADD THIS
      return; 
    }
    this.loading.set(true);
    const { username, email, password } = this.form.value;
    this.auth.register({ username: username!, email: email!, password: password! }).subscribe({
      next: () => {
        this.toast.success('Account created! Welcome to SurveyFlow.');
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/dashboard';
        this.router.navigateByUrl(returnUrl);
      },
      error: (err) => {
        if (err.status === 409) { this.toast.error('Username or email is already taken.'); }
        else { this.toast.error(err.error?.message || 'Registration failed'); }  // ← ADD THIS
        this.loading.set(false);
      },
      complete: () => this.loading.set(false)
    });
  }
}