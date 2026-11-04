using System.Diagnostics;
using Crafter.Core.Library;
using Crafter.Storage;

namespace Crafter.Storage.Tests;

public sealed class GitLibraryTest : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("crafter-library-").FullName;

    private string Remote => Path.Combine(root, "remote.git");

    private string Work => Path.Combine(root, "work");

    private string Mirror => Path.Combine(root, "mirror");

    public GitLibraryTest()
    {
        // Saving commits as whoever git says; tests sign as a stand-in so any machine can run them.
        Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME", "Hero");
        Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", "hero@example.com");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Hero");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", "hero@example.com");

        Git(root, "init", "--quiet", "--bare", "remote.git");
        Git(Remote, "symbolic-ref", "HEAD", "refs/heads/main");
        Git(root, "init", "--quiet", "work");
        Git(Work, "checkout", "--quiet", "-b", "main");
        Git(Work, "remote", "add", "origin", Remote);
    }

    [Fact]
    public async Task ClonesTheRepositoryAndReadsItsSkills()
    {
        Write("skills/laravel-migrations/skill.json", """{ "name": "Laravel migrations", "description": "Writes reversible migrations.", "whenToUse": "Changing the schema" }""");
        Write("skills/laravel-migrations/instructions.md", "# Migrations");
        Publish("first skill");

        var library = new GitLibrary(Mirror);
        var outcome = await library.Sync(Remote, TestContext.Current.CancellationToken);

        Assert.Equal(Head(), Assert.IsType<Synced>(outcome).Commit);
        var skill = Assert.Single(library.Read().Skills);
        Assert.Equal(("laravel-migrations", "Laravel migrations", "Writes reversible migrations.", "Changing the schema"), (skill.Id, skill.Name, skill.Description, skill.WhenToUse));
        Assert.Equal("# Migrations", File.ReadAllText(skill.Instructions));
    }

    [Fact]
    public async Task FollowsTheRepositoryAndDropsLocalChanges()
    {
        Write("skills/review/skill.json", """{ "description": "Reviews a diff." }""");
        Write("skills/review/instructions.md", "v1");
        Publish("v1");
        var library = new GitLibrary(Mirror);
        await library.Sync(Remote, TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(Mirror, "skills", "review", "instructions.md"), "edited by hand");

        Write("skills/review/instructions.md", "v2");
        Publish("v2");
        var outcome = await library.Sync(Remote, TestContext.Current.CancellationToken);

        Assert.Equal(Head(), Assert.IsType<Synced>(outcome).Commit);
        Assert.Equal("v2", File.ReadAllText(Assert.Single(library.Read().Skills).Instructions));
    }

    [Fact]
    public async Task ClonesAgainWhenTheRepositoryChanges()
    {
        Write("skills/one/skill.json", """{ "description": "One." }""");
        Write("skills/one/instructions.md", "one");
        Publish("one");
        var library = new GitLibrary(Mirror);
        await library.Sync(Remote, TestContext.Current.CancellationToken);

        var other = Path.Combine(root, "other.git");
        Git(root, "clone", "--quiet", "--bare", Remote, other);
        Git(Work, "remote", "set-url", "origin", other);
        Write("skills/two/skill.json", """{ "description": "Two." }""");
        Write("skills/two/instructions.md", "two");
        Publish("two");

        await library.Sync(other, TestContext.Current.CancellationToken);

        Assert.Equal(["one", "two"], library.Read().Skills.Select(skill => skill.Id));
    }

    [Fact]
    public async Task ListsTheFoldersItCannotReadWithTheReason()
    {
        Write("skills/no-json/instructions.md", "x");
        Write("skills/broken/skill.json", "{ not json");
        Write("skills/broken/instructions.md", "x");
        Write("skills/vague/skill.json", """{ "name": "Vague" }""");
        Write("skills/vague/instructions.md", "x");
        Write("skills/silent/skill.json", """{ "description": "No instructions." }""");
        Publish("problems");
        var library = new GitLibrary(Mirror);

        await library.Sync(Remote, TestContext.Current.CancellationToken);

        Assert.Empty(library.Read().Skills);
        Assert.Equal(
            [
                ("skills/broken", "skill.json is missing or not valid JSON"),
                ("skills/no-json", "skill.json is missing or not valid JSON"),
                ("skills/silent", "instructions.md is missing"),
                ("skills/vague", "skill.json has no description"),
            ],
            library.Read().Problems.Select(problem => (problem.Place, problem.Reason)));
    }

    [Fact]
    public async Task SaysWhyARepositoryCannotBeReached()
    {
        var outcome = await new GitLibrary(Mirror).Sync(Path.Combine(root, "nowhere.git"), TestContext.Current.CancellationToken);

        Assert.Contains("nowhere.git", Assert.IsType<SyncFailed>(outcome).Reason);
    }

    [Fact]
    public async Task SavesANewSkillAndPushesIt()
    {
        Write("README.md", "library");
        Publish("start");
        var library = new GitLibrary(Mirror);

        var outcome = await library.Save(new SkillDraft("pr-review", "PR review", "Reviews a pull request.", "Before merging", "# Review\nRead the diff."), Remote, TestContext.Current.CancellationToken);

        Assert.IsType<Synced>(outcome);
        Assert.Equal("Add skill PR review", Git(Remote, "log", "-1", "--format=%s").Trim());
        Assert.Contains("\"whenToUse\": \"Before merging\"", Git(Remote, "show", "HEAD:skills/pr-review/skill.json"));
        Assert.Equal("# Review\nRead the diff.\n", Git(Remote, "show", "HEAD:skills/pr-review/instructions.md"));
        Assert.Equal("PR review", Assert.Single(library.Read().Skills).Name);
    }

    [Fact]
    public async Task UpdatesASkillOnTopOfWhatOthersPushedAndSkipsAnUnchangedSave()
    {
        Write("skills/pr-review/skill.json", """{ "name": "PR review", "description": "Reviews a pull request." }""");
        Write("skills/pr-review/instructions.md", "v1");
        Publish("v1");
        var library = new GitLibrary(Mirror);
        await library.Sync(Remote, TestContext.Current.CancellationToken);

        Write("skills/other/skill.json", """{ "description": "Pushed from another machine." }""");
        Write("skills/other/instructions.md", "other");
        Publish("elsewhere");

        var draft = new SkillDraft("pr-review", "PR review", "Reviews a pull request.", null, "v2");
        await library.Save(draft, Remote, TestContext.Current.CancellationToken);
        var commits = Git(Remote, "rev-list", "--count", "HEAD").Trim();
        await library.Save(draft, Remote, TestContext.Current.CancellationToken);

        Assert.Equal("Update skill PR review", Git(Remote, "log", "-1", "--format=%s").Trim());
        Assert.Equal(commits, Git(Remote, "rev-list", "--count", "HEAD").Trim());
        Assert.Equal(["other", "pr-review"], library.Read().Skills.Select(skill => skill.Id).Order());
    }

    [Theory]
    [InlineData("PR Review")]
    [InlineData("../escape")]
    [InlineData("")]
    public async Task RefusesAnIdThatIsNotAFolderName(string id)
    {
        var outcome = await new GitLibrary(Mirror).Save(new SkillDraft(id, "x", "x", null, "x"), Remote, TestContext.Current.CancellationToken);

        Assert.Contains("lowercase", Assert.IsType<SyncFailed>(outcome).Reason);
    }

    [Fact]
    public void HoldsNothingBeforeTheFirstSync()
    {
        Assert.Equal(LibraryContents.Empty, new GitLibrary(Mirror).Read());
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }

    private void Write(string path, string content)
    {
        var file = Path.Combine(Work, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private void Publish(string message)
    {
        Git(Work, "add", "--all");
        Git(Work, "-c", "user.name=Hero", "-c", "user.email=hero@example.com", "commit", "--quiet", "-m", message);
        Git(Work, "push", "--quiet", "origin", "main");
    }

    private string Head() => Git(Work, "rev-parse", "--short", "HEAD").Trim();

    private static string Git(string where, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = where, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start)!;
        var output = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        return git.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error}");
    }
}
