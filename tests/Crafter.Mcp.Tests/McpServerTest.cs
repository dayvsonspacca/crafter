using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Crafter.Mcp.Tests;

public sealed class McpServerTest : IDisposable
{
    private readonly McpServer server;

    private readonly HttpClient http = new();

    public McpServerTest()
    {
        McpTool[] tools =
        [
            new("whoami", "Says who called.", Schema(), ReadOnly: true, call => Task.FromResult(new McpResult($"you are {call.Caller}"))),
            new("echo", "Repeats a word.", Schema("word"), ReadOnly: false, call => Task.FromResult(new McpResult(call.Text("word") ?? "nothing"))),
            new("broken", "Always fails.", Schema(), ReadOnly: false, _ => throw new InvalidOperationException("it broke")),
        ];
        server = new McpServer("crafter", "1.0", tools, token => token == "secret" ? "/home/hero/crafter" : null);
        server.Start();
    }

    [Fact]
    public async Task TurnsAwayCallersWithoutTheirToken()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(Request(1, "ping"), token: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(Request(1, "ping"), token: "guess")).StatusCode);
    }

    [Fact]
    public async Task TurnsAwayBrowsersAndOtherMethods()
    {
        using var fromPage = new HttpRequestMessage(HttpMethod.Post, server.Endpoint) { Content = Json(Request(1, "ping")) };
        fromPage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");
        fromPage.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(fromPage, TestContext.Current.CancellationToken)).StatusCode);

        using var listen = new HttpRequestMessage(HttpMethod.Get, server.Endpoint);
        listen.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await http.SendAsync(listen, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task InitializesWithTheVersionTheClientSpeaks()
    {
        var answer = await Ask(Request(1, "initialize", new JsonObject { ["protocolVersion"] = "2025-03-26" }));

        Assert.Equal("2025-03-26", answer["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.Equal("crafter", answer["result"]!["serverInfo"]!["name"]!.GetValue<string>());
        Assert.NotNull(answer["result"]!["capabilities"]!["tools"]);
        Assert.Null(answer["result"]!["instructions"]);
    }

    [Fact]
    public async Task ListsItsToolsMarkingTheReadOnlyOnes()
    {
        var tools = (await Ask(Request(2, "tools/list")))["result"]!["tools"]!.AsArray();

        Assert.Equal(["whoami", "echo", "broken"], tools.Select(tool => tool!["name"]!.GetValue<string>()));
        Assert.True(tools[0]!["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.False(tools[1]!["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.Equal("word", tools[1]!["inputSchema"]!["required"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task CallsAToolWithItsArgumentsAndTheCaller()
    {
        Assert.Equal("you are /home/hero/crafter", Text(await Ask(Call(3, "whoami"))));
        Assert.Equal("anvil", Text(await Ask(Call(4, "echo", new JsonObject { ["word"] = "anvil" }))));
    }

    [Fact]
    public async Task ReportsAFailingOrMissingToolAsAnErrorResult()
    {
        var broken = await Ask(Call(5, "broken"));
        Assert.True(broken["result"]!["isError"]!.GetValue<bool>());
        Assert.Equal("it broke", Text(broken));

        Assert.Equal("There is no tool called fly.", Text(await Ask(Call(6, "fly"))));
    }

    [Fact]
    public async Task AcceptsNotificationsWithoutAnAnswerAndRefusesUnknownMethods()
    {
        var notified = await Post(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }, "secret");
        Assert.Equal(HttpStatusCode.Accepted, notified.StatusCode);

        Assert.Equal(-32601, (await Ask(Request(7, "resources/list")))["error"]!["code"]!.GetValue<int>());
    }

    public void Dispose()
    {
        http.Dispose();
        server.Dispose();
    }

    private async Task<JsonNode> Ask(JsonObject request)
    {
        var response = await Post(request, "secret");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<HttpResponseMessage> Post(JsonObject request, string? token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, server.Endpoint) { Content = Json(request) };
        if (token is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await http.SendAsync(message, TestContext.Current.CancellationToken);
    }

    private static StringContent Json(JsonObject request) => new(request.ToJsonString(), Encoding.UTF8, "application/json");

    private static JsonObject Request(int id, string method, JsonObject? parameters = null) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters ?? [] };

    private static JsonObject Call(int id, string tool, JsonObject? arguments = null) =>
        Request(id, "tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments ?? [] });

    private static string Text(JsonNode answer) => answer["result"]!["content"]![0]!["text"]!.GetValue<string>();

    private static JsonObject Schema(params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(required.Select(each => KeyValuePair.Create(each, (JsonNode?)new JsonObject { ["type"] = "string" }))),
        ["required"] = new JsonArray([.. required.Select(each => (JsonNode)each)]),
    };
}
