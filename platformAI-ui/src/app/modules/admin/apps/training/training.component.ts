import {ChangeDetectionStrategy,ChangeDetectorRef,Component,OnInit,ViewEncapsulation,} from '@angular/core';
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
import { MatSliderModule } from '@angular/material/slider';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from 'app/core/auth/auth.service';
import {DEFAULT_CONFIG,SeedDataResult,TrainingCheckpoint,TrainingConfig,TrainingResult,TrainingService,} from './training.service';

@Component({
    selector: 'app-training',
    templateUrl: './training.component.html',
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatChipsModule,
        MatDividerModule,
        MatExpansionModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatProgressSpinnerModule,
        MatSelectModule,
        MatSliderModule,
        MatSlideToggleModule,
        MatTooltipModule,
        TranslocoModule,
    ],
})
export class TrainingComponent implements OnInit {
    form: FormGroup;
    isRunning = false;
    result: TrainingResult | null = null;
    checkpoint: TrainingCheckpoint | null = null;
    errorMessage: string | null = null;

    // ── Seed ────────────────────────────────────────────────────────────────
    isSeeding = false;
    seedResult: SeedDataResult | null = null;
    seedError: string | null = null;

    constructor(
        private _fb: FormBuilder,
        private _trainingService: TrainingService,
        private _authService: AuthService,
        private _cdr: ChangeDetectorRef
    ) {}

    ngOnInit(): void {
        this._buildForm();
        this._loadCheckpoint();
    }

    // ── Avvia il training ───────────────────────────────────────────────────
    runTraining(): void {
        if (this.form.invalid || this.isRunning) return;

        this.isRunning = true;
        this.result = null;
        this.errorMessage = null;
        this._cdr.markForCheck();

        const v = this.form.value;
        const config: TrainingConfig = {
            minDataPoints:                v.minDataPoints,
            includeHistoricalContext:     v.includeHistoricalContext,
            historicalContextDays:        v.historicalContextDays,
            maxHistoricalRecords:         v.maxHistoricalRecords,
            historicalSamplingRatio:      v.historicalSamplingRatio,
            numberOfTrees:                v.numberOfTrees,
            numberOfLeaves:               v.numberOfLeaves,
            minimumExampleCountPerLeaf:   v.minimumExampleCountPerLeaf,
            learningRate:                 v.learningRate,
            testFraction:                 v.testFraction,
            trainer:                      v.trainer,
        };

        this._trainingService
            .runTraining({
                userId:            this._authService.user.id,
                forceFullTraining: v.forceFullTraining,
                config,
            })
            .subscribe({
                next: (res) => {
                    this.result    = res;
                    this.isRunning = false;
                    // Ricarica il checkpoint aggiornato
                    this._loadCheckpoint();
                    this._cdr.markForCheck();
                },
                error: (err) => {
                    this.errorMessage = err?.error?.message ?? err?.message ?? 'Errore durante il training.';
                    this.isRunning    = false;
                    this._cdr.markForCheck();
                },
            });
    }

    // ── Seed dati di produzione simulati ────────────────────────────────────
    seedData(): void {
        if (this.isSeeding) return;
        this.isSeeding   = true;
        this.seedResult  = null;
        this.seedError   = null;
        this._cdr.markForCheck();

        this._trainingService
            .seedData(this._authService.user.id)
            .subscribe({
                next: (res) => {
                    this.seedResult  = res;
                    this.isSeeding   = false;
                    this._cdr.markForCheck();
                },
                error: (err) => {
                    this.seedError = err?.error?.message ?? err?.message ?? 'Errore durante il seed.';
                    this.isSeeding = false;
                    this._cdr.markForCheck();
                },
            });
    }

    // ── Reset checkpoint ────────────────────────────────────────────────────
    resetCheckpoint(): void {
        this._trainingService
            .resetCheckpoint(this._authService.user.id)
            .subscribe({
                next: () => {
                    this.checkpoint = null;
                    this._cdr.markForCheck();
                },
            });
    }

    // ── Reset form ai valori default ────────────────────────────────────────
    resetForm(): void {
        this.form.patchValue({ ...DEFAULT_CONFIG, forceFullTraining: false });
    }

    // ── Helper per il template ──────────────────────────────────────────────

    get r2Percent(): number {
        return Math.round((this.result?.rSquared ?? 0) * 100);
    }

    get r2Color(): string {
        const r2 = this.result?.rSquared ?? 0;
        if (r2 >= 0.8)  return 'accent';   // verde
        if (r2 >= 0.6)  return 'primary';  // blu
        return 'warn';                      // rosso
    }

