using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Domain;

namespace Restaurant.Api.Services;
public partial class ApiService
{
    public async Task<object> Login(JsonObject b)
    {
        var username = Text(b, "username", 64).ToLowerInvariant(); var password = Text(b, "password", 128);
        var user = await db.StaffUsers.SingleOrDefaultAsync(u => u.Username == username && u.IsActive);
        Rules.Require(user != null && passwords.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed, "UNAUTHENTICATED", "Tên đăng nhập hoặc mật khẩu không đúng.", 401);
        var token = Token(); db.StaffSessions.Add(new() { UserId = user!.Id, TokenHash = Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(8) });
        await db.SaveChangesAsync(); Cookie("ros_staff", token, 8);
        return Profile(user, await db.UserRoles.Where(r => r.UserId == user.Id).Select(r => r.RoleCode).ToArrayAsync());
    }
    private static object Profile(StaffUser u, string[] roles) => new { u.Id, u.Username, u.DisplayName, roles, u.IsActive, u.MustChangePassword, u.Version };
    public object Me() { var a = Require(); return new { a.Id, a.DisplayName, a.Roles, a.IsGuest, a.VisitId, a.MustChangePassword }; }
    public async Task<object> Logout()
    {
        if (User is { IsGuest: false })
        {
            var hash = Hash(Http.Request.Cookies["ros_staff"] ?? "");
            await db.StaffSessions.Where(s => s.TokenHash == hash && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
            Http.Response.Cookies.Delete("ros_staff");
        }
        else { if (User != null) await db.GuestSessions.Where(s => s.Id == User.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow)); Http.Response.Cookies.Delete("ros_guest"); }
        return new { success = true };
    }
    public async Task<IResult> ChangePassword(JsonObject b) => await Command(b, async () =>
    {
        var a = Require(); Rules.Require(!a.IsGuest, "FORBIDDEN", "Chỉ tài khoản nhân viên.", 403);
        var u = await Lock<StaffUser>("staff_users", a.Id);
        Rules.Require(passwords.VerifyHashedPassword(u, u.PasswordHash, Text(b, "currentPassword", 128)) != PasswordVerificationResult.Failed, "VALIDATION_FAILED", "Mật khẩu hiện tại không đúng.", 422);
        u.PasswordHash = passwords.HashPassword(u, Password(b, "newPassword")); u.MustChangePassword = false; u.Touch();
        await Revoke(u.Id); Audit("PASSWORD_CHANGED", "STAFF_USER", u.Id, null, new { u.MustChangePassword });
        var token = Token(); db.StaffSessions.Add(new() { UserId = u.Id, TokenHash = Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(8) }); Cookie("ros_staff", token, 8);
        return new { success = true };
    }, false);
    private static string Password(JsonObject b, string key) { var p = Text(b, key, 128); Rules.Require(p.Length >= 12, "VALIDATION_FAILED", "Mật khẩu tối thiểu 12 ký tự.", 422); return p; }
    private async Task Revoke(Guid userId) => await db.StaffSessions.Where(s => s.UserId == userId && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
    public async Task<object> StaffList()
    {
        Require("ADMIN"); var users = await db.StaffUsers.OrderBy(u => u.Username).ToListAsync(); var roles = await db.UserRoles.ToListAsync();
        return new { items = users.Select(u => Profile(u, roles.Where(r => r.UserId == u.Id).Select(r => r.RoleCode).ToArray())), nextCursor = (string?)null };
    }
    public async Task<IResult> StaffCreate(JsonObject b) => await Command(b, async () =>
    {
        var a = Require("ADMIN"); var username = Text(b, "username", 64).ToLowerInvariant();
        Rules.Require(Regex.IsMatch(username, "^[a-z0-9._-]{3,64}$"), "VALIDATION_FAILED", "Username chỉ gồm chữ thường, số, dấu chấm, gạch dưới hoặc gạch ngang.", 422);
        var roles = RoleInput(b); var u = new StaffUser { Username = username, DisplayName = Text(b, "displayName", 120) };
        u.PasswordHash = passwords.HashPassword(u, Password(b, "initialPassword")); db.StaffUsers.Add(u);
        foreach (var r in roles) db.UserRoles.Add(new() { UserId = u.Id, RoleCode = r, GrantedByStaffId = a.Id });
        Audit("STAFF_CREATED", "STAFF_USER", u.Id, null, new { u.Username, u.DisplayName, roles }); return Profile(u, roles);
    }, false);
    private static string[] RoleInput(JsonObject b)
    {
        var roles = b["roles"]?.AsArray().Select(r => r!.GetValue<string>()).Distinct().ToArray() ?? [];
        Rules.Require(roles.Length>0 && roles.All(Rules.StaffRoles.Contains), "VALIDATION_FAILED", "Cần ít nhất một role hợp lệ.", 422); return roles;
    }
    public async Task<IResult> StaffUpdate(Guid id, JsonObject b, string mode) => await Command(b, async () =>
    {
        var a = Require("ADMIN"); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(902026)");
        var u = await Lock<StaffUser>("staff_users", id); CheckVersion(u,b);
        var before = new { u.IsActive, u.DisplayName }; var roles = await db.UserRoles.Where(r => r.UserId == id).Select(r => r.RoleCode).ToArrayAsync();
        if (mode == "roles")
        {
            var next = RoleInput(b); await LastAdmin(u, !next.Contains("ADMIN"));
            var old = await db.UserRoles.Where(r => r.UserId == id).ToListAsync(); db.UserRoles.RemoveRange(old);
            await db.SaveChangesAsync();
            foreach (var r in next) db.UserRoles.Add(new() { UserId=id, RoleCode=r, GrantedByStaffId=a.Id }); roles=next;
        }
        else if (mode == "reset-password") { u.PasswordHash = passwords.HashPassword(u, Password(b,"temporaryPassword")); u.MustChangePassword=true; }
        else
        {
            var active=Flag(b,"active",u.IsActive); await LastAdmin(u,!active);
            u.IsActive=active; if(b["displayName"]!=null) u.DisplayName=Text(b,"displayName",120);
        }
        u.Touch(); await Revoke(id); Audit("STAFF_"+mode.ToUpperInvariant(),"STAFF_USER",id,before,new {u.DisplayName,u.IsActive,roles}); return Profile(u,roles);
    }, false);
    private async Task LastAdmin(StaffUser user, bool removing)
    {
        if (!removing || !user.IsActive || !await db.UserRoles.AnyAsync(r=>r.UserId==user.Id && r.RoleCode=="ADMIN")) return;
        var count=await (from r in db.UserRoles join u in db.StaffUsers on r.UserId equals u.Id where r.RoleCode=="ADMIN" && u.IsActive select u.Id).CountAsync();
        Rules.Require(count>1,"LAST_ADMIN","Không được vô hiệu hóa admin cuối cùng.");
    }
}
