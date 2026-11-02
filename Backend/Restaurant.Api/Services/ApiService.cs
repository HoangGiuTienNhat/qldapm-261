using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Data;
using Restaurant.Api.Domain;

namespace Restaurant.Api.Services;
public record Actor(Guid Id, bool IsGuest, Guid? VisitId, string[] Roles, string DisplayName, bool MustChangePassword = false)
{
    public bool Has(params string[] roles) => !IsGuest && Roles.Intersect(roles).Any();
}
public partial class ApiService(AppDb db, IDataProtectionProvider protection, IConfiguration config)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector protector = protection.CreateProtector("restaurant.idempotency.v1");
    private readonly IDataProtector qrProtector = protection.CreateProtector("restaurant.table-qr.v1");
    private readonly PasswordHasher<StaffUser> passwords = new();
    public Actor? User { get; private set; }
    public HttpContext Http { get; private set; } = null!;
    private Guid RequestId { get; } = Guid.NewGuid();
    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public async Task Authenticate(HttpContext ctx)
    {
        Http = ctx;
        var now = DateTimeOffset.UtcNow;
        if (ctx.Request.Headers["X-Actor"] != "guest" && ctx.Request.Cookies.TryGetValue("ros_staff", out var staffToken))
        {
            var h = Hash(staffToken);
            var session = await db.StaffSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == h && s.RevokedAt == null && s.ExpiresAt > now);
            if (session != null)
            {
                var staff = await db.StaffUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId && u.IsActive);
                if (staff != null) User = new(staff.Id, false, null, await db.UserRoles.Where(r => r.UserId == staff.Id).Select(r => r.RoleCode).ToArrayAsync(), staff.DisplayName, staff.MustChangePassword);
            }
        }
        if (User == null && ctx.Request.Cookies.TryGetValue("ros_guest", out var guestToken))
        {
            var h = Hash(guestToken);
            var session = await db.GuestSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == h && s.RevokedAt == null && s.ExpiresAt > now);
            if (session != null && await db.Visits.AnyAsync(v => v.Id == session.VisitId && v.Status == "OPEN"))
                User = new(session.Id, true, session.VisitId, ["CUSTOMER"], "Khách tại bàn");
        }
        if (User != null) ctx.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, User.Id.ToString())], "session"));
    }
    public Actor Require(params string[] roles)
    {
        Rules.Require(User != null, "UNAUTHENTICATED", "Vui lòng đăng nhập hoặc tham gia lượt khách.", 401);
        Rules.Require(!User!.MustChangePassword || Http.Request.Path.StartsWithSegments("/api/v1/auth"), "PASSWORD_CHANGE_REQUIRED", "Vui lòng đổi mật khẩu trước khi tiếp tục.", 403);
        if (roles.Length > 0) Rules.Require(User.Has(roles), "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", 403);
        return User;
    }
    public static string Text(JsonObject b, string key, int max = 500, bool optional = false)
    {
        var value = b[key]?.GetValue<string>()?.Trim();
        Rules.Require(optional || !string.IsNullOrWhiteSpace(value), "VALIDATION_FAILED", $"Thiếu trường {key}.", 422);
        Rules.Require(value == null || value.Length <= max, "VALIDATION_FAILED", $"{key} tối đa {max} ký tự.", 422);
        return value ?? "";
    }
    public static long Number(JsonObject b, string key, long min = 0, long max = Rules.MoneyLimit)
    {
        Rules.Require(b[key] != null && long.TryParse(b[key]!.ToString(), out _), "VALIDATION_FAILED", $"{key} phải là số nguyên.", 422);
        var n = long.Parse(b[key]!.ToString());
        Rules.Require(n >= min && n <= max, "VALIDATION_FAILED", $"{key} phải trong khoảng {min}..{max}.", 422);
        return n;
    }
    public static Guid Id(JsonObject b, string key)
    {
        Rules.Require(Guid.TryParse(b[key]?.ToString(), out _), "VALIDATION_FAILED", $"{key} phải là UUID.", 422);
        return Guid.Parse(b[key]!.ToString());
    }
    public static bool Flag(JsonObject b, string key, bool fallback = true) => b[key]?.GetValue<bool>() ?? fallback;
    private static string? Optional(JsonObject b, string key, int max = 500) { var s = Text(b, key, max, true); return s.Length == 0 ? null : s; }
    private static void CheckVersion(Mutable entity, JsonObject b, string name = "expectedVersion") => Rules.Version(entity, Number(b, name, 1, long.MaxValue));
    public static async Task<JsonObject> Body(HttpContext ctx) => (await JsonNode.ParseAsync(ctx.Request.Body))?.AsObject() ?? throw new ApiException(422, "VALIDATION_FAILED", "Cần JSON object.");
    private void Cookie(string name, string token, int hours) => Http.Response.Cookies.Append(name, token, new CookieOptions { HttpOnly = true, Secure = Http.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/", MaxAge = TimeSpan.FromHours(hours) });
    private void Audit(string action, string type, Guid id, object? before, object? after, string? reason = null)
    {
        db.AuditEvents.Add(new() { ActorKind = User == null ? "SYSTEM" : User.IsGuest ? "GUEST" : "STAFF", ActorStaffId = User is { IsGuest: false } ? User.Id : null,
            ActorGuestSessionId = User is { IsGuest: true } ? User.Id : null, Action = action, TargetType = type, TargetId = id,
            BeforeData = before == null ? null : JsonSerializer.Serialize(before, Json), AfterData = after == null ? null : JsonSerializer.Serialize(after, Json), Reason = reason, RequestId = RequestId, CorrelationId = RequestId });
    }
    public async Task<IResult> Command(JsonObject body, Func<Task<object>> action, bool keyRequired = true)
    {
        var actor = Require();
        Guid? key = null;
        if (Http.Request.Headers.TryGetValue("Idempotency-Key", out var h))
        { Rules.Require(Guid.TryParse(h, out _), "INVALID_IDEMPOTENCY_KEY", "Idempotency-Key phải là UUID.", 400); key = Guid.Parse(h!); }
        Rules.Require(!keyRequired || key != null, "INVALID_IDEMPOTENCY_KEY", "Cần header Idempotency-Key.", 400);
        await using var tx = await db.Database.BeginTransactionAsync();
        byte[] requestHash = Hash(Http.Request.Method + Http.Request.Path + Canonical(body));
        if (key.HasValue)
        {
            var lockId = BitConverter.ToInt64(Hash($"{actor.Id}:{key}"), 0);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})");
            var previous = await db.IdempotencyRecords.SingleOrDefaultAsync(r => r.Key == key && (actor.IsGuest ? r.ActorGuestSessionId == actor.Id : r.ActorStaffId == actor.Id));
            if (previous != null)
            {
                Rules.Require(previous.RequestHash.SequenceEqual(requestHash), "IDEMPOTENCY_KEY_REUSED", "Key đã được dùng cho yêu cầu khác.");
                Rules.Require(previous.ReplayUntil > DateTimeOffset.UtcNow && previous.ResponseCiphertext != null, "IDEMPOTENCY_REPLAY_EXPIRED", "Kết quả đã hết hạn replay. Hãy tra đơn hiện tại.", 410);
                var raw = Encoding.UTF8.GetString(protector.Unprotect(previous.ResponseCiphertext!));
                return Results.Content(raw, "application/json", statusCode: previous.ResponseStatus);
            }
        }
        var result = await action();
        await db.SaveChangesAsync();
        var serialized = JsonSerializer.Serialize(result, Json);
        if (key.HasValue)
        {
            db.IdempotencyRecords.Add(new() { ActorStaffId = actor.IsGuest ? null : actor.Id, ActorGuestSessionId = actor.IsGuest ? actor.Id : null, Key = key.Value,
                Method = Http.Request.Method, Route = Http.Request.Path, RequestHash = requestHash, ResponseCiphertext = protector.Protect(Encoding.UTF8.GetBytes(serialized)), EncryptionKeyId = "aspnet-data-protection-v1" });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Results.Content(serialized, "application/json", statusCode: 200);
    }
    private static string Canonical(JsonNode? node) => node switch
    {
        JsonObject o => "{" + string.Join(",", o.OrderBy(p => p.Key).Select(p => JsonSerializer.Serialize(p.Key) + ":" + Canonical(p.Value))) + "}",
        JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        _ => node?.ToJsonString() ?? "null"
    };
    private async Task<T> Lock<T>(string table, Guid id) where T : Mutable
    {
        var allowed = typeof(T).Name switch { nameof(StaffUser)=>"staff_users", nameof(MenuCategory)=>"menu_categories", nameof(MenuItem)=>"menu_items", nameof(RestaurantTable)=>"restaurant_tables", nameof(Visit)=>"visits", nameof(Order)=>"orders", nameof(OrderBatch)=>"order_batches", nameof(OrderItem)=>"order_items", _=>throw new InvalidOperationException("Unsupported lock target") };
        if(table!=allowed) throw new InvalidOperationException("Invalid lock target");
        // Identifiers come only from the type allowlist above; resource ID remains a SQL parameter.
#pragma warning disable EF1002
        return await db.Set<T>().FromSqlRaw($"SELECT * FROM {allowed} WHERE id = {{0}} FOR UPDATE", id).SingleOrDefaultAsync() ?? throw new ApiException(404, "RESOURCE_NOT_FOUND", "Không tìm thấy dữ liệu.");
#pragma warning restore EF1002
    }
    private async Task<Order> OrderLock(Guid id)
    {
        var info = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id) ?? throw new ApiException(404, "RESOURCE_NOT_FOUND", "Không tìm thấy đơn.");
        Access(info);
        if (info.VisitId is Guid visitId)
        {
            var visit = await db.Visits.AsNoTracking().SingleAsync(v => v.Id == visitId);
            await Lock<RestaurantTable>("restaurant_tables", visit.TableId);
            await Lock<Visit>("visits", visit.Id);
        }
        var order = await Lock<Order>("orders", id);
        Access(order);
        return order;
    }
    private void Access(Order o)
    {
        var a = Require();
        Rules.Require(!a.IsGuest || o.VisitId == a.VisitId, "RESOURCE_NOT_FOUND", "Không tìm thấy đơn.", 404);
    }
    private void OrderWriter(Order o)
    {
        var a = Require(); Access(o);
        if (!a.IsGuest) Rules.Require(a.Has("WAITER", "MANAGER", "ADMIN") || o.OrderType == "TAKEAWAY" && a.Has("CASHIER"), "FORBIDDEN", "Không có quyền điều chỉnh đơn.", 403);
    }
    private static void Serving(Order o) => Rules.Require(o.Status == "SERVING", "INVALID_TRANSITION", "Đơn hiện không nhận thay đổi món.");
    public async Task<object> RequestStatus(Guid key)
    {
        var a = Require();
        var r = await db.IdempotencyRecords.SingleOrDefaultAsync(r => r.Key == key && (a.IsGuest ? r.ActorGuestSessionId == a.Id : r.ActorStaffId == a.Id));
        Rules.Require(r != null, "RESOURCE_NOT_FOUND", "Chưa tìm thấy kết quả. Có thể retry cùng key.", 404);
        Rules.Require(r!.ReplayUntil > DateTimeOffset.UtcNow && r.ResponseCiphertext != null, "IDEMPOTENCY_REPLAY_EXPIRED", "Hết hạn replay.", 410);
        return new { status = "COMPLETED", response = JsonNode.Parse(protector.Unprotect(r.ResponseCiphertext!)) };
    }
}
