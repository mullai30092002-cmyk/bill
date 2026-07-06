using BillSoft.Application.Auth;
using BillSoft.Application.Kitchen;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Orders;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Kitchen;

internal static class KitchenTicketWorkflow
{
    internal static async Task<KitchenTicket> CreateAsync(
        BillSoftDbContext context,
        AuthUserContext currentUser,
        PosOrder order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (order.PosOrderLines.Count == 0)
        {
            throw new InvalidOperationException("At least one POS order line is required.");
        }

        var existingActiveTicket = await context.KitchenTickets
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == order.RestaurantId &&
                entity.PosOrderId == order.PosOrderId &&
                entity.Status != KitchenTicketStatus.Cancelled,
                cancellationToken);

        if (existingActiveTicket is not null)
        {
            throw new InvalidOperationException("A kitchen ticket already exists for this POS order.");
        }

        var ticketDate = ResolveSequenceDate(now);
        var ticket = new KitchenTicket
        {
            RestaurantId = order.RestaurantId,
            BranchId = order.BranchId,
            PosOrderId = order.PosOrderId,
            TicketNumber = await AllocateTicketNumberAsync(context, order.RestaurantId, order.BranchId, ticketDate, now, cancellationToken),
            Status = KitchenTicketStatus.Pending,
            OrderNumberSnapshot = order.OrderNumber,
            OrderTypeSnapshot = order.OrderType.ToString(),
            TableNameSnapshot = order.TableName,
            CustomerNameSnapshot = order.CustomerName,
            OrderNotesSnapshot = order.Notes,
            CreatedByUserId = currentUser.UserId,
            CreatedAt = now
        };

        foreach (var (line, index) in order.PosOrderLines
            .OrderBy(line => line.DisplayOrder)
            .Select((line, index) => (line, index)))
        {
            ticket.KitchenTicketLines.Add(new KitchenTicketLine
            {
                RestaurantId = order.RestaurantId,
                PosOrderLineId = line.PosOrderLineId,
                MenuItemId = line.MenuItemId,
                MenuCategoryId = line.MenuCategoryId,
                MenuItemNameSnapshot = line.MenuItemNameSnapshot,
                MenuCategoryNameSnapshot = line.MenuCategoryNameSnapshot,
                SkuSnapshot = line.SkuSnapshot,
                Quantity = line.Quantity,
                Notes = line.Notes,
                DisplayOrder = index + 1,
                CreatedAt = now
            });
        }

        context.KitchenTickets.Add(ticket);
        return ticket;
    }

    internal static KitchenTicketDetail ToDetail(KitchenTicket ticket)
    {
        return new KitchenTicketDetail(
            ticket.KitchenTicketId,
            ticket.RestaurantId,
            ticket.BranchId,
            ticket.PosOrderId,
            ticket.TicketNumber,
            ticket.OrderNumberSnapshot,
            ticket.OrderTypeSnapshot,
            ticket.TableNameSnapshot,
            ticket.CustomerNameSnapshot,
            ticket.OrderNotesSnapshot,
            ticket.Status.ToString(),
            ticket.CreatedByUserId,
            ticket.LastStatusChangedByUserId,
            ticket.CancelledByUserId,
            ticket.CancelledAt,
            ticket.CancelReason,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.PreparingAt,
            ticket.ReadyAt,
            ticket.ServedAt,
            ticket.InventoryDeductionStatus.ToString(),
            ticket.KitchenTicketLines
                .OrderBy(line => line.DisplayOrder)
                .Select(line => new KitchenTicketLineDetail(
                    line.KitchenTicketLineId,
                    line.PosOrderLineId,
                    line.MenuItemId,
                    line.MenuCategoryId,
                    line.MenuItemNameSnapshot,
                    line.MenuCategoryNameSnapshot,
                    line.SkuSnapshot,
                    line.Quantity,
                    line.Notes,
                    line.DisplayOrder,
                    line.CreatedAt))
                .ToArray());
    }

    private static async Task<string> AllocateTicketNumberAsync(
        BillSoftDbContext context,
        Guid restaurantId,
        Guid branchId,
        DateTime ticketDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sequence = await context.KitchenTicketNumberSequences
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.TicketDate == ticketDate,
                cancellationToken);

        if (sequence is null)
        {
            sequence = new KitchenTicketNumberSequence
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                TicketDate = ticketDate,
                LastSequence = 1,
                CreatedAt = now
            };

            context.KitchenTicketNumberSequences.Add(sequence);
        }
        else
        {
            context.Attach(sequence);
            sequence.LastSequence += 1;
            sequence.UpdatedAt = now;
        }

        return $"KIT-{ticketDate:yyyyMMdd}-{sequence.LastSequence:0000}";
    }

    private static DateTime ResolveSequenceDate(DateTimeOffset dateTimeOffset)
    {
        return DateTime.SpecifyKind(dateTimeOffset.UtcDateTime.Date, DateTimeKind.Utc);
    }
}
