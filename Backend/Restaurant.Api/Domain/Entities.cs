namespace Restaurant.Api.Domain;

public abstract class Mutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;
    public void Touch() { Version++; UpdatedAt = DateTimeOffset.UtcNow; }
}
public class StaffUser : Mutable
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
}
public class Role { public string Code { get; set; } = ""; public string DisplayName { get; set; } = ""; }
public class UserRole
{
    public Guid UserId { get; set; }
    public string RoleCode { get; set; } = "";
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? GrantedByStaffId { get; set; }
}
public class StaffSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
public class MenuCategory : Mutable
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public class MenuItem : Mutable
{
    public Guid CategoryId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageUrl { get; set; } = "/menu/placeholder.svg";
    public long PriceVnd { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
}
public class RestaurantTable : Mutable
{
    public string Code { get; set; } = "";
    public short Capacity { get; set; } = 4;
    public bool IsActive { get; set; } = true;
    public byte[] QrTokenHash { get; set; } = [];
}
public class Visit : Mutable
{
    public Guid TableId { get; set; }
    public string TableCodeSnapshot { get; set; } = "";
    public string Status { get; set; } = "OPEN";
    public short PartySize { get; set; }
    public Guid OpenedByStaffId { get; set; }
    public byte[] JoinCodeHmac { get; set; } = [];
    public int JoinGeneration { get; set; } = 1;
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedByStaffId { get; set; }
}
public class GuestSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public int JoinGeneration { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
public class Order : Mutable
{
    public string Code { get; set; } = "";
    public string OrderType { get; set; } = "DINE_IN";
    public Guid? VisitId { get; set; }
    public string? PickupCode { get; set; }
    public string? PickupName { get; set; }
    public string Status { get; set; } = "SERVING";
    public long? TotalVnd { get; set; }
    public Guid CreatedByStaffId { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class OrderBatch : Mutable
{
    public Guid OrderId { get; set; }
    public int SequenceNo { get; set; }
    public string Status { get; set; } = "PENDING";
    public Guid? SubmittedByStaffId { get; set; }
    public Guid? SubmittedByGuestSessionId { get; set; }
    public Guid? ReviewedByStaffId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? EligibleAt { get; set; }
}
public class OrderItem : Mutable
{
    public Guid BatchId { get; set; }
    public Guid MenuItemId { get; set; }
    public string MenuCodeSnapshot { get; set; } = "";
    public string MenuNameSnapshot { get; set; } = "";
    public short Quantity { get; set; }
    public long UnitPriceVnd { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "PLACED";
    public DateTimeOffset StatusChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string ReceiptNo { get; set; } = "";
    public string Method { get; set; } = "";
    public long AmountVnd { get; set; }
    public long? TenderedVnd { get; set; }
    public string? TransferReference { get; set; }
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ReceivedByStaffId { get; set; }
}
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string ActorKind { get; set; } = "STAFF";
    public Guid? ActorStaffId { get; set; }
    public Guid? ActorGuestSessionId { get; set; }
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? Reason { get; set; }
    public Guid RequestId { get; set; }
    public Guid CorrelationId { get; set; }
}
public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorStaffId { get; set; }
    public Guid? ActorGuestSessionId { get; set; }
    public Guid Key { get; set; }
    public string Method { get; set; } = "";
    public string Route { get; set; } = "";
    public byte[] RequestHash { get; set; } = [];
    public short ResponseStatus { get; set; } = 200;
    public byte[]? ResponseCiphertext { get; set; }
    public string? EncryptionKeyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ReplayUntil { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}
