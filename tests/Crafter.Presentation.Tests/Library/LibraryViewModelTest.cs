using Crafter.Core.Library;
using Crafter.Core.Preferences;
using Crafter.Presentation.Library;
using Crafter.Presentation.Tests.Fakes;
using Crafter.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Crafter.Presentation.Tests.Library;

public sealed class LibraryViewModelTest
{
    private const string Repository = "git@github.com:hero/crafter-library.git";

    private static readonly Skill Migrations = new("laravel-migrations", "Laravel migrations", "Writes reversible migrations.", null, "/library/skills/laravel-migrations");

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 4, 14, 32, 0, TimeSpan.Zero));

    private readonly LibraryInMemory library = new();

    [Fact]
    public void StaysQuietWithoutARepository()
    {
        using var viewModel = Library(linked: null);

        time.Advance(LibraryViewModel.Every);

        Assert.False(viewModel.IsLinked);
        Assert.Empty(library.Synced);
        Assert.StartsWith("Not linked", viewModel.Status);
    }

    [Fact]
    public void ShowsTheLocalCopyAndSyncsWhenTheAppStarts()
    {
        library.Contents = new LibraryContents([Migrations], []);

        using var viewModel = Library(linked: Repository);

        Assert.Equal([Repository], library.Synced);
        Assert.Equal([Migrations], viewModel.Skills);
        Assert.Equal($"Synced at {time.GetLocalNow():HH:mm} · a1b2c3d", viewModel.Status);
    }

    [Fact]
    public async Task LinksARepositoryAndSyncsIt()
    {
        using var viewModel = Library(linked: null);
        library.Contents = new LibraryContents([Migrations], [new LibraryProblem("skills/vague", "skill.json has no description")]);

        viewModel.Draft = $"  {Repository}  ";
        await viewModel.LinkCommand.ExecuteAsync(null);

        Assert.Equal(Repository, viewModel.Repository);
        Assert.Equal([Repository], library.Synced);
        Assert.Equal([Migrations], viewModel.Skills);
        Assert.True(viewModel.HasProblems);
        Assert.False(viewModel.LinkCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaysWhyASyncFailedAndKeepsWhatItHad()
    {
        library.Contents = new LibraryContents([Migrations], []);
        using var viewModel = Library(linked: Repository);
        library.Outcome = new SyncFailed("Permission denied (publickey).");

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasFailed);
        Assert.Equal("Could not sync: Permission denied (publickey).", viewModel.Status);
        Assert.Equal([Migrations], viewModel.Skills);
    }

    [Fact]
    public void SyncsAgainOnItsOwn()
    {
        using var viewModel = Library(linked: Repository);

        time.Advance(LibraryViewModel.Every);
        time.Advance(LibraryViewModel.Every);

        Assert.Equal(3, library.Synced.Count);
    }

    [Fact]
    public async Task UnlinksWhenTheRepositoryIsCleared()
    {
        library.Contents = new LibraryContents([Migrations], []);
        using var viewModel = Library(linked: Repository);

        viewModel.Draft = " ";
        await viewModel.LinkCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLinked);
        Assert.Empty(viewModel.Skills);
        Assert.StartsWith("Not linked", viewModel.Status);
    }

    private LibraryViewModel Library(string? linked) =>
        new(new PreferenceBook(new PreferencesInMemory(new UserPreferences("/home/hero") { Library = linked })), library, time);
}
