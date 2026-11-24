import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <div class="title-wrap">
        <h1>{{ title }}</h1>
        <p *ngIf="subtitle">{{ subtitle }}</p>
      </div>
      <div class="actions-wrap">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
      flex-wrap: wrap;
      gap: 16px;

      h1 {
        font-size: 1.5rem;
        font-weight: 700;
        margin: 0;
        color: var(--text-primary, #0f172a);
      }

      p {
        font-size: 0.875rem;
        color: var(--text-secondary, #64748b);
        margin: 4px 0 0 0;
      }

      .actions-wrap {
        display: flex;
        gap: 12px;
        align-items: center;
      }
    }
  `]
})
export class PageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle?: string;
}
