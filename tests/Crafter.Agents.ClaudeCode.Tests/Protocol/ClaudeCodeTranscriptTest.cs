using Crafter.Agents.ClaudeCode.Protocol;
using Crafter.Core.Agents.Background;
using Crafter.Core.Conversations.Replies;

namespace Crafter.Agents.ClaudeCode.Tests.Protocol;

public sealed class ClaudeCodeTranscriptTest
{
    [Fact]
    public void HearsWhichModelBegan()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}""");

        Assert.Equal([new Began("Opus 5.5")], replies);
    }

    [Fact]
    public void RemembersTheSession()
    {
        var transcript = new ClaudeCodeTranscript();

        transcript.Hear("""{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}""");
        transcript.Hear("""{"type":"rate_limit_event"}""");

        Assert.Equal("s-1", transcript.SessionId);
    }

    [Fact]
    public void IgnoresAnIntroductionWithoutModel()
    {
        Assert.Empty(new ClaudeCodeTranscript().Hear("""{"type":"system","subtype":"init","session_id":"s-1"}"""));
    }

    [Fact]
    public void IgnoresOtherSystemMessages()
    {
        Assert.Empty(new ClaudeCodeTranscript().Hear("""{"type":"system","subtype":"status","status":"requesting"}"""));
    }

    [Fact]
    public void HearsWhatWasWritten()
    {
        var replies = new ClaudeCodeTranscript().Hear(
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
        Assert.Empty(new ClaudeCodeTranscript().Hear(line));
    }

    [Fact]
    public void HearsAFinishedAnswer()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"result","subtype":"success","is_error":false,"stop_reason":"end_turn"}""");

        Assert.Equal([new Finished()], replies);
    }

    [Fact]
    public void HearsAnAnswerCutShort()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"result","subtype":"success","is_error":false,"stop_reason":"max_tokens"}""");

        Assert.Equal([new CutShort()], replies);
    }

    [Fact]
    public void HearsARefusalWithItsCategory()
    {
        var replies = new ClaudeCodeTranscript().Hear(
            """{"type":"result","subtype":"success","is_error":false,"stop_reason":"refusal","stop_details":{"category":"cyber"}}""");

        Assert.Equal([new Refused("cyber")], replies);
    }

    [Fact]
    public void HearsARefusalWithoutCategory()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"result","subtype":"success","is_error":false,"stop_reason":"refusal","stop_details":null}""");

        Assert.Equal([new Refused(null)], replies);
    }

    [Fact]
    public void HearsAFailureWithWhatClaudeCodeSaid()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"result","subtype":"success","is_error":true,"result":"Not logged in"}""");

        Assert.Equal([new Failed("Not logged in")], replies);
    }

    [Fact]
    public void HearsAFailureByItsKind()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"result","subtype":"error_during_execution","is_error":true}""");

        Assert.Equal([new Failed("error_during_execution")], replies);
    }

    [Fact]
    public void HearsAFailureThatSaysNothing()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"result","is_error":true}""");

        Assert.Equal([new Failed("Claude Code failed without saying why.")], replies);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"type":"user"}""")]
    [InlineData("""{"session_id":42}""")]
    public void IgnoresWhatItDoesNotUnderstand(string line)
    {
        Assert.Empty(new ClaudeCodeTranscript().Hear(line));
    }

    [Fact]
    public void HearsTheToolsItUses()
    {
        var transcript = new ClaudeCodeTranscript("/home/hero/crafter");

        var replies = transcript.Hear("""{"type":"assistant","message":{"content":[{"type":"text","text":"ok"},{"type":"tool_use","id":"t-1","name":"Edit","input":{"file_path":"/home/hero/crafter/a.cs","old_string":"x","new_string":"y"}},{"type":"tool_use","name":"NoId"},"junk"]}}""");

        Assert.Equal([new Acting("t-1", new Tool(ToolKind.Edit, "Edit"), "a.cs", new Changes(1, 1))], replies);
    }

    [Fact]
    public void HearsWhatTheToolsAnswered()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t-1","content":"done"},{"type":"tool_result","tool_use_id":"t-2","is_error":true,"content":[{"type":"text","text":"no"},{"type":"image"},"x"]},{"type":"tool_result","tool_use_id":"t-3"},{"type":"tool_result","tool_use_id":"t-4","content":42},{"type":"tool_result"}]}}""");

        Assert.Equal(
            [new Acted("t-1", false, "done"), new Acted("t-2", true, "no"), new Acted("t-3", false, string.Empty), new Acted("t-4", false, string.Empty)],
            replies);
    }

    [Theory]
    [InlineData("""{"type":"user","message":{"content":"plain prompt"}}""")]
    [InlineData("""{"type":"assistant","message":"odd"}""")]
    [InlineData("""{"type":"assistant"}""")]
    public void HearsNothingFromMessagesWithoutTools(string line)
    {
        Assert.Empty(new ClaudeCodeTranscript().Hear(line));
    }

    [Fact]
    public void HearsARequestForApprovalAndKeepsItsInput()
    {
        var transcript = new ClaudeCodeTranscript("/home/hero/crafter");

        var replies = transcript.Hear("""{"type":"control_request","request_id":"r-1","request":{"subtype":"can_use_tool","tool_name":"Bash","display_name":"Bash","input":{"command":"dotnet test"},"description":"Run the tests"}}""");

        Assert.Equal([new NeedsApproval("r-1", new Tool(ToolKind.Shell, "Bash"), "dotnet test", "Run the tests")], replies);
        Assert.Equal("""{"command":"dotnet test"}""", transcript.InputOf("r-1"));
        Assert.Equal("{}", transcript.InputOf("r-9"));
    }

    [Fact]
    public void KeepsAnEmptyInputWhenTheRequestHasNone()
    {
        var transcript = new ClaudeCodeTranscript();

        var replies = transcript.Hear("""{"type":"control_request","request_id":"r-2","request":{"subtype":"can_use_tool","tool_name":"Mystery"}}""");

        Assert.Equal([new NeedsApproval("r-2", new Tool(ToolKind.Other, "Mystery"), "Mystery", null)], replies);
        Assert.Equal("{}", transcript.InputOf("r-2"));
    }

    [Theory]
    [InlineData("""{"type":"control_request","request":{"subtype":"can_use_tool","tool_name":"Bash"}}""")]
    [InlineData("""{"type":"control_request","request_id":"r-1"}""")]
    [InlineData("""{"type":"control_request","request_id":"r-1","request":{"subtype":"interrupt"}}""")]
    [InlineData("""{"type":"control_request","request_id":"r-1","request":{"subtype":"can_use_tool"}}""")]
    public void IgnoresOtherControlRequests(string line)
    {
        Assert.Empty(new ClaudeCodeTranscript().Hear(line));
    }

    [Fact]
    public void SaysWhatAlwaysAllowingWouldRemember()
    {
        var transcript = new ClaudeCodeTranscript();

        var replies = transcript.Hear("""{"type":"control_request","request_id":"r-3","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"npm test"},"permission_suggestions":[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"npm test:*"}],"behavior":"allow","destination":"localSettings"},{"type":"addDirectories","directories":["/tmp/out"],"destination":"localSettings"}]}}""");

        Assert.Equal([new Rule("Bash(npm test:*)", Reach.Project), new Rule("/tmp/out", Reach.Project)], Assert.IsType<NeedsApproval>(Assert.Single(replies)).Remembers);
        Assert.Contains("\"ruleContent\":\"npm test:*\"", transcript.SuggestionsOf("r-3"));
    }

    [Theory]
    [InlineData("""[{"type":"addRules","rules":[{"toolName":"WebFetch"}],"behavior":"allow","destination":"session"}]""", "WebFetch@Chat")]
    [InlineData("""[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"ls /tmp/x"}],"behavior":"allow","destination":"localSettings"},{"type":"addRules","rules":[{"toolName":"Read","ruleContent":"//tmp/x/**"}],"behavior":"allow","destination":"session"}]""", "Bash(ls /tmp/x)@Project, Read(//tmp/x/**)@Chat")]
    [InlineData("""[{"type":"setMode","mode":"acceptEdits","destination":"session"}]""", "acceptEdits mode@Chat")]
    [InlineData("""[{"type":"addRules","rules":[{"toolName":"Read"}],"behavior":"allow","destination":"userSettings"}]""", "Read@Everywhere")]
    [InlineData("""[{"type":"addRules","rules":[{"toolName":"Bash"}],"behavior":"deny","destination":"session"}]""", null)]
    [InlineData("[]", null)]
    public void DescribesEachKindOfSuggestion(string suggestions, string? remembers)
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"control_request","request_id":"r-4","request":{"subtype":"can_use_tool","tool_name":"Bash","permission_suggestions":""" + suggestions + "}}");

        var rules = Assert.IsType<NeedsApproval>(Assert.Single(replies)).Remembers;

        Assert.Equal(remembers, rules is null ? null : string.Join(", ", rules.Select(rule => $"{rule.What}@{rule.Reach}")));
    }

    [Fact]
    public void HearsQuestionsForTheUser()
    {
        var transcript = new ClaudeCodeTranscript();

        var replies = transcript.Hear("""{"type":"control_request","request_id":"r-5","request":{"subtype":"can_use_tool","tool_name":"AskUserQuestion","input":{"questions":[{"question":"Which database?","header":"Storage","multiSelect":false,"options":[{"label":"SQLite","description":"A file"},{"label":"Postgres","description":"A server"}]},{"question":"Which extras?","header":"Extras","multiSelect":true,"options":[{"label":"Cache"}]}]}}}""");

        var asked = Assert.IsType<NeedsAnswers>(Assert.Single(replies));
        Assert.Equal("r-5", asked.Id);
        Assert.Equal(2, asked.Questions.Count);
        Assert.Equal(("Which database?", "Storage", false), (asked.Questions[0].Text, asked.Questions[0].Header, asked.Questions[0].Several));
        Assert.Equal([new Pick("SQLite", "A file"), new Pick("Postgres", "A server")], asked.Questions[0].Picks);
        Assert.True(asked.Questions[1].Several);
        Assert.Equal([new Pick("Cache", string.Empty)], asked.Questions[1].Picks);
        Assert.Contains("Which database?", transcript.InputOf("r-5"));
    }

    [Fact]
    public void AsksForApprovalWhenTheQuestionsMakeNoSense()
    {
        var replies = new ClaudeCodeTranscript().Hear("""{"type":"control_request","request_id":"r-6","request":{"subtype":"can_use_tool","tool_name":"AskUserQuestion","input":{"questions":"?"}}}""");

        Assert.Equal("r-6", Assert.IsType<NeedsApproval>(Assert.Single(replies)).Id);
    }

    [Fact]
    public void HearsARequestBeingWithdrawn()
    {
        const string line = """{"type":"control_cancel_request","request_id":"r-1"}""";

        Assert.Equal([new Withdrawn("r-1")], new ClaudeCodeTranscript().Hear(line));
        Assert.Equal("r-1", ClaudeCodeTranscript.Withdraws(line));
        Assert.Null(ClaudeCodeTranscript.Withdraws("""{"type":"control_request","request_id":"r-1"}"""));
        Assert.Null(ClaudeCodeTranscript.Withdraws("control_cancel_request"));
    }

    [Fact]
    public void TracksAShellLeftRunning()
    {
        var transcript = new ClaudeCodeTranscript();

        transcript.Hear("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t-1","name":"Bash","input":{"command":"npm run dev","run_in_background":true}}]}}""");
        transcript.Hear("""{"type":"system","subtype":"task_started","task_id":"b-1","tool_use_id":"t-1","description":"Dev server","task_type":"local_bash"}""");

        Assert.True(transcript.JobsMoved);

        transcript.Hear("""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t-1","content":"Command running in background with ID: b-1. Output is being written to: /tmp/tasks/b-1.output. You will be notified."}]}}""");

        Assert.Equal([new Job("b-1", JobKind.Shell, "Dev server", "/tmp/tasks/b-1.output")], transcript.Jobs);
    }

    [Fact]
    public void KnowsAMonitorByItsTool()
    {
        var transcript = new ClaudeCodeTranscript();

        transcript.Hear("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t-1","name":"Monitor","input":{"command":"tail -f log"}}]}}""");
        transcript.Hear("""{"type":"system","subtype":"background_tasks_changed","tasks":[{"task_id":"b-1","task_type":"local_bash","description":"Watch the log"}]}""");
        transcript.Hear("""{"type":"system","subtype":"task_started","task_id":"b-1","tool_use_id":"t-1","description":"Watch the log","task_type":"local_bash"}""");

        Assert.Equal(JobKind.Monitor, Assert.Single(transcript.Jobs).Kind);
    }

    [Theory]
    [InlineData("""{"type":"system","subtype":"task_notification","task_id":"b-1","status":"completed"}""")]
    [InlineData("""{"type":"system","subtype":"task_updated","task_id":"b-1","patch":{"status":"killed"}}""")]
    [InlineData("""{"type":"system","subtype":"background_tasks_changed","tasks":[]}""")]
    public void ForgetsAJobOnceItSettles(string settled)
    {
        var transcript = new ClaudeCodeTranscript();
        transcript.Hear("""{"type":"system","subtype":"task_started","task_id":"b-1","description":"Tests","task_type":"local_agent"}""");

        transcript.Hear(settled);

        Assert.True(transcript.JobsMoved);
        Assert.Empty(transcript.Jobs);
    }

    [Fact]
    public void HearsWhichPromptStarted()
    {
        var transcript = new ClaudeCodeTranscript();

        transcript.Hear("""{"type":"command_lifecycle","command_uuid":"u-1","state":"queued"}""");

        Assert.Null(transcript.Started);

        transcript.Hear("""{"type":"command_lifecycle","command_uuid":"u-1","state":"started"}""");

        Assert.Equal("u-1", transcript.Started);
    }
}
