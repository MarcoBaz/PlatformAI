import {
    Component,
    OnInit,
    ViewChild,
    ViewEncapsulation,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NgApexchartsModule, ChartComponent } from 'ng-apexcharts';
import {
    ApexChart,
    ApexDataLabels,
    ApexFill,
    ApexLegend,
    ApexPlotOptions,
    ApexTooltip,
    ApexXAxis,
    ApexYAxis,
    ApexAxisChartSeries,
    ApexTitleSubtitle,
} from 'ng-apexcharts';

import {
    ProductionLinesService,
    ProductionLineDto,
    MachineDto,
    DepartmentOption,
} from './production-lines.service';

export type ChartOptions = {
    series: ApexAxisChartSeries;
    chart: ApexChart;
    xaxis: ApexXAxis;
    yaxis: ApexYAxis;
    plotOptions: ApexPlotOptions;
    dataLabels: ApexDataLabels;
    fill: ApexFill;
    legend: ApexLegend;
    tooltip: ApexTooltip;
    title: ApexTitleSubtitle;
    colors: string[];
};

@Component({
    selector: 'app-production-lines',
    templateUrl: './production-lines.component.html',
    encapsulation: ViewEncapsulation.None,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatDividerModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatProgressSpinnerModule,
        MatSelectModule,
        MatSlideToggleModule,
        MatSnackBarModule,
        MatTooltipModule,
        NgApexchartsModule,
    ],
})
export class ProductionLinesComponent implements OnInit {
    @ViewChild('chart') chart: ChartComponent;

    // ── Stato ─────────────────────────────────────────────────────────────────
    lines: ProductionLineDto[] = [];
    departments: DepartmentOption[] = [];
    selectedLine: ProductionLineDto | null = null;

    isLoadingLines      = false;
    isLoadingMachines   = false;
    isSavingLine        = false;
    isSavingMachine     = false;
    deletingLineId:     string | null = null;
    deletingMachineId:  string | null = null;
    loadError:          string | null = null;

    // ── Modal stato ───────────────────────────────────────────────────────────
    showLineModal    = false;
    showMachineModal = false;
    lineModalMode:    'create' | 'edit' = 'create';
    machineModalMode: 'create' | 'edit' = 'create';
    editingLine:    ProductionLineDto | null = null;
    editingMachine: MachineDto | null = null;

    // ── Forms ─────────────────────────────────────────────────────────────────
    lineForm: FormGroup;
    machineForm: FormGroup;

    // ── Chart ─────────────────────────────────────────────────────────────────
    chartOptions: Partial<ChartOptions> = {
        series: [{ name: 'Macchine', data: [] }],
        chart: {
            type: 'bar',
            height: 260,
            toolbar: { show: false },
            fontFamily: 'inherit',
            animations: { enabled: true },
        },
        plotOptions: {
            bar: { horizontal: false, columnWidth: '50%', borderRadius: 4 },
        },
        dataLabels: { enabled: true },
        xaxis: { categories: [] },
        yaxis: { title: { text: 'N° Macchine' }, min: 0, tickAmount: 4 },
        fill: { opacity: 1 },
        legend: { show: false },
        tooltip: { y: { formatter: (val) => `${val} macchine` } },
        title: {
            text: 'Macchine per Linea di Produzione',
            align: 'left',
            style: { fontSize: '13px', fontWeight: '600' },
        },
        colors: ['#4f46e5'],
    };

    readonly machineTypes    = ['CNC', 'Assembly', 'Welding', 'Painting', 'Inspection', 'Packaging', 'Other'];
    readonly machineStatuses = ['Idle', 'Running', 'Maintenance', 'Error', 'Offline'];

    constructor(
        private _service: ProductionLinesService,
        private _fb: FormBuilder,
        private _snack: MatSnackBar,
    ) {}

