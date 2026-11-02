using Restaurant.Api.Domain;
using Xunit;

public class PaymentHistoryQueryTests
{
    private static PaymentHistoryQuery Parse(string? from=null,string? to=null,string? method=null,string? receiver=null,string? page=null,string? size=null,string? search=null)=>PaymentHistoryQuery.Parse(search,method,null,receiver,from,to,page,size);
    [Fact] public void DefaultsAllowAllPaidHistory(){var q=Parse();Assert.Null(q.PaidFrom);Assert.Null(q.PaidUntil);Assert.Equal(1,q.Page);Assert.Equal(20,q.PageSize);}
    [Fact] public void RestaurantDayUsesInclusiveStartExclusiveEndInUtc(){var q=Parse("2026-11-03","2026-11-03");Assert.Equal(DateTimeOffset.Parse("2026-11-02T17:00:00Z"),q.PaidFrom);Assert.Equal(DateTimeOffset.Parse("2026-11-03T17:00:00Z"),q.PaidUntil);}
    [Theory][InlineData("2026-11-31")][InlineData("03/11/2026")][InlineData("2026-11-03T00:00:00Z")]
    public void RejectInvalidCalendarDates(string value)=>Assert.Equal(422,Assert.Throws<ApiException>(()=>Parse(value)).Status);
    [Fact] public void RejectReversedRange()=>Assert.Throws<ApiException>(()=>Parse("2026-11-04","2026-11-03"));
    [Theory][InlineData("0","20")][InlineData("1","101")][InlineData("1.5","20")][InlineData("1000001","20")]
    public void RejectInvalidPagination(string page,string size)=>Assert.Throws<ApiException>(()=>Parse(page:page,size:size));
    [Fact] public void RejectUnknownMethodOrReceiver(){Assert.Throws<ApiException>(()=>Parse(method:"CARD"));Assert.Throws<ApiException>(()=>Parse(receiver:"cashier"));}
    [Fact] public void SearchEscapesSqlLikeWildcards()=>Assert.Equal("%\\%\\_\\\\%",Parse(search:"%_\\").LikePattern);
}
