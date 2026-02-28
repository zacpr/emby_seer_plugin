# Inseerrtion

An Emby Server plugin that embeds Seerr discovery and request flows directly into Emby's web UI.

## Features

- **Seamless Integration**: Users can request movies and TV shows without leaving Emby
- **Native UI**: Uses Emby's modern declarative UI framework for a consistent experience
- **Secure**: All Seerr API calls are proxied server-side - no secrets exposed to clients
- **Per-User Support**: Individual user authentication and request quotas

## Requirements

- Emby Server 4.10.0.4 or later
- Seerr instance (accessible from Emby server)
- .NET Standard 2.0 compatible runtime

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/inseerrtion/releases) page
2. Copy the plugin DLL to your Emby server's `plugins` folder
3. Restart Emby Server
4. Navigate to Dashboard > Plugins > Inseerrtion to configure

## Configuration

1. Go to Dashboard > Plugins > Inseerrtion
2. Enter your Seerr base URL (e.g., `http://localhost:5050`)
3. Enter your Seerr admin API key (found in Seerr Settings > General)
4. Click "Test Connection" to verify
5. Adjust request quotas and auto-approval settings as needed
6. Save the configuration

## Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for detailed development and testing instructions.

### Quick Start

```bash
# Clone
git clone https://github.com/yourusername/inseerrtion.git
cd inseerrtion

# Build
dotnet restore
dotnet build

# Test
dotnet test

# Create release package
dotnet build --configuration Release
mkdir -p release/Inseerrtion
cp src/Inseerrtion/bin/Release/netstandard2.0/Inseerrtion.dll release/Inseerrtion/
cd release && zip -r Inseerrtion.zip Inseerrtion/
```

### Project Structure

```
Inseerrtion/
├── src/Inseerrtion/
│   ├── Configuration/       # Plugin configuration classes
│   ├── Api/                 # REST API endpoints (IService implementations)
│   ├── Services/            # Business logic and Seerr client
│   ├── UI/                  # Declarative UI pages
│   │   └── Configuration/   # Settings page
│   └── Plugin.cs            # Main plugin entry point
├── tests/Inseerrtion.Tests/ # Unit tests
└── .github/workflows/       # CI/CD pipelines
```

## Architecture

### Emby Plugin UI Framework

This plugin uses Emby's modern declarative UI framework (introduced in Emby 4.10). Instead of writing HTML/JS:

1. Define UI classes inheriting from `EditableOptionsBase`
2. Add properties with attributes like `[DisplayName]`, `[Description]`, `[Required]`
3. Implement `IHasUIPages` in your Plugin class
4. Emby auto-generates the UI matching its native look

### Seerr Integration

All Seerr API calls are proxied through server-side endpoints:

- `/Inseerrtion/Health` - Health check endpoint
- `/Inseerrtion/Search` - Search for media
- `/Inseerrtion/Request` - Submit requests
- `/Inseerrtion/Auth` - User authentication

### API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/Inseerrtion/Health` | GET | Plugin and Seerr health status |
| `/Inseerrtion/Search` | GET | Search for movies/TV shows |
| `/Inseerrtion/Request` | GET | List user's requests |
| `/Inseerrtion/Request` | POST | Create a new request |
| `/Inseerrtion/Auth` | POST | Authenticate/link user |
| `/Inseerrtion/Auth/Me` | GET | Get current user info |

This ensures API keys and user tokens never reach the client browser.

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## Acknowledgments

- Built using the [Emby SDK](https://github.com/MediaBrowser/Emby.SDK)
- Inspired by the Seerr/Jellyseerr projects
