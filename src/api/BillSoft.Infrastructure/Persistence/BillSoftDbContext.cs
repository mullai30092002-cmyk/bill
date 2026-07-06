using BillSoft.Domain.Auditing;
using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Vendors;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Persistence;

public sealed class BillSoftDbContext : DbContext
{
    public BillSoftDbContext(DbContextOptions<BillSoftDbContext> options)
        : base(options)
    {
    }

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<MenuItemStockItem> MenuItemStockItems => Set<MenuItemStockItem>();

    public DbSet<MenuItemRecipeIngredient> MenuItemRecipeIngredients => Set<MenuItemRecipeIngredient>();

    public DbSet<MenuItemPriceHistory> MenuItemPriceHistory => Set<MenuItemPriceHistory>();

    public DbSet<BatchProduction> BatchProductions => Set<BatchProduction>();

    public DbSet<BatchProductionIngredientConsumption> BatchProductionIngredientConsumptions => Set<BatchProductionIngredientConsumption>();

    public DbSet<PosOrder> PosOrders => Set<PosOrder>();

    public DbSet<PosOrderLine> PosOrderLines => Set<PosOrderLine>();

    public DbSet<PosOrderNumberSequence> PosOrderNumberSequences => Set<PosOrderNumberSequence>();

    public DbSet<KitchenTicket> KitchenTickets => Set<KitchenTicket>();

    public DbSet<KitchenTicketLine> KitchenTicketLines => Set<KitchenTicketLine>();

    public DbSet<KitchenTicketInventoryDeduction> KitchenTicketInventoryDeductions => Set<KitchenTicketInventoryDeduction>();

    public DbSet<KitchenTicketNumberSequence> KitchenTicketNumberSequences => Set<KitchenTicketNumberSequence>();

    public DbSet<Bill> Bills => Set<Bill>();

    public DbSet<BillLine> BillLines => Set<BillLine>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<BillPrintEvent> BillPrintEvents => Set<BillPrintEvent>();

    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public DbSet<CashDrawerMovement> CashDrawerMovements => Set<CashDrawerMovement>();

    public DbSet<BillNumberSequence> BillNumberSequences => Set<BillNumberSequence>();

    public DbSet<PaymentNumberSequence> PaymentNumberSequences => Set<PaymentNumberSequence>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();

    public DbSet<InventoryLotAllocation> InventoryLotAllocations => Set<InventoryLotAllocation>();

    public DbSet<Vendor> Vendors => Set<Vendor>();

    public DbSet<VendorBill> VendorBills => Set<VendorBill>();

    public DbSet<VendorBillLine> VendorBillLines => Set<VendorBillLine>();

    public DbSet<VendorSettlement> VendorSettlements => Set<VendorSettlement>();

    public DbSet<VendorBillOcrDraft> VendorBillOcrDrafts => Set<VendorBillOcrDraft>();

    public DbSet<VendorBillOcrDraftLine> VendorBillOcrDraftLines => Set<VendorBillOcrDraftLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillSoftDbContext).Assembly);
    }
}
