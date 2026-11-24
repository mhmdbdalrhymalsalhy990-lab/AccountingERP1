import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'حدث خطأ أثناء معالجة الطلب';

      if (error.error && typeof error.error === 'string') {
        errorMessage = error.error;
      } else if (error.error && error.error.message) {
        errorMessage = error.error.message;
      } else if (error.status === 401) {
        errorMessage = 'انتهت صلاحية الجلسة، الرجاء تسجيل الدخول مجدداً';
        authService.logout();
        router.navigate(['/login']);
      } else if (error.status === 403) {
        errorMessage = 'ليس لديك الصلاحية الكافية لتنفيذ هذا الإجراء';
      } else if (error.status === 404) {
        errorMessage = 'السجل المطلوب غير موجود';
      } else if (error.status >= 500) {
        errorMessage = 'حدث خطأ في الخادم، يرجى المحاولة لاحقاً';
      }

      snackBar.open(errorMessage, 'إغلاق', {
        duration: 4500,
        horizontalPosition: 'center',
        verticalPosition: 'bottom',
        panelClass: ['error-snackbar']
      });

      return throwError(() => error);
    })
  );
};
