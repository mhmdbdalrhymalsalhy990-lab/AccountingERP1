import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { UserInfo } from '../../../core/models/auth.models';
import { LanguageService } from '../../../core/services/language.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-users-permissions',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './users-permissions.html',
  styleUrls: ['./users-permissions.scss']
})
export class UsersPermissionsComponent implements OnInit {
  authService = inject(AuthService);
  langService = inject(LanguageService);
  tenantService = inject(TenantService);

  isLoading = signal(true);
  currentUser = signal<UserInfo | null>(null);

  ngOnInit(): void {
    this.authService.getProfile().subscribe({
      next: () => {
        this.currentUser.set(this.authService.currentUser());
        this.isLoading.set(false);
      },
      error: () => {
        this.currentUser.set(this.authService.currentUser());
        this.isLoading.set(false);
      }
    });
  }
}
