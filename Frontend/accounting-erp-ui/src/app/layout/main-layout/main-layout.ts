import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { TenantService } from '../../core/services/tenant.service';
import { LanguageService } from '../../core/services/language.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatSelectModule,
    MatTooltipModule
  ],
  templateUrl: './main-layout.html',
  styleUrls: ['./main-layout.scss']
})
export class MainLayoutComponent {
  authService = inject(AuthService);
  tenantService = inject(TenantService);
  langService = inject(LanguageService);
  router = inject(Router);

  isSidenavOpen = true;

  toggleSidenav(): void {
    this.isSidenavOpen = !this.isSidenavOpen;
  }

  onCompanyChange(companyId: string): void {
    this.tenantService.setCompany(companyId);
  }

  toggleLanguage(): void {
    this.langService.toggleLanguage();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
