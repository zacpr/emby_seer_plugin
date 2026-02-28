# Inseerrtion Plugin Plan (EmbySeerr)

## Objective
Ship a first‑class Emby Server plugin that embeds Seerr discovery and request flows inside Emby's web UI, using Emby user context and Seerr's REST API.

## Technical Requirements Overview

### Emby Plugin Foundation
- .NET Standard class library targeting `netstandard2.0`.
- Reference `mediabrowser.server.core` NuGet package.
- Core classes:
  - `PluginConfiguration : BasePluginConfiguration`
  - `Plugin : BasePlugin<PluginConfiguration>` with stable GUID `Id`
  - Entry point implementing `IServerEntryPoint` for initialization.
- Use Emby DI for services like `ILogManager`, `IServerConfigurationManager`, `IUserManager`, `IHttpClient`, `INetworkManager`.

### Emby UI Integration
- Uses Emby's declarative UI framework (Plugin UI)
- Plugin provides dashboard pages via `IHasUIPages` and `IPluginUIPageController`
- UI is defined via classes inheriting from `EditableOptionsBase` with attributes
- No manual HTML/JS required - Emby auto-generates UI from ViewModel classes

### Seerr Integration
- Server‑side proxy endpoints implemented via Emby `IService` classes (`/emby/…` routes).
- Proxy calls to Seerr `/api/v1/search`, `/api/v1/request`, `/api/v1/user`, `/api/v1/auth/emby`
- Per-user mapping between Emby users and Seerr users (link or create on first use).

### Security & Auth
- Plugin settings store Seerr base URL and admin API key.
- No secrets in client JS; all Seerr calls go through server-side proxy.
- Emby user context from auth headers or token handling; respect 401 flows.

## Research Findings

### Emby Plugin UI Framework
**Key Discovery**: Emby has a modern declarative UI framework that eliminates manual HTML/JS!

**How it works**:
1. Create a class inheriting from `EditableOptionsBase` (from `Emby.Web.GenericEdit`)
2. Add properties with attributes like `[DisplayName]`, `[Description]`, `[Required]`
3. Implement `IHasUIPages` in your Plugin class
4. Return `IPluginUIPageController` instances that define page metadata
5. Emby auto-generates the UI from your class structure

**Benefits**:
- No HTML/CSS/JS to maintain
- UI matches Emby's native look automatically
- Breaking changes handled by Emby's UI generator
- Supports tabs, buttons, status items, dialogs, grids, validation

**Key Interfaces**:
- `IHasUIPages` - Exposes UI pages to Emby
- `IPluginUIPageController` - Defines page metadata (name, icon, etc.)
- `IPluginUIView` - The actual view with content and command handling
- `EditableOptionsBase` - Base class for UI ViewModels

**Example UI Elements**:
```csharp
public class MainPageUI : EditableOptionsBase
{
    public override string EditorTitle => "My Plugin";
    
    [DisplayName("Seerr URL")]
    [Description("The URL of your Seerr instance")]
    public string SeerrUrl { get; set; }
    
    [DisplayName("API Key")]
    [EditPassword]  // masks input
    public string ApiKey { get; set; }
    
    public ButtonItem TestConnection { get; set; } = new ButtonItem("Test");
}
```

### Seerr Authentication Flow
Based on research, Seerr supports:
- `/api/v1/auth/emby` - Emby authentication endpoint
- User import from Emby - Seerr can import users from Emby server
- Per-user tokens for API access after auth

## Project Structure
```
Inseerrtion/
├── src/
│   └── Inseerrtion/
│       ├── Configuration/
│       │   └── PluginConfiguration.cs
│       ├── Api/
│       │   ├── SeerrProxyService.cs
│       │   └── Models/
│       ├── Services/
│       │   ├── SeerrClient.cs
│       │   └── UserMappingService.cs
│       ├── UI/
│       │   ├── Configuration/
│       │   │   ├── ConfigPageUI.cs
│       │   │   ├── ConfigPageView.cs
│       │   │   └── ConfigPageController.cs
│       │   └── Requests/
│       │       ├── RequestsPageUI.cs
│       │       ├── RequestsPageView.cs
│       │       └── RequestsPageController.cs
│       └── Plugin.cs
├── tests/
│   └── Inseerrtion.Tests/
└── .github/workflows/
    └── build.yml
```

## Phased Delivery Plan

### Phase 0: Feasibility Spike
Objectives
n- Validate plugin skeleton, DI, and API endpoint creation.
- Confirm UI injection path using modern Plugin UI framework.
Deliverables
- `Plugin`, `PluginConfiguration`, and `IServerEntryPoint` scaffold.
- Simple `IService` endpoint returning JSON.
- Basic config UI page using declarative framework.
Tests
- Manual: Emby loads plugin and shows custom tab.
- Manual: service endpoint returns JSON via Emby API.

### Phase 1: Configuration & Connectivity
Objectives
- Add settings UI for Seerr base URL + admin API key using declarative UI.
- Implement server‑side Seerr proxy with health check.
Deliverables
- Settings page visible to admins.
- Proxy endpoint `GET /emby/Seerr/health`.
Tests
- Unit: settings read/write round‑trip.
- Manual: health endpoint returns Seerr status.

