namespace Properties.Tests;

using Properties.Application;
using Properties.Application.Commands;
using Properties.Application.Queries;
using Properties.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Application.Events;
using Gma.Framework.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesApplicationRegistrationTests
{
    [Fact]
    public void Properties_application_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddPropertiesApplication();
        services.AddPropertiesApplication();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<CreatePropertyCommand, PropertyDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<UpdatePropertyCommand, PropertyDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<CreateRoomCommand, RoomDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<UpdateRoomCommand, RoomDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<RetireRoomCommand, Unit>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<AddBedCommand, BedDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<UpdateBedCommand, BedDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<RetireBedCommand, Unit>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<GetPropertyQuery, PropertyDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<ListPropertiesQuery, PropertyListResponse>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<GetRoomQuery, RoomDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<ListRoomsQuery, RoomListResponse>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<ListBedsQuery, BedListResponse>));
        Assert.Equal(8, services.Count(descriptor => IsDomainEventHandler(descriptor.ServiceType)));
    }

    [Fact]
    public void Properties_application_registration_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => DependencyInjection.AddPropertiesApplication(null!));
    }

    private static bool IsDomainEventHandler(Type serviceType) =>
        serviceType.IsGenericType &&
        serviceType.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>);
}
