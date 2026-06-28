using InventoryHold.Contracts.Requests;
using InventoryHold.Contracts.Responses;
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
            var response = new HoldResponse(
                hold.Id, hold.CustomerName, hold.Status.ToString(),
                hold.Items
                    .Select(i => new HoldItemResponse(i.ProductId, i.ProductName, i.Quantity))
                    .ToList(),
                hold.CreatedAt, hold.ExpiresAt, hold.ReleasedAt, hold.ExpiredAt);
            return Results.Created($"/api/holds/{hold.Id}", response);
        })
        .WithName("CreateHold")
        .Produces<HoldResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
