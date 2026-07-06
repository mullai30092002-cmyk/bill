using BillSoft.Infrastructure.Setup;
using BillSoft.Infrastructure;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Api.Auth;
using BillSoft.Api.Cashiering;
using BillSoft.Api.Billing;
using BillSoft.Api.Admin;
using BillSoft.Api.Dashboard;
using BillSoft.Api.Inventory;
using BillSoft.Api.Setup;
using BillSoft.Api.Reports;
using BillSoft.Api.Kitchen;
using BillSoft.Api.Pos;
using BillSoft.Api.Vendors;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddBillSoftAuthentication(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
const string FrontendCorsPolicyName = "FrontendDevelopment";

if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(FrontendCorsPolicyName, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

var databaseOptions = app.Services.GetRequiredService<DatabaseOptions>();

if (SqliteBootstrapper.ShouldBootstrapSqlite(databaseOptions, app.Environment))
{
    await using var sqliteBootstrapScope = app.Services.CreateAsyncScope();
    var sqliteBootstrapContext = sqliteBootstrapScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
    await sqliteBootstrapContext.Database.EnsureCreatedAsync();
}

var seedOptions = FoundationSeedRuntime.ParseOptions(app.Configuration, args);
var demoSeedOptions = DemoLoginSeedRuntime.ParseOptions(args);

if (seedOptions.ShouldRun)
{
    await FoundationSeedRuntime.ExecuteAsync(app.Services, app.Logger, seedOptions);
}

if (demoSeedOptions.ShouldRun)
{
    await DemoLoginSeedRuntime.ExecuteAsync(app.Services, app.Logger, demoSeedOptions);
}

if (seedOptions.ShouldExitAfterSeed || demoSeedOptions.ShouldExitAfterSeed)
{
    return;
}

if (allowedOrigins.Length > 0)
{
    app.UseCors(FrontendCorsPolicyName);
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCashierShiftEndpoints();
app.MapBillingEndpoints();
app.MapBranchAdminEndpoints();
app.MapMenuAdminEndpoints();
app.MapOwnerDashboardEndpoints();
app.MapDailyCashSalesReportEndpoints();
app.MapCashReconciliationReportEndpoints();
app.MapPreparedStockReportEndpoints();
app.MapExpiryStockReportEndpoints();
app.MapVendorPayablesReportEndpoints();
app.MapSetupEndpoints();
app.MapInventoryEndpoints();
app.MapKitchenTicketEndpoints();
app.MapPosOrderEndpoints();
app.MapUserAdminEndpoints();
app.MapRolePermissionAdminEndpoints();
app.MapVendorEndpoints();
app.MapVendorBillOcrEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    service = "BillSoft.Api",
    status = "Healthy",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/", () => Results.Ok(new
{
    name = "BillSoft API",
    purpose = "Restaurant billing, kitchen, inventory, vendor OCR, cash-control, and audit platform"
}));

app.Run();

public partial class Program;
