export interface UserVM {
    id: string;
    name: string;
    surname: string;
    login: string;
    email?: string;
    mobilePhone?: string;
    enabled: boolean;
    roleId: string;
    role?: RoleVM;
    tenantId: string;
    tenant?: { id: string; code: string; name: string };
    languageCode: string;
}

export interface RoleVM {
    id: string;
    code: string;
    description: string;
}

export interface UserDialogData {
    user: UserVM | null;
    roles: RoleVM[];
    isNew: boolean;
}