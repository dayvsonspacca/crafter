using Crafter.Core.Conversations;

namespace Crafter.Agents.ClaudeCode.Tests;

public sealed class TranscriptTest
{
    [Fact]
    public void HearsWhichModelBegan()
    {
        var replies = new Transcript().Hear("""{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}""");

        Assert.Equal([new Began("Opus 5.5")], replies);
    }

    [Fact]
    public void RemembersTheSession()
    {
        var transcript = new Transcript();

        transcript.Hear("""{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}""");
        transcript.Hear("""{"type":"rate_limit_event"}""");

        Assert.Equal("s-1", transcript.SessionId);
    }

    [Fact]
    public void IgnoresAnIntroductionWithoutModel()
    {
        Assert.Empty(new Transcript().Hear("""{"type":"system","subtype":"init","session_id":"s-1"}"""));
    }

    [Fact]
    public void IgnoresOtherSystemMessages()
    {
        Assert.Empty(new Transcript().Hear("""{"type":"system","subtype":"status","status":"requesting"}"""));
    }

    [Fact]
    public void HearsWhatWasWritten()
    {
        var replies = new Transcript().Hear(
            """{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"Olá"}}}""");

        Assert.Equal([new Wrote("Olá")], replies);
    }

    [Theory]
    [InlineData("""{"type":"stream_event"}""")]
    [InlineData("""{"type":"stream_event","event":{"type":"message_start"}}""")]
    [InlineData("""{"type":"stream_event","event":{"type":"content_block_delta"}}""")]
    [InlineData("""{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"thinking_delta","thinking":"hm"}}}""")]
    [InlineData("""{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":""}}}""")]
    public void IgnoresStreamEventsWithoutText(string line)
    {
        Assert.Empty(new Transcript().Hear(line));
    }

    [Fact]
    public void HearsAFinishedAnswer()
    {
        var replies = new Transcript().Hear("""{"type":"result","subtype":"success","is_error":false,"stop_reason":"end_turn"}""");

        Assert.Equal([new Finished()], replies);
    }

    [Fact]
    public void HearsAnAnswerCutShort()
    {
        var replies = new Transcript().Hear("""{"type":"result","subtype":"success","is_error":false,"stop_reason":"max_tokens"}""");

        Assert.Equal([new CutShort()], replies);
    }

    [Fact]
    public void HearsARefusalWithItsCategory()
    {
        var replies = new Transcript().Hear(
            """{"type":"result","subtype":"success","is_error":false,"stop_reason":"refusal","stop_details":{"category":"cyber"}}""");

        Assert.Equal([new Refused("cyber")], replies);
    }

    [Fact]
    public void HearsARefusalWithoutCategory()
    {
        var replies = new Transcript().Hear("""{"type":"result","subtype":"success","is_error":false,"stop_reason":"refusal","stop_details":null}""");

        Assert.Equal([new Refused(null)], replies);
    }

    [Fact]
    public void HearsAFailureWithWhatClaudeCodeSaid()
    {
        var replies = new Transcript().Hear("""{"type":"result","subtype":"success","is_error":true,"result":"Not logged in"}""");

        Assert.Equal([new Failed("Not logged in")], replies);
    }

    [Fact]
    public void HearsAFailureByItsKind()
    {
        var replies = new Transcript().Hear("""{"type":"result","subtype":"error_during_execution","is_error":true}""");

        Assert.Equal([new Failed("error_during_execution")], replies);
    }

    [Fact]
    public void HearsAFailureThatSaysNothing()
    {
        var replies = new Transcript().Hear("""{"type":"result","is_error":true}""");

        Assert.Equal([new Failed("Claude Code failed without saying why.")], replies);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"type":"user"}""")]
    [InlineData("""{"session_id":42}""")]
    public void IgnoresWhatItDoesNotUnderstand(string line)
    {
        Assert.Empty(new Transcript().Hear(line));
    }
}
