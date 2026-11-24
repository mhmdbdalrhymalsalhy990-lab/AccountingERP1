using AccountingERP.Application.Interfaces;
using AccountingERP.Infrastructure.Persistence.Context;
using AccountingERP.Infrastructure.Persistence.Interceptors;
using AccountingERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Register Scoped Tenant and User Context Services
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Register Interceptors
        services.AddScoped<AuditingAndTenantInterceptor>();

        // Register EF Core ApplicationDbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=.\\MSSQLSERVER1;Database=AccountingERP_Db;Integrated Security=True;TrustServerCertificate=True;";

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditingAndTenantInterceptor>();

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            })
            .AddInterceptors(interceptor);
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Register Security, Password Hasher & JWT Token Service
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Register Business Domain Services
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
        services.AddScoped<IPurchaseInvoiceService, PurchaseInvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IStockTransactionService, StockTransactionService>();
        services.AddScoped<IBankAccountService, BankAccountService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<IFixedAssetService, FixedAssetService>();
        services.AddScoped<ICostCenterService, CostCenterService>();
        services.AddScoped<ITaxService, TaxService>();
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IReportingExportService, ReportingExportService>();

        // Register Custom Permission Authorization
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Security.PermissionPolicyProvider>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Security.PermissionAuthorizationHandler>();

        // Register Authentication Handler
        services.AddAuthentication("Bearer")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Security.JwtAuthenticationHandler>("Bearer", _ => { });

        services.AddAuthorization();

        return services;
    }
}
