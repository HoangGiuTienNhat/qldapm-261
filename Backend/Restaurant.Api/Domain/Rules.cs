namespace Restaurant.Api.Domain;

public class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public static class Rules
{
    public const long MoneyLimit = 1_000_000_000_000;
    public static readonly string[] StaffRoles = ["WAITER", "KITCHEN", "CASHIER", "MANAGER", "ADMIN"];
    public static void Require(bool condition, string code, string message, int status = 409)
    { if (!condition) throw new ApiException(status, code, message); }
    public static void Version(Mutable entity, long expected) => Require(entity.Version == expected, "VERSION_CONFLICT", $"Dữ liệu đã thay đổi. Phiên bản hiện tại: {entity.Version}.");
    public static bool CanTransition(string current, string next) => (current, next) switch
    {
        ("PLACED", "ACCEPTED" or "CANCELLED") => true,
        ("ACCEPTED", "PREPARING" or "CANCELLED") => true,
        ("PREPARING", "READY" or "CANCELLED") => true,
        ("READY", "SERVED" or "CANCELLED") => true,
        _ => false
    };
    public static long Total(IEnumerable<OrderItem> items) => checked(items.Where(i => i.Status == "SERVED").Sum(i => checked(i.UnitPriceVnd * i.Quantity)));
    public static void Payment(string method, long amount, long? tendered, string? reference)
    {
        Require(amount is >= 0 and <= MoneyLimit, "VALIDATION_FAILED", "Số tiền không hợp lệ.", 422);
        Require(method switch
        {
            "ZERO" => amount == 0 && tendered == null && reference == null,
            "CASH" => amount > 0 && tendered >= amount && tendered <= MoneyLimit && reference == null,
            "BANK_TRANSFER" => amount > 0 && tendered == null && !string.IsNullOrWhiteSpace(reference),
            _ => false
        }, "VALIDATION_FAILED", "Phương thức, tiền nhận hoặc mã chuyển khoản không hợp lệ.", 422);
    }
}
