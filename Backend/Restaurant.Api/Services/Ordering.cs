using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Domain;

namespace Restaurant.Api.Services;
public partial class ApiService
{
    public async Task<object> OrderRead(Guid id)
    {
        var o=await db.Orders.AsNoTracking().SingleOrDefaultAsync(o=>o.Id==id)??throw new ApiException(404,"RESOURCE_NOT_FOUND","Không tìm thấy đơn.");Access(o);
        var batches=await db.OrderBatches.AsNoTracking().Where(b=>b.OrderId==id).OrderBy(b=>b.SequenceNo).ToListAsync();var ids=batches.Select(b=>b.Id).ToArray();
        var items=await db.OrderItems.AsNoTracking().Where(i=>ids.Contains(i.BatchId)).OrderBy(i=>i.CreatedAt).ThenBy(i=>i.Id).ToListAsync();
        var v=o.VisitId.HasValue?await db.Visits.AsNoTracking().SingleAsync(v=>v.Id==o.VisitId):null;
        return new {o.Id,o.Code,o.OrderType,o.PickupCode,o.PickupName,o.Status,o.Version,o.CreatedAt,tableCode=v?.TableCodeSnapshot,o.VisitId,o.TotalVnd,
            subtotalVnd=items.Where(i=>!new[]{"PENDING_APPROVAL","REJECTED","CANCELLED"}.Contains(i.Status)).Sum(i=>i.UnitPriceVnd*i.Quantity),
            pendingVnd=items.Where(i=>i.Status=="PENDING_APPROVAL").Sum(i=>i.UnitPriceVnd*i.Quantity),
            batches=batches.Select(b=>new {b.Id,b.SequenceNo,b.Status,b.Version,b.CreatedAt,b.DecisionReason,b.ReviewedAt,b.EligibleAt,b.SubmittedByStaffId,items=items.Where(i=>i.BatchId==b.Id)})};
    }
    public async Task<object> OrdersList(bool checkout=false)
    {
        if(checkout)Require("CASHIER","MANAGER","ADMIN");else Require("WAITER","KITCHEN","CASHIER","MANAGER","ADMIN");
        var q=db.Orders.AsNoTracking().AsQueryable();if(checkout)q=q.Where(o=>o.Status=="AWAITING_PAYMENT");
        else if(Http.Request.Query["status"] is var status&&!string.IsNullOrEmpty(status))q=q.Where(o=>o.Status==status.ToString());
        else q=q.Where(o=>o.Status=="SERVING"||o.Status=="AWAITING_PAYMENT");
        var orders=await q.OrderByDescending(o=>o.CreatedAt).Take(200).ToListAsync();var vids=orders.Select(o=>o.VisitId).ToArray();var visits=await db.Visits.AsNoTracking().Where(v=>vids.Contains(v.Id)).ToListAsync();
        return new {items=orders.Select(o=>new {o.Id,o.Code,o.OrderType,o.PickupCode,o.PickupName,o.Status,o.Version,o.TotalVnd,o.CreatedAt,tableCode=visits.FirstOrDefault(v=>v.Id==o.VisitId)?.TableCodeSnapshot})};
    }
    public async Task<IResult> Takeaway(JsonObject b)=>await Command(b,async()=>
    {
        var a=Require("WAITER","CASHIER","MANAGER","ADMIN");var o=new Order {Code="DH-"+Token()[..12].ToUpperInvariant(),OrderType="TAKEAWAY",PickupCode="MV-"+Token()[..8].ToUpperInvariant(),PickupName=Optional(b,"pickupName",120),CreatedByStaffId=a.Id};db.Orders.Add(o);
        var batch=await AddBatch(o,b,1);Audit("TAKEAWAY_CREATED","ORDER",o.Id,null,new {o.Code,o.PickupCode});return new {orderId=o.Id,o.Code,o.PickupCode,orderVersion=o.Version,batchId=batch.Id,batch.Status,batch.Version};
    });
    public async Task<IResult> Submit(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        var o=await OrderLock(id);OrderWriter(o);Serving(o);CheckVersion(o,b,"expectedOrderVersion");
        var seq=(await db.OrderBatches.Where(x=>x.OrderId==id).MaxAsync(x=>(int?)x.SequenceNo)??0)+1;
        var batch=await AddBatch(o,b,seq);o.Touch();return new {orderId=o.Id,orderVersion=o.Version,batchId=batch.Id,batch.Status,batch.Version};
    });
    private async Task<OrderBatch> AddBatch(Order o,JsonObject body,int sequence)
    {
        var a=Require();var rows=body["items"]?.AsArray();Rules.Require(rows is {Count:>=1 and <=50},"VALIDATION_FAILED","Mỗi đợt cần 1–50 dòng món.",422);
        var inputs=rows!.Select(r=>r!.AsObject()).ToArray();var ids=inputs.Select(r=>Id(r,"menuItemId")).Distinct().Order().ToArray();
        var categories=await db.MenuItems.Where(i=>ids.Contains(i.Id)).Select(i=>i.CategoryId).Distinct().ToArrayAsync();
        foreach(var cid in categories.Order())await Lock<MenuCategory>("menu_categories",cid);
        var menu=new Dictionary<Guid,MenuItem>();foreach(var mid in ids)menu[mid]=await Lock<MenuItem>("menu_items",mid);
        var categoryRows=await db.MenuCategories.Where(c=>categories.Contains(c.Id)).ToListAsync();
        var batch=new OrderBatch {OrderId=o.Id,SequenceNo=sequence,Status=sequence==1?"APPROVED":"PENDING",SubmittedByStaffId=a.IsGuest?null:a.Id,SubmittedByGuestSessionId=a.IsGuest?a.Id:null};
        if(sequence==1)batch.EligibleAt=batch.CreatedAt;db.OrderBatches.Add(batch);
        foreach(var input in inputs)
        {
            var m=menu[Id(input,"menuItemId")];Rules.Require(m.IsActive&&m.IsAvailable&&categoryRows.Single(c=>c.Id==m.CategoryId).IsActive,"MENU_UNAVAILABLE",$"{m.Name} hiện không thể đặt.");
            Rules.Require(Number(input,"observedPriceVnd")==m.PriceVnd,"PRICE_CHANGED",$"Giá {m.Name} đã thay đổi thành {m.PriceVnd:N0}đ. Vui lòng xem lại thực đơn.");
            db.OrderItems.Add(new() {BatchId=batch.Id,MenuItemId=m.Id,MenuCodeSnapshot=m.Code,MenuNameSnapshot=m.Name,Quantity=(short)Number(input,"quantity",1,99),UnitPriceVnd=m.PriceVnd,Note=Optional(input,"note"),Status=sequence==1?"PLACED":"PENDING_APPROVAL"});
        }
        Audit("BATCH_SUBMITTED","BATCH",batch.Id,null,new {batch.SequenceNo,batch.Status});return batch;
    }
    public async Task<object> Approvals()
    {
        Require("KITCHEN","MANAGER","ADMIN");var batches=await db.OrderBatches.AsNoTracking().Where(b=>b.Status=="PENDING").OrderBy(b=>b.CreatedAt).ToListAsync();
        var orderIds=batches.Select(b=>b.OrderId).ToArray();var orders=await db.Orders.AsNoTracking().Where(o=>orderIds.Contains(o.Id)).ToListAsync();var ids=batches.Select(b=>b.Id).ToArray();var items=await db.OrderItems.AsNoTracking().Where(i=>ids.Contains(i.BatchId)).ToListAsync();
        var visits=await db.Visits.AsNoTracking().Where(v=>v.Status=="OPEN").ToListAsync();
        return new {items=batches.Select(b=>new {b.Id,b.OrderId,b.SequenceNo,b.Version,b.CreatedAt,b.SubmittedByStaffId,orderCode=orders.Single(o=>o.Id==b.OrderId).Code,
            destination=orders.Single(o=>o.Id==b.OrderId) is var o && o.OrderType=="TAKEAWAY"?o.PickupCode:visits.FirstOrDefault(v=>v.Id==o.VisitId)?.TableCodeSnapshot,items=items.Where(i=>i.BatchId==b.Id)})};
    }
    public async Task<IResult> Review(Guid id,JsonObject b,string decision)=>await Command(b,async()=>
    {
        var a=Require();if(decision!="WITHDRAWN")Require("KITCHEN","MANAGER","ADMIN");
        var info=await db.OrderBatches.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiException(404,"RESOURCE_NOT_FOUND","Không tìm thấy đợt gọi.");
        var o=await OrderLock(info.OrderId);Serving(o);if(decision=="WITHDRAWN")OrderWriter(o);
        var batch=await Lock<OrderBatch>("order_batches",id);CheckVersion(batch,b);Rules.Require(batch.Status=="PENDING","INVALID_TRANSITION","Đợt gọi đã có quyết định.");
        if(decision!="WITHDRAWN")Rules.Require(batch.SubmittedByStaffId!=a.Id||a.Has("MANAGER","ADMIN"),"FORBIDDEN","Nhân viên bếp không được tự duyệt đợt mình gửi.",403);
        var items=await db.OrderItems.Where(i=>i.BatchId==id).OrderBy(i=>i.Id).ToListAsync();
        if(decision=="APPROVED")
        {
            var mids=items.Select(i=>i.MenuItemId).Distinct().Order().ToArray();var cats=await db.MenuItems.Where(m=>mids.Contains(m.Id)).Select(m=>m.CategoryId).Distinct().ToArrayAsync();
            foreach(var cid in cats.Order()) {var c=await Lock<MenuCategory>("menu_categories",cid);Rules.Require(c.IsActive,"MENU_UNAVAILABLE","Danh mục đã tạm ngưng.");}
            foreach(var mid in mids){var m=await Lock<MenuItem>("menu_items",mid);Rules.Require(m.IsActive&&m.IsAvailable,"MENU_UNAVAILABLE",$"{m.Name} hiện không còn. Hãy từ chối đợt gọi có lý do.");}
            batch.EligibleAt=DateTimeOffset.UtcNow;
        }
        batch.Status=decision;batch.Touch();if(decision!="WITHDRAWN"){batch.ReviewedByStaffId=a.Id;batch.ReviewedAt=batch.EligibleAt??DateTimeOffset.UtcNow;}
        if(decision=="REJECTED")batch.DecisionReason=Text(b,"reason");
        foreach(var item in items){item.Status=decision=="APPROVED"?"PLACED":decision=="REJECTED"?"REJECTED":"CANCELLED";item.StatusChangedAt=DateTimeOffset.UtcNow;item.Touch();}
        o.Touch();Audit("BATCH_"+decision,"BATCH",id,new {status="PENDING"},new {batch.Status},batch.DecisionReason);return new {batch.Id,batch.Status,batch.Version,orderVersion=o.Version};
    });
    public async Task<object> Queue(bool serving=false)
    {
        if(serving)Require("WAITER","CASHIER","MANAGER","ADMIN");else Require("KITCHEN","MANAGER","ADMIN");
        var query=from i in db.OrderItems.AsNoTracking() join b in db.OrderBatches on i.BatchId equals b.Id join o in db.Orders on b.OrderId equals o.Id
                  where b.Status=="APPROVED"&&o.Status=="SERVING"&&(serving?i.Status=="READY":new[]{"PLACED","ACCEPTED","PREPARING","READY"}.Contains(i.Status))
                  select new {item=i,batch=b,order=o};
        if(serving&&User!.Has("CASHIER")&&!User.Has("WAITER","MANAGER","ADMIN"))query=query.Where(x=>x.order.OrderType=="TAKEAWAY");
        var rows=await query.OrderBy(x=>serving?x.item.StatusChangedAt:x.batch.EligibleAt).ThenBy(x=>x.item.Id).ToListAsync();
        var visits=await db.Visits.AsNoTracking().Where(v=>v.Status=="OPEN").ToListAsync();
        return new {items=rows.Select(x=>new {x.item.Id,x.item.BatchId,x.item.MenuItemId,x.item.MenuNameSnapshot,x.item.Quantity,x.item.Note,x.item.Status,x.item.Version,x.item.StatusChangedAt,x.batch.SequenceNo,x.batch.EligibleAt,
            orderId=x.order.Id,orderCode=x.order.Code,orderType=x.order.OrderType,destination=x.order.PickupCode??visits.FirstOrDefault(v=>v.Id==x.order.VisitId)?.TableCodeSnapshot}),serverTime=DateTimeOffset.UtcNow};
    }
    public async Task<IResult> ItemAction(Guid id,JsonObject b,string action)=>await Command(b,async()=>
    {
        var info=await (from i in db.OrderItems join batch in db.OrderBatches on i.BatchId equals batch.Id where i.Id==id select new {batch.OrderId}).SingleOrDefaultAsync()??throw new ApiException(404,"RESOURCE_NOT_FOUND","Không tìm thấy món.");
        var o=await OrderLock(info.OrderId);Serving(o);var item=await Lock<OrderItem>("order_items",id);CheckVersion(item,b);var before=new {item.Status,item.Quantity,item.Note,item.Version};string? reason=null;
        if(action=="amend")
        {
            OrderWriter(o);Rules.Require(item.Status is "PLACED" or "ACCEPTED","INVALID_TRANSITION","Chỉ được sửa trước khi bếp chuẩn bị.");
            if(b["quantity"]!=null)item.Quantity=(short)Number(b,"quantity",1,item.Quantity);if(b.ContainsKey("note"))item.Note=Optional(b,"note");
        }
        else
        {
            string next=action switch {"accept"=>"ACCEPTED","prepare"=>"PREPARING","ready"=>"READY","serve"=>"SERVED","cancel"=>"CANCELLED",_=>throw new ApiException(400,"INVALID_ACTION","Thao tác không hợp lệ.")};
            if(action=="cancel")
            {
                if(item.Status is "PREPARING" or "READY"){Require("MANAGER","ADMIN");reason=Text(b,"reason");}else {OrderWriter(o);reason=Optional(b,"reason");}
            }
            else if(action=="serve") {Require("WAITER","CASHIER","MANAGER","ADMIN");Rules.Require(o.OrderType=="TAKEAWAY"||User!.Has("WAITER","MANAGER","ADMIN"),"FORBIDDEN","Thu ngân chỉ bàn giao mang về.",403);}
            else Require("KITCHEN","MANAGER","ADMIN");
            Rules.Require(Rules.CanTransition(item.Status,next),"INVALID_TRANSITION","Không thể chuyển trạng thái món theo thao tác này.");item.Status=next;item.StatusChangedAt=DateTimeOffset.UtcNow;
        }
        item.Touch();o.Touch();Audit("ITEM_"+action.ToUpperInvariant(),"ORDER_ITEM",id,before,new {item.Status,item.Quantity,item.Note,item.Version},reason);return new {item.Id,item.Status,item.Quantity,item.Note,item.Version,orderVersion=o.Version};
    });
    public async Task<IResult> CancelOrder(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        var o=await OrderLock(id);Require("WAITER","CASHIER","MANAGER","ADMIN");OrderWriter(o);Serving(o);CheckVersion(o,b);var reason=Text(b,"reason");
        var batches=await db.OrderBatches.Where(x=>x.OrderId==id).ToListAsync();var bids=batches.Select(x=>x.Id).ToArray();var items=await db.OrderItems.Where(i=>bids.Contains(i.BatchId)).ToListAsync();var iids=items.Select(i=>i.Id).ToArray();
        var cooked=await db.AuditEvents.AnyAsync(e=>e.TargetType=="ORDER_ITEM"&&iids.Contains(e.TargetId)&&(e.Action=="ITEM_PREPARE"||e.Action=="ITEM_READY"||e.Action=="ITEM_SERVE"));
        Rules.Require(!cooked&&!items.Any(i=>i.Status is "PREPARING" or "READY" or "SERVED"),"INVALID_TRANSITION","Đơn đã bắt đầu chế biến. Xử lý ngoại lệ từng món rồi thanh toán.");
        foreach(var batch in batches.Where(x=>x.Status=="PENDING")){batch.Status="WITHDRAWN";batch.Touch();}
        foreach(var i in items.Where(i=>i.Status is "PENDING_APPROVAL" or "PLACED" or "ACCEPTED")){var prev=i.Status;i.Status="CANCELLED";i.StatusChangedAt=DateTimeOffset.UtcNow;i.Touch();Audit("ITEM_CANCEL","ORDER_ITEM",i.Id,new {status=prev},new {i.Status},reason);}
        // Persist item changes while the order is still SERVING (database transition guard).
        await db.SaveChangesAsync();o.Status="CANCELLED";o.StatusChangedAt=DateTimeOffset.UtcNow;o.Touch();await EndVisit(o,"CANCELLED");Audit("ORDER_CANCELLED","ORDER",id,new {status="SERVING"},new {o.Status},reason);return new {o.Id,o.Status,o.Version};
    });
    public async Task<object> Changes()
    {
        Require("KITCHEN","MANAGER","ADMIN");var cutoff=DateTimeOffset.UtcNow.AddMinutes(-15);
        return new {items=await db.AuditEvents.AsNoTracking().Where(e=>e.TargetType=="ORDER_ITEM"&&e.OccurredAt>=cutoff&&(e.Action=="ITEM_CANCEL"||e.Action=="ITEM_AMEND")).OrderByDescending(e=>e.OccurredAt).Take(100).Select(e=>new {e.Id,e.TargetId,e.Action,e.BeforeData,e.AfterData,e.Reason,e.OccurredAt}).ToListAsync()};
    }
    private async Task EndVisit(Order o,string status)
    {
        if(o.VisitId is not Guid vid)return;var v=await db.Visits.SingleAsync(v=>v.Id==vid);v.Status=status;v.EndedAt=DateTimeOffset.UtcNow;v.EndedByStaffId=Require().Id;v.Touch();
        await db.GuestSessions.Where(s=>s.VisitId==vid&&s.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,DateTimeOffset.UtcNow));
    }
}
