import { NgClass } from '@angular/common';
import { AfterViewInit,ChangeDetectionStrategy,ChangeDetectorRef,Component, Inject,inject, OnDestroy,OnInit,ViewChild,ViewEncapsulation} from '@angular/core';
import { FormBuilder,FormGroup,ReactiveFormsModule,Validators,} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {MAT_DIALOG_DATA, MatDialog,MatDialogModule, MatDialogRef,} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HttpClient } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from 'app/core/auth/auth.service';
import { environment } from 'environments/environment';
import { RoleVM, UserDialogData, UserVM } from './user-management.model';

// ── Tipi ──────────────────────────────────────────────────────────────────────



// ── Dialog di creazione / modifica utente ─────────────────────────────────────

@Component({
    selector: 'user-edit-dialog',
    standalone: true,
    encapsulation: ViewEncapsulation.None,
    imports: [
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatSlideToggleModule,
        MatIconModule,
        MatTooltipModule,
        MatProgressSpinnerModule,
        TranslocoModule,
    ],
    template: `
        <ng-container *transloco="let t">
        <div class="flex max-h-[90vh] flex-col">

            <!-- Header -->
            <div class="flex flex-shrink-0 items-center justify-between border-b px-6 py-4">
                <div class="flex items-center gap-3">
                    <div
                        class="flex h-10 w-10 items-center justify-center rounded-full"
                        [class]="data.isNew
                            ? 'bg-primary-100 dark:bg-primary-900'
                            : 'bg-amber-100 dark:bg-amber-900'"
                    >
                        <mat-icon
                            class="text-primary"
                            [svgIcon]="data.isNew
                                ? 'heroicons_outline:user-plus'
                                : 'heroicons_outline:pencil-square'"
                        ></mat-icon>
                    </div>
                    <div>
                        <p class="text-lg font-semibold leading-tight">
                            {{ data.isNew ? t('userManagement.dialog.titleNew') : t('userManagement.dialog.titleEdit') }}
                        </p>
                        @if (!data.isNew && data.user) {
                            <p class="text-hint text-xs">{{ data.user.name }} {{ data.user.surname }}</p>
                        }
                    </div>
                </div>
                <button mat-icon-button [mat-dialog-close]="null">
                    <mat-icon [svgIcon]="'heroicons_outline:x-mark'"></mat-icon>
                </button>
            </div>

            <!-- Form -->
            <div class="flex-1 overflow-y-auto px-6 py-5">
                <form [formGroup]="form" class="grid grid-cols-2 gap-x-4 gap-y-1">

                    <mat-form-field class="col-span-1">
                        <mat-label>{{ t('userManagement.dialog.fieldName') }}</mat-label>
                        <input matInput formControlName="name" [placeholder]="t('userManagement.dialog.placeholderName')" autocomplete="off">
                        <mat-error>{{ t('userManagement.dialog.errorRequired') }}</mat-error>
                    </mat-form-field>

                    <mat-form-field class="col-span-1">
                        <mat-label>{{ t('userManagement.dialog.fieldSurname') }}</mat-label>
                        <input matInput formControlName="surname" [placeholder]="t('userManagement.dialog.placeholderSurname')" autocomplete="off">
                        <mat-error>{{ t('userManagement.dialog.errorRequired') }}</mat-error>
                    </mat-form-field>

                    <mat-form-field class="col-span-1">
                        <mat-label>{{ t('userManagement.dialog.fieldLogin') }}</mat-label>
                        <input matInput formControlName="login" [placeholder]="t('userManagement.dialog.placeholderLogin')" autocomplete="off">
                        <mat-icon matSuffix [svgIcon]="'heroicons_outline:at-symbol'"></mat-icon>
                        <mat-error>{{ t('userManagement.dialog.errorRequired') }}</mat-error>
                    </mat-form-field>

                    <mat-form-field class="col-span-1">
                        <mat-label>{{ t('userManagement.dialog.fieldEmail') }}</mat-label>
                        <input matInput formControlName="email" type="email" [placeholder]="t('userManagement.dialog.placeholderEmail')">
                        <mat-icon matSuffix [svgIcon]="'heroicons_outline:envelope'"></mat-icon>
                        <mat-error>{{ t('userManagement.dialog.errorEmail') }}</mat-error>
                    </mat-form-field>

                    <mat-form-field class="col-span-1">
                        <mat-label>{{ t('userManagement.dialog.fieldPhone') }}</mat-label>
                        <input matInput formControlName="mobilePhone" [placeholder]="t('userManagement.dialog.placeholderPhone')">
                        <mat-icon matSuffix [svgIcon]="'heroicons_outline:phone'"></mat-icon>
                    </mat-form-field>

                    <mat-form-field class="col-span-1">
                        <mat-label>{{ t('userManagement.dialog.fieldLanguage') }}</mat-label>
                        <mat-select formControlName="languageCode">
                            <mat-option value="it">🇮🇹 Italiano</mat-option>
                            <mat-option value="en">🇬🇧 English</mat-option>
                            <mat-option value="de">🇩🇪 Deutsch</mat-option>
                            <mat-option value="fr">🇫🇷 Français</mat-option>
                        </mat-select>
                    </mat-form-field>

                    <mat-form-field class="col-span-2">
                        <mat-label>{{ t('userManagement.dialog.fieldRole') }}</mat-label>
                        <mat-select formControlName="roleId">
                            @for (role of data.roles; track role.id) {
                                <mat-option [value]="role.id">
                                    {{ role.description || role.code }}
                                </mat-option>
                            }
                        </mat-select>
                        <mat-error>{{ t('userManagement.dialog.errorRole') }}</mat-error>
                    </mat-form-field>

                    <!-- Password: obbligatoria su creazione, opzionale su modifica -->
                    <mat-form-field class="col-span-2">
                        <mat-label>{{ data.isNew ? t('userManagement.dialog.fieldPassword') : t('userManagement.dialog.fieldNewPassword') }}</mat-label>
                        <input
                            matInput
                            formControlName="password"
                            [type]="showPassword ? 'text' : 'password'"
                            [placeholder]="data.isNew ? t('userManagement.dialog.placeholderPasswordNew') : t('userManagement.dialog.placeholderPasswordEdit')"
                            autocomplete="new-password"
                        >
                        <button
                            mat-icon-button
                            matSuffix
                            type="button"
                            (click)="showPassword = !showPassword"
                            [matTooltip]="showPassword ? t('userManagement.dialog.hidePassword') : t('userManagement.dialog.showPassword')"
                        >
                            <mat-icon [svgIcon]="showPassword
                                ? 'heroicons_outline:eye-slash'
                                : 'heroicons_outline:eye'">
                            </mat-icon>
                        </button>
                        @if (data.isNew) {
                            <mat-hint>{{ t('userManagement.dialog.hintPasswordNew') }}</mat-hint>
                        } @else {
                            <mat-hint>{{ t('userManagement.dialog.hintPasswordEdit') }}</mat-hint>
                        }
                        <mat-error>{{ t('userManagement.dialog.errorPassword') }}</mat-error>
                    </mat-form-field>

                    <!-- Toggle account attivo -->
                    <div class="col-span-2 mt-1 flex items-center gap-4 rounded-xl border bg-gray-50 p-4 dark:bg-gray-800">
                        <mat-slide-toggle formControlName="enabled" color="primary"></mat-slide-toggle>
                        <div>
                            <p class="text-sm font-medium">{{ t('userManagement.dialog.accountActive') }}</p>
                            <p class="text-hint text-xs">{{ t('userManagement.dialog.accountActiveHint') }}</p>
                        </div>
                    </div>

                </form>
            </div>

            <!-- Azioni -->
            <div class="flex flex-shrink-0 items-center justify-end gap-3 border-t px-6 py-4">
                <button mat-button [mat-dialog-close]="null">{{ t('userManagement.dialog.cancel') }}</button>
                <button
                    mat-flat-button
                    color="primary"
                    [disabled]="form.invalid || isSaving"
                    (click)="save()"
                >
                    @if (isSaving) {
                        <mat-spinner diameter="18" class="mr-2"></mat-spinner>
                    }
                    {{ data.isNew ? t('userManagement.dialog.createUser') : t('userManagement.dialog.saveChanges') }}
                </button>
            </div>
        </div>
        </ng-container>
    `,
})
export class UserEditDialogComponent {
    form: FormGroup;
    isSaving = false;
    // Default true: la password è visibile in chiaro appena si apre il dialog
    showPassword = true;

