import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from 'environments/environment';

export interface DepartmentDto {
    id: string;
    code: string;
    name: string;
    description: string | null;
    isActive: boolean;
    productionLinesCount: number;
}

export interface CreateDepartmentRequest {
    code: string;
    name: string;
    description: string | null;
}

export interface UpdateDepartmentRequest {
    code: string;
    name: string;
    description: string | null;
    isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class DepartmentsService {
    private readonly _base = `${environment.apiUrl}/departments`;

    constructor(private _http: HttpClient) {}

    getAll(): Observable<DepartmentDto[]> {
        return this._http.get<DepartmentDto[]>(this._base);
    }

    create(req: CreateDepartmentRequest): Observable<DepartmentDto> {
        return this._http.post<DepartmentDto>(this._base, req);
    }

    update(id: string, req: UpdateDepartmentRequest): Observable<DepartmentDto> {
        return this._http.put<DepartmentDto>(`${this._base}/${id}`, req);
    }

    delete(id: string): Observable<void> {
        return this._http.delete<void>(`${this._base}/${id}`);
    }
}
