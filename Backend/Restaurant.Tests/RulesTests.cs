using Restaurant.Api.Domain;
using Xunit;
public class RulesTests
{
    [Theory]
    [InlineData("PLACED","ACCEPTED",true)]
    [InlineData("ACCEPTED","PREPARING",true)]
    [InlineData("PREPARING","READY",true)]
    [InlineData("READY","SERVED",true)]
    [InlineData("PLACED","READY",false)]
    [InlineData("SERVED","CANCELLED",false)]
    [InlineData("PREPARING","PLACED",false)]
    [InlineData("PENDING_APPROVAL","PREPARING",false)]
    public void StateMachineDoesNotSkipOrRewind(string from,string to,bool allowed)=>Assert.Equal(allowed,Rules.CanTransition(from,to));
    [Fact] public void BillingOnlyIncludesServedSnapshotPrices()=>Assert.Equal(190000,Rules.Total([
        new(){Status="SERVED",Quantity=2,UnitPriceVnd=95000},new(){Status="CANCELLED",Quantity=1,UnitPriceVnd=145000},new(){Status="PENDING_APPROVAL",Quantity=3,UnitPriceVnd=95000}]));
    [Fact] public void CashMustCoverTotal()=>Assert.Throws<ApiException>(()=>Rules.Payment("CASH",190000,100000,null));
    [Fact] public void TransferNeedsReference()=>Assert.Throws<ApiException>(()=>Rules.Payment("BANK_TRANSFER",190000,null," "));
    [Fact] public void ZeroDoesNotInventCash(){Rules.Payment("ZERO",0,null,null);Assert.Throws<ApiException>(()=>Rules.Payment("CASH",0,0,null));}
    [Fact] public void StaleVersionIsRejected()=>Assert.Throws<ApiException>(()=>Rules.Version(new Order{Version=3},2));
}