    constructor(
        private _fb: FormBuilder,
        public dialogRef: MatDialogRef<UserEditDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: UserDialogData,
    ) {
        const u = data.user;
        this.form = this._fb.group({
            id:           [u?.id ?? ''],
            name:         [u?.name ?? '',     Validators.required],
            surname:      [u?.surname ?? '',  Validators.required],
            login:        [u?.login ?? '',    Validators.required],
            email:        [u?.email ?? '',    Validators.email],
            mobilePhone:  [u?.mobilePhone ?? ''],
            languageCode: [u?.languageCode ?? 'it'],
            roleId:       [u?.roleId ?? '',   Validators.required],
            enabled:      [u?.enabled ?? true],
            // Creazione: obbligatoria (required + minLength)
            // Modifica:  opzionale — se valorizzata deve essere >= 6 caratteri
            password: data.isNew
                ? ['', [Validators.required, Validators.minLength(6)]]
                : ['', Validators.minLength(6)],
        });
    }

    save(): void {
        if (this.form.invalid) return;
        this.dialogRef.close(this.form.getRawValue());
    }
}

// ── Componente principale ─────────────────────────────────────────────────────

@Component({
    selector: 'user-management',
    templateUrl: './user-management.component.html',
    styleUrls: ['./user-management.component.scss'],
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [
        NgClass,
        MatTableModule,
        MatSortModule,
        MatPaginatorModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatSlideToggleModule,
        MatTooltipModule,
        MatMenuModule,
        MatDividerModule,
        MatProgressSpinnerModule,
        MatSnackBarModule,
        TranslocoModule,
    ],
})
export class UserManagementComponent implements OnInit, AfterViewInit, OnDestroy {
    @ViewChild(MatSort)      sort!: MatSort;
    @ViewChild(MatPaginator) paginator!: MatPaginator;