    get diagnosisIcon(): string {
        if (!this.result) return '';
        const gap = this._getR2Gap();
        const r2  = this.result.rSquared;
        if (r2 < 0.5)   return 'heroicons_outline:exclamation-triangle';
        if (gap > 0.20) return 'heroicons_outline:exclamation-circle';
        if (gap > 0.10) return 'heroicons_outline:bolt';
        if (r2 >= 0.80) return 'heroicons_outline:check-circle';
        if (r2 >= 0.60) return 'heroicons_outline:information-circle';
        return 'heroicons_outline:x-circle';
    }

    get diagnosisLabel(): string {
        if (!this.result) return '';
        const gap = this._getR2Gap();
        const r2  = this.result.rSquared;
        if (r2 < 0.5)   return 'Underfitting';
        if (gap > 0.20) return 'Overfitting';
        if (gap > 0.10) return 'Overfitting lieve';
        if (r2 >= 0.80) return 'Buono';
        if (r2 >= 0.60) return 'Accettabile';
        return 'Debole';
    }

    get diagnosisClass(): string {
        const label = this.diagnosisLabel;
        if (label === 'Buono')            return 'text-green-600 bg-green-50';
        if (label === 'Accettabile')      return 'text-yellow-700 bg-yellow-50';
        if (label === 'Overfitting lieve') return 'text-orange-600 bg-orange-50';
        return 'text-red-600 bg-red-50';
    }

    formatDuration(iso: string): string {
        // iso è nel formato "00:00:05.123" — mostra secondi
        if (!iso) return '—';
        const parts = iso.split(':');
        if (parts.length < 3) return iso;
        const secs = parseFloat(parts[2]);
        const mins = parseInt(parts[1], 10);
        const hrs  = parseInt(parts[0], 10);
        if (hrs  > 0) return `${hrs}h ${mins}m`;
        if (mins > 0) return `${mins}m ${secs.toFixed(0)}s`;
        return `${secs.toFixed(1)}s`;
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private _buildForm(): void {
        this.form = this._fb.group({
            // Modalità
            forceFullTraining: [false],

            // Dati
            minDataPoints:             [DEFAULT_CONFIG.minDataPoints, [Validators.required, Validators.min(10)]],
            includeHistoricalContext:  [DEFAULT_CONFIG.includeHistoricalContext],
            historicalContextDays:     [DEFAULT_CONFIG.historicalContextDays,     [Validators.required, Validators.min(1), Validators.max(365)]],
            maxHistoricalRecords:      [DEFAULT_CONFIG.maxHistoricalRecords,      [Validators.required, Validators.min(50)]],
            historicalSamplingRatio:   [DEFAULT_CONFIG.historicalSamplingRatio,   [Validators.required, Validators.min(0.1), Validators.max(1)]],
            testFraction:              [DEFAULT_CONFIG.testFraction,              [Validators.required, Validators.min(0.1), Validators.max(0.4)]],

            // Algoritmo
            trainer:                      [DEFAULT_CONFIG.trainer],
            numberOfTrees:                [DEFAULT_CONFIG.numberOfTrees,                [Validators.required, Validators.min(10), Validators.max(500)]],
            numberOfLeaves:               [DEFAULT_CONFIG.numberOfLeaves,               [Validators.required, Validators.min(5), Validators.max(200)]],
            minimumExampleCountPerLeaf:   [DEFAULT_CONFIG.minimumExampleCountPerLeaf,   [Validators.required, Validators.min(1), Validators.max(50)]],
            learningRate:                 [DEFAULT_CONFIG.learningRate,                 [Validators.required, Validators.min(0.001), Validators.max(1)]],
        });
    }

    private _loadCheckpoint(): void {
        this._trainingService
            .getCheckpoint(this._authService.user.id)
            .subscribe({
                next: (cp) => {
                    this.checkpoint = cp;
                    this._cdr.markForCheck();
                },
            });
    }

    // Stima il gap R² train/test — non abbiamo il valore train diretto
    // ma lo deduciamo dall'explanation del log (indisponibile nel result).
    // Per il display usiamo una approssimazione basata sulla diagnosi nel message.
    private _getR2Gap(): number {
        const msg = this.result?.message ?? '';
        if (msg.includes('OVERFITTING'))      return 0.25;
        if (msg.includes('lieve'))            return 0.12;
        return 0.05;
    }
}
