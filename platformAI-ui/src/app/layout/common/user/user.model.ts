export interface User {
    id:string;
    name: string;
    surname: string;
    login: string;
    password: string;
    email: string | null;
    mobilePhone: string | null;
    bearerToken: string | null;
    enabled: boolean;
    roleId: string;
    tenantId: string;
    languageCode: string;
}