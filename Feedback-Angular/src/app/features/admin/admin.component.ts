import {
  ChangeDetectionStrategy, Component, DestroyRef,
  inject, OnInit, signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';
import { UserService }        from '../../core/services/user.service';
import { AdminSurveyService } from '../../core/services/admin-survey.service';
import { AuditService }       from '../../core/services/audit.service';
import { ToastService }       from '../../core/services/toast.service';
import { AuthService }        from '../../core/services/auth.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import {
  AppUser, SurveyListItem, AuditLog, AuditMeta,
  AdminSurveyListItem, AdminSurveyDetail, AdminStats
} from '../../shared/models';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

interface EnrichedUser extends AppUser {
  calculatedSurveyCount: number;
}

type AdminTab = 'users' | 'surveys' | 'audit';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, EmptyStateComponent],
  templateUrl: './admin.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminComponent implements OnInit {
  private readonly userService        = inject(UserService);
  private readonly adminSurveyService = inject(AdminSurveyService);
  private readonly auditService       = inject(AuditService);
  private readonly toast              = inject(ToastService);
  private readonly confirmDialog      = inject(ConfirmDialogService);
  private readonly destroyRef         = inject(DestroyRef);
  readonly auth = inject(AuthService);

  readonly activeTab = signal<AdminTab>('users');

  readonly today = new Date().toISOString().split('T')[0];

  readonly loading          = signal(true);
  readonly usersFiltering   = signal(false);
  readonly enrichedUsers    = signal<EnrichedUser[]>([]);
  readonly actionUserId     = signal<number | null>(null);
  readonly statusUserId     = signal<number | null>(null);
  readonly userPage         = signal(1);
  readonly userTotal        = signal(0);
  readonly userTotalPages   = signal(1);
  readonly userPageSize     = 20;
  userSearch     = '';
  userRoleFilter = '';
  userFromDate   = '';
  userToDate     = '';
  private readonly userSearchSubject = new Subject<string>();

  readonly userDetailLoading             = signal(false);
  readonly selectedUser                  = signal<AppUser | null>(null);
  readonly selectedUserSurveys           = signal<SurveyListItem[]>([]);
  readonly selectedUserSurveysLoading    = signal(false);
  readonly selectedUserSurveysPage       = signal(1);
  readonly selectedUserSurveysTotal      = signal(0);
  readonly selectedUserSurveysTotalPages = signal(1);

  readonly surveysLoading   = signal(false);
  readonly surveysFiltering = signal(false);
  readonly adminSurveys     = signal<AdminSurveyListItem[]>([]);
  readonly adminStats       = signal<AdminStats>({ totalSurveys: 0, activeSurveys: 0, deletedSurveys: 0, totalResponses: 0 });
  readonly surveyDetail     = signal<AdminSurveyDetail | null>(null);
  readonly surveyActionId   = signal<number | null>(null);
  readonly surveyTotal      = signal(0);
  readonly surveyTotalPages = signal(1);
  readonly surveyPage       = signal(1);
  readonly surveyPageSize   = 20;
  surveySearch        = '';
  surveyDeletedFilter = 'false';
  surveyFromDate      = '';
  surveyToDate        = '';
  private readonly surveySearchSubject = new Subject<string>();

  readonly auditLoading    = signal(false);
  readonly auditFiltering  = signal(false);
  readonly auditLogs       = signal<AuditLog[]>([]);
  readonly auditTotal      = signal(0);
  readonly auditTotalPages = signal(1);
  readonly auditPage       = signal(1);
  readonly auditPageSize   = 20;
  readonly auditMeta       = signal<AuditMeta>({ actions: [], entities: [] });
  readonly changesLog      = signal<AuditLog | null>(null);
  auditSearch       = '';
  auditActionFilter = '';
  auditEntityFilter = '';
  auditFromDate     = '';
  auditToDate       = '';
  private readonly searchSubject = new Subject<string>();

  ngOnInit(): void {
    this.loadUsers();
    this.loadAdminStats();

    this.userSearchSubject.pipe(
      debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => { this.userPage.set(1); this.loadUsers(); });

    this.searchSubject.pipe(
      debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => { this.auditPage.set(1); this.loadAuditLogs(); });

    this.surveySearchSubject.pipe(
      debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => { this.surveyPage.set(1); this.loadAdminSurveys(); });
  }

  switchToSurveys(): void {
    this.activeTab.set('surveys');
    if (this.adminSurveys().length === 0) this.loadAdminSurveys();
  }

  switchToAudit(): void {
    this.activeTab.set('audit');
    if (this.auditLogs().length === 0) {
      this.loadAuditLogs();
      this.auditService.getMeta().subscribe(m => this.auditMeta.set(m));
    }
  }

  loadUsers(isFilter = false): void {
    if (isFilter) this.usersFiltering.set(true);
    else this.loading.set(true);
    this.userService.getAll({
      search:     this.userSearch     || undefined,
      role:       this.userRoleFilter || undefined,
      fromDate:   this.userFromDate   || undefined,
      toDate:     this.userToDate     || undefined,
      pageNumber: this.userPage(),
      pageSize:   this.userPageSize
    }).subscribe({
      next: (result) => {
        this.enrichedUsers.set(result.items.map(u => ({ ...u, calculatedSurveyCount: u.surveyCount })));
        this.userTotal.set(result.totalCount);
        this.userTotalPages.set(result.totalPages || 1);
        this.loading.set(false);
        this.usersFiltering.set(false);
      },
      error: () => { this.loading.set(false); this.usersFiltering.set(false); }
    });
  }

  onUserSearchChange(): void { this.userSearchSubject.next(this.userSearch); }

  onUserFromDateChange(): void {
    if (this.userToDate && this.userToDate < this.userFromDate) {
      this.userToDate = this.userFromDate;
    }
    this.loadUsers();
  }

  userPageChange(page: number): void {
    if (page < 1 || page > this.userTotalPages()) return;
    this.userPage.set(page);
    this.loadUsers(true);
  }

  countRole(role: string): number {
    return this.enrichedUsers().filter(u => u.role === role).length;
  }

  async toggleStatus(user: EnrichedUser): Promise<void> {
    const activating = !user.isActive;
    const confirmed  = await this.confirmDialog.confirm({
      title:        activating ? 'Activate User?' : 'Deactivate User?',
      message:      activating
        ? `${user.username} will be able to log in and use the platform.`
        : `${user.username} will be blocked from logging in.`,
      confirmLabel: activating ? 'Activate' : 'Deactivate',
      danger:       !activating
    });
    if (!confirmed) return;

    this.statusUserId.set(user.id);
    this.userService.setStatus(user.id, activating).subscribe({
      next: (updated) => {
        this.enrichedUsers.update(list => list.map(u => u.id === user.id ? { ...u, isActive: updated.isActive } : u));
        this.toast.success(`${user.username} ${updated.isActive ? 'activated' : 'deactivated'}.`);
        this.statusUserId.set(null);
      },
      error: () => this.statusUserId.set(null)
    });
  }

  async toggleRole(user: EnrichedUser): Promise<void> {
    const newRole    = user.role === 'Admin' ? 'Creator' : 'Admin';
    const isPromoting = newRole === 'Admin';
    const confirmed  = await this.confirmDialog.confirm({
      title:        isPromoting ? 'Promote to Admin?' : 'Demote to Creator?',
      message:      isPromoting
        ? `${user.username} will gain full admin access.`
        : `${user.username} will lose admin privileges.`,
      confirmLabel: isPromoting ? 'Promote' : 'Demote',
      danger:       !isPromoting
    });
    if (!confirmed) return;

    this.actionUserId.set(user.id);
    this.userService.updateRole(user.id, { role: newRole }).subscribe({
      next: (updated) => {
        this.enrichedUsers.update(list => list.map(u => u.id === user.id ? { ...u, role: updated.role } : u));
        this.toast.success(`${user.username} is now a ${newRole}.`);
        this.actionUserId.set(null);
      },
      error: () => this.actionUserId.set(null)
    });
  }

  confirmDelete(user: EnrichedUser): void {
    this.confirmDialog.confirm({
      title: 'Delete User?',
      message: `Permanently delete ${user.username} (${user.email})? This cannot be undone.`,
      confirmLabel: 'Delete User',
      danger: true
    }).then(confirmed => {
      if (!confirmed) return;
      this.userService.delete(user.id).subscribe({
        next: () => {
          this.enrichedUsers.update(list => list.filter(u => u.id !== user.id));
          this.userTotal.update(t => t - 1);
          this.toast.success(`${user.username} has been deleted.`);
        }
      });
    });
  }

  openUserDetail(user: EnrichedUser): void {
    this.userDetailLoading.set(true);
    this.selectedUser.set(null);
    this.selectedUserSurveys.set([]);
    this.selectedUserSurveysPage.set(1);
    this.userService.getById(user.id).subscribe({
      next: (detail) => {
        this.selectedUser.set(detail);
        this.userDetailLoading.set(false);
        this.loadUserSurveys(user.id, 1);
      },
      error: () => {
        this.userDetailLoading.set(false);
        this.toast.error('Could not load user details.');
      }
    });
  }

  closeUserDetail(): void {
    this.selectedUser.set(null);
    this.userDetailLoading.set(false);
    this.selectedUserSurveys.set([]);
  }

  private loadUserSurveys(userId: number, page: number): void {
    this.selectedUserSurveysLoading.set(true);
    this.userService.getSurveysByUser(userId, page, 10).subscribe({
      next: (result) => {
        this.selectedUserSurveys.set(result.items);
        this.selectedUserSurveysTotal.set(result.totalCount);
        this.selectedUserSurveysTotalPages.set(result.totalPages || 1);
        this.selectedUserSurveysLoading.set(false);
      },
      error: () => this.selectedUserSurveysLoading.set(false)
    });
  }

  userSurveysPageChange(page: number): void {
    const user = this.selectedUser();
    if (!user || page < 1 || page > this.selectedUserSurveysTotalPages()) return;
    this.selectedUserSurveysPage.set(page);
    this.loadUserSurveys(user.id, page);
  }

  private loadAdminStats(): void {
    this.adminSurveyService.getStats().subscribe(s => this.adminStats.set(s));
  }

  loadAdminSurveys(isFilter = false): void {
    if (isFilter) this.surveysFiltering.set(true);
    else this.surveysLoading.set(true);
    const isDeleted = this.surveyDeletedFilter === '' ? undefined : this.surveyDeletedFilter === 'true';
    this.adminSurveyService.getAll({
      search:     this.surveySearch   || undefined,
      isDeleted,
      fromDate:   this.surveyFromDate || undefined,
      toDate:     this.surveyToDate   || undefined,
      pageNumber: this.surveyPage(),
      pageSize:   this.surveyPageSize
    }).subscribe({
      next: (result) => {
        this.adminSurveys.set(result.items);
        this.surveyTotal.set(result.totalCount);
        this.surveyTotalPages.set(result.totalPages || 1);
        this.surveysLoading.set(false);
        this.surveysFiltering.set(false);
      },
      error: () => { this.surveysLoading.set(false); this.surveysFiltering.set(false); }
    });
  }

  onSurveySearchChange(): void { this.surveySearchSubject.next(this.surveySearch); }

  onSurveyFromDateChange(): void {
    if (this.surveyToDate && this.surveyToDate < this.surveyFromDate) {
      this.surveyToDate = this.surveyFromDate;
    }
    this.loadAdminSurveys();
  }

  surveyPageChange(page: number): void {
    if (page < 1 || page > this.surveyTotalPages()) return;
    this.surveyPage.set(page);
    this.loadAdminSurveys(true);
  }

  viewSurveyDetail(id: number): void {
    this.adminSurveyService.getDetail(id).subscribe(d => this.surveyDetail.set(d));
  }

  async setSurveyState(id: number, state: 'Inactive' | 'Active' | 'Closed'): Promise<void> {
    const labels:   Record<string, string> = { Inactive: 'Pause', Active: 'Publish', Closed: 'Close' };
    const messages: Record<string, string> = {
      Inactive: 'This survey will be paused.',
      Active:   'This survey will go live and accept responses.',
      Closed:   'This survey will be permanently closed.'
    };
    const confirmed = await this.confirmDialog.confirm({
      title:        `${labels[state]} Survey?`,
      message:      messages[state],
      confirmLabel: labels[state],
      danger:       state === 'Closed'
    });
    if (!confirmed) return;

    this.surveyActionId.set(id);
    this.adminSurveyService.setState(id, state).subscribe({
      next: () => {
        this.adminSurveys.update(list => list.map(s => s.id === id ? { ...s, status: state } : s));
        this.toast.success(`Survey state updated to ${state}.`);
        this.surveyActionId.set(null);
      },
      error: () => this.surveyActionId.set(null)
    });
  }

  softDeleteSurvey(id: number): void {
    this.surveyActionId.set(id);
    this.adminSurveyService.softDelete(id).subscribe({
      next: () => {
        this.adminSurveys.update(list => list.map(s => s.id === id ? { ...s, isDeleted: true } : s));
        this.toast.success('Survey deleted.');
        this.surveyActionId.set(null);
      },
      error: () => this.surveyActionId.set(null)
    });
  }

  restoreSurvey(id: number, ev?: Event): void {
    ev?.stopPropagation();
    this.surveyActionId.set(id);
    this.adminSurveyService.restore(id).subscribe({
      next: () => {
        this.adminSurveys.update(list => list.map(s => s.id === id ? { ...s, isDeleted: false } : s));
        this.toast.success('Survey restored.');
        this.surveyActionId.set(null);
        this.loadAdminStats();
      },
      error: () => this.surveyActionId.set(null)
    });
  }

  surveyBadgeClass(status: string): string {
    const map: Record<string, string> = {
      'Active':   'badge-active',
      'Inactive': 'badge-draft',
      'Draft':    'badge-draft',
      'Closed':   'badge-closed'
    };
    return map[status] ?? 'badge-draft';
  }

  loadAuditLogs(isFilter = false): void {
    if (isFilter) this.auditFiltering.set(true);
    else this.auditLoading.set(true);
    this.auditService.getLogs({
      page:     this.auditPage(),
      pageSize: this.auditPageSize,
      search:   this.auditSearch        || undefined,
      action:   this.auditActionFilter  || undefined,
      entity:   this.auditEntityFilter  || undefined,
      fromDate: this.auditFromDate      || undefined,
      toDate:   this.auditToDate        || undefined
    }).subscribe({
      next: (result) => {
        this.auditLogs.set(result.items);
        this.auditTotal.set(result.totalCount);
        this.auditTotalPages.set(result.totalPages || 1);
        this.auditLoading.set(false);
        this.auditFiltering.set(false);
      },
      error: () => { this.auditLoading.set(false); this.auditFiltering.set(false); }
    });
  }

  onSearchChange(): void { this.searchSubject.next(this.auditSearch); }

  onAuditFromDateChange(): void {
    if (this.auditToDate && this.auditToDate < this.auditFromDate) {
      this.auditToDate = this.auditFromDate;
    }
    this.loadAuditLogs();
  }

  auditPageChange(page: number): void {
    if (page < 1 || page > this.auditTotalPages()) return;
    this.auditPage.set(page);
    this.loadAuditLogs(true);
  }

  viewChanges(log: AuditLog): void { this.changesLog.set(log); }

  formatChanges(raw?: string | null): string {
    if (!raw) return '';
    try { return JSON.stringify(JSON.parse(raw), null, 2); }
    catch { return raw; }
  }

  actionBadgeClass(action: string): string {
    const map: Record<string, string> = {
      'Create':    'badge-active',
      'Update':    'badge-draft',
      'Delete':    'bg-red-100 text-red-700',
      'Publish':   'bg-green-100 text-green-700',
      'Unpublish': 'bg-yellow-100 text-yellow-700',
      'Close':     'bg-surface-100 text-surface-600',
      'Login':     'bg-blue-100 text-blue-700',
      'Export':    'bg-purple-100 text-purple-700',
    };
    return map[action] ?? 'badge-draft';
  }

  min(a: number, b: number): number { return Math.min(a, b); }
}
