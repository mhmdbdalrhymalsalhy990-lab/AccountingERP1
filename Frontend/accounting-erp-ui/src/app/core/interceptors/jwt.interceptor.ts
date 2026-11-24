import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { TenantService } from '../services/tenant.service';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const tenantService = inject(TenantService);

  const token = authService.getToken();
  const companyId = tenantService.selectedCompanyId();
  const branchId = tenantService.selectedBranchId();

  let headers = req.headers;

  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (companyId) {
    headers = headers.set('X-Company-Id', companyId);
  }

  if (branchId) {
    headers = headers.set('X-Branch-Id', branchId);
  }

  const clonedRequest = req.clone({ headers });
  return next(clonedRequest);
};
