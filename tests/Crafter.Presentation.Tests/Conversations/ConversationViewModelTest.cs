using Crafter.Core.Agents;
using Crafter.Core.Agents.Background;
using Crafter.Core.Agents.Options;
using Crafter.Core.Conversations;
using Crafter.Core.Conversations.Replies;
using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Presentation.Conversations;
using Crafter.Presentation.Conversations.Messages;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Conversations;

public sealed class ConversationViewModelTest
{
    private readonly FakeTimeProvider time = new();

    private readonly ClipboardInMemory clipboard = new();

    private readonly KeeperInMemory keeper = new();

    private readonly PreferenceBook preferences = new(new PreferencesInMemory(UserPreferences.Default("/home/hero")));

    [Fact]
    public async Task CountsTheSecondsWhileCrafting()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Thinking")) { Hangs = true });

        var crafting = Ask(viewModel, "oi");
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("crafting · 5s", viewModel.Elapsed);

        viewModel.Leave();
        await crafting;
    }

    [Fact]
    public async Task CountsInMinutesAfterTheFirstOne()
    {
        var viewModel = ViewModel(new ScriptedSession { Hangs = true });

        var crafting = Ask(viewModel, "oi");
        time.Advance(TimeSpan.FromSeconds(72));

        Assert.Equal("crafting · 1m 12s", viewModel.Elapsed);

        viewModel.Leave();
        await crafting;
    }

    [Fact]
    public async Task StopsTheClockWhenTheAnswerEnds()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Done"), new Finished()));

        await Ask(viewModel, "oi");
        time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal("crafting · 0s", viewModel.Elapsed);
        Assert.Equal(ChatState.Unread, viewModel.State);
    }

    [Fact]
    public async Task CopiesAnAnswerToTheClipboard()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Olá, "), new Wrote("mundo"), new Finished()));

        await Ask(viewModel, "oi");
        await viewModel.Messages.OfType<CrafterMessageViewModel>().Single().CopyCommand.ExecuteAsync(null);

        Assert.Equal(["Olá, mundo"], clipboard.Copied);
    }

    [Fact]
    public async Task CopiesACodeBlockOnItsOwn()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Run:\n```bash\njust test\n```"), new Finished()));

        await Ask(viewModel, "oi");
        var code = viewModel.Messages.OfType<CrafterMessageViewModel>().Single().Items.OfType<TextSegmentViewModel>().Single().Pieces.OfType<CodeBlockViewModel>().Single();
        await code.CopyCommand.ExecuteAsync(null);

        Assert.Equal(["just test"], clipboard.Copied);
    }

    [Fact]
    public async Task AsksWithThePreferencesOfTheMoment()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        preferences.Configure(new AgentId("stand-in"), Configuration.Empty.With("model", "opus"));
        await Ask(viewModel, "oi");

        Assert.Equal([Configuration.Empty.With("model", "opus")], session.Configurations);
    }

    [Fact]
    public async Task SendsALongPasteInFullWhileShowingItsLabel()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);
        var pasted = string.Join('\n', Enumerable.Range(1, 40).Select(line => $"line {line}"));

        var label = viewModel.Paste(pasted);
        await Ask(viewModel, $"Explain {label}");

        Assert.Equal("[Pasted text #1 +39 lines]", label);
        Assert.Equal($"Explain {pasted}", session.Heard.Single().Text);
        Assert.Equal("Explain [Pasted text #1 +39 lines]", viewModel.Messages.OfType<YourMessageViewModel>().Single().Text);
    }

    [Fact]
    public void KeepsAShortPasteAsItIs()
    {
        Assert.Equal("just test", ViewModel(new ScriptedSession()).Paste("just test"));
    }

    [Fact]
    public async Task SendsTheAttachedPictures()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);
        var picture = new Picture("image/png", [1, 2, 3]);

        viewModel.Attach(picture);
        await Ask(viewModel, "What is in it?");

        Assert.Equal(new Prompt("What is in it?", [picture]), session.Heard.Single());
        Assert.Empty(viewModel.Attached);
        Assert.False(viewModel.HasAttached);
    }

    [Fact]
    public async Task SendsAPictureWithoutWords()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.Attach(new Picture("image/png", [1]));
        await viewModel.CraftCommand.ExecuteAsync(null);

        Assert.Equal("(1 picture attached)", session.Heard.Single().Text);
        Assert.False(viewModel.Messages.OfType<YourMessageViewModel>().Single().HasText);
    }

    [Fact]
    public async Task ForgetsARemovedPicture()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.Attach(new Picture("image/png", [1]));
        viewModel.Attach(new Picture("image/png", [2]));
        viewModel.Attached[0].RemoveCommand.Execute(null);
        viewModel.DetachLast();
        await Ask(viewModel, "look");

        Assert.Empty(session.Heard.Single().Pictures);
    }

    [Fact]
    public async Task RetriesTheSamePrompt()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.Attach(new Picture("image/png", [1]));
        await Ask(viewModel, "Look at this");
        viewModel.Draft = "half typed";
        viewModel.Messages.OfType<CrafterMessageViewModel>().Single().RetryCommand.Execute(null);
        await viewModel.CraftCommand.ExecutionTask!;

        Assert.Equal(2, session.Heard.Count);
        Assert.Equal(session.Heard[0], session.Heard[1]);
        Assert.Equal("half typed", viewModel.Draft);
    }

    [Fact]
    public async Task SendsAttachedFilesAndShowsTheirNamesOnTheMessage()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.AttachFile("/home/hero/crafter/docs/plan.pdf");
        viewModel.AttachFile("/tmp/report final.xlsx");
        viewModel.AttachFile("/home/hero/crafter/docs/plan.pdf");
        Assert.Equal(["plan.pdf", "report final.xlsx"], viewModel.Attached.Select(each => each.Name));
        Assert.All(viewModel.Attached, each => Assert.True(each.IsFile));

        await Ask(viewModel, "Compare them");

        Assert.Equal("Compare them\n\n@docs/plan.pdf @\"/tmp/report final.xlsx\"", session.Heard.Single().Text);
        var sent = viewModel.Messages.OfType<YourMessageViewModel>().Single();
        Assert.Equal("Compare them", sent.Text);
        Assert.Equal(["plan.pdf", "report final.xlsx"], sent.Files);
        Assert.Empty(viewModel.Attached);
    }

    [Fact]
    public async Task ForgetsARemovedFile()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        viewModel.AttachFile("/home/hero/crafter/docs/plan.pdf");
        viewModel.Attached.Single().RemoveCommand.Execute(null);
        await Ask(viewModel, "never mind");

        Assert.Equal("never mind", session.Heard.Single().Text);
        Assert.False(viewModel.Messages.OfType<YourMessageViewModel>().Single().HasFiles);
    }

    [Fact]
    public void MentionsAFileInsideTheProjectByItsRelativePath()
    {
        var viewModel = ViewModel(new ScriptedSession());

        Assert.Equal("@src/Prompt.cs ", viewModel.Mention("/home/hero/crafter/src/Prompt.cs"));
        Assert.Equal("@/tmp/shot.png ", viewModel.Mention("/tmp/shot.png"));
    }

    [Fact]
    public async Task ShowsTheJobsLeftRunning()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        await Ask(viewModel, "start the server");
        session.Run(new Job("b-1", JobKind.Shell, "Dev server"), new Job("b-2", JobKind.Monitor, "Watch the log"));
        time.Advance(TimeSpan.FromSeconds(75));

        Assert.True(viewModel.HasJobs);
        Assert.Equal("2 jobs", viewModel.JobsLabel);
        Assert.Equal(["shell", "monitor"], viewModel.Jobs.Select(job => job.Kind));
        Assert.Equal("1m 15s", viewModel.Jobs[0].Elapsed);
        Assert.False(viewModel.IsAsking);

        session.Run(new Job("b-1", JobKind.Shell, "Dev server"));
        Assert.Equal("1 job", viewModel.JobsLabel);

        session.Run();

        Assert.False(viewModel.HasJobs);
        Assert.Equal("No jobs", viewModel.JobsLabel);
    }

    [Fact]
    public async Task AsksWhileAJobRuns()
    {
        var session = new ScriptedSession(new Finished());
        var viewModel = ViewModel(session);

        await Ask(viewModel, "start the server");
        session.Run(new Job("b-1", JobKind.Shell, "Dev server"));
        await Ask(viewModel, "and the tests?");

        Assert.Equal(2, session.Heard.Count);
    }

    [Fact]
    public void StopsAJobWhenAsked()
    {
        var session = new ScriptedSession();
        var viewModel = ViewModel(session);

        session.Run(new Job("b-1", JobKind.Shell, "Dev server"));
        viewModel.Jobs.Single().StopCommand.Execute(null);

        Assert.Equal(["b-1"], session.StoppedJobs);
        Assert.True(viewModel.Jobs.Single().IsStopping);
    }

    [Fact]
    public async Task ShowsWhatTheAgentSaysWhenItWakes()
    {
        var session = new ScriptedSession();
        var viewModel = ViewModel(session);
        var concluded = new TaskCompletionSource();
        viewModel.Concluded += (_, _) => concluded.TrySetResult();

        session.Wake(new Wrote("The server is up."), new Finished());
        await concluded.Task.WaitAsync(TestContext.Current.CancellationToken);

        var woken = Assert.IsType<CrafterMessageViewModel>(Assert.Single(viewModel.Messages));
        Assert.True(woken.IsWoken);
        Assert.Equal("The server is up.", woken.Text);
        Assert.Equal(ChatState.Unread, viewModel.State);
    }

    [Fact]
    public void LetsTheJobsGoWhenLeft()
    {
        var session = new ScriptedSession();

        ViewModel(session).Leave();

        Assert.True(session.IsDismissed);
    }

    [Fact]
    public async Task EscapeStopsTheAgentAndHandsThePromptBack()
    {
        var session = new ScriptedSession(new Wrote("Thinking")) { Hangs = true };
        var viewModel = ViewModel(session);
        var picture = new Picture("image/png", [1]);
        viewModel.Attach(picture);

        var crafting = Ask(viewModel, "fix the table");
        viewModel.Escape();
        await crafting;

        Assert.False(viewModel.IsAsking);
        Assert.Equal("fix the table", viewModel.Draft);
        Assert.Equal([picture], viewModel.Attached.Select(each => each.Picture));
        Assert.Single(viewModel.Messages.OfType<YourMessageViewModel>());
    }

    [Fact]
    public async Task EscapeKeepsWhatWasTypedWhileTheAgentWorked()
    {
        var viewModel = ViewModel(new ScriptedSession { Hangs = true });

        var crafting = Ask(viewModel, "fix the table");
        viewModel.Draft = "and the copy";
        viewModel.Escape();
        await crafting;

        Assert.Equal("and the copy", viewModel.Draft);
    }

    [Fact]
    public void EscapeTwiceClearsTheDraftAndItsAttachments()
    {
        var viewModel = ViewModel(new ScriptedSession());
        viewModel.Draft = "half a thought";
        viewModel.Attach(new Picture("image/png", [1]));

        viewModel.Escape();
        Assert.True(viewModel.IsClearArmed);
        Assert.Equal("half a thought", viewModel.Draft);

        viewModel.Escape();
        Assert.Equal(string.Empty, viewModel.Draft);
        Assert.Empty(viewModel.Attached);
        Assert.False(viewModel.IsClearArmed);
    }

    [Fact]
    public void ASecondEscapeTooLateOrAfterTypingKeepsTheDraft()
    {
        var viewModel = ViewModel(new ScriptedSession());
        viewModel.Draft = "half a thought";

        viewModel.Escape();
        time.Advance(ConversationViewModel.ClearWindow);
        Assert.False(viewModel.IsClearArmed);
        viewModel.Escape();
        Assert.Equal("half a thought", viewModel.Draft);

        viewModel.Draft = "half a thought, more";
        viewModel.Escape();
        Assert.Equal("half a thought, more", viewModel.Draft);
    }

    [Fact]
    public void EscapeOnAnEmptyComposerDoesNothing()
    {
        var viewModel = ViewModel(new ScriptedSession());

        viewModel.Escape();

        Assert.False(viewModel.IsClearArmed);
    }

    [Fact]
    public void OpensAndClosesTheJobsWindow()
    {
        var viewModel = ViewModel(new ScriptedSession());

        Assert.False(viewModel.IsJobsOpen);

        viewModel.ToggleJobsCommand.Execute(null);
        Assert.True(viewModel.IsJobsOpen);

        viewModel.CloseJobsCommand.Execute(null);
        Assert.False(viewModel.IsJobsOpen);
    }

    [Fact]
    public async Task CallsCraftersSkillsAndRefusesAnyOtherCommand()
    {
        var session = new ScriptedSession();
        var viewModel = ViewModel(session, [new Skill("commit", "Commit", "Writes commits.", null, "/library/skills/commit")]);

        await Ask(viewModel, "/init");
        Assert.Empty(session.Heard);
        Assert.Equal("/init is not a Crafter skill", viewModel.Refusal);
        Assert.Equal("/init", viewModel.Draft);

        viewModel.Draft = "/commit the staged files";
        Assert.False(viewModel.IsRefused);

        await viewModel.CraftCommand.ExecuteAsync(null);
        Assert.Equal(["commit"], session.Heard.Single().Skills.Select(skill => skill.Id));
    }

    [Fact]
    public void AReopenedChatShowsTheToolsItsAnswersUsed()
    {
        var session = new ScriptedSession();
        var agent = new ScriptedAgent { Sessions = () => session };
        Turn[] turns =
        [
            new Asked(new Prompt("Run the tests")),
            new Answered("Running.Green.", new Finished())
            {
                Steps = [new Wrote("Running."), new Acting("t-1", new Tool(ToolKind.Shell, "Bash"), "dotnet test"), new Acted("t-1", Failed: false, Output: "ok"), new Wrote("Green.")],
            },
            new Asked(new Prompt("Older")),
            new Answered("Only words.", new Finished()),
        ];
        var chat = new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session, turns));

        var viewModel = new ConversationViewModel(chat, agent, preferences, "crafter", keeper, clipboard, new FilesInMemory(), time);

        var answers = viewModel.Messages.OfType<CrafterMessageViewModel>().ToList();
        Assert.Collection(
            answers[0].Items,
            item => Assert.Equal("Running.", Assert.IsType<TextSegmentViewModel>(item).Text),
            item => Assert.Equal("ok", Assert.IsType<ActivityItemViewModel>(item).Output),
            item => Assert.Equal("Green.", Assert.IsType<TextSegmentViewModel>(item).Text));
        Assert.Equal("Only words.", Assert.IsType<TextSegmentViewModel>(Assert.Single(answers[1].Items)).Text);
    }

    [Fact]
    public void OpensOnItsLatestFewExchangesAndBringsEarlierOnesIn()
    {
        var session = new ScriptedSession();
        var agent = new ScriptedAgent { Sessions = () => session };
        Turn[] turns = [.. Enumerable.Range(1, ConversationViewModel.Window + 3).SelectMany(at => new Turn[] { new Asked(new Prompt($"Ask {at}")), new Answered($"Answer {at}", new Finished()) })];
        var chat = new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session, turns));

        var viewModel = new ConversationViewModel(chat, agent, preferences, "crafter", keeper, clipboard, new FilesInMemory(), time);

        Assert.Equal(ConversationViewModel.Window * 2, viewModel.Messages.Count);
        Assert.Equal("Ask 4", Assert.IsType<YourMessageViewModel>(viewModel.Messages[0]).Text);
        Assert.Equal("Show 6 earlier messages", viewModel.EarlierLabel);

        viewModel.ShowEarlierCommand.Execute(null);

        Assert.Equal(turns.Length, viewModel.Messages.Count);
        Assert.Equal("Ask 1", Assert.IsType<YourMessageViewModel>(viewModel.Messages[0]).Text);
        Assert.False(viewModel.HasEarlier);
    }

    [Fact]
    public async Task KeepsItsLatestExchangesOnScreenAsTheChatGoesOn()
    {
        var viewModel = ViewModel(new ScriptedSession(new Wrote("Done."), new Finished()));

        for (var at = 1; at <= ConversationViewModel.Kept + 2; at++)
        {
            await Ask(viewModel, $"Ask {at}");
        }

        Assert.Equal(ConversationViewModel.Kept * 2, viewModel.Messages.Count);
        Assert.Equal("Ask 3", Assert.IsType<YourMessageViewModel>(viewModel.Messages[0]).Text);
        Assert.Equal("Show 4 earlier messages", viewModel.EarlierLabel);

        viewModel.ShowEarlierCommand.Execute(null);

        Assert.Equal("Ask 1", Assert.IsType<YourMessageViewModel>(viewModel.Messages[0]).Text);
        Assert.False(viewModel.HasEarlier);
    }

    [Fact]
    public async Task AsksAboutAnAxiomInTheAnswerUnderWayAndDeclinesWithoutOne()
    {
        var idle = ViewModel(new ScriptedSession(new Finished()));
        Assert.False(await idle.ProposeAxiom("crafter", "Never add comments"));

        var viewModel = ViewModel(new ScriptedSession { Hangs = true });
        viewModel.Draft = "Tidy the code";
        _ = viewModel.CraftCommand.ExecuteAsync(null);
        var asking = viewModel.ProposeAxiom("crafter", "Never add comments");

        var card = Assert.Single(viewModel.Messages.OfType<CrafterMessageViewModel>().Last().Items.OfType<AxiomProposalItemViewModel>());
        Assert.Equal("stand-in proposes an axiom for crafter", card.Asking);
        Assert.True(viewModel.IsWaiting);

        card.ApproveCommand.Execute(null);

        Assert.True(await asking);
        Assert.False(viewModel.IsWaiting);
        viewModel.CraftCancelCommand.Execute(null);
    }

    [Fact]
    public async Task AsksAboutASummaryInTheAnswerUnderWay()
    {
        var viewModel = ViewModel(new ScriptedSession { Hangs = true });
        viewModel.Draft = "Learn the project";
        _ = viewModel.CraftCommand.ExecuteAsync(null);
        var asking = viewModel.ProposeSummary("crafter", "Crafter is my IDE.", "Crafter is my IDE, in C#.");

        var card = Assert.Single(viewModel.Messages.OfType<CrafterMessageViewModel>().Last().Items.OfType<SummaryProposalItemViewModel>());
        Assert.Equal("stand-in proposes a new summary for crafter", card.Asking);
        Assert.True(viewModel.IsWaiting);

        card.DeclineCommand.Execute(null);

        Assert.False(await asking);
        Assert.False(viewModel.IsWaiting);
        viewModel.CraftCancelCommand.Execute(null);
    }

    private static Task Ask(ConversationViewModel viewModel, string prompt)
    {
        viewModel.Draft = prompt;

        return viewModel.CraftCommand.ExecuteAsync(null);
    }

    private ConversationViewModel ViewModel(IAgentSession session, IReadOnlyList<Skill>? skills = null)
    {
        var agent = new ScriptedAgent { Sessions = () => session };
        var chat = new Chat(ChatId.New(), agent.Id, ChatTitle.Untitled, new Conversation(session));

        return new ConversationViewModel(chat, agent, preferences, "crafter", keeper, clipboard, new FilesInMemory("README.md", "src/", "src/Prompt.cs"), time, skills: () => skills ?? []);
    }
}
