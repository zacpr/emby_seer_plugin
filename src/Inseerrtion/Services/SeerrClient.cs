using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Inseerrtion.Configuration;
using MediaBrowser.Model.Logging;

namespace Inseerrtion.Services
{
    /// <summary>
    /// HTTP client for communicating with the Seerr API.
    /// </summary>
    public class SeerrClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly PluginConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeerrClient"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The plugin configuration.</param>
        public SeerrClient(ILogger logger, PluginConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            UpdateBaseAddress();
        }

        /// <summary>
        /// Gets or sets the base URL of the Seerr instance.
        /// </summary>
        public string BaseUrl => _configuration.SeerrBaseUrl;

        /// <summary>
        /// Gets a value indicating whether the client is configured.
        /// </summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration.SeerrBaseUrl) 
            && !string.IsNullOrWhiteSpace(_configuration.SeerrApiKey);

        /// <summary>
        /// Updates the HTTP client base address from configuration.
        /// </summary>
        private void UpdateBaseAddress()
        {
            if (!string.IsNullOrWhiteSpace(_configuration.SeerrBaseUrl))
            {
                if (Uri.TryCreate(_configuration.SeerrBaseUrl, UriKind.Absolute, out var uri))
                {
                    _httpClient.BaseAddress = uri;
                    _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _configuration.SeerrApiKey);
                }
            }
        }

        /// <summary>
        /// Checks the health/status of the Seerr instance.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The status response from Seerr.</returns>
        public async Task<SeerrStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                _logger.Warn("Seerr client is not configured");
                return null;
            }

            try
            {
                _logger.Debug("Checking Seerr status at {0}", _httpClient.BaseAddress);
                
                var response = await _httpClient.GetAsync("/api/v1/status", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var status = JsonSerializer.Deserialize<SeerrStatus>(content, _jsonOptions);
                    _logger.Info("Seerr status check successful - version {0}", status?.Version);
                    return status;
                }
                
                _logger.Error("Seerr status check failed: {0} - {1}", 
                    (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error checking Seerr status", ex);
                return null;
            }
        }

        /// <summary>
        /// Authenticates a user with the Seerr instance using Emby/Jellyfin credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The authenticated user, or null if authentication failed.</returns>
        public async Task<SeerrUser?> AuthenticateAsync(
            string username, 
            string password, 
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                _logger.Warn("Seerr client is not configured");
                return null;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.Warn("Username and password are required for authentication");
                return null;
            }

            try
            {
                _logger.Debug("Authenticating user {0} with Seerr", username);

                var requestBody = new
                {
                    username,
                    password,
                    hostname = _httpClient.BaseAddress?.Host ?? "localhost",
                    email = $"{username}@emby.local", // Seerr requires an email
                    serverType = 3 // 3 = Emby (based on Seerr source: 1=Plex, 2=Jellyfin, 3=Emby)
                };

                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/auth/jellyfin", content, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<SeerrUser>(responseContent, _jsonOptions);
                    
                    // Extract the session cookie for subsequent requests
                    if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                    {
                        foreach (var cookie in cookies)
                        {
                            _httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                        }
                    }

                    _logger.Info("User {0} authenticated successfully with Seerr (ID: {1})", 
                        username, user?.Id);
                    return user;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.Error("Authentication failed for user {0}: {1} - {2}", 
                    username, (int)response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error authenticating with Seerr", ex);
                return null;
            }
        }

        /// <summary>
        /// Gets the current authenticated user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current user.</returns>
        public async Task<SeerrUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            try
            {
                var response = await _httpClient.GetAsync("/api/v1/auth/me", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SeerrUser>(content, _jsonOptions);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting current user", ex);
                return null;
            }
        }

        /// <summary>
        /// Gets all users (requires admin permissions).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all users.</returns>
        public async Task<SeerrUser[]?> GetAllUsersAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            try
            {
                var response = await _httpClient.GetAsync("/api/v1/user", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SeerrUser[]>(content, _jsonOptions);
                }

                _logger.Warn("Failed to get users list: {0}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting users list", ex);
                return null;
            }
        }

        /// <summary>
        /// Searches for media in Seerr.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="page">The page number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The search results.</returns>
        public async Task<SearchResults?> SearchAsync(
            string query, 
            int page = 1, 
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            try
            {
                var url = $"/api/v1/search?query={Uri.EscapeDataString(query)}&page={page}";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SearchResults>(content, _jsonOptions);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error searching Seerr", ex);
                return null;
            }
        }

        /// <summary>
        /// Creates a new media request.
        /// </summary>
        /// <param name="request">The request data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created request.</returns>
        public async Task<SeerrMediaRequest?> CreateRequestAsync(object request, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            try
            {
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.Debug("Creating request: {0}", json);

                var response = await _httpClient.PostAsync("/api/v1/request", content, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SeerrMediaRequest>(responseContent, _jsonOptions);
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.Error("Failed to create request: {0} - {1}", response.StatusCode, error);
                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error creating request", ex);
                return null;
            }
        }

        /// <summary>
        /// Gets requests for a user.
        /// </summary>
        /// <param name="take">Number of items to take.</param>
        /// <param name="skip">Number of items to skip.</param>
        /// <param name="filter">Status filter.</param>
        /// <param name="mediaType">Media type filter.</param>
        /// <param name="requestedBy">User ID who made the requests.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of requests.</returns>
        public async Task<SeerrRequestsList?> GetRequestsAsync(
            int take = 20,
            int skip = 0,
            string? filter = null,
            string? mediaType = null,
            int? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            try
            {
                var query = $"take={take}&skip={skip}";
                if (!string.IsNullOrEmpty(filter))
                    query += $"&filter={Uri.EscapeDataString(filter)}";
                if (!string.IsNullOrEmpty(mediaType))
                    query += $"&mediaType={Uri.EscapeDataString(mediaType)}";
                if (requestedBy.HasValue)
                    query += $"&requestedBy={requestedBy.Value}";

                var response = await _httpClient.GetAsync($"/api/v1/request?{query}", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SeerrRequestsList>(content, _jsonOptions);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting requests", ex);
                return null;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #region Seerr API Models

    /// <summary>
    /// Represents the status response from Seerr.
    /// </summary>
    public class SeerrStatus
    {
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the commit tag.
        /// </summary>
        public string? CommitTag { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an update is available.
        /// </summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// Gets or sets the number of commits behind.
        /// </summary>
        public int CommitsBehind { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a restart is required.
        /// </summary>
        public bool RestartRequired { get; set; }
    }

    /// <summary>
    /// Represents a Seerr user.
    /// </summary>
    public class SeerrUser
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the email.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string? Username { get; set; }
        
        /// <summary>
        /// Gets or sets the Jellyfin/Emby username.
        /// </summary>
        public string? JellyfinUsername { get; set; }
        
        /// <summary>
        /// Gets or sets the Jellyfin/Emby user ID.
        /// </summary>
        public string? JellyfinUserId { get; set; }

        /// <summary>
        /// Gets or sets the user type.
        /// </summary>
        public int UserType { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the permissions.
        /// </summary>
        public int Permissions { get; set; }

        /// <summary>
        /// Gets or sets the avatar URL.
        /// </summary>
        public string? Avatar { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the update date.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the request count.
        /// </summary>
        public int RequestCount { get; set; }
    }

    /// <summary>
    /// Represents search results from Seerr.
    /// </summary>
    public class SearchResults
    {
        /// <summary>
        /// Gets or sets the current page.
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Gets or sets the total number of pages.
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Gets or sets the total number of results.
        /// </summary>
        public int TotalResults { get; set; }

        /// <summary>
        /// Gets or sets the results.
        /// </summary>
        public SearchResultItem[]? Results { get; set; }
    }

    /// <summary>
    /// Represents a single search result item.
    /// </summary>
    public class SearchResultItem
    {
        /// <summary>
        /// Gets or sets the ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the media type (movie, tv).
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        public string? Overview { get; set; }

        /// <summary>
        /// Gets or sets the poster path.
        /// </summary>
        public string? PosterPath { get; set; }

        /// <summary>
        /// Gets or sets the release date.
        /// </summary>
        public string? ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is already requested.
        /// </summary>
        public bool Requested { get; set; }
    }

    #endregion

    #region Request DTOs

    /// <summary>
    /// Request body for creating a new media request.
    /// </summary>
    public class CreateMediaRequest
    {
        /// <summary>
        /// The media type (movie or tv).
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// The TMDB ID of the media.
        /// </summary>
        [JsonPropertyName("mediaId")]
        public int MediaId { get; set; }

        /// <summary>
        /// The TVDB ID (optional, for TV shows).
        /// </summary>
        [JsonPropertyName("tvdbId")]
        public int? TvdbId { get; set; }

        /// <summary>
        /// Whether the request is for 4K.
        /// </summary>
        [JsonPropertyName("is4k")]
        public bool Is4k { get; set; }

        /// <summary>
        /// The ID of the user making the request (admin only).
        /// </summary>
        [JsonPropertyName("userId")]
        public int? UserId { get; set; }

        /// <summary>
        /// Server ID for the request.
        /// </summary>
        [JsonPropertyName("serverId")]
        public int? ServerId { get; set; }

        /// <summary>
        /// Profile ID for TV seasons (Sonarr/Radarr profile).
        /// </summary>
        [JsonPropertyName("profileId")]
        public int? ProfileId { get; set; }

        /// <summary>
        /// Root folder path for TV seasons.
        /// </summary>
        [JsonPropertyName("rootFolder")]
        public string? RootFolder { get; set; }

        /// <summary>
        /// Language profile ID for TV seasons.
        /// </summary>
        [JsonPropertyName("languageProfileId")]
        public int? LanguageProfileId { get; set; }

        /// <summary>
        /// Anime quality profile ID for TV seasons.
        /// </summary>
        [JsonPropertyName("animeProfileId")]
        public int? AnimeProfileId { get; set; }

        /// <summary>
        /// Anime root folder path for TV seasons.
        /// </summary>
        [JsonPropertyName("animeRootFolder")]
        public string? AnimeRootFolder { get; set; }

        /// <summary>
        /// Tags to apply to the request.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<int>? Tags { get; set; }

        /// <summary>
        /// Whether the request is an anime.
        /// </summary>
        [JsonPropertyName("isAnime")]
        public bool? IsAnime { get; set; }
    }

    /// <summary>
    /// Represents a media request returned by Seerr API.
    /// </summary>
    public class SeerrMediaRequest
    {
        /// <summary>
        /// The unique ID of the request.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// The status code of the request (1=pending, 2=approved, 3=declined, 4=available, 5=failed).
        /// </summary>
        [JsonPropertyName("status")]
        public int Status { get; set; }

        /// <summary>
        /// Gets the status as a string.
        /// </summary>
        public string StatusString => Status switch
        {
            1 => "pending",
            2 => "approved",
            3 => "declined",
            4 => "available",
            5 => "failed",
            _ => "unknown"
        };

        /// <summary>
        /// Whether the request was auto-approved.
        /// </summary>
        [JsonPropertyName("isAutoApproved")]
        public bool IsAutoApproved { get; set; }

        /// <summary>
        /// The media type (movie or tv).
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// The associated media info.
        /// </summary>
        [JsonPropertyName("media")]
        public SeerrMediaInfo? Media { get; set; }

        /// <summary>
        /// The user who made the request.
        /// </summary>
        [JsonPropertyName("requestedBy")]
        public SeerrUser? RequestedBy { get; set; }

        /// <summary>
        /// The date the request was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The date the request was updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        /// <summary>
        /// The user who last updated the request.
        /// </summary>
        [JsonPropertyName("updatedBy")]
        public SeerrUser? UpdatedBy { get; set; }

        /// <summary>
        /// Whether the request is for 4K.
        /// </summary>
        [JsonPropertyName("is4k")]
        public bool Is4k { get; set; }

        /// <summary>
        /// Server ID for 4K requests.
        /// </summary>
        [JsonPropertyName("serverId")]
        public int? ServerId { get; set; }

        /// <summary>
        /// Profile ID for the request.
        /// </summary>
        [JsonPropertyName("profileId")]
        public int? ProfileId { get; set; }

        /// <summary>
        /// Season numbers to request (for TV shows).
        /// </summary>
        [JsonPropertyName("seasons")]
        public List<int>? Seasons { get; set; }

        /// <summary>
        /// Root folder path.
        /// </summary>
        [JsonPropertyName("rootFolder")]
        public string? RootFolder { get; set; }
    }

    /// <summary>
    /// Represents media information within a request.
    /// </summary>
    public class SeerrMediaInfo
    {
        /// <summary>
        /// The unique ID of the media entry.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// The TMDB ID of the media.
        /// </summary>
        [JsonPropertyName("tmdbId")]
        public int TmdbId { get; set; }

        /// <summary>
        /// The TVDB ID (for TV shows).
        /// </summary>
        [JsonPropertyName("tvdbId")]
        public int? TvdbId { get; set; }

        /// <summary>
        /// The media type (movie or tv).
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// The current status (1=unknown, 2=pending, 3=processing, 4=partially available, 5=available).
        /// </summary>
        [JsonPropertyName("status")]
        public int Status { get; set; }

        /// <summary>
        /// The 4K status.
        /// </summary>
        [JsonPropertyName("status4k")]
        public int Status4k { get; set; }

        /// <summary>
        /// The title of the media.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// The poster path/URL.
        /// </summary>
        [JsonPropertyName("posterPath")]
        public string? PosterPath { get; set; }
    }

    /// <summary>
    /// Represents a list of requests with pagination.
    /// </summary>
    public class SeerrRequestsList
    {
        /// <summary>
        /// The list of requests.
        /// </summary>
        [JsonPropertyName("results")]
        public List<SeerrMediaRequest> Results { get; set; } = new();

        /// <summary>
        /// Pagination information.
        /// </summary>
        [JsonPropertyName("pageInfo")]
        public SeerrPageInfo PageInfo { get; set; } = new();

        /// <summary>
        /// Gets the total results count from page info.
        /// </summary>
        public int TotalResults => PageInfo?.TotalResults ?? 0;
    }

    /// <summary>
    /// Represents pagination info.
    /// </summary>
    public class SeerrPageInfo
    {
        /// <summary>
        /// Gets or sets the current page.
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Gets or sets the total pages.
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Gets or sets the total results.
        /// </summary>
        public int TotalResults { get; set; }
    }

    #endregion
}