### Phase 2: Auth Mapping (Emby → Seerr)
Objectives
- Use Emby user context to identify current user.
- Exchange or create Seerr user tokens server‑side.
Deliverables
- Server endpoint `POST /emby/Seerr/auth` returns Seerr user info.
- Token cache keyed by Emby user id.
Tests
- Unit: token cache behavior.
- Manual: per‑user auth succeeds and data matches Seerr.

### Phase 3: Search UI & Results
Objectives
- Build "Requests" page with search + results using declarative UI.
- Show availability and request status badges.
Deliverables
- Client UI calling `/emby/Seerr/search`.
- Result cards with status indicators.
Tests
- Unit: Seerr search response mapping.
- Manual: search results match Seerr UI for same query.

### Phase 4: Request Flow
Objectives
- Allow users to request items from Emby UI.
- Provide clear success/failure states.
Deliverables
- `POST /emby/Seerr/request` endpoint.
- UI request button + confirmation.
Tests
- Unit: request payload construction.
- Manual: request appears in Seerr and triggers Radarr/Sonarr.

### Phase 5: Permissions, Errors, Polish
Objectives
- Respect Emby permissions (who can request).
- Improve error states and logging.
Deliverables
- Permission checks based on Emby user policy.
- Friendly errors for Seerr offline/auth failure.
Tests
- Manual: permission-denied UX.
- Manual: offline Seerr shows recoverable error.

### Phase 6: Packaging & Release
Objectives
- Package plugin for distribution and catalog submission.
- Document install/update steps.
Deliverables
- Release artifact and README.
- Versioning and upgrade notes.
Tests
- Manual: clean install and upgrade on fresh Emby server.

## Risks & Mitigations
- UI injection constraints: Using modern Plugin UI framework eliminates HTML/JS maintenance
- Seerr auth flow changes: Isolate auth logic and add diagnostics.
- CORS/secret leakage: All Seerr calls are server-side only.

## Open Questions
- [ ] Exact payload for Seerr `/api/v1/auth/emby` endpoint
- [ ] Emby catalog submission requirements for plugins using new UI framework
- [ ] How to add top-level navigation items vs settings pages only

## Suggested Next Step
Implement Phase 0 spike: minimal plugin + custom endpoint + placeholder "Requests" tab using declarative UI.


## Seerr API Research Summary (Completed)

Located full API specification from the Seerr GitHub repository (`seerr-api.yml`).

### Key Endpoints Discovered:

**Authentication:**
- `POST /api/v1/auth/jellyfin` - Authenticate with Jellyfin/Emby credentials
  - Request: `{ username, password, hostname, email, serverType }`
  - `serverType: 3` for Emby (1=Plex, 2=Jellyfin, 3=Emby)
  - Returns: User object + session cookie
- `GET /api/v1/auth/me` - Get current authenticated user
- `POST /api/v1/auth/logout` - Sign out

**Status:**
- `GET /api/v1/status` - Get Seerr version and status (public, no auth required)
  - Returns: `{ version, commitTag, updateAvailable, restartRequired }`

**Search:**
- `GET /api/v1/search?query={term}&page={page}` - Search for movies/TV
  - Returns: `{ page, totalPages, totalResults, results[] }`
  - Results include: `id, mediaType, title, overview, posterPath, releaseDate, requested`

**Requests:**
- `GET /api/v1/request` - Get all requests (admin) or user's requests
- `POST /api/v1/request` - Create new request
  - Request: `{ mediaType, mediaId, seasons?, is4k?, serverId?, profileId? }`
- `GET /api/v1/request/{requestId}` - Get specific request
- `PUT /api/v1/request/{requestId}` - Update request (requires MANAGE_REQUESTS)

**Users:**
- `GET /api/v1/user` - List all users (requires MANAGE_USERS)
- `GET /api/v1/user/{userId}` - Get user by ID
- `POST /api/v1/user/import-from-jellyfin` - Import users from Jellyfin/Emby
- `GET /api/v1/user/{userId}/requests` - Get requests for specific user
- `GET /api/v1/user/{userId}/quota` - Get quota info for user

**Authentication Methods:**
1. **Cookie**: Sign in via `/auth/jellyfin` to get session cookie
2. **API Key**: Pass `X-Api-Key` header with admin API key

### Implementation Notes:
- Server-side proxy uses API Key authentication (admin key stored in config)
- User authentication via Emby/Jellyfin endpoint creates/updates Seerr users
- Seerr handles user creation automatically on first auth

## Implementation Status

### Completed ✅

**Phase 0: Feasibility Spike**
- ✅ Plugin skeleton with DI
- ✅ PluginConfiguration with settings
- ✅ Basic API endpoints

**Phase 1: Configuration & Connectivity**
- ✅ SeerrClient service with HTTP client
- ✅ Health check endpoint (`GET /Inseerrtion/Health`)
- ✅ Search endpoint (`GET /Inseerrtion/Search`)
- ✅ Seerr API models (Status, User, SearchResults, Requests)
- ✅ Declarative UI configuration page
- ✅ GitHub Actions CI/CD with release workflow
- ✅ Unit tests for configuration

### In Progress 🔄
- 🔄 Testing against live Seerr instance

### Next Steps
1. **Test health check endpoint** against live Seerr instance
2. **Phase 2: Auth Mapping** - Complete user context integration
3. **Phase 3: Search UI** - Declarative UI page for searching media
4. **Phase 4: Request Flow** - UI for making requests
5. **Phase 5: Polish** - Error handling, permissions
6. **Phase 6: Release** - Catalog submission, documentation
