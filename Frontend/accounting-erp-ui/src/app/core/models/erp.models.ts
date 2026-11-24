export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface AccountTreeDto {
  id: number;
  parentAccountId?: number;
  accountCode: string;
  nameAr: string;
  nameEn: string;
  accountType: number;
  nature: number;
  level: number;
  isPosting: boolean;
  isActive: boolean;
  children: AccountTreeDto[];
}

export interface JournalEntryDto {
  id: number;
  companyId: number;
  branchId: number;
  branchName: string;
  fiscalPeriodId: number;
  fiscalPeriodName: string;
  entryNumber: string;
  entryDate: string;
  referenceNumber?: string;
  description: string;
  entryType: number;
  entryTypeName: string;
  status: number;
  statusName: string;
  totalDebit: number;
  totalCredit: number;
  postedAt?: string;
  isReversed: boolean;
  reversedEntryNumber?: string;
  lines: JournalEntryLineDto[];
}

export interface JournalEntryLineDto {
  id: number;
  accountId: number;
  accountCode: string;
  accountNameAr: string;
  description?: string;
  debit: number;
  credit: number;
  foreignDebit?: number;
  foreignCredit?: number;
  costCenterName?: string;
}

export interface CreateJournalEntryDto {
  entryDate: string;
  referenceNumber?: string;
  description: string;
  branchId?: number;
  entryType?: number;
  lines: CreateJournalEntryLineDto[];
}

export interface CreateJournalEntryLineDto {
  accountId: number;
  description?: string;
  debit: number;
  credit: number;
  foreignCurrencyId?: number;
  exchangeRate?: number;
  foreignDebit?: number;
  foreignCredit?: number;
  costCenterId?: number;
}

export interface SalesInvoiceDto {
  id: number;
  companyId: number;
  branchName: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string;
  customerId: number;
  customerNameAr: string;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  grandTotal: number;
  paidAmount: number;
  remainingAmount: number;
  status: number;
  statusName: string;
  journalEntryNumber?: string;
  items: SalesInvoiceItemDto[];
}

export interface SalesInvoiceItemDto {
  id: number;
  productId: number;
  productName: string;
  warehouseName: string;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  taxRate: number;
  taxAmount: number;
  lineTotal: number;
}

export interface CustomerDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  accountId: number;
  accountCode?: string;
  creditLimit: number;
  phone?: string;
  email?: string;
  address?: string;
  isActive: boolean;
  currentBalance: number;
}

export interface SupplierDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  accountId: number;
  accountCode?: string;
  phone?: string;
  email?: string;
  address?: string;
  isActive: boolean;
  currentBalance: number;
}

export interface PaymentDto {
  id: number;
  paymentNumber: string;
  paymentType: number;
  paymentTypeName: string;
  paymentMethod: number;
  paymentDate: string;
  bankAccountName: string;
  customerName?: string;
  supplierName?: string;
  amount: number;
  currencyCode: string;
  referenceNumber?: string;
  journalEntryNumber?: string;
}

export interface ProductDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  unitName: string;
  costPrice: number;
  sellingPrice: number;
  isActive: boolean;
  currentStockQuantity: number;
  totalStockValuation: number;
}

export interface WarehouseDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  location?: string;
  isActive: boolean;
}

export interface FixedAssetDto {
  id: number;
  assetCode: string;
  nameAr: string;
  nameEn: string;
  purchaseDate: string;
  purchaseCost: number;
  salvageValue: number;
  usefulLifeMonths: number;
  status: number;
  statusName: string;
  totalAccumulatedDepreciation: number;
  netBookValue: number;
  monthlyDepreciationAmount: number;
}

export interface CostCenterTreeDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  level: number;
  isActive: boolean;
  children: CostCenterTreeDto[];
}

export interface TrialBalanceReportDto {
  companyId: number;
  companyNameAr: string;
  fromDate: string;
  toDate: string;
  totalBeginningDebit: number;
  totalBeginningCredit: number;
  totalPeriodDebit: number;
  totalPeriodCredit: number;
  totalEndingDebit: number;
  totalEndingCredit: number;
  isBalanced: boolean;
  items: TrialBalanceItemDto[];
}

export interface TrialBalanceItemDto {
  accountId: number;
  accountCode: string;
  accountNameAr: string;
  level: number;
  isPosting: boolean;
  beginningDebit: number;
  beginningCredit: number;
  periodDebit: number;
  periodCredit: number;
  endingDebit: number;
  endingCredit: number;
}

export interface IncomeStatementReportDto {
  fromDate: string;
  toDate: string;
  totalRevenue: number;
  totalCostOfGoodsSold: number;
  grossProfit: number;
  totalOperatingExpenses: number;
  operatingProfit: number;
  netProfit: number;
}

export interface BalanceSheetReportDto {
  asOfDate: string;
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  currentYearNetProfit: number;
  totalLiabilitiesAndEquity: number;
  isBalanced: boolean;
}
