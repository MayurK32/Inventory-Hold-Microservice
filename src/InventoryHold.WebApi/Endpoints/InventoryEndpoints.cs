using InventoryHold.Contracts.Responses;
using InventoryHold.Domain.Entities;
using InventoryHold.WebApi.Services;

namespace InventoryHold.WebApi.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory");

        group.MapGet("/", async (InventoryService service, CancellationToken ct) =>
            Results.Ok((await service.GetInventoryAsync(ct)).Select(ToResponse).ToList()))
        .WithName("GetInventory")
        .Produces<List<InventoryItemResponse>>(StatusCodes.Status200OK);

        group.MapPost("/reset", async (InventoryService service, CancellationToken ct) =>
            Results.Ok((await service.ResetInventoryAsync(ct)).Select(ToResponse).ToList()))
        .WithName("ResetInventory")
        .Produces<List<InventoryItemResponse>>(StatusCodes.Status200OK);
    }

    private static InventoryItemResponse ToResponse(InventoryItem i) =>
        new(i.ProductId, i.Name, i.TotalQuantity, i.AvailableQuantity, i.HeldQuantity);
}
