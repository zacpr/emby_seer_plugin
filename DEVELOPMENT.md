# Development Guide

## Building Locally

### Prerequisites
- .NET 8.0 SDK or later
- Docker (optional, for testing with Emby/Seerr)

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build Debug
dotnet build

# Build Release
dotnet build --configuration Release

# Run tests
dotnet test

# Create release package manually
dotnet build --configuration Release
mkdir -p release/Inseerrtion
cp src/Inseerrtion/bin/Release/netstandard2.0/Inseerrtion.dll release/Inseerrtion/
cp src/Inseerrtion/bin/Release/netstandard2.0/Inseerrtion.pdb release/Inseerrtion/
cd release && zip -r Inseerrtion.zip Inseerrtion/
```

## Testing Against Live Seerr

### Manual Testing with curl

1. **Health Check:**
```bash
curl -u USERNAME:TOKEN \
  "http://YOUR_EMBY_SERVER:8096/Inseerrtion/Health"
```

2. **Search:**
```bash
curl -u USERNAME:TOKEN \
  "http://YOUR_EMBY_SERVER:8096/Inseerrtion/Search?Query=inception"
```

3. **Get Current User:**
```bash
curl -u USERNAME:TOKEN \
  "http://YOUR_EMBY_SERVER:8096/Inseerrtion/Auth/Me"
```

4. **Create Request:**
```bash
curl -u USERNAME:TOKEN \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"MediaType":"movie","MediaId":27205}' \
  "http://YOUR_EMBY_SERVER:8096/Inseerrtion/Request"
```

### Testing with Docker Compose

Create a `docker-compose.test.yml`:

```yaml
version: '3'
services:
  emby:
    image: emby/embyserver:latest
    ports:
      - "8096:8096"
    volumes:
      - ./release/Inseerrtion:/config/plugins/Inseerrtion
      
  seerr:
    image: fallenbagel/jellyseerr:latest
    ports:
      - "5055:5055"
    environment:
      - LOG_LEVEL=debug
```

Run:
```bash
docker-compose -f docker-compose.test.yml up
```

## Release Process

1. Update version in `src/Inseerrtion/Inseerrtion.csproj`
2. Create and push a tag:
```bash
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```
3. GitHub Actions will automatically:
   - Build and test
   - Create a release with the ZIP artifact
   - Generate release notes

## Project Structure

```
Inseerrtion/
├── src/Inseerrtion/              # Main plugin project
│   ├── Api/                      # REST API services (IService implementations)
│   │   ├── AuthProxyService.cs
│   │   ├── RequestProxyService.cs
│   │   └── SeerrProxyService.cs
│   ├── Configuration/            # Plugin configuration
│   │   └── PluginConfiguration.cs
│   ├── Services/                 # Business logic
│   │   ├── SeerrClient.cs        # HTTP client for Seerr API
│   │   └── UserMappingService.cs # Emby ↔ Seerr user mapping
│   ├── UI/                       # Declarative UI
│   │   ├── Base/                 # Base classes for UI framework
│   │   └── Configuration/        # Settings page
│   ├── Resources/                # Embedded resources (icons, images)
│   └── Plugin.cs                 # Main plugin class
├── tests/Inseerrtion.Tests/      # xUnit test project
└── .github/workflows/            # CI/CD pipelines
```

## Troubleshooting

### Plugin not showing in Emby
- Check that the DLL is in the correct plugins folder
- Verify the plugin ID is unique
- Check Emby logs for errors

### Connection to Seerr fails
- Verify Seerr URL is accessible from Emby server
- Check API key has admin permissions
- Enable debug logging in plugin settings

### UI not appearing
- Ensure plugin implements `IHasUIPages`
- Check that UI classes inherit from `EditableOptionsBase`
- Verify controller implements `IPluginUIPageController`
