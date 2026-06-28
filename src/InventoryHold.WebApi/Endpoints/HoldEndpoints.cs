using InventoryHold.Contracts.Requests;
using InventoryHold.Contracts.Responses;
using InventoryHold.Domain.Entities;
using InventoryHold.WebApi.Services;

namespace InventoryHold.WebApi.Endpoints;

public static class HoldEndpoints
{
    public static void MapHoldEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/holds").WithTags("Holds");

        group.MapPost("/", async (
            CreateHoldRequest request,
            HoldService service,
            CancellationToken ct) =>
        {
            var hold = await service.CreateHoldAsync(request, ct);
            return Results.Created($"/api/holds/{hold.Id}", ToResponse(hold));
        })
        .WithName("CreateHold")
        .WithSummary("Create an inventory hold")
        .Produces<HoldResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{holdId}", async (string holdId, HoldService service, CancellationToken ct) =>
            Results.Ok(ToResponse(await service.GetHoldAsync(holdId, ct))))
        .WithName("GetHold")
        .WithSummary("Get a hold by ID")
        .Produces<HoldResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{holdId}", async (string holdId, HoldService service, CancellationToken ct) =>
            Results.Ok(ToResponse(await service.ReleaseHoldAsync(holdId, ct))))
        .WithName("ReleaseHold")
        .WithSummary("Release an active hold and restore inventory")
        .Produces<HoldResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status410Gone);

        group.MapGet("/", async (
            HoldService service, CancellationToken ct,
            string? status, int page = 1, int pageSize = 20) =>
        {
            var (items, total) = await service.ListHoldsAsync(status, page, pageSize, ct);
            var totalPages = (int)Math.Ceiling((double)total / pageSize);
            return Results.Ok(new PagedResponse<HoldResponse>(
                items.Select(ToResponse).ToList(), total, page, pageSize, totalPages));
        })
        .WithName("ListHolds")
        .WithSummary("List holds with optional status filter and pagination")
        .Produces<PagedResponse<HoldResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static HoldResponse ToResponse(Hold hold) => new(
        hold.Id, hold.CustomerName, hold.Status.ToString(),
        hold.Items.Select(i => new HoldItemResponse(i.ProductId, i.ProductName, i.Quantity)).ToList(),
        hold.CreatedAt, hold.ExpiresAt, hold.ReleasedAt, hold.ExpiredAt);
}
