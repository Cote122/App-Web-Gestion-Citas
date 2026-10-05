import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  loading = false;
  error = '';

  form = this.fb.nonNullable.group({
    identifier: ['70000011', Validators.required],
    password: ['Demo#2026', Validators.required]
  });

  constructor(private readonly fb: FormBuilder, private readonly auth: AuthService, private readonly router: Router) {}

  submit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    this.error = '';
    const { identifier, password } = this.form.getRawValue();
    this.auth.login(identifier, password)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: () => this.router.navigate(['/app/home']),
        error: error => this.error = error?.error?.message ?? 'No se pudo iniciar sesión.'
      });
  }

  useDemo(identifier: string): void {
    this.form.setValue({ identifier, password: 'Demo#2026' });
  }
}
