import {
    Component,
    OnInit,
    ViewEncapsulation,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';

import { DepartmentsService, DepartmentDto } from './departments.service';

@Component({
    selector: 'app-departments',
    templateUrl: './departments.component.html',
    encapsulation: ViewEncapsulation.None,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatProgressSpinnerModule,
        MatSlideToggleModule,
        MatSnackBarModule,
        MatTooltipModule,
    ],
})
export class DepartmentsComponent implements OnInit {

    // ── Stato ─────────────────────────────────────────────────────────────────
    departments: DepartmentDto[] = [];
    isLoading        = false;
    isSaving         = false;
    deletingId: string | null = null;
    loadError: string | null  = null;

    // ── Modal ─────────────────────────────────────────────────────────────────
    showModal  = false;
    modalMode: 'create' | 'edit' = 'create';
    editingDept: DepartmentDto | null = null;

    // ── Form ──────────────────────────────────────────────────────────────────
    form: FormGroup;

    constructor(
        private _service: DepartmentsService,
        private _fb: FormBuilder,
        private _snack: MatSnackBar,
    ) {}

    ngOnInit(): void {
        this._buildForm();
        this.load();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    load(): void {
        this.isLoading = true;
        this.loadError = null;

        this._service.getAll().subscribe({
            next: (list) => {
                this.departments = list;
                this.isLoading   = false;
            },
            error: (err) => {
                this.isLoading = false;
                this.loadError = err?.error?.message ?? err?.message
                    ?? 'Errore nel caricamento dei dipartimenti.';
            },
        });
    }

    // ── Modal ─────────────────────────────────────────────────────────────────

    openCreate(): void {
        this.modalMode  = 'create';
        this.editingDept = null;
        this.form.reset({ isActive: true });
        this.showModal  = true;
    }

    openEdit(dept: DepartmentDto, event: Event): void {
        event.stopPropagation();
        this.modalMode   = 'edit';
        this.editingDept = dept;
        this.form.patchValue({
            code:        dept.code,
            name:        dept.name,
            description: dept.description ?? '',
            isActive:    dept.isActive,
        });
        this.showModal = true;
    }

    closeModal(): void {
        this.showModal = false;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    save(): void {
        if (this.form.invalid || this.isSaving) return;
        this.isSaving = true;

        const v   = this.form.value;
        const obs = this.modalMode === 'create'
            ? this._service.create({ code: v.code, name: v.name, description: v.description || null })
            : this._service.update(this.editingDept!.id, {
                code: v.code, name: v.name,
                description: v.description || null, isActive: v.isActive,
              });

        obs.subscribe({
            next: () => {
                this.isSaving  = false;
                this.showModal = false;
                this._snack.open(
                    this.modalMode === 'create' ? 'Dipartimento creato' : 'Dipartimento aggiornato',
                    'OK', { duration: 3000 }
                );
                this.load();
            },
            error: (err) => {
                this.isSaving = false;
                const msg = err?.error?.message ?? err?.message ?? 'Errore nel salvataggio.';
                this._snack.open(msg, 'Chiudi', { duration: 5000 });
            },
        });
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    delete(dept: DepartmentDto, event: Event): void {
        event.stopPropagation();
        if (!confirm(`Eliminare il dipartimento "${dept.name}"?`)) return;

        this.deletingId = dept.id;

        this._service.delete(dept.id).subscribe({
            next: () => {
                this.deletingId = null;
                this._snack.open('Dipartimento eliminato', 'OK', { duration: 3000 });
                this.load();
            },
            error: (err) => {
                this.deletingId = null;
                const msg = err?.error?.message ?? err?.message ?? 'Errore nella cancellazione.';
                this._snack.open(msg, 'Chiudi', { duration: 5000 });
            },
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    trackById(_: number, item: { id: string }): string { return item.id; }

    private _buildForm(): void {
        this.form = this._fb.group({
            code:        ['', [Validators.required, Validators.maxLength(20)]],
            name:        ['', [Validators.required, Validators.maxLength(100)]],
            description: [''],
            isActive:    [true],
        });
    }
}
