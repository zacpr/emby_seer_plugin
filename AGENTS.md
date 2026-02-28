# Repository Guidelines - Inseerrtion

## Project Structure & Module Organization

```
Inseerrtion/
├── src/Inseerrtion/           # Main plugin project
│   ├── Configuration/         # PluginConfiguration and related classes
│   ├── Api/                   # IService implementations for REST endpoints
│   │   └── Models/            # DTOs for API requests/responses
│   ├── Services/              # Business logic
│   │   ├── SeerrClient.cs     # HTTP client for Seerr API
│   │   └── UserMappingService.cs  # Emby ↔ Seerr user mapping
│   ├── UI/                    # Declarative UI pages
│   │   ├── Configuration/     # Settings page (ConfigPageUI, ConfigPageView, ConfigPageController)
│   │   └── Requests/          # Media requests page (coming soon)
│   ├── Resources/             # Embedded resources (icons, images)
│   └── Plugin.cs              # Main plugin class
├── tests/Inseerrtion.Tests/   # xUnit test project
├── .github/workflows/         # GitHub Actions CI/CD
├── Inseerrtion.sln            # Solution file
└── theplan.md                 # Detailed project plan
```

## Build, Test, and Development Commands

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Package for release
dotnet pack --configuration Release

# Watch mode for development
dotnet watch --project src/Inseerrtion/Inseerrtion.csproj
```

## Coding Style & Naming Conventions

- **Language**: C# 9.0 with nullable reference types enabled
- **Framework**: .NET Standard 2.0 (plugin), .NET 8.0 (tests)
- **Indentation**: 4 spaces
- **Naming**: PascalCase for types/methods/properties, camelCase for locals/parameters, _prefix for private fields
- **Braces**: K&R style (opening brace on same line)
- **Usings**: Inside namespace, sorted alphabetically

### Example

```csharp
using System;

namespace Inseerrtion.Services
{
    public class SeerrClient
    {
        private readonly ILogger _logger;
        private readonly string _baseUrl;

        public SeerrClient(ILogger logger, string baseUrl)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        public async Task<HealthStatus> CheckHealthAsync()
        {
            // Implementation
        }
    }
}
```

## Testing Guidelines

- **Framework**: xUnit with FluentAssertions and NSubstitute
- **Naming**: `MethodName_Scenario_ExpectedResult`
- **Coverage**: Aim for >80% coverage on business logic
- **Structure**: Mirror source structure in test project

### Example Test

```csharp
[Fact]
public void PluginConfiguration_SetValues_AreStored()
{
    // Arrange
    var config = new PluginConfiguration
    {
        SeerrBaseUrl = "http://localhost:5050"
    };

    // Assert
    config.SeerrBaseUrl.Should().Be("http://localhost:5050");
}
```

## Commit & Pull Request Guidelines

- Use [Conventional Commits](https://www.conventionalcommits.org/):
  - `feat:` - New features
  - `fix:` - Bug fixes
  - `docs:` - Documentation changes
  - `test:` - Test additions/changes
  - `refactor:` - Code refactoring
  - `chore:` - Build/tooling changes

- PR requirements:
  1. All tests must pass
  2. Code review approval required
  3. Linked issue (if applicable)
  4. Updated documentation for API changes

## Security & Configuration Tips

- Never commit the `mediabrowser.server.core` NuGet package - it's proprietary
- API keys and secrets are stored in Emby's configuration system
- All Seerr API calls are server-side proxied - no secrets in client code
- Use `EditPassword` attribute for sensitive configuration fields

## Emby Plugin Development Notes

### UI Framework

This project uses Emby's modern **declarative UI framework**:

1. **No HTML/JS required** - UI is auto-generated from C# classes
2. Base class: `EditableOptionsBase` (from `Emby.Web.GenericEdit`)
3. Key attributes: `[DisplayName]`, `[Description]`, `[Required]`, `[EditPassword]`, `[Range]`
4. UI elements: `ButtonItem`, `StatusItem`, `CaptionItem`, `SpacerItem`

### Key Interfaces

- `IHasUIPages` - Exposes UI pages to Emby
- `IPluginUIPageController` - Defines page metadata
- `IPluginUIView` - Handles view logic and commands
- `IService` - Creates REST API endpoints

### Debugging

1. Set Emby server to Debug logging in Dashboard > Logs
2. Plugin logs appear in Emby's log files with plugin name prefix
3. Use `EnableDebugLogging` config option for verbose plugin logging

## Resources

- [Emby SDK Documentation](https://github.com/MediaBrowser/Emby.SDK)
- [Seerr API Documentation](https://docs.seerr.dev/)
- [Emby Community Forums](https://emby.media/community/)
