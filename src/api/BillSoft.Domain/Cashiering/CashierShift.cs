using BillSoft.Domain.Billing;
using BillSoft.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillSoft.Domain.Cashiering;

public sealed class CashierShift : BaseEntity
{
    public Guid CashierShiftId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid OpenedByUserId { get; set; }

    [NotMapped]
    public Guid CashierUserId
    {
        get => OpenedByUserId;
        set => OpenedByUserId = value;
    }

    public Guid? ClosedByUserId { get; set; }

    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;

    public DateTime BusinessDate { get; set; } = DateTime.UtcNow.Date;

    public decimal OpeningCashAmount { get; set; }

    public decimal ExpectedCashAmount { get; set; }

    [NotMapped]
    public decimal ExpectedClosingCashAmount
    {
        get => ExpectedCashAmount;
        set => ExpectedCashAmount = value;
    }

    public decimal? CountedCashAmount { get; set; }

    [NotMapped]
    public decimal? DeclaredClosingCashAmount
    {
        get => CountedCashAmount;
        set => CountedCashAmount = value;
    }

    public decimal? CashVarianceAmount { get; set; }

    public DateTimeOffset OpenedAt { get; set; } = UtcNow();

    [NotMapped]
    public DateTimeOffset OpenedAtUtc
    {
        get => OpenedAt;
        set => OpenedAt = value;
    }

    public DateTimeOffset? ClosedAt { get; set; }

    [NotMapped]
    public DateTimeOffset? ClosedAtUtc
    {
        get => ClosedAt;
        set => ClosedAt = value;
    }

    public string? OpeningNote { get; set; }

    public string? ClosingNote { get; set; }

    [NotMapped]
    public string? CloseNotes
    {
        get => ClosingNote;
        set => ClosingNote = value;
    }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    [NotMapped]
    public DateTimeOffset CreatedAtUtc
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    public DateTimeOffset? UpdatedAt { get; set; }

    [NotMapped]
    public DateTimeOffset? UpdatedAtUtc
    {
        get => UpdatedAt;
        set => UpdatedAt = value;
    }

    public ICollection<CashDrawerMovement> CashDrawerMovements { get; set; } = new List<CashDrawerMovement>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
