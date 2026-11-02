using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Domain;

namespace Restaurant.Api.Services;
public partial class ApiService
{
    public async Task<object> Menu(bool manage=false)
    {
        if(manage) Require("MANAGER","ADMIN");
        var categories=await db.MenuCategories.AsNoTracking().Where(c=>manage || c.IsActive).OrderBy(c=>c.SortOrder).ToListAsync();
        var ids=categories.Select(c=>c.Id).ToArray();
        var query=db.MenuItems.AsNoTracking().Where(i=>ids.Contains(i.CategoryId) && (manage || i.IsActive));
        if(Http.Request.Query.TryGetValue("categoryId",out var cat) && Guid.TryParse(cat,out var categoryId)) query=query.Where(i=>i.CategoryId==categoryId);
        if(Http.Request.Query.TryGetValue("search",out var search) && !string.IsNullOrWhiteSpace(search)) query=query.Where(i=>EF.Functions.ILike(i.Name,"%"+search.ToString()+"%"));
        return new {categories,items=await query.OrderBy(i=>i.Name).ToListAsync(),serverTime=DateTimeOffset.UtcNow};
    }
    public async Task<IResult> CategoryWrite(Guid? id,JsonObject b) => await Command(b,async()=>
    {
        Require("MANAGER","ADMIN"); var c=id.HasValue?await Lock<MenuCategory>("menu_categories",id.Value):new MenuCategory();
        if(id.HasValue) {CheckVersion(c,b);c.Touch();} else db.MenuCategories.Add(c);
        if(b["code"]!=null || !id.HasValue) c.Code=Text(b,"code",32).ToUpperInvariant();
        if(b["name"]!=null || !id.HasValue)c.Name=Text(b,"name",100);
        if(b["sortOrder"]!=null)c.SortOrder=(int)Number(b,"sortOrder",0,int.MaxValue);
        c.IsActive=Flag(b,"isActive",c.IsActive); Audit("CATEGORY_SAVED","MENU_CATEGORY",c.Id,null,new {c.Code,c.Name,c.IsActive});return c;
    },false);
    public async Task<IResult> MenuWrite(Guid? id,JsonObject b,bool availability=false)=>await Command(b,async()=>
    {
        if(availability)Require("KITCHEN","MANAGER","ADMIN");else Require("MANAGER","ADMIN");
        Guid categoryId;
        if(id.HasValue)categoryId=await db.MenuItems.Where(i=>i.Id==id.Value).Select(i=>i.CategoryId).SingleOrDefaultAsync();
        else categoryId=Id(b,"categoryId");
        var nextCategory=b["categoryId"]==null?categoryId:Id(b,"categoryId");
        foreach(var cid in new[]{categoryId,nextCategory}.Distinct().Order()) await Lock<MenuCategory>("menu_categories",cid);
        var item=id.HasValue?await Lock<MenuItem>("menu_items",id.Value):new MenuItem {CategoryId=categoryId};
        if(id.HasValue){CheckVersion(item,b);item.Touch();}else db.MenuItems.Add(item);
        var before=new {item.Name,item.PriceVnd,item.IsAvailable,item.IsActive};
        if(availability)item.IsAvailable=Flag(b,"isAvailable");
        else
        {
            item.CategoryId=nextCategory;
            if(b["code"]!=null || !id.HasValue)item.Code=Text(b,"code",32).ToUpperInvariant();
            if(b["name"]!=null || !id.HasValue)item.Name=Text(b,"name",160);
            if(b["description"]!=null || !id.HasValue)item.Description=Text(b,"description",2000);
            if(b["priceVnd"]!=null || !id.HasValue)item.PriceVnd=Number(b,"priceVnd");
            if(b["imageUrl"]!=null || !id.HasValue)
            {
                var url=Text(b,"imageUrl",2048);Rules.Require((url.StartsWith("/menu/")&&!url.Contains("..")) || Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https","VALIDATION_FAILED","Ảnh phải là đường dẫn /menu/ hoặc URL HTTPS.",422);item.ImageUrl=url;
            }
            item.IsActive=Flag(b,"isActive",item.IsActive);
        }
        Audit("MENU_UPDATED","MENU_ITEM",item.Id,before,new {item.Name,item.PriceVnd,item.IsAvailable,item.IsActive});return item;
    },false);
    public async Task<object> Tables()
    {
        Require("WAITER","KITCHEN","CASHIER","MANAGER","ADMIN");
        var tables=await db.RestaurantTables.AsNoTracking().OrderBy(t=>t.Code).ToListAsync();
        var visits=await db.Visits.AsNoTracking().Where(v=>v.Status=="OPEN").ToListAsync();
        var orders=await db.Orders.AsNoTracking().Where(o=>o.VisitId!=null && o.Status!="PAID"&&o.Status!="CANCELLED").ToListAsync();
        return new {items=tables.Select(t=>new {t.Id,t.Code,t.Capacity,t.IsActive,t.Version,visit=visits.Where(v=>v.TableId==t.Id).Select(v=>new {v.Id,v.PartySize,v.Version,orderId=orders.FirstOrDefault(o=>o.VisitId==v.Id)?.Id}).FirstOrDefault()})};
    }
    public async Task<IResult> TableWrite(Guid? id,JsonObject b)=>await Command(b,async()=>
    {
        Require("MANAGER","ADMIN");var t=id.HasValue?await Lock<RestaurantTable>("restaurant_tables",id.Value):new RestaurantTable {QrTokenHash=Hash(Token())};
        if(id.HasValue){CheckVersion(t,b);Rules.Require(!await db.Visits.AnyAsync(v=>v.TableId==t.Id&&v.Status=="OPEN"),"TABLE_OCCUPIED","Không sửa bàn đang có khách.");t.Touch();}else db.RestaurantTables.Add(t);
        if(b["code"]!=null || !id.HasValue)t.Code=Text(b,"code",20).ToUpperInvariant();
        if(b["capacity"]!=null || !id.HasValue)t.Capacity=(short)Number(b,"capacity",1,30);
        t.IsActive=Flag(b,"isActive",t.IsActive);Audit("TABLE_SAVED","TABLE",t.Id,null,new {t.Code,t.Capacity,t.IsActive});return new {t.Id,t.Code,t.Capacity,t.IsActive,t.Version};
    },false);
    public async Task<IResult> QrRotate(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        Require("MANAGER","ADMIN");var t=await Lock<RestaurantTable>("restaurant_tables",id);CheckVersion(t,b);var raw=Token();t.QrTokenHash=Hash(raw);t.Touch();
        Audit("QR_ROTATED","TABLE",t.Id,null,new {t.Version});return new {t.Id,t.Code,t.Version,qrToken=raw,qrUrl=$"/qr/{raw}"};
    });
    public async Task<object> QrRead(string token)
    {
        var h=Hash(token);var t=await db.RestaurantTables.AsNoTracking().SingleOrDefaultAsync(t=>t.QrTokenHash==h&&t.IsActive);
        Rules.Require(t!=null,"RESOURCE_NOT_FOUND","Mã bàn không hợp lệ.",404);
        return new {tableCode=t!.Code,hasOpenVisit=await db.Visits.AnyAsync(v=>v.TableId==t.Id&&v.Status=="OPEN")};
    }
    public async Task<IResult> OpenVisit(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        var a=Require("WAITER","MANAGER","ADMIN");var t=await Lock<RestaurantTable>("restaurant_tables",id);
        Rules.Require(t.IsActive,"TABLE_INACTIVE","Bàn đang tạm ngưng.");Rules.Require(!await db.Visits.AnyAsync(v=>v.TableId==id&&v.Status=="OPEN"),"TABLE_OCCUPIED","Bàn đang có khách.");
        var v=new Visit {TableId=id,TableCodeSnapshot=t.Code,PartySize=(short)Number(b,"partySize",1,t.Capacity),OpenedByStaffId=a.Id};
        var code=RandomNumberGenerator.GetInt32(10000000,100000000).ToString();v.JoinCodeHmac=JoinHash(v,code);db.Visits.Add(v);
        var o=new Order {Code="DH-"+Token()[..12].ToUpperInvariant(),VisitId=v.Id,CreatedByStaffId=a.Id};db.Orders.Add(o);
        Audit("VISIT_OPENED","VISIT",v.Id,null,new {v.TableId,v.PartySize,orderId=o.Id});
        return new {visitId=v.Id,orderId=o.Id,tableCode=t.Code,joinCode=code,version=v.Version,orderVersion=o.Version};
    });
    public async Task<IResult> JoinRotate(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        Require("WAITER","MANAGER","ADMIN");var info=await db.Visits.AsNoTracking().SingleOrDefaultAsync(v=>v.Id==id)??throw new ApiException(404,"RESOURCE_NOT_FOUND","Không tìm thấy lượt.");
        await Lock<RestaurantTable>("restaurant_tables",info.TableId);var v=await Lock<Visit>("visits",id);CheckVersion(v,b);Rules.Require(v.Status=="OPEN","INVALID_TRANSITION","Lượt đã đóng.");
        var code=RandomNumberGenerator.GetInt32(10000000,100000000).ToString();v.JoinGeneration++;v.JoinCodeHmac=JoinHash(v,code);v.Touch();
        await db.GuestSessions.Where(s=>s.VisitId==id&&s.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,DateTimeOffset.UtcNow));
        Audit("JOIN_CODE_ROTATED","VISIT",id,null,new {v.JoinGeneration});return new {visitId=id,joinCode=code,v.Version};
    });
    public async Task<object> Join(JsonObject b)
    {
        var h=Hash(Text(b,"qrToken",128));var t=await db.RestaurantTables.SingleOrDefaultAsync(t=>t.QrTokenHash==h&&t.IsActive);
        var v=t==null?null:await db.Visits.SingleOrDefaultAsync(v=>v.TableId==t.Id&&v.Status=="OPEN");
        Rules.Require(v!=null&&CryptographicOperations.FixedTimeEquals(v.JoinCodeHmac,JoinHash(v,Text(b,"joinCode",8))),"UNAUTHENTICATED","Mã tham gia không đúng hoặc lượt đã kết thúc.",401);
        var token=Token();var s=new GuestSession {VisitId=v!.Id,JoinGeneration=v.JoinGeneration,TokenHash=Hash(token),ExpiresAt=DateTimeOffset.UtcNow.AddHours(12)};db.GuestSessions.Add(s);
        await db.SaveChangesAsync();Cookie("ros_guest",token,12);var order=await db.Orders.SingleAsync(o=>o.VisitId==v.Id);
        return new {visitId=v.Id,orderId=order.Id,tableCode=v.TableCodeSnapshot};
    }
    public async Task<object> GuestCurrent()
    {
        var a=Require();Rules.Require(a.IsGuest,"FORBIDDEN","Hãy dùng cookie khách và header X-Actor: guest.",403);
        var v=await db.Visits.AsNoTracking().SingleAsync(v=>v.Id==a.VisitId);var o=await db.Orders.SingleAsync(o=>o.VisitId==v.Id);
        return new {visitId=v.Id,orderId=o.Id,tableCode=v.TableCodeSnapshot};
    }
}
