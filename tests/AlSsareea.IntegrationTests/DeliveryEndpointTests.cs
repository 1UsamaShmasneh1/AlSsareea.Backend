using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Delivery.Domain;
using AlSsareea.Modules.Delivery.Infrastructure.Persistence;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Orders.Domain;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class DeliveryEndpointTests(PostgresFixture fixture)
{
    private const string Password = "Valid-Password-123!";

    [Fact]
    public async Task DeliveryEndpointsRequireAuthentication()
    {
        using HttpClient client = fixture.ApiFactory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/deliveries/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/v1/deliveries/{Guid.NewGuid()}/complete", new DeliveryTransitionRequest(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task EligibleOrderCreatesOneDeliveryAndReturnsPinOnlyOnce()
    {
        (_, HttpClient operations) = await AuthenticatedClient(UserType.Administrator, DeliveryPermissions.Manage);
        Guid customerUserId = Guid.NewGuid();
        AlSsareea.Modules.Customers.Domain.Customer customer = AlSsareea.Modules.Customers.Domain.Customer.Create(AlSsareea.Modules.Customers.Domain.CustomerId.New(), customerUserId, "Delivery", "Customer", null, DateTime.UtcNow, customerUserId);
        Order order = ReadyOrder(customer.Id.Value);
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            CustomersDbContext customers = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
            OrdersDbContext orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            customers.Customers.Add(customer); orders.Orders.Add(order);
            await customers.SaveChangesAsync(); await orders.SaveChangesAsync();
        }

        SetKey(operations, "create-delivery-key");
        HttpResponseMessage createdMessage = await operations.PostAsJsonAsync("/api/v1/deliveries/", new CreateDeliveryRequest(order.Id.Value, (short)(ProofRequirement.Pin | ProofRequirement.RecipientName)));
        Assert.Equal(HttpStatusCode.Created, createdMessage.StatusCode);
        DeliveryCreatedResponse created = (await createdMessage.Content.ReadFromJsonAsync<DeliveryCreatedResponse>())!;
        Assert.Equal(6, created.Pin!.Length); Assert.All(created.Pin, character => Assert.True(char.IsDigit(character)));

        HttpResponseMessage retryMessage = await operations.PostAsJsonAsync("/api/v1/deliveries/", new CreateDeliveryRequest(order.Id.Value, (short)(ProofRequirement.Pin | ProofRequirement.RecipientName)));
        Assert.Equal(HttpStatusCode.OK, retryMessage.StatusCode);
        DeliveryCreatedResponse retried = (await retryMessage.Content.ReadFromJsonAsync<DeliveryCreatedResponse>())!;
        Assert.Equal(created.Delivery.Id, retried.Delivery.Id); Assert.Null(retried.Pin);

        Guid driverId = await PersistActiveDriver(Guid.NewGuid()); SetKey(operations, "assign-created-delivery-key");
        HttpResponseMessage assignmentMessage = await operations.PostAsJsonAsync($"/api/v1/deliveries/{created.Delivery.Id}/assign", new AssignDeliveryRequest(driverId, created.Delivery.ConcurrencyStamp));
        Assert.Equal(HttpStatusCode.OK, assignmentMessage.StatusCode);
    }

    [Fact]
    public async Task DriverCanCompleteAssignedJourneyWithIdempotentRetryAndCustomerIdorIsDenied()
    {
        (Guid driverUserId, HttpClient driverClient) = await AuthenticatedClient(UserType.Driver, DeliveryPermissions.OperateSelf, DeliveryPermissions.ReadSelf);
        Guid driverId = await PersistActiveDriver(driverUserId); Guid customerUserId = Guid.NewGuid(); DeliveryAggregate delivery = await PersistAssignedDelivery(driverId, customerUserId, ProofRequirement.RecipientName);

        DeliveryResponse heading = await Transition(driverClient, delivery.Id.Value, "heading-to-pickup", delivery.ConcurrencyStamp, "heading-key");
        DeliveryResponse retried = await Transition(driverClient, delivery.Id.Value, "heading-to-pickup", delivery.ConcurrencyStamp, "heading-key"); Assert.Equal(heading.ConcurrencyStamp, retried.ConcurrencyStamp);
        DeliveryResponse pickupArrival = await Transition(driverClient, delivery.Id.Value, "arrive-at-pickup", heading.ConcurrencyStamp, "pickup-arrival-key");
        DeliveryResponse pickedUp = await Transition(driverClient, delivery.Id.Value, "confirm-pickup", pickupArrival.ConcurrencyStamp, "pickup-key");
        DeliveryResponse started = await Transition(driverClient, delivery.Id.Value, "start", pickedUp.ConcurrencyStamp, "start-key");
        DeliveryResponse dropOff = await Transition(driverClient, delivery.Id.Value, "arrive-at-drop-off", started.ConcurrencyStamp, "dropoff-key");
        SetKey(driverClient, "invalid-media-key"); HttpResponseMessage invalidMedia = await driverClient.PostAsJsonAsync($"/api/v1/deliveries/{delivery.Id.Value}/proofs", new SubmitDeliveryProofRequest((short)DeliveryProofType.Photo, null, Guid.NewGuid(), null, dropOff.ConcurrencyStamp)); Assert.Equal(HttpStatusCode.BadRequest, invalidMedia.StatusCode);
        SetKey(driverClient, "proof-key"); HttpResponseMessage proofResponse = await driverClient.PostAsJsonAsync($"/api/v1/deliveries/{delivery.Id.Value}/proofs", new SubmitDeliveryProofRequest((short)DeliveryProofType.RecipientName, null, null, " Actual Recipient ", dropOff.ConcurrencyStamp)); Assert.Equal(HttpStatusCode.OK, proofResponse.StatusCode); DeliveryResponse proof = (await proofResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!;
        DeliveryResponse completed = await Transition(driverClient, delivery.Id.Value, "complete", proof.ConcurrencyStamp, "complete-key"); Assert.Equal((short)DeliveryStatus.Delivered, completed.Status); Assert.Equal(8, completed.Timeline.Count); Assert.Single(completed.Proofs);
        await using (AsyncServiceScope verificationScope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            DeliveryDbContext db = verificationScope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            Assert.Equal(7L, await CountForDelivery(db, "delivery_audit", delivery.Id.Value));
            Assert.Equal(7L, await CountForDelivery(db, "delivery_operation_idempotency", delivery.Id.Value));
            Assert.Equal(7L, await CountOutboxForDelivery(db, delivery.Id.Value));
        }

        (_, HttpClient wrongCustomer) = await AuthenticatedClient(UserType.Customer, DeliveryPermissions.ReadOwn); Assert.Equal(HttpStatusCode.NotFound, (await wrongCustomer.GetAsync($"/api/v1/deliveries/{delivery.Id.Value}")).StatusCode);
        (Guid ownerId, HttpClient owner) = await AuthenticatedClient(UserType.Customer, DeliveryPermissions.ReadOwn); DeliveryAggregate owned = await PersistAssignedDelivery(driverId, ownerId, ProofRequirement.None); Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/deliveries/{owned.Id.Value}")).StatusCode);
    }

    [Fact]
    public async Task DriverCannotOperateAnotherDriversDeliveryAndFailureIsTerminal()
    {
        (Guid assignedUserId, HttpClient assignedClient) = await AuthenticatedClient(UserType.Driver, DeliveryPermissions.OperateSelf); Guid assignedDriverId = await PersistActiveDriver(assignedUserId);
        (Guid otherUserId, HttpClient otherClient) = await AuthenticatedClient(UserType.Driver, DeliveryPermissions.OperateSelf); await PersistActiveDriver(otherUserId);
        DeliveryAggregate delivery = await PersistAssignedDelivery(assignedDriverId, Guid.NewGuid(), ProofRequirement.None);
        SetKey(otherClient, "idor-key"); Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PostAsJsonAsync($"/api/v1/deliveries/{delivery.Id.Value}/heading-to-pickup", new DeliveryTransitionRequest(delivery.ConcurrencyStamp))).StatusCode);
        SetKey(assignedClient, "fail-key"); HttpResponseMessage failed = await assignedClient.PostAsJsonAsync($"/api/v1/deliveries/{delivery.Id.Value}/fail", new ReportFailedDeliveryRequest((short)DeliveryFailureReason.RecipientUnavailable, "No answer", delivery.ConcurrencyStamp)); Assert.Equal(HttpStatusCode.OK, failed.StatusCode); DeliveryResponse response = (await failed.Content.ReadFromJsonAsync<DeliveryResponse>())!; Assert.Equal((short)DeliveryStatus.Failed, response.Status); Assert.Equal((short)DeliveryFailureReason.RecipientUnavailable, response.FailureReason);
        SetKey(assignedClient, "after-fail-key"); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await assignedClient.PostAsJsonAsync($"/api/v1/deliveries/{delivery.Id.Value}/heading-to-pickup", new DeliveryTransitionRequest(response.ConcurrencyStamp))).StatusCode);
    }

    private static async Task<DeliveryResponse> Transition(HttpClient client, Guid deliveryId, string operation, Guid stamp, string key)
    {
        SetKey(client, key); HttpResponseMessage response = await client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/{operation}", new DeliveryTransitionRequest(stamp)); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<DeliveryResponse>())!;
    }
    private static void SetKey(HttpClient client, string key) { client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", key); }

    private async Task<DeliveryAggregate> PersistAssignedDelivery(Guid driverId, Guid customerUserId, ProofRequirement requirements)
    {
        DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = DeliveryAggregate.Create(DeliveryId.New(), Guid.NewGuid(), Guid.NewGuid(), customerUserId, new PickupSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Pickup", "Merchant", null, null, 31.7, 35.2), new DropOffSnapshot(Guid.NewGuid(), "Drop-off", "Recipient", null, null, null, 31.8, 35.3), requirements, null, null, now); delivery.Assign(driverId, now.AddSeconds(1));
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DeliveryDbContext db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>(); db.Deliveries.Add(delivery); await db.SaveChangesAsync(); return delivery;
    }

    private async Task<Guid> PersistActiveDriver(Guid userId)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); DateTime now = DateTime.UtcNow; Driver driver = Driver.Create(DriverId.New(), userId, "Delivery Driver", EmploymentType.Employee, 4, null, now); driver.SubmitForReview(now); driver.Approve(now); driver.Activate(now); db.Drivers.Add(driver); await db.SaveChangesAsync(); return driver.Id.Value;
    }

    private static Order ReadyOrder(Guid customerId)
    {
        DateTime now = DateTime.UtcNow; Guid merchant = Guid.NewGuid(); Guid branch = Guid.NewGuid(); Guid actor = Guid.NewGuid();
        OrderItemInput item = new(Guid.NewGuid(), 1, null, "Item", null, null, 1, 1000, 0, 0, 1000, 1000, 0, 1000, null, []);
        Order order = Order.Create(OrderId.New(), Guid.NewGuid().ToString("N").ToUpperInvariant(), customerId, merchant, branch, Guid.NewGuid(), OrderType.Restaurant, new(1000, 0, 0, 0, 0, 100, 50, 25, 0, 25, 1200, "ILS", null, now), new(customerId, "Customer", "+970599123456", "ar"), new(Guid.NewGuid(), "Home", "City", null, "Street", "1", null, null, "Door", 31.8, 35.3, null, "Street 1, City"), new(merchant, branch, "Merchant", "Branch", "Branch Street", "+970599000001"), [item], null, null, now);
        order.MarkPaymentAuthorized(now.AddSeconds(1)); order.Submit(now.AddSeconds(2)); order.AcceptByMerchant(now.AddSeconds(3), actor); order.StartPreparing(now.AddSeconds(4), actor); order.MarkReadyForPickup(now.AddSeconds(5), actor); return order;
    }

    private static async Task<long> CountForDelivery(DeliveryDbContext db, string table, Guid deliveryId)
    {
        if (table is not ("delivery_audit" or "delivery_operation_idempotency")) throw new ArgumentOutOfRangeException(nameof(table));
        await db.Database.OpenConnectionAsync(); await using System.Data.Common.DbCommand command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = $"SELECT count(*) FROM delivery.{table} WHERE delivery_id = @id"; System.Data.Common.DbParameter parameter = command.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = deliveryId; command.Parameters.Add(parameter); return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountOutboxForDelivery(DeliveryDbContext db, Guid deliveryId)
    {
        await db.Database.OpenConnectionAsync(); await using System.Data.Common.DbCommand command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT count(*) FROM delivery.outbox_messages WHERE payload::jsonb ->> 'deliveryId' = @id"; System.Data.Common.DbParameter parameter = command.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = deliveryId.ToString(); command.Parameters.Add(parameter); return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<(Guid UserId, HttpClient Client)> AuthenticatedClient(UserType type, params string[] permissions)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>(); DateTime now = DateTime.UtcNow; string suffix = Guid.NewGuid().ToString("N"); string email = $"delivery-{suffix}@example.com"; User user = User.Create(UserId.New(), type, new Email(email), null, new PasswordHash(hasher.Hash(Password).EncodedHash), now); user.Activate(now); Role role = Role.Create(RoleId.New(), "delivery-role-" + suffix, null, false, now); user.AssignRole(role.Id, now);
        foreach (string name in permissions) { Permission? permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name); if (permission is null) { permission = Permission.Create(PermissionId.New(), name, name, null, "delivery", false, now); db.Add(permission); } role.AssignPermission(permission.Id, now); }
        db.AddRange(user, role); await db.SaveChangesAsync(); HttpClient client = fixture.ApiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }); LoginRequest login = new(email, Password, new LoginDeviceRequest("delivery-" + suffix, "Delivery test", DevicePlatform.Android, "1.0", "15")); TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login)).Content.ReadFromJsonAsync<TokenResponse>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); return (user.Id.Value, client);
    }
}
