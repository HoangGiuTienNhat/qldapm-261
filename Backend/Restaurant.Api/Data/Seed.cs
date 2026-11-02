using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Domain;
using Restaurant.Api.Services;

namespace Restaurant.Api.Data;
public static class Seed
{
    public static async Task Run(AppDb db,IConfiguration config,IDataProtectionProvider protection)
    {
        var password=config["Demo:Password"]??throw new InvalidOperationException("Set Demo__Password for local seed.");
        if(password.Length<12)throw new InvalidOperationException("Demo password must have at least 12 characters.");
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(902027)");
        var hasher=new PasswordHasher<StaffUser>();
        var names=new Dictionary<string,string>{{"ADMIN","Quản trị hệ thống"},{"MANAGER","Quản lý nhà hàng"},{"WAITER","Nhân viên phục vụ"},{"KITCHEN","Nhân viên bếp"},{"CASHIER","Nhân viên thu ngân"}};
        foreach(var (role,name) in names)
        {
            if(!await db.Roles.AnyAsync(r=>r.Code==role))db.Roles.Add(new(){Code=role,DisplayName=name});
            var username=role.ToLowerInvariant();if(await db.StaffUsers.AnyAsync(u=>u.Username==username))continue;
            var u=new StaffUser{Id=Stable("10000000",Array.IndexOf(Rules.StaffRoles,role)+1),Username=username,DisplayName=name,MustChangePassword=false};
            u.PasswordHash=hasher.HashPassword(u,password);db.StaffUsers.Add(u);db.UserRoles.Add(new(){UserId=u.Id,RoleCode=role});
        }
        await db.SaveChangesAsync();
        var categories=new[]{"Khai vị","Món chính","Món ăn kèm","Đồ uống"};
        for(var c=0;c<4;c++)if(!await db.MenuCategories.AnyAsync(x=>x.Id==Stable("20000000",c+1)))db.MenuCategories.Add(new(){Id=Stable("20000000",c+1),Code="CAT-"+(c+1),Name=categories[c],SortOrder=c});
        await db.SaveChangesAsync();
        (string Name,long Price,string Description)[] dishes=[
            ("Gỏi cuốn tôm thịt",45000,"Tôm, thịt và rau tươi cuốn bánh tráng, dùng kèm tương đậu."),
            ("Chả giò giòn",55000,"Chả giò nhân thịt và nấm, ăn kèm rau sống."),
            ("Gỏi ngó sen tôm thịt",85000,"Ngó sen giòn, tôm thịt và nước trộn chua ngọt."),
            ("Súp cua nấm tuyết",39000,"Súp cua nóng với trứng cút và nấm tuyết."),
            ("Đậu hũ chiên sả",42000,"Đậu hũ vàng giòn, sả thơm và muối tiêu."),
            ("Cơm chiên hải sản",95000,"Cơm chiên tơi với tôm, mực và rau củ."),
            ("Bò lúc lắc khoai tây",145000,"Bò áp chảo với ớt chuông, hành tây và khoai tây."),
            ("Gà nướng mật ong",125000,"Gà nướng da vàng với sốt mật ong."),
            ("Cá kho tộ",110000,"Cá kho tiêu trong niêu, dùng cùng cơm nóng."),
            ("Mì xào bò",89000,"Mì xào với bò mềm, cải xanh và nấm."),
            ("Canh chua cá",99000,"Canh chua miền Nam với cá, thơm và bạc hà."),
            ("Rau muống xào tỏi",49000,"Rau muống xanh giòn xào tỏi."),
            ("Cơm trắng",15000,"Một chén cơm gạo thơm."),
            ("Khoai tây chiên",39000,"Khoai tây giòn, kèm tương cà."),
            ("Salad rau vườn",59000,"Xà lách, cà chua, dưa leo và sốt mè rang."),
            ("Trà đào cam sả",39000,"Trà đào thanh mát với cam và sả."),
            ("Nước chanh tươi",29000,"Chanh tươi, đường và đá."),
            ("Nước suối",15000,"Nước khoáng đóng chai 500ml."),
            ("Cà phê sữa đá",29000,"Cà phê rang xay với sữa đặc."),
            ("Trà lài",19000,"Trà lài thơm nhẹ, dùng nóng hoặc đá.")];
        for(var i=0;i<dishes.Length;i++)
        {
            var id=Stable("30000000",i+1);if(await db.MenuItems.AnyAsync(m=>m.Id==id))continue;var d=dishes[i];
            db.MenuItems.Add(new(){Id=id,CategoryId=Stable("20000000",i/5+1),Code="MON-"+(i+1).ToString("D3"),Name=d.Name,PriceVnd=d.Price,Description=d.Description,IsAvailable=i!=14});
        }
        var qrProtector=protection.CreateProtector("restaurant.table-qr.v1");
        for(var i=1;i<=30;i++)
        {
            var id=Stable("40000000",i);if(await db.RestaurantTables.AnyAsync(t=>t.Id==id))continue;
            var raw=ApiService.Token();db.RestaurantTables.Add(new(){Id=id,Code="B"+i.ToString("D2"),Capacity=(short)(i%5==0?6:4),QrTokenHash=ApiService.Hash(raw),QrTokenCiphertext=qrProtector.Protect(Encoding.UTF8.GetBytes(raw))});
        }
        // One-time upgrade: legacy hashes cannot be reversed. Never rotate an initialized QR.
        foreach(var table in await db.RestaurantTables.Where(t=>t.QrTokenCiphertext==null).ToListAsync())
        {
            var raw=ApiService.Token();table.QrTokenHash=ApiService.Hash(raw);table.QrTokenCiphertext=qrProtector.Protect(Encoding.UTF8.GetBytes(raw));table.Touch();
            db.AuditEvents.Add(new(){ActorKind="SYSTEM",Action="QR_INITIALIZED",TargetType="TABLE",TargetId=table.Id,AfterData="{\"fixedQr\":true}",RequestId=Guid.NewGuid(),CorrelationId=Guid.NewGuid()});
        }
        await db.SaveChangesAsync();await tx.CommitAsync();
    }
    public static Guid Stable(string prefix,int index)=>Guid.Parse($"{prefix}-0000-0000-0000-{index:D12}");
}
