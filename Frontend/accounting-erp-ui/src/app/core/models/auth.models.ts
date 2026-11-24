export interface LoginRequest {
  userNameOrEmail?: string;
  userName?: string;
  email?: string;
  password: string;
  companyId?: number;
  branchId?: number;
}

export interface CompanySummaryDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
}

export interface BranchSummaryDto {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isMainBranch: boolean;
}

export interface CompanyBranchAccessDto {
  companyId: number;
  companyCode: string;
  companyNameAr: string;
  companyNameEn: string;
  role: string;
  roleName?: string;
  defaultBranchId?: number;
  defaultBranchNameAr?: string;
  branches: BranchSummaryDto[];
}

export interface UserSummaryDto {
  id: string;
  userName: string;
  email: string;
  fullName: string;
}

export interface AuthResponse {
  accessToken: string;
  token?: string;
  refreshToken: string;
  expiresAt?: string;
  refreshTokenExpiryTime?: string;
  user: UserSummaryDto;
  activeCompany: CompanySummaryDto;
  activeBranch?: BranchSummaryDto;
  role: string;
  permissions: string[];
  availableCompanies: CompanyBranchAccessDto[];
}

export interface UserInfo {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  companyId: number;
  companyName: string;
  branchId: number;
  branchName: string;
  role: string;
  roles: string[];
  permissions: string[];
  availableCompanies: CompanyBranchAccessDto[];
}

export interface UserCompanyDto {
  companyId: number;
  companyCode: string;
  companyNameAr: string;
  companyNameEn: string;
  defaultBranchId: number;
  defaultBranchNameAr: string;
  roleName: string;
}

export interface SelectCompanyRequest {
  companyId: number;
  branchId?: number;
}
