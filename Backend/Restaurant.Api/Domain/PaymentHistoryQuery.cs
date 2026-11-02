using System.Globalization;
namespace Restaurant.Api.Domain;

public record PaymentHistoryQuery(string? Search,string? Method,string? OrderType,Guid? ReceivedByStaffId,DateTimeOffset? PaidFrom,DateTimeOffset? PaidUntil,int Page,int PageSize)
{
    public static PaymentHistoryQuery Parse(string? search,string? method,string? orderType,string? receiver,string? fromDate,string? toDate,string? page,string? pageSize)
    {
        search=string.IsNullOrWhiteSpace(search)?null:search.Trim();
        Rules.Require(search==null||search.Length<=120,"VALIDATION_FAILED","Từ khóa tối đa120 ký tự.",422);
        method=string.IsNullOrEmpty(method)?null:method;
        orderType=string.IsNullOrEmpty(orderType)?null:orderType;
        Rules.Require(method==null||method is "CASH" or "BANK_TRANSFER" or "ZERO","VALIDATION_FAILED","Phương thức không hợp lệ.",422);
        Rules.Require(orderType==null||orderType is "DINE_IN" or "TAKEAWAY","VALIDATION_FAILED","Loại đơn không hợp lệ.",422);
        Guid? staff=null;
        if(!string.IsNullOrEmpty(receiver)) {Rules.Require(Guid.TryParse(receiver,out _),"VALIDATION_FAILED","receivedByStaffId phải là UUID.",422);staff=Guid.Parse(receiver);}
        var from=Date(fromDate,"fromDate");var to=Date(toDate,"toDate");
        Rules.Require(from==null||to==null||from<=to,"VALIDATION_FAILED","Ngày bắt đầu không được sau ngày kết thúc.",422);
        Rules.Require(to!=DateOnly.MaxValue,"VALIDATION_FAILED","Ngày kết thúc vượt giới hạn.",422);
        // Restaurant calendar dates are UTC+07:00; the upper bound is exclusive.
        return new(search,method,orderType,staff,from.HasValue?Midnight(from.Value):null,to.HasValue?Midnight(to.Value.AddDays(1)):null,Positive(page,"page",1,1_000_000),Positive(pageSize,"pageSize",20,100));
    }
    public string? LikePattern=>Search==null?null:"%"+Search.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";
    private static DateOnly? Date(string? value,string field)
    {
        if(string.IsNullOrEmpty(value))return null;
        Rules.Require(DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),"VALIDATION_FAILED",$"{field} phải có định dạng yyyy-MM-dd.",422);
        var date=DateOnly.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        Rules.Require(date>DateOnly.MinValue,"VALIDATION_FAILED",$"{field} nằm ngoài khoảng thời gian hỗ trợ.",422);
        return date;
    }
    private static DateTimeOffset Midnight(DateOnly date)=>new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(7)).ToUniversalTime();
    private static int Positive(string? value,string field,int fallback,int max)
    {
        if(string.IsNullOrEmpty(value))return fallback;
        Rules.Require(int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>=1&&n<=max,"VALIDATION_FAILED",$"{field} phải là số nguyên1..{max}.",422);
        return n;
    }
}
