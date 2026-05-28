import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from 'environments/environment';

// ── DTOs che specchiano i record C# ──────────────────────────────────────────

export interface MachineDto {
    id: string;
    code: string;
    name: string;
    type: string;
    status: string;
    productionLineId: string;
}

export interface ProductionLineDto {
    id: string;
    code: string;
    name: string;
    description: string | null;
    isActive: boolean;
    departmentId: string;
    departmentName: string;
    machines: MachineDto[];
}

export interface DepartmentOption {
    id: string;
    code: string;
    name: string;
}

export interface CreateProductionLineRequest {
    code: string;
    name: string;
    description: string | null;
    departmentId: string;
}

export interface UpdateProductionLineRequest {
    code: string;
    name: string;
    description: string | null;
    isActive: boolean;
}

export interface CreateMachineRequest {
    code: string;
    name: string;
    type: string;
    productionLineId: string;
}

export interface UpdateMachineRequest {
    code: string;
    name: string;
    type: string;
    status: string;
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ProductionLinesService {
    private readonly _base = `${environment.apiUrl}/production-lines`;

    constructor(private _http: HttpClient) {}

    // Production Lines
    getLines(): Observable<ProductionLineDto[]> {
        return this._http.get<ProductionLineDto[]>(this._base);
    }

    getLine(id: string): Observable<ProductionLineDto> {
        return this._http.get<ProductionLineDto>(`${this._base}/${id}`);
    }

    createLine(req: CreateProductionLineRequest): Observable<ProductionLineDto> {
        return this._http.post<ProductionLineDto>(this._base, req);
    }

    updateLine(id: string, req: UpdateProductionLineRequest): Observable<ProductionLineDto> {
        return this._http.put<ProductionLineDto>(`${this._base}/${id}`, req);
    }

    deleteLine(id: string): Observable<void> {
        return this._http.delete<void>(`${this._base}/${id}`);
    }

    // Machines
    createMachine(lineId: string, req: CreateMachineRequest): Observable<MachineDto> {
        return this._http.post<MachineDto>(`${this._base}/${lineId}/machines`, req);
    }

    updateMachine(lineId: string, machineId: string, req: UpdateMachineRequest): Observable<MachineDto> {
        return this._http.put<MachineDto>(`${this._base}/${lineId}/machines/${machineId}`, req);
    }

    deleteMachine(lineId: string, machineId: string): Observable<void> {
        return this._http.delete<void>(`${this._base}/${lineId}/machines/${machineId}`);
    }

    // Departments
    getDepartments(): Observable<DepartmentOption[]> {
        return this._http.get<DepartmentOption[]>(`${this._base}/departments`);
    }
}
