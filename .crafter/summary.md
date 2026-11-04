Crafter is a desktop IDE for coding with AI agents. It keeps a sidebar of projects, each a folder with its own chats; each chat talks to an agent (today Claude Code, driven through its CLI in stream-json mode). Crafter hands every session the project's summary, its library skills and its axioms, and turns the agent's own memory, CLAUDE.md/AGENTS.md and slash commands off. A special project, Home, is the chat Crafter itself is managed from, through the MCP server Crafter offers its agents.

## Stack

- C# on .NET 10, Avalonia 12 (Fluent theme, Wayland) for the UI, CommunityToolkit.Mvvm for view models, Markdig for Markdown
- xUnit v3 on Microsoft.Testing.Platform; Avalonia.Headless for view tests
- Central package versions in Directory.Packages.props; nullable on and warnings are errors (Directory.Build.props)
- Released as a native AOT build for linux-x64

## Layout

- `src/Crafter.Core`: the domain, with no UI or IO: projects and the bookshelf, conversations, agents, preferences, the skills library
- `src/Crafter.Storage`: files on disk: JSON stores, the git-backed skills library, git branches
- `src/Crafter.Presentation`: view models, one per screen or card, and `CrafterAbilities`, what the MCP tools do
- `src/Crafter.Desktop`: Avalonia views (`.axaml`), themes, the composition root (`Composition.cs`) and the MCP server (`Mcp/CrafterMcp.cs`)
- `src/Crafter.Agents.ClaudeCode`: Claude Code as an agent: process, protocol, history, and how skills, axioms and the summary reach its system prompt
- `src/Crafter.Mcp`, `src/Crafter.Markdown`, `src/Crafter.Highlight`: the MCP server over HTTP, Markdown parsing and code highlighting
- `tests/`: one test project per source project, plus `Crafter.Testing` with in-memory fakes of the stores

## Where things live

- A project keeps itself in its own `.crafter` folder: `settings.json` (name, icon, axioms), `icon.*` and `summary.md`
- Crafter's own state sits in the user's config folder (`preferences.json`, `projects.json`) and local data folder (chats, the cloned skills library, the skills copied for each session), both under `crafter`
- The skills library is a git repository each user links in Preferences → Library

## Commands

- `just run`, `just build`, `just test`
- `just publish` builds the release into `dist/`

## Style

- Code reads as plain English: types and members are named for what they mean (`Bookshelf`, `Shelved`, `Told`, `Named`), and test names are sentences (`KeepsTheAxiomsThroughARenameAndANewIcon`)
- Comments are few and say why, not what
- Interface text speaks of agents in general, and commit messages follow Conventional Commits with scopes like `feat(axioms)` or `fix(chat)`
