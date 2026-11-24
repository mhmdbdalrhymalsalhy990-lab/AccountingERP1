import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, SelectCompanyRequest, UserInfo } from '../models/auth.models';
import { TenantService } from './tenant.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'erp_token';
  private readonly REFRESH_KEY = 'erp_refresh_token';
  private readonly USER_KEY = 'erp_user';

  private http = inject(HttpClient);
  private router = inject(Router);
  private tenantService = inject(TenantService);

  public currentUser = signal<UserInfo | null>(this.getStoredUser());
  public isAuthenticated = computed(() => !!this.currentUser());
  public activeCompanyId = computed(() => this.currentUser()?.companyId ?? 0);
  public activeBranchId = computed(() => this.currentUser()?.branchId ?? 0);
  public userPermissions = computed(() => this.currentUser()?.permissions ?? []);
  public userRoles = computed(() => this.currentUser()?.roles ?? []);

  constructor() {}

  public login(credentials: LoginRequest): Observable<AuthResponse> {
    const identifier = (credentials.userNameOrEmail || credentials.userName || credentials.email || '').trim();
    const payload = {
      userName: identifier,
      userNameOrEmail: identifier,
      password: credentials.password
    };
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, payload).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  public selectCompany(companyId: number, branchId?: number): Observable<AuthResponse> {
    const payload: SelectCompanyRequest = { companyId, branchId };
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/select-company`, payload).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  public refreshToken(): Observable<AuthResponse> {
    const accessToken = this.getToken() || '';
    const refreshToken = this.getRefreshToken() || '';
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/refresh-token`, { accessToken, refreshToken }).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  public logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_KEY);
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem('erp_company_id');
    localStorage.removeItem('erp_branch_id');
    this.tenantService.clear();
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  public getProfile(): Observable<AuthResponse> {
    return this.http.get<AuthResponse>(`${environment.apiUrl}/auth/me`).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  public getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  public getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_KEY);
  }

  public hasPermission(permission: string): boolean {
    const perms = this.userPermissions();
    return perms.includes(permission) || this.userRoles().includes('SuperAdmin');
  }

  public hasRole(role: string): boolean {
    return this.userRoles().includes(role);
  }

  private handleAuthSuccess(res: AuthResponse): void {
    const token = res.accessToken || res.token || '';
    if (token) {
      localStorage.setItem(this.TOKEN_KEY, token);
    }
    if (res.refreshToken) {
      localStorage.setItem(this.REFRESH_KEY, res.refreshToken);
    }

    const companyId = res.activeCompany?.id ?? 0;
    const branchId = res.activeBranch?.id ?? 0;

    if (companyId > 0) {
      localStorage.setItem('erp_company_id', companyId.toString());
      this.tenantService.setCompany(companyId.toString());
    }
    if (branchId > 0) {
      localStorage.setItem('erp_branch_id', branchId.toString());
      this.tenantService.setBranch(branchId.toString());
    }

    const userInfo: UserInfo = {
      id: res.user?.id || '',
      userName: res.user?.userName || '',
      email: res.user?.email || '',
      fullName: res.user?.fullName || '',
      companyId: companyId,
      companyName: res.activeCompany?.nameAr || res.activeCompany?.nameEn || '',
      branchId: branchId,
      branchName: res.activeBranch?.nameAr || res.activeBranch?.nameEn || '',
      role: res.role || 'User',
      roles: res.role ? [res.role] : [],
      permissions: res.permissions || [],
      availableCompanies: (res.availableCompanies || []).map(c => ({
        ...c,
        roleName: c.role || (c as any).roleName || 'User',
        defaultBranchNameAr: c.branches?.find(b => b.id === c.defaultBranchId)?.nameAr || (c.branches?.length > 0 ? c.branches[0].nameAr : '')
      }))
    };

    localStorage.setItem(this.USER_KEY, JSON.stringify(userInfo));
    this.currentUser.set(userInfo);
  }

  private getStoredUser(): UserInfo | null {
    const raw = localStorage.getItem(this.USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UserInfo;
    } catch {
      return null;
    }
  }
}
