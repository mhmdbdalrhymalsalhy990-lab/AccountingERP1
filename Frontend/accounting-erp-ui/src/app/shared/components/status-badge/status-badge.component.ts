import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type BadgeVariant = 'posted' | 'draft' | 'active' | 'inactive' | 'cancelled' | 'approved' | 'pending';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="status-badge" [ngClass]="currentVariant">
      <span class="badge-dot"></span>
      <span class="badge-text"><ng-content>{{ displayText }}</ng-content></span>
    </span>
  `,
  styles: [`
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;

      .badge-dot {
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background-color: currentColor;
      }

      &.posted, &.active, &.approved {
        background-color: #dcfce7;
        color: #15803d;
      }

      &.draft, &.pending {
        background-color: #fef3c7;
        color: #b45309;
      }

      &.cancelled, &.inactive, &.reversed {
        background-color: #fee2e2;
        color: #b91c1c;
      }
    }
  `]
})
export class StatusBadgeComponent {
  @Input() variant: BadgeVariant | string = 'active';
  
  displayText = '';

  @Input() set status(val: any) {
    if (val !== undefined && val !== null) {
      const s = String(val).toLowerCase();
      if (s === 'posted' || s === '2') {
        this.variant = 'posted';
        this.displayText = 'مرحل';
      } else if (s === 'draft' || s === '1') {
        this.variant = 'draft';
        this.displayText = 'مسودة';
      } else if (s === 'reversed' || s === '3') {
        this.variant = 'reversed';
        this.displayText = 'معكوس';
      } else if (s === 'active') {
        this.variant = 'active';
        this.displayText = 'نشط';
      } else {
        this.variant = s;
        this.displayText = String(val);
      }
    }
  }

  get currentVariant(): string {
    return String(this.variant).toLowerCase();
  }
}
