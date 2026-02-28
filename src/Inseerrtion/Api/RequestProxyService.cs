using System;
using System.Linq;
using System.Threading.Tasks;
using Inseerrtion.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace Inseerrtion.Api
{
    #region Request/Response DTOs

    /// <summary>
    /// Request DTO for creating a new media request.
    /// </summary>
    [Route("/Inseerrtion/Request", "POST")]
    public class CreateRequest : IReturn<RequestResponse>
    {
        /// <summary>
        /// Gets or sets the media type (movie or tv).
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the TMDB media ID.
        /// </summary>
        public int MediaId { get; set; }

        /// <summary>
        /// Gets or sets the TVDB ID (for TV shows).
        /// </summary>
        public int? TvdbId { get; set; }

        /// <summary>
        /// Gets or sets the seasons to request (for TV shows).
        /// Can be an array of season numbers or "all".
        /// </summary>
        public object? Seasons { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a 4K request.
        /// </summary>
        public bool Is4k { get; set; }

        /// <summary>
        /// Gets or sets the server ID (for multi-server setups).
        /// </summary>
        public int? ServerId { get; set; }

        /// <summary>
        /// Gets or sets the profile ID.
        /// </summary>
        public int? ProfileId { get; set; }

        /// <summary>
        /// Gets or sets the root folder path.
        /// </summary>
        public string? RootFolder { get; set; }
    }

    /// <summary>
    /// Request DTO for getting all requests.
    /// </summary>
    [Route("/Inseerrtion/Request", "GET")]
    public class GetRequests : IReturn<RequestsListResponse>
    {
        /// <summary>
        /// Gets or sets the number of items to take.
        /// </summary>
        public int? Take { get; set; }

        /// <summary>
        /// Gets or sets the number of items to skip.
        /// </summary>
        public int? Skip { get; set; }

        /// <summary>
        /// Gets or sets the filter (all, approved, available, pending, processing, etc.).
        /// </summary>
        public string? Filter { get; set; }

        /// <summary>
        /// Gets or sets the media type filter (movie, tv, all).
        /// </summary>
        public string? MediaType { get; set; }
    }

    /// <summary>
    /// Response DTO for a request operation.
    /// </summary>
    public class RequestResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the request was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the request ID.
        /// </summary>
        public int? RequestId { get; set; }

        /// <summary>
        /// Gets or sets the request status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request was auto-approved.
        /// </summary>
        public bool? IsAutoApproved { get; set; }

        /// <summary>
        /// Gets or sets the error message if the request failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response DTO for requests list.
    /// </summary>
    public class RequestsListResponse
    {
        /// <summary>
        /// Gets or sets the total number of results.
        /// </summary>
        public int TotalResults { get; set; }

        /// <summary>
        /// Gets or sets the requests.
        /// </summary>
        public MediaRequestItem[]? Results { get; set; }
    }

    /// <summary>
    /// Represents a media request item.
    /// </summary>
    public class MediaRequestItem
    {
        /// <summary>
        /// Gets or sets the request ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the TMDB ID.
        /// </summary>
        public int TmdbId { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the poster URL.
        /// </summary>
        public string? PosterPath { get; set; }

        /// <summary>
        /// Gets or sets the request status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the request date.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the username who made the request.
        /// </summary>
        public string? RequestedBy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is available.
        /// </summary>
        public bool IsAvailable { get; set; }
    }

    #endregion

    /// <summary>
    /// Service that handles media requests through Seerr.
    /// </summary>
    public class RequestProxyService : IService, IRequiresRequest
    {
        private readonly ILogger _logger;
        private readonly UserMappingService _userMappingService;
        private readonly IUserManager _userManager;
        private readonly ISessionContext _sessionContext;
        private readonly Plugin _plugin;

        /// <summary>
        /// Gets or sets the current HTTP request.
        /// </summary>
        public IRequest? Request { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestProxyService"/> class.
        /// </summary>
        public RequestProxyService(
            ILogManager logManager,
            UserMappingService userMappingService,
            IUserManager userManager,
            ISessionContext sessionContext,
            Plugin plugin)
        {
            _logger = logManager?.GetLogger(nameof(RequestProxyService))
                ?? throw new ArgumentNullException(nameof(logManager));
            _userMappingService = userMappingService
                ?? throw new ArgumentNullException(nameof(userMappingService));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Creates a new media request.
        /// </summary>
        public async Task<object> Post(CreateRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.MediaType) || request.MediaId <= 0)
                {
                    return new RequestResponse
                    {
                        Success = false,
                        ErrorMessage = "Media type and media ID are required"
                    };
                }

                // Get current Emby user
                var embyUser = GetCurrentUser();
                if (embyUser == null)
                {
                    return new RequestResponse
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated with Emby"
                    };
                }

                // Check if user has a Seerr mapping
                if (!_userMappingService.TryGetMapping(embyUser.Id.ToString(), out var mapping))
                {
                    // Try to create mapping on-the-fly
                    mapping = await _userMappingService.GetOrCreateMappingAsync(
                        embyUser.Id.ToString(),
                        embyUser.Name,
                        null,
                        default);

                    if (mapping == null)
                    {
                        return new RequestResponse
                        {
                            Success = false,
                            ErrorMessage = "Not linked to Seerr. Please authenticate first."
                        };
                    }
                }

                _logger.Info("User {0} requesting {1} (ID: {2})",
                    embyUser.Name, request.MediaType, request.MediaId);

                // Create the request in Seerr
                using var client = new SeerrClient(_logger, _plugin.Configuration);
                var seerrRequest = CreateSeerrRequest(request, mapping!);

                var result = await client.CreateRequestAsync(seerrRequest);

                if (result == null)
                {
                    return new RequestResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to create request in Seerr"
                    };
                }

                return new RequestResponse
                {
                    Success = true,
                    RequestId = result.Id,
                    Status = result.StatusString,
                    IsAutoApproved = result.IsAutoApproved
                };
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error creating request", ex);
                return new RequestResponse
                {
                    Success = false,
                    ErrorMessage = "Internal error creating request"
                };
            }
        }

        /// <summary>
        /// Gets the user's requests.
        /// </summary>
        public async Task<object> Get(GetRequests request)
        {
            try
            {
                var embyUser = GetCurrentUser();
                if (embyUser == null)
                {
                    return new RequestsListResponse
                    {
                        TotalResults = 0,
                        Results = Array.Empty<MediaRequestItem>()
                    };
                }

                // Ensure user has a mapping
                if (!_userMappingService.TryGetMapping(embyUser.Id.ToString(), out var mapping))
                {
                    return new RequestsListResponse
                    {
                        TotalResults = 0,
                        Results = Array.Empty<MediaRequestItem>()
                    };
                }

                using var client = new SeerrClient(_logger, _plugin.Configuration);

                var seerrRequests = await client.GetRequestsAsync(
                    request.Take ?? 20,
                    request.Skip ?? 0,
                    request.Filter,
                    request.MediaType,
                    mapping!.SeerrUserId);

                if (seerrRequests == null)
                {
                    return new RequestsListResponse
                    {
                        TotalResults = 0,
                        Results = Array.Empty<MediaRequestItem>()
                    };
                }

                var items = seerrRequests.Results?.Select(r => new MediaRequestItem
                {
                    Id = r.Id,
                    MediaType = r.Media?.MediaType,
                    TmdbId = r.Media?.TmdbId ?? 0,
                    Title = r.Media?.Title ?? "Unknown",
                    PosterPath = r.Media?.PosterPath,
                    Status = r.StatusString,
                    CreatedAt = r.CreatedAt,
                    RequestedBy = r.RequestedBy?.DisplayName ?? r.RequestedBy?.Username ?? "Unknown",
                    IsAvailable = r.Media?.Status == 5 // 5 = Available
                }).ToArray() ?? Array.Empty<MediaRequestItem>();

                return new RequestsListResponse
                {
                    TotalResults = seerrRequests.TotalResults,
                    Results = items
                };
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting requests", ex);
                return new RequestsListResponse
                {
                    TotalResults = 0,
                    Results = Array.Empty<MediaRequestItem>()
                };
            }
        }

        private CreateMediaRequest CreateSeerrRequest(CreateRequest request, UserMapping mapping)
        {
            // Build the request object for Seerr API
            var seerrRequest = new CreateMediaRequest
            {
                MediaType = request.MediaType ?? "movie",
                MediaId = request.MediaId,
                TvdbId = request.TvdbId,
                Is4k = request.Is4k,
                ServerId = request.ServerId,
                ProfileId = request.ProfileId,
                RootFolder = request.RootFolder,
                UserId = mapping.SeerrUserId
            };

            return seerrRequest;
        }

        /// <summary>
        /// Gets the current Emby user from the request context.
        /// </summary>
        private MediaBrowser.Controller.Entities.User? GetCurrentUser()
        {
            try
            {
                if (Request == null)
                {
                    _logger.Warn("Request context is null");
                    return null;
                }

                // Use ISessionContext to get the user from the request
                var user = _sessionContext.GetUser(Request);
                if (user != null)
                {
                    _logger.Debug("Got user {0} ({1}) from session context", user.Name, user.Id);
                    return user;
                }

                _logger.Warn("No user found in session context");
                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting current user from session context", ex);
                return null;
            }
        }
    }
}
