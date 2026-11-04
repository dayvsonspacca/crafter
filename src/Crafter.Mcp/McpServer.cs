using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crafter.Mcp;

// A Model Context Protocol server over HTTP for agents on this machine: JSON-RPC posted to
// /mcp, answered as JSON. It listens on loopback only, wants a bearer token that names the
// caller, and turns away browsers, which send an Origin.
public sealed class McpServer(string name, string version, IReadOnlyList<McpTool> tools, Func<string, string?> callerOf) : IDisposable
{
    private static readonly string[] Versions = ["2025-06-18", "2025-03-26", "2024-11-05"];

    private readonly HttpListener listener = new();

    private readonly CancellationTokenSource closing = new();

    public Uri? Endpoint { get; private set; }

    public void Start()
    {
        var port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");
        listener.Start();
        Endpoint = new Uri($"http://127.0.0.1:{port}/mcp");
        _ = Listen();
    }

    public void Dispose()
    {
        closing.Cancel();
        if (listener.IsListening)
        {
            listener.Stop();
        }

        listener.Close();
        closing.Dispose();
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    private async Task Listen()
    {
        while (!closing.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception stopped) when (stopped is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => Serve(context));
        }
    }

    private async Task Serve(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            var request = context.Request;
            if (request.Headers["Origin"] is not null)
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var authorization = request.Headers["Authorization"];
            if (authorization is null
                || !authorization.StartsWith("Bearer ", StringComparison.Ordinal)
                || callerOf(authorization["Bearer ".Length..].Trim()) is not { } caller)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.AddHeader("Allow", "POST");
                return;
            }

            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var answer = await Answer(await reader.ReadToEndAsync(closing.Token), caller);
            if (answer is null)
            {
                response.StatusCode = (int)HttpStatusCode.Accepted;
                return;
            }

            var body = Encoding.UTF8.GetBytes(answer.ToJsonString());
            response.ContentType = "application/json";
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body, closing.Token);
        }
        catch (Exception broken) when (broken is HttpListenerException or IOException or OperationCanceledException or ObjectDisposedException)
        {
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception gone) when (gone is HttpListenerException or ObjectDisposedException)
            {
            }
        }
    }

    // A message, or a batch of them; notifications get no answer.
    internal async Task<JsonNode?> Answer(string body, string caller)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return Error(null, -32700, "Parse error");
        }

        if (message is JsonArray batch)
        {
            var answers = new JsonArray();
            foreach (var each in batch)
            {
                if (each is JsonObject single && await Reply(single, caller) is { } answer)
                {
                    answers.Add(answer);
                }
            }

            return answers.Count > 0 ? answers : null;
        }

        return message is JsonObject one ? await Reply(one, caller) : Error(null, -32600, "Invalid Request");
    }

    private async Task<JsonNode?> Reply(JsonObject message, string caller)
    {
        var id = message["id"]?.DeepClone();
        var method = message["method"]?.GetValue<string>();
        var parameters = message["params"] as JsonObject;

        if (id is null)
        {
            return null;
        }

        return method switch
        {
            "initialize" => Result(id, Initialize(parameters)),
            "ping" => Result(id, new JsonObject()),
            "tools/list" => Result(id, new JsonObject { ["tools"] = new JsonArray([.. tools.Select(Describe)]) }),
            "tools/call" => Result(id, await Call(parameters, caller)),
            _ => Error(id, -32601, $"Method not found: {method}"),
        };
    }

    private JsonObject Initialize(JsonObject? parameters)
    {
        var asked = parameters?["protocolVersion"]?.GetValue<string>();

        return new JsonObject
        {
            ["protocolVersion"] = asked is not null && Versions.Contains(asked) ? asked : Versions[0],
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject { ["name"] = name, ["version"] = version },
        };
    }

    private static JsonObject Describe(McpTool tool) => new()
    {
        ["name"] = tool.Name,
        ["description"] = tool.Description,
        ["inputSchema"] = tool.InputSchema.DeepClone(),
        ["annotations"] = new JsonObject { ["readOnlyHint"] = tool.ReadOnly },
    };

    private async Task<JsonObject> Call(JsonObject? parameters, string caller)
    {
        var wanted = parameters?["name"]?.GetValue<string>();
        McpResult result;

        if (tools.FirstOrDefault(tool => tool.Name == wanted) is not { } tool)
        {
            result = McpResult.Failed($"There is no tool called {wanted}.");
        }
        else
        {
            try
            {
                result = await tool.Run(new McpCall(parameters?["arguments"] as JsonObject ?? [], caller));
            }
            catch (Exception failure)
            {
                result = McpResult.Failed(failure.Message);
            }
        }

        return new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = result.Text }),
            ["isError"] = result.IsError,
        };
    }

    private static JsonObject Result(JsonNode id, JsonNode result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };
}
