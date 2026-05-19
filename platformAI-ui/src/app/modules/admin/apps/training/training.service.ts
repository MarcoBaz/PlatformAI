import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from 'environments/environment';

// ── Modelli che specchiano i DTO C# ──────────────────────────────────────────

export type TrainerType = 'FastTree' | 'Sdca';

export interface TrainingConfig {
    minDataPoints: number;
    includeHistoricalContext: boolean;
    historicalContextDays: number;
    maxHistoricalRecords: number;
    historicalSamplingRatio: number;
    numberOfTrees: number;
    numberOfLeaves: number;
    minimumExampleCountPerLeaf: number;
    learningRate: number;
    testFraction: number;
    trainer: TrainerType;
}

export interface TrainingRunRequest {
    userId: string;
    forceFullTraining: boolean;
    config: TrainingConfig;
}

export interface TrainingResult {
    tenantCode: string;
    success: boolean;
    message: string;
    newRecordsCount: number;
    totalDataUsed: number;
    modelVersion: string | null;
    rSquared: number;
    rmse: number;
    mae: number;
    previousCheckpoint: string | null;
    newCheckpoint: string | null;
    startTime: string;
    endTime: string;
    duration: string;
}

export interface SeedDataResult {
    success: boolean;
    orderNumber: string;
    lineName: string;
    machinesCount: number;
    productionDataCount: number;
    machineEventsCount: number;
    period: string;
    message: string;
}

export interface TrainingCheckpoint {
    tenantCode: string;
    lastProcessedDate: string;
    lastTrainingDate: string;
    modelVersion: string;
    rSquared: number;
    rmse: number;
    recordsProcessed: number;
}

// ── Configurazione di default — corrisponde ai default in IncrementalTrainingConfig.cs ─

export const DEFAULT_CONFIG: TrainingConfig = {
    minDataPoints: 50,
    includeHistoricalContext: true,
    historicalContextDays: 30,
    maxHistoricalRecords: 500,
    historicalSamplingRatio: 1.0,
    numberOfTrees: 100,
    numberOfLeaves: 40,
    minimumExampleCountPerLeaf: 5,
    learningRate: 0.1,
    testFraction: 0.2,
    trainer: 'FastTree',
};

@Injectable({ providedIn: 'root' })
export class TrainingService {
    private readonly _base = `${environment.apiUrl}/training`;

    constructor(private _http: HttpClient) {}

    runTraining(request: TrainingRunRequest): Observable<TrainingResult> {
        return this._http.post<TrainingResult>(`${this._base}/run`, request);
    }

    getCheckpoint(userId: string): Observable<TrainingCheckpoint | null> {
        return this._http.get<TrainingCheckpoint | null>(
            `${this._base}/checkpoint`,
            { params: { userId } }
        ).pipe(
            // 204 No Content (nessun checkpoint) → null; qualsiasi altro errore → null silenzioso
            catchError(() => of(null))
        );
    }

    resetCheckpoint(userId: string): Observable<{ message: string }> {
        return this._http.post<{ message: string }>(`${this._base}/reset`, { userId });
    }

    seedData(userId: string, lineName = 'Linea A', daysAgo = 1): Observable<SeedDataResult> {
        return this._http.post<SeedDataResult>(`${this._base}/seed`, { userId, lineName, daysAgo });
    }
}
