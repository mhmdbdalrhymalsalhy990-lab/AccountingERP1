import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { MainLayoutComponent } from './layout/main-layout/main-layout';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then(m => m.LoginComponent)
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then(m => m.DashboardComponent)
      },
      {
        path: 'accounts',
        loadComponent: () => import('./features/general-ledger/chart-of-accounts/chart-of-accounts').then(m => m.ChartOfAccountsComponent)
      },
      {
        path: 'journal-entries',
        loadComponent: () => import('./features/general-ledger/journal-entries/journal-entries').then(m => m.JournalEntriesComponent)
      },
      {
        path: 'general-ledger',
        loadComponent: () => import('./features/general-ledger/ledger-statement/ledger-statement').then(m => m.LedgerStatementComponent)
      },
      {
        path: 'reports/trial-balance',
        loadComponent: () => import('./features/reports/trial-balance/trial-balance').then(m => m.TrialBalanceComponent)
      },
      {
        path: 'customers',
        loadComponent: () => import('./features/commercial/customers/customers').then(m => m.CustomersComponent)
      },
      {
        path: 'suppliers',
        loadComponent: () => import('./features/commercial/suppliers/suppliers').then(m => m.SuppliersComponent)
      },
      {
        path: 'sales-invoices',
        loadComponent: () => import('./features/commercial/sales-invoices/sales-invoices').then(m => m.SalesInvoicesComponent)
      },
      {
        path: 'invoices/:id/print',
        loadComponent: () => import('./features/commercial/invoice-print/invoice-print').then(m => m.InvoicePrintComponent)
      },
      {
        path: 'purchase-invoices',
        loadComponent: () => import('./features/commercial/purchase-invoices/purchase-invoices').then(m => m.PurchaseInvoicesComponent)
      },
      {
        path: 'inventory',
        loadComponent: () => import('./features/inventory/products-inventory/products-inventory').then(m => m.ProductsInventoryComponent)
      },
      {
        path: 'banking',
        loadComponent: () => import('./features/treasury/banking/banking').then(m => m.BankingComponent)
      },
      {
        path: 'fixed-assets',
        loadComponent: () => import('./features/assets/fixed-assets/fixed-assets').then(m => m.FixedAssetsComponent)
      },
      {
        path: 'reports/financial-statements',
        loadComponent: () => import('./features/reports/financial-statements/financial-statements').then(m => m.FinancialStatementsComponent)
      },
      {
        path: 'users-permissions',
        loadComponent: () => import('./features/security/users-permissions/users-permissions').then(m => m.UsersPermissionsComponent)
      },
      {
        path: 'system-settings',
        loadComponent: () => import('./features/settings/system-settings/system-settings').then(m => m.SystemSettingsComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
