using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Crafter.Desktop.Prose;

namespace Crafter.Desktop.Tests;

// The app's tokens, palette and control styles on a headless platform that lays text out for real.
public sealed class TestApp : Application
{
    private static readonly Uri Crafter = new("avares://crafter/");

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    // Runs a test body on the UI thread of one shared headless app.
    public static void Run(Action test) => Session.Value.Dispatch(test, TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    // For tests that await work the UI thread must serve meanwhile, like a server calling back into it.
    // Only the Func<Task<T>> overload runs the body to its end; a bare Func<Task> lands on Func<T>,
    // which hands back once the body reaches its first await and leaves the rest unrun.
    public static Task RunAsync(Func<Task> test) => Session.Value.Dispatch(async () =>
    {
        await test();

        return true;
    }, TestContext.Current.CancellationToken);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .With(new FontManagerOptions { DefaultFamilyName = "Noto Sans" });

    public override void Initialize()
    {
        // The avares:// lookups below find the app's XAML only once its assembly is loaded.
        _ = typeof(ProseView).Assembly;
        Resources.MergedDictionaries.Add(new ResourceInclude(Crafter) { Source = new Uri("avares://crafter/Themes/Tokens.axaml") });
        Resources.MergedDictionaries.Add(new ResourceInclude(Crafter) { Source = new Uri("avares://crafter/Themes/Deepslate.axaml") });
        Resources.MergedDictionaries.Add(new ResourceInclude(Crafter) { Source = new Uri("avares://crafter/Controls/Modal.axaml") });
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(Crafter) { Source = new Uri("avares://crafter/Themes/Controls.axaml") });
    }
}
