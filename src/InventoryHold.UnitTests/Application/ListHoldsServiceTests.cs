using FluentAssertions;
using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Exceptions;
using InventoryHold.Domain.Messaging;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using InventoryHold.WebApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class ListHoldsServiceTests
{
    private readonly Mock<IHoldRepository> _holds = new();
    private readonly HoldService _service;

    public ListHoldsServiceTests()
    {
        _service = new HoldService(
            _holds.Object, Mock.Of<IInventoryRepository>(),
            Mock.Of<ISettingsRepository>(), Mock.Of<ITransactionFactory>(),
            Options.Create(new HoldSettings()), Mock.Of<IInventoryCache>(),
            Mock.Of<IHoldEventPublisher>(), NullLogger<HoldService>.Instance);
    }

    [Fact]
    public async Task ListHoldsAsync_DefaultParams_CallsRepoWithStatus()
    {
        _holds.Setup(r => r.GetPagedAsync("active", 1, 20, default))
              .ReturnsAsync((Array.Empty<Hold>() as IReadOnlyList<Hold>, 0L));

        await _service.ListHoldsAsync("active", 1, 20);

        _holds.Verify(r => r.GetPagedAsync("active", 1, 20, default), Times.Once);
    }

    [Fact]
    public async Task ListHoldsAsync_StatusFilter_PassesFilterToRepo()
    {
        _holds.Setup(r => r.GetPagedAsync("expired", 1, 20, default))
              .ReturnsAsync((Array.Empty<Hold>() as IReadOnlyList<Hold>, 0L));

        await _service.ListHoldsAsync("expired", 1, 20);

        _holds.Verify(r => r.GetPagedAsync("expired", 1, 20, default), Times.Once);
    }

    [Fact]
    public async Task ListHoldsAsync_PageSizeTooLarge_ThrowsDomainException()
    {
        await FluentActions.Invoking(() => _service.ListHoldsAsync(null, 1, 101))
            .Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ListHoldsAsync_PageSizeTooSmall_ThrowsDomainException()
    {
        await FluentActions.Invoking(() => _service.ListHoldsAsync(null, 1, 0))
            .Should().ThrowAsync<DomainException>();
    }
}