    // Colonne visibili nella tabella
    displayedColumns = [
        'avatar', 'name', 'email', 'mobilePhone', 'role', 'tenant', 'status', 'actions',
    ];

    dataSource   = new MatTableDataSource<UserVM>([]);
    roles: RoleVM[] = [];
    isLoading    = false;
    searchValue  = '';
    roleFilter   = '';

    // Lista originale (non filtrata per ruolo)
    private _allUsers: UserVM[] = [];

    private readonly _apiUrl       = environment.apiUrl;
    private readonly _http         = inject(HttpClient);
    private readonly _authService  = inject(AuthService);
    private readonly _dialog       = inject(MatDialog);
    private readonly _snack        = inject(MatSnackBar);
    private readonly _cdr          = inject(ChangeDetectorRef);
    private readonly _destroy$     = new Subject<void>();

    // ── Lifecycle ──────────────────────────────────────────────────────────

    ngOnInit(): void {
        this._loadRoles();
        this._loadUsers();
    }

    ngAfterViewInit(): void {
        this.dataSource.sort      = this.sort;
        this.dataSource.paginator = this.paginator;
        this.dataSource.filterPredicate = (user, filter) =>
            user.name.toLowerCase().includes(filter)    ||
            user.surname.toLowerCase().includes(filter) ||
            (user.email  ?? '').toLowerCase().includes(filter) ||
            (user.login  ?? '').toLowerCase().includes(filter);
    }

    ngOnDestroy(): void {
        this._destroy$.next();
        this._destroy$.complete();
    }

    // ── Getter per le stats nel header ─────────────────────────────────────

    get totalUsers():    number { return this._allUsers.length; }
    get activeUsers():   number { return this._allUsers.filter(u => u.enabled).length; }
    get inactiveUsers(): number { return this._allUsers.filter(u => !u.enabled).length; }

    // ── Caricamento dati ───────────────────────────────────────────────────

    private _loadUsers(): void {
        this.isLoading = true;
        this._cdr.markForCheck();

        this._http
            .get<UserVM[]>(`${this._apiUrl}/admin/users`)
            .pipe(takeUntil(this._destroy$))
            .subscribe({
                next: users => {
                    this._allUsers       = users;
                    this.dataSource.data = users;
                    this.isLoading       = false;
                    this._applyRoleFilter();
                    this._cdr.markForCheck();
                },
                error: () => {
                    this.isLoading = false;
                    this._cdr.markForCheck();
                    this._snack.open('Errore nel caricamento degli utenti', 'Chiudi', { duration: 4000 });
                },
            });
    }

    private _loadRoles(): void {
        this._http
            .get<RoleVM[]>(`${this._apiUrl}/admin/roles`)
            .pipe(takeUntil(this._destroy$))
            .subscribe({
                next: roles => {
                    this.roles = roles;
                    this._cdr.markForCheck();
                },
                error: () => {
                    // Fallback finché l'endpoint non è pronto
                    this.roles = [
                        { id: '1', code: 'ADMIN',   description: 'Amministratore' },
                        { id: '2', code: 'MANAGER', description: 'Manager' },
                        { id: '3', code: 'USER',    description: 'Utente' },
                    ];
                    this._cdr.markForCheck();
                },
            });
    }

    // ── Filtri ─────────────────────────────────────────────────────────────

    onSearch(value: string): void {
        this.searchValue          = value.trim().toLowerCase();
        this.dataSource.filter    = this.searchValue;
        if (this.dataSource.paginator) {
            this.dataSource.paginator.firstPage();
        }
    }

    onRoleFilter(roleId: string): void {
        this.roleFilter = roleId;
        this._applyRoleFilter();
        if (this.dataSource.paginator) {
            this.dataSource.paginator.firstPage();
        }
    }

