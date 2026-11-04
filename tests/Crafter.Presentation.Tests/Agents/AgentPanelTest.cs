using Crafter.Core.Agents;
using Crafter.Core.Preferences;
using Crafter.Presentation.Agents;

namespace Crafter.Presentation.Tests.Agents;

public sealed class AgentPanelTest
{
    [Fact]
    public async Task CopiesTheRemedyOfABlockedAgent()
    {
        var clipboard = new ClipboardInMemory();
        var agent = new StandInAgent(() => new ScriptedSession([]), new Blocked("Signed out", "Sign in from a terminal.", "claude auth login"));
        var panel = new AgentPanel(agent, new PreferenceBook(new PreferencesInMemory(UserPreferences.Default("/home/hero"))), clipboard);

        await panel.CheckCommand.ExecuteAsync(null);
        await panel.CopyRemedyCommand.ExecuteAsync(null);

        Assert.Equal(["claude auth login"], clipboard.Copied);
    }
}
