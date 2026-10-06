import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AdminUser, RoleDefinition, UserRole } from '../../../models/models';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-users.component.html',
  styleUrl: '../admin.css'
})
export class AdminUsersComponent implements OnInit {
  users: AdminUser[] = [];
  roles: RoleDefinition[] = [];
  pendingRoles: Record<string, UserRole> = {};
  loading = false;
  savingId = '';
  error = '';
  message = '';

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    forkJoin({ users: this.api.adminUsers(), roles: this.api.roleDefinitions() })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: ({ users, roles }) => {
          this.users = users;
          this.roles = roles;
          this.pendingRoles = Object.fromEntries(users.map(x => [x.id, x.role]));
        },
        error: () => this.error = 'No se pudieron cargar los usuarios y roles.'
      });
  }

  permissionsFor(role: UserRole): string[] {
    return this.roles.find(x => x.role === role)?.permissions ?? [];
  }

  saveRole(user: AdminUser): void {
    const role = this.pendingRoles[user.id];
    if (!role || role === user.role || this.savingId) return;

    this.savingId = user.id;
    this.error = '';
    this.message = '';
    this.api.updateUserRole(user.id, role)
      .pipe(finalize(() => this.savingId = ''))
      .subscribe({
        next: updated => {
          this.users = this.users.map(x => x.id === updated.id ? updated : x);
          this.pendingRoles[updated.id] = updated.role;
          this.message = `Rol actualizado para ${updated.fullName}: ${updated.role}.`;
        },
        error: error => {
          this.pendingRoles[user.id] = user.role;
          this.error = error?.error?.message ?? 'No se pudo actualizar el rol.';
        }
      });
  }
}
