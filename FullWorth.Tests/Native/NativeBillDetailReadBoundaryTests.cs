using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FullWorth.Services;
namespace FullWorth.Tests.Native;
public sealed class NativeBillDetailReadBoundaryTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("wrong-id")]
    [InlineData("empty-id")]
    [InlineData("provider")]
    [InlineData("statements-null")]
    [InlineData("changes-null")]
    [InlineData("statement-null")]
    [InlineData("change-null")]
    [InlineData("statement-duplicate")]
    [InlineData("change-duplicate")]
    [InlineData("statement-empty-id")]
    [InlineData("change-empty-id")]
    [InlineData("accepted")]
    [InlineData("wrong-media")]
    [InlineData("malformed")]
    public async Task InvalidDetail_IsNotAConfirmedBill(string mode)
    {
        var id = Guid.NewGuid();
        var detail = Valid(id);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonSerializer.SerializeToNode(detail, options)!.AsObject();
        switch (mode)
        {
            case "wrong-id": node["id"] = Guid.NewGuid(); break;
            case "empty-id": node["id"] = Guid.Empty; break;
            case "provider": node["providerName"] = " "; break;
            case "statements-null": node["statements"] = null; break;
            case "changes-null": node["changes"] = null; break;
            case "statement-null": node["statements"] = JsonNode.Parse("[null]"); break;
            case "change-null": node["changes"] = JsonNode.Parse("[null]"); break;
            case "statement-duplicate": node["statements"] = JsonSerializer.SerializeToNode(new[] {detail.Statements[0],detail.Statements[0]},options); break;
            case "change-duplicate": node["changes"] = JsonSerializer.SerializeToNode(new[] {detail.Changes[0],detail.Changes[0]},options); break;
            case "statement-empty-id": node["statements"]![0]!["id"] = Guid.Empty; break;
            case "change-empty-id": node["changes"]![0]!["id"] = Guid.Empty; break;
        }
        var json = mode == "null" ? "null" : mode == "malformed" ? "{PRIVATE-CANARY" : node.ToJsonString();
        using var handler = new Handler(json, mode == "accepted" ? HttpStatusCode.Accepted : HttpStatusCode.OK,
            mode == "wrong-media" ? "text/plain" : "application/json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://native-detail.test") };
        var api = new FullWorthApiClient(http);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => api.GetBillStreamDetailAsync("synthetic-access",id));
        Assert.DoesNotContain("PRIVATE",error.Message);
        Assert.Equal(1,handler.Reads);
    }

    [Fact]
    public async Task OldResponseWithoutAnnualFlag_PreservesHistoryWithoutInventingEvidence()
    {
        var id = Guid.NewGuid();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonSerializer.SerializeToNode(Valid(id),options)!;
        node["changes"]![0]!.AsObject().Remove("hasAnnualizedImpactEvidence");
        using var http = new HttpClient(new Handler(node.ToJsonString())) { BaseAddress = new Uri("https://native-detail.test") };
        var result = await new FullWorthApiClient(http).GetBillStreamDetailAsync("synthetic-access",id);
        Assert.Equal(id,result.Id);
        Assert.False(Assert.Single(result.Changes).HasAnnualizedImpactEvidence);
        Assert.Equal(240m,result.Changes[0].AnnualizedImpact);
        Assert.Single(result.Statements);
    }

    [Fact]
    public async Task ConfirmedEmptyHistory_RemainsValid()
    {
        var id = Guid.NewGuid();
        var detail = Valid(id) with { Statements = [], Changes = [] };
        using var http = new HttpClient(new Handler(JsonSerializer.Serialize(detail,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))) { BaseAddress = new Uri("https://native-detail.test") };
        var result = await new FullWorthApiClient(http).GetBillStreamDetailAsync("synthetic-access",id);
        Assert.Empty(result.Statements);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task EmptyRequestIdentity_FailsBeforeNetwork()
    {
        using var handler = new Handler("{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://native-detail.test") };
        await Assert.ThrowsAsync<ArgumentException>(() => new FullWorthApiClient(http)
            .GetBillStreamDetailAsync("synthetic-access",Guid.Empty));
        Assert.Equal(0,handler.Reads);
    }

    private static BillStreamDetailResult Valid(Guid id)
    {
        var statement = Guid.NewGuid();
        return new(id,"Synthetic provider","Utility",true,0m,0m,
            [new(statement,new(2026,1,1),new(2026,3,31),null,null,120m,"USD")],
            [new(Guid.NewGuid(),null,statement,"TotalIncrease","Unknown","Synthetic history",
                100m,120m,20m,240m,false,DateTimeOffset.UtcNow){CurrencyCode="USD"}]);
    }
    private sealed class Handler(string json,HttpStatusCode status=HttpStatusCode.OK,string media="application/json") : HttpMessageHandler
    {
        public int Reads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Reads++;
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.Equal("synthetic-access",request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent(json,System.Text.Encoding.UTF8,media) });
        }
    }
}
