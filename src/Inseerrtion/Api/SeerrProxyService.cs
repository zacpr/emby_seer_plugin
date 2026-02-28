using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inseerrtion.Services;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.Logging;

namespace Inseerrtion.Api
{
    #region Request/Response DTOs

    /// <summary>
    /// Request DTO for health check endpoint.
    /// </summary>
    [Route("/Inseerrtion/Health", "GET")]
    public class GetHealth : IReturn<HealthResponse>
    {
    }

    /// <summary>
    /// Response DTO for health check.
    /// </summary>
    public class HealthResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the plugin is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Seerr is reachable.
        /// </summary>
        public bool IsSeerrConnected { get; set; }

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the Seerr version.
        /// </summary>
        public string? SeerrVersion { get; set; }

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the health check.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Request DTO for searching.
    /// </summary>
    [Route("/Inseerrtion/Search", "GET")]
    public class SearchRequest : IReturn<SearchResponse>
    {
        /// <summary>
        /// Gets or sets the search query.
        /// </summary>
        public string? Query { get; set; }

        /// <summary>
        /// Gets or sets the page number.
        /// </summary>
        public int Page { get; set; } = 1;
    }

    /// <summary>
    /// Response DTO for search.
    /// </summary>
    public class SearchResponse
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

        /// <summary>
        /// Gets or sets the search results.
        /// </summary>
        public SearchResultItem[]? Results { get; set; }
    }

    /// <summary>
    /// Represents a search result item.
    /// </summary>
    public class SearchResultItem
    {
        /// <summary>
        /// Gets or sets the media ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the media type (movie or tv).
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
        /// Gets or sets the poster URL.
        /// </summary>
        public string? PosterUrl { get; set; }

        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is already requested.
        /// </summary>
        public bool IsRequested { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is available.
        /// </summary>
        public bool IsAvailable { get; set; }
    }

    #endregion

    /// <summary>
    /// Service that proxies requests to Seerr API.
    /// </summary>
    public class SeerrProxyService : IService
    {
        private readonly ILogger _logger;
        private readonly Plugin _plugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeerrProxyService"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="plugin">The plugin instance.</param>
        public SeerrProxyService(ILogManager logManager, Plugin plugin)
        {
            _logger = logManager?.GetLogger(nameof(SeerrProxyService)) 
                ?? throw new ArgumentNullException(nameof(logManager));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Gets the health status of the plugin and Seerr connection.
        /// </summary>
        /// <param name="request">The health check request.</param>
        /// <returns>The health check response.</returns>
        public async Task<object> Get(GetHealth request)
        {
            _logger.Debug("Health check requested");

            var isConfigured = !string.IsNullOrWhiteSpace(_plugin.Configuration.SeerrBaseUrl) 
                && !string.IsNullOrWhiteSpace(_plugin.Configuration.SeerrApiKey);

            var response = new HealthResponse
            {
                IsHealthy = isConfigured,
                IsSeerrConnected = false,
                Version = _plugin.Version.ToString(),
                Timestamp = DateTime.UtcNow
            };

            if (!isConfigured)
            {
                response.Message = "Plugin is not fully configured - please set Seerr URL and API Key";
                return response;
            }

            // Try to connect to Seerr
            try
            {
                using var client = new SeerrClient(_logger, _plugin.Configuration);
                var status = await client.GetStatusAsync();

                if (status != null)
                {
                    response.IsSeerrConnected = true;
                    response.SeerrVersion = status.Version;
                    response.Message = $"Connected to Seerr v{status.Version}";
                }
                else
                {
                    response.Message = "Seerr is not reachable - check URL and API key";
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Health check failed", ex);
                response.Message = $"Error connecting to Seerr: {ex.Message}";
            }

            return response;
        }

        /// <summary>
        /// Searches for media in Seerr.
        /// </summary>
        /// <param name="request">The search request.</param>
        /// <returns>The search results.</returns>
        public async Task<object> Get(SearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return new SearchResponse
                {
                    Page = 1,
                    TotalPages = 0,
                    TotalResults = 0,
                    Results = Array.Empty<SearchResultItem>()
                };
            }

            try
            {
                using var client = new SeerrClient(_logger, _plugin.Configuration);
                var results = await client.SearchAsync(request.Query, request.Page);

                if (results == null)
                {
                    return new SearchResponse
                    {
                        Page = request.Page,
                        TotalPages = 0,
                        TotalResults = 0,
                        Results = Array.Empty<SearchResultItem>()
                    };
                }

                // Map Seerr results to our response format
                var items = new List<SearchResultItem>();
                if (results.Results != null)
                {
                    foreach (var result in results.Results)
                    {
                        int? year = null;
                        if (!string.IsNullOrEmpty(result.ReleaseDate) && result.ReleaseDate.Length >= 4)
                        {
                            if (int.TryParse(result.ReleaseDate.Substring(0, 4), out var y))
                            {
                                year = y;
                            }
                        }

                        items.Add(new SearchResultItem
                        {
                            Id = result.Id,
                            MediaType = result.MediaType,
                            Title = result.Title,
                            Overview = result.Overview,
                            PosterUrl = result.PosterPath,
                            Year = year,
                            IsRequested = result.Requested,
                            IsAvailable = false // TODO: Determine from media info
                        });
                    }
                }

                return new SearchResponse
                {
                    Page = results.Page,
                    TotalPages = results.TotalPages,
                    TotalResults = results.TotalResults,
                    Results = items.ToArray()
                };
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Search failed", ex);
                return new SearchResponse
                {
                    Page = request.Page,
                    TotalPages = 0,
                    TotalResults = 0,
                    Results = Array.Empty<SearchResultItem>()
                };
            }
        }
    }
}
