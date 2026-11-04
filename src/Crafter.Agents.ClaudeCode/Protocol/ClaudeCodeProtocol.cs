using System.Text;
using System.Text.Json;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Decisions;

namespace Crafter.Agents.ClaudeCode.Protocol;

public static class ClaudeCodeProtocol
{
    public const string Introduction = """{"type":"control_request","request_id":"crafter","request":{"subtype":"initialize"}}""";

    private const string Refusal = "The user denied this in Crafter. Don't try it again; take another way or ask them how to proceed.";

    private const string Silence = "The user chose not to answer. Carry on with your best judgment or ask in plain text.";

    public static string Asking(Prompt prompt) =>
        Json(writer =>
        {
            writer.WriteString("type", "user");
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteString("content", prompt.Text);
            writer.WriteEndObject();
        });

    public static string Answering(string request, Decision decision, string input, string? suggestions, bool asked) =>
        Json(writer =>
        {
            writer.WriteString("type", "control_response");
            writer.WriteStartObject("response");
            writer.WriteString("subtype", "success");
            writer.WriteString("request_id", request);
            writer.WriteStartObject("response");
            switch (decision)
            {
                case Deny deny:
                    writer.WriteString("behavior", "deny");
                    writer.WriteString("message", Refused(deny.Reason, asked));
                    break;
                case Answers answers:
                    writer.WriteString("behavior", "allow");
                    writer.WritePropertyName("updatedInput");
                    Answered(writer, input, answers);
                    break;
                default:
                    writer.WriteString("behavior", "allow");
                    writer.WritePropertyName("updatedInput");
                    writer.WriteRawValue(input);
                    if (decision is AlwaysAllow && suggestions is not null)
                    {
                        writer.WritePropertyName("updatedPermissions");
                        writer.WriteRawValue(suggestions);
                    }

                    break;
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });

    private static string Refused(string? reason, bool asked) =>
        reason?.Trim() is { Length: > 0 } said
            ? $"The user {(asked ? "chose not to answer" : "denied this in Crafter")} and said: {said}"
            : asked ? Silence : Refusal;

    private static void Answered(Utf8JsonWriter writer, string input, Answers answers)
    {
        using var document = JsonDocument.Parse(input);
        writer.WriteStartObject();
        foreach (var property in document.RootElement.EnumerateObject().Where(property => property.Name != "answers"))
        {
            property.WriteTo(writer);
        }

        writer.WriteStartObject("answers");
        foreach (var (question, answer) in answers.Chosen)
        {
            writer.WriteString(question, answer);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