    private _applyRoleFilter(): void {
        this.dataSource.data = this.roleFilter
            ? this._allUsers.filter(u => u.roleId === this.roleFilter)
            : [...this._allUsers];
    }

    clearFilters(): void {
        this.searchValue          = '';
        this.roleFilter           = '';
        this.dataSource.filter    = '';
        this.dataSource.data      = [...this._allUsers];
        if (this.dataSource.paginator) {
            this.dataSource.paginator.firstPage();
        }
    }

    // ── Dialog creazione ───────────────────────────────────────────────────

    openCreate(): void {
        const ref = this._dialog.open(UserEditDialogComponent, {
            width:      '640px',
            maxWidth:   '95vw',
            disableClose: true,
            data: { user: null, roles: this.roles, isNew: true } satisfies UserDialogData,
        });

        ref.afterClosed()
            .pipe(takeUntil(this._destroy$))
            .subscribe((result: UserVM | null) => {
                if (!result) return;
                this._http
                    .post<UserVM>(`${this._apiUrl}/admin/users`, result)
                    .subscribe({
                        next: created => {
                            this._allUsers = [created, ...this._allUsers];
                            this._applyRoleFilter();
                            this._cdr.markForCheck();
                            this._snack.open('Utente creato con successo ✓', 'Chiudi', { duration: 3000 });
                        },
                        error: () => this._snack.open('Errore durante la creazione', 'Chiudi', { duration: 4000 }),
                    });
            });
    }

    // ── Dialog modifica ────────────────────────────────────────────────────

    openEdit(user: UserVM): void {
        const ref = this._dialog.open(UserEditDialogComponent, {
            width:      '640px',
            maxWidth:   '95vw',
            disableClose: true,
            data: { user, roles: this.roles, isNew: false } satisfies UserDialogData,
        });

        ref.afterClosed()
            .pipe(takeUntil(this._destroy$))
            .subscribe((result: UserVM | null) => {
                if (!result) return;
                this._http
                    .put<UserVM>(`${this._apiUrl}/admin/users/${result.id}`, result)
                    .subscribe({
                        next: updated => {
                            this._allUsers = this._allUsers.map(u =>
                                u.id === updated.id ? updated : u,
                            );
                            this._applyRoleFilter();
                            this._cdr.markForCheck();
                            this._snack.open('Utente aggiornato ✓', 'Chiudi', { duration: 3000 });
                        },
                        error: () => this._snack.open('Errore durante il salvataggio', 'Chiudi', { duration: 4000 }),
                    });
            });
    }

    // ── Abilita / disabilita ───────────────────────────────────────────────

    toggleEnabled(user: UserVM): void {
        const enabled = !user.enabled;
        this._http
            .patch<void>(`${this._apiUrl}/admin/users/${user.id}/toggle-enabled`, { enabled })
            .subscribe({
                next: () => {
                    this._allUsers = this._allUsers.map(u =>
                        u.id === user.id ? { ...u, enabled } : u,
                    );
                    this._applyRoleFilter();
                    this._cdr.markForCheck();
                    this._snack.open(
                        enabled ? 'Utente abilitato' : 'Utente disabilitato',
                        'Chiudi',
                        { duration: 2500 },
                    );
                },
                error: () => this._snack.open('Errore durante l\'aggiornamento', 'Chiudi', { duration: 4000 }),
            });
    }

    // ── Elimina ────────────────────────────────────────────────────────────

    deleteUser(user: UserVM): void {
        if (!confirm(`Eliminare l'utente ${user.name} ${user.surname}?\nQuesta azione non può essere annullata.`)) {
            return;
        }
        this._http
            .delete(`${this._apiUrl}/admin/users/${user.id}`)
            .subscribe({
                next: () => {
                    this._allUsers = this._allUsers.filter(u => u.id !== user.id);
                    this._applyRoleFilter();
                    this._cdr.markForCheck();
                    this._snack.open('Utente eliminato', 'Chiudi', { duration: 3000 });
                },
                error: () => this._snack.open('Errore durante l\'eliminazione', 'Chiudi', { duration: 4000 }),
            });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    getInitials(user: UserVM): string {
        return `${user.name?.charAt(0) ?? ''}${user.surname?.charAt(0) ?? ''}`.toUpperCase();
    }

    getRoleBadgeClass(code?: string): string {
        const map: Record<string, string> = {
            ADMIN:   'badge-red',
            MANAGER: 'badge-purple',
            USER:    'badge-blue',
        };
        return map[code ?? ''] ?? 'badge-gray';
    }

    trackByFn(index: number, item: UserVM): string {
        return item.id ?? String(index);
    }
}
