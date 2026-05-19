import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
//import { User } from 'app/core/user/user.types';
import { User } from 'app/layout/common/user/user.model';
import { environment } from 'environments/environment';
import { map, Observable, of, ReplaySubject, tap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class UserService {
    private _httpClient = inject(HttpClient);
    //private _user: ReplaySubject<User> = new ReplaySubject<User>(1);
    public _user: User;
    private readonly _apiUrl = environment.apiUrl;

    // -----------------------------------------------------------------------------------------------------
    // @ Accessors
    // -----------------------------------------------------------------------------------------------------

    /**
     * Setter & getter for user
     *
     * @param value
     */
   set user(value: User) {
    localStorage.setItem('user', JSON.stringify(value));
    this._user = value;
}

get user$(): Observable<User> {
    if (!this._user) {
        const stored = localStorage.getItem('user');
        if (stored) {
            try {
                this._user = JSON.parse(stored) as User;
            } catch {}
        }
    }
    return of(this._user);
}

    // -----------------------------------------------------------------------------------------------------
    // @ Public methods
    // -----------------------------------------------------------------------------------------------------

    /**
     * Get the current signed-in user data
     */
    get(): Observable<User> {
        // return this._httpClient.get<User>(`${this._apiUrl}/common/user`).pipe(
        //     tap((user) => {
        //         this._user = user;
        //     })
        // );
        const stored = localStorage.getItem('user');
        if (stored) {
            try {
                this._user = JSON.parse(stored) as User;
            } catch {}
        }
        return of(this._user);
    }

    /**
     * Update the user
     *
     * @param user
     */
    update(user: User): Observable<any> {
        return this._httpClient.patch<User>(`${this._apiUrl}/common/user`, { user }).pipe(
            map((response) => {
                this._user= response;
                return of(this._user);
            })
        );
    }
}
