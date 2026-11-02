using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Domain;

namespace Restaurant.Api.Services;
public partial class ApiService
{
    public async Task<IResult> RequestPayment(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        var a=Require();if(!a.IsGuest)Require("WAITER","CASHIER","MANAGER","ADMIN");
        var o=await OrderLock(id);Serving(o);CheckVersion(o,b);
        var batches=await db.OrderBatches.Where(x=>x.OrderId==id).ToListAsync();Rules.Require(batches.Count>0,"INVALID_TRANSITION","Đơn chưa có món. Hãy hủy lượt trống.");
        Rules.Require(batches.All(x=>x.Status!="PENDING"),"PENDING_APPROVAL_EXISTS","Cần duyệt, từ chối hoặc rút các đợt đang chờ.");var bids=batches.Select(x=>x.Id).ToArray();var items=await db.OrderItems.Where(i=>bids.Contains(i.BatchId)).ToListAsync();
        Rules.Require(items.All(i=>i.Status is "SERVED" or "CANCELLED" or "REJECTED"),"UNFINISHED_ITEMS","Cần phục vụ/bàn giao hết các món trước khi thanh toán.");
        o.TotalVnd=Rules.Total(items);Rules.Require(o.TotalVnd<=Rules.MoneyLimit,"VALIDATION_FAILED","Tổng tiền vượt giới hạn.",422);o.Status="AWAITING_PAYMENT";o.StatusChangedAt=DateTimeOffset.UtcNow;o.Touch();
        Audit("PAYMENT_REQUESTED","ORDER",id,new {status="SERVING"},new {o.Status,o.TotalVnd});return new {o.Id,o.Status,o.TotalVnd,o.Version};
    });
    public async Task<IResult> Reopen(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        Require("CASHIER","MANAGER","ADMIN");var o=await OrderLock(id);CheckVersion(o,b);Rules.Require(o.Status=="AWAITING_PAYMENT","INVALID_TRANSITION","Chỉ mở lại đơn chưa thanh toán đang chờ trả.");var reason=Text(b,"reason");
        o.Status="SERVING";o.TotalVnd=null;o.StatusChangedAt=DateTimeOffset.UtcNow;o.Touch();Audit("ORDER_REOPENED","ORDER",id,new {status="AWAITING_PAYMENT"},new {o.Status},reason);return new {o.Id,o.Status,o.Version};
    });
    public async Task<IResult> Pay(Guid id,JsonObject b)=>await Command(b,async()=>
    {
        var a=Require("CASHIER","MANAGER","ADMIN");var o=await OrderLock(id);CheckVersion(o,b);Rules.Require(o.Status=="AWAITING_PAYMENT","INVALID_TRANSITION","Đơn chưa sẵn sàng thu tiền hoặc đã thanh toán.");
        var amount=Number(b,"amountVnd");var method=Text(b,"method",16);long? tendered=b["tenderedVnd"]==null?null:Number(b,"tenderedVnd");var reference=Optional(b,"transferReference",120);
        Rules.Require(amount==o.TotalVnd,"AMOUNT_MISMATCH","Số tiền phải bằng tổng đã chốt.");Rules.Payment(method,amount,tendered,reference);
        var payment=new Payment {OrderId=id,ReceiptNo="BN-"+Token()[..14].ToUpperInvariant(),Method=method,AmountVnd=amount,TenderedVnd=tendered,TransferReference=reference,ReceivedByStaffId=a.Id};db.Payments.Add(payment);
        o.Status="PAID";o.StatusChangedAt=DateTimeOffset.UtcNow;o.Touch();await EndVisit(o,"CLOSED");Audit("PAYMENT_RECORDED","PAYMENT",payment.Id,null,new {payment.OrderId,payment.ReceiptNo,payment.Method,payment.AmountVnd});return new {payment.Id,payment.ReceiptNo,payment.Method,payment.AmountVnd,payment.PaidAt,changeVnd=tendered-amount,orderStatus=o.Status,orderVersion=o.Version};
    });
    public async Task<object> Receipt(Guid id)
    {
        Require("WAITER","CASHIER","MANAGER","ADMIN");var p=await db.Payments.AsNoTracking().SingleOrDefaultAsync(p=>p.OrderId==id)??throw new ApiException(404,"RESOURCE_NOT_FOUND","Chưa có biên nhận.");
        return new {p.Id,p.ReceiptNo,p.Method,p.AmountVnd,p.TenderedVnd,p.TransferReference,p.PaidAt,changeVnd=p.TenderedVnd-p.AmountVnd,order=await OrderRead(id)};
    }
}