    ngOnInit(): void {
        this._buildForms();
        this._loadAll();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    _loadAll(): void {
        this.isLoadingLines = true;
        this.loadError      = null;

        this._service.getDepartments().subscribe({
            next: (d) => { this.departments = d; },
            error: () => { /* non bloccante */ },
        });

        this._service.getLines().subscribe({
            next: (lines) => {
                this.lines = lines;
                if (this.selectedLine) {
                    this.selectedLine = lines.find(l => l.id === this.selectedLine!.id) ?? null;
                }
                this.isLoadingLines = false;
                this._updateChart();
            },
            error: (err) => {
                this.isLoadingLines = false;
                this.loadError = err?.error?.message
                    ?? err?.message
                    ?? 'Errore nel caricamento delle linee. Verifica la connessione al server.';
            },
        });
    }

    // ── Selezione riga linea ──────────────────────────────────────────────────

    selectLine(line: ProductionLineDto): void {
        // Toggle: click sulla riga già espansa la chiude
        if (this.selectedLine?.id === line.id) {
            this.selectedLine = null;
            return;
        }

        // Imposta subito la riga come selezionata (mostra la sezione espansa)
        this.selectedLine      = { ...line, machines: line.machines ?? [] };
        this.isLoadingMachines = true;

        // Richiesta dedicata GET /api/production-lines/{id}
        this._service.getLine(line.id).subscribe({
            next: (full) => {
                this.selectedLine      = full;
                this.isLoadingMachines = false;
            },
            error: () => {
                this.isLoadingMachines = false;
            },
        });
    }

    // ── Modal Linea ───────────────────────────────────────────────────────────

    openCreateLineModal(): void {
        this.lineModalMode = 'create';
        this.editingLine   = null;
        this.lineForm.reset({ isActive: true });
        this.showLineModal = true;
        /* noop */
    }

    openEditLineModal(line: ProductionLineDto, event: Event): void {
        event.stopPropagation();
        this.lineModalMode = 'edit';
        this.editingLine   = line;
        this.lineForm.patchValue({
            code:         line.code,
            name:         line.name,
            description:  line.description ?? '',
            departmentId: line.departmentId,
            isActive:     line.isActive,
        });
        this.showLineModal = true;
        /* noop */
    }

    closeLineModal(): void {
        this.showLineModal = false;
        /* noop */
    }

    saveLine(): void {
        if (this.lineForm.invalid || this.isSavingLine) return;
        this.isSavingLine = true;
        /* noop */

        const v = this.lineForm.value;

        const obs = this.lineModalMode === 'create'
            ? this._service.createLine({
                code: v.code, name: v.name,
                description: v.description || null, departmentId: v.departmentId,
              })
            : this._service.updateLine(this.editingLine!.id, {
                code: v.code, name: v.name,
                description: v.description || null, isActive: v.isActive,
              });

        obs.subscribe({
            next: () => {
                this.isSavingLine = false;
                this.showLineModal = false;
                this._snack.open(
                    this.lineModalMode === 'create' ? 'Linea creata' : 'Linea aggiornata',
                    'OK', { duration: 3000 }
                );
                this._loadAll();
            },
            error: (err) => { this._onError(err, 'Errore nel salvataggio della linea'); },
        });
    }

    deleteLine(line: ProductionLineDto, event: Event): void {
        event.stopPropagation();
        if (!confirm(`Eliminare la linea "${line.name}"? L'operazione è irreversibile.`)) return;

        this.deletingLineId = line.id;
        /* noop */

        this._service.deleteLine(line.id).subscribe({
            next: () => {
                this.deletingLineId = null;
                if (this.selectedLine?.id === line.id) this.selectedLine = null;
                this._snack.open('Linea eliminata', 'OK', { duration: 3000 });
                this._loadAll();
            },
            error: (err) => {
                this.deletingLineId = null;
                this._onError(err, 'Errore nella cancellazione');
            },
        });
    }

    // ── Modal Macchina ────────────────────────────────────────────────────────

    openCreateMachineModal(): void {
        this.machineModalMode = 'create';
        this.editingMachine   = null;
        this.machineForm.reset({ status: 'Idle' });
        this.showMachineModal = true;
        /* noop */
    }

    openEditMachineModal(m: MachineDto, event: Event): void {
        event.stopPropagation();
        this.machineModalMode = 'edit';
        this.editingMachine   = m;
        this.machineForm.patchValue({ code: m.code, name: m.name, type: m.type, status: m.status });
        this.showMachineModal = true;
        /* noop */
    }

    closeMachineModal(): void {
        this.showMachineModal = false;
        /* noop */
    }

    saveMachine(): void {
        if (this.machineForm.invalid || this.isSavingMachine || !this.selectedLine) return;
        this.isSavingMachine = true;
        /* noop */

        const v   = this.machineForm.value;
        const lid = this.selectedLine.id;

        const obs = this.machineModalMode === 'create'
            ? this._service.createMachine(lid, {
                code: v.code, name: v.name, type: v.type, productionLineId: lid,
              })
            : this._service.updateMachine(lid, this.editingMachine!.id, {
                code: v.code, name: v.name, type: v.type, status: v.status,
              });

        obs.subscribe({
            next: () => {
                this.isSavingMachine  = false;
                this.showMachineModal = false;
                this._snack.open(
                    this.machineModalMode === 'create' ? 'Macchina aggiunta' : 'Macchina aggiornata',
                    'OK', { duration: 3000 }
                );
                this._loadAll();
            },
            error: (err) => { this._onError(err, 'Errore nel salvataggio della macchina'); },
        });
    }

    deleteMachine(m: MachineDto, event: Event): void {
        event.stopPropagation();
        if (!confirm(`Eliminare la macchina "${m.name}"?`)) return;

        this.deletingMachineId = m.id;
        /* noop */

        this._service.deleteMachine(this.selectedLine!.id, m.id).subscribe({
            next: () => {
                this.deletingMachineId = null;
                this._snack.open('Macchina eliminata', 'OK', { duration: 3000 });
                this._loadAll();
            },
            error: (err) => {
                this.deletingMachineId = null;
                this._onError(err, 'Errore nella cancellazione');
            },
        });
    }

    // ── Helpers UI ────────────────────────────────────────────────────────────

    statusColor(status: string): string {
        const map: Record<string, string> = {
            'Running':     'bg-green-100 text-green-800',
            'Idle':        'bg-gray-100 text-gray-600',
            'Maintenance': 'bg-yellow-100 text-yellow-800',
            'Error':       'bg-red-100 text-red-800',
            'Offline':     'bg-slate-200 text-slate-600',
        };
        return map[status] ?? 'bg-gray-100 text-gray-600';
    }

    trackById(_: number, item: { id: string }): string { return item.id; }

    // ── Chart ─────────────────────────────────────────────────────────────────

    private _updateChart(): void {
        const active = this.lines.filter(l => l.isActive);
        this.chartOptions = {
            ...this.chartOptions,
            series: [{ name: 'Macchine', data: active.map(l => l.machines.length) }],
            xaxis:  { categories: active.map(l => l.code) },
        };
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private _buildForms(): void {
        this.lineForm = this._fb.group({
            code:         ['', [Validators.required, Validators.maxLength(20)]],
            name:         ['', [Validators.required, Validators.maxLength(100)]],
            description:  [''],
            departmentId: ['', Validators.required],
            isActive:     [true],
        });

        this.machineForm = this._fb.group({
            code:   ['', [Validators.required, Validators.maxLength(20)]],
            name:   ['', [Validators.required, Validators.maxLength(100)]],
            type:   ['', Validators.required],
            status: ['Idle'],
        });
    }

    private _onError(err: any, fallback: string): void {
        this.isSavingLine    = false;
        this.isSavingMachine = false;
        const msg = err?.error?.message ?? err?.message ?? fallback;
        this._snack.open(msg, 'Chiudi', { duration: 5000 });
    }
}
