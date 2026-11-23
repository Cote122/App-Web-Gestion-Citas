import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AdminUser, RoleDefinition, UserRole } from '../../../models/models';
import { ListPagerComponent } from '../../../shared/list-pager/list-pager.component';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule, ListPagerComponent],
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
  page = 1;
  readonly pageSize = 8;
  searchText = '';
  get filteredUsers(): AdminUser[] {
    const query = this.searchText.trim().toLocaleLowerCase('es-PE');
    return this.users.filter(x => !query || [x.fullName, x.dni, x.email, x.role].some(value => value.toLocaleLowerCase('es-PE').includes(query)));
  }
  get visibleUsers(): AdminUser[] { return this.filteredUsers.slice((this.page - 1) * this.pageSize, this.page * this.pageSize); }
  filter(text: string): void { this.searchText = text; this.page = 1; }

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
          this.page = Math.min(this.page, Math.max(1, Math.ceil(this.filteredUsers.length / this.pageSize)));
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
