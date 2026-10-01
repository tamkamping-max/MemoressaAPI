using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class InternalRealtimeServiceTests
{
    [Fact]
    public async Task GetPendingCommandsAsync_IncludesDeliveredAndRedeliversWithoutUpdatingDeliveredAt()
    {
        var deviceId = Guid.NewGuid();
        var deliveredAt = DateTime.UtcNow.AddMinutes(-10);

        await using var db = CreateDb();
        db.DisplayDevices.Add(new DisplayDevice
        {
            Id = deviceId,
            FamilyId = Guid.NewGuid(),
            Name = "Frame",
            QrCode = Guid.NewGuid().ToString()
        });

        var deliveredCommand = new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Delivered,
            DeliveredAt = deliveredAt,
            PayloadJson = "{}"
        };
        db.FrameCommands.Add(deliveredCommand);
        await db.SaveChangesAsync();

        var service = new InternalRealtimeService(db);
        var result = await service.GetPendingCommandsAsync(deviceId);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(deliveredCommand.Id, result.Data![0].Id);

        var reloaded = await db.FrameCommands.SingleAsync(c => c.Id == deliveredCommand.Id);
        Assert.Equal(FrameCommandStatus.Delivered, reloaded.Status);
        Assert.Equal(deliveredAt, reloaded.DeliveredAt!.Value, precision: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetPendingCommandsAsync_MarksPendingAsDeliveredOnce()
    {
        var deviceId = Guid.NewGuid();

        await using var db = CreateDb();
        db.DisplayDevices.Add(new DisplayDevice
        {
            Id = deviceId,
            FamilyId = Guid.NewGuid(),
            Name = "Frame",
            QrCode = Guid.NewGuid().ToString()
        });

        var pending = new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = "{}"
        };
        db.FrameCommands.Add(pending);
        await db.SaveChangesAsync();

        var service = new InternalRealtimeService(db);
        var first = await service.GetPendingCommandsAsync(deviceId);
        Assert.True(first.Success);
        Assert.Single(first.Data!);

        var afterFirst = await db.FrameCommands.SingleAsync(c => c.Id == pending.Id);
        Assert.Equal(FrameCommandStatus.Delivered, afterFirst.Status);
        Assert.NotNull(afterFirst.DeliveredAt);
        var firstDeliveredAt = afterFirst.DeliveredAt!.Value;

        var second = await service.GetPendingCommandsAsync(deviceId);
        Assert.True(second.Success);
        Assert.Single(second.Data!);

        var afterSecond = await db.FrameCommands.SingleAsync(c => c.Id == pending.Id);
        Assert.Equal(firstDeliveredAt, afterSecond.DeliveredAt!.Value);
    }

    [Fact]
    public async Task AcknowledgeCommandAsync_MovesDeliveredToAcknowledged()
    {
        var deviceId = Guid.NewGuid();

        await using var db = CreateDb();
        db.DisplayDevices.Add(new DisplayDevice
        {
            Id = deviceId,
            FamilyId = Guid.NewGuid(),
            Name = "Frame",
            QrCode = Guid.NewGuid().ToString()
        });

        var command = new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Delivered,
            DeliveredAt = DateTime.UtcNow,
            PayloadJson = "{}"
        };
        db.FrameCommands.Add(command);
        await db.SaveChangesAsync();

        var service = new InternalRealtimeService(db);
        var ack = await service.AcknowledgeCommandAsync(
            command.Id,
            new Memoressa.Application.DTOs.AckFrameCommandRequestDto { Success = true });

        Assert.True(ack.Success);

        var poll = await service.GetPendingCommandsAsync(deviceId);
        Assert.True(poll.Success);
        Assert.Empty(poll.Data!);

        var reloaded = await db.FrameCommands.SingleAsync(c => c.Id == command.Id);
        Assert.Equal(FrameCommandStatus.Acknowledged, reloaded.Status);
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MemoressaDbContext(options);
    }
}
