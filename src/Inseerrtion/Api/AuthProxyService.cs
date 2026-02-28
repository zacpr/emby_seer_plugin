using System;
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
    /// Request DTO for authenticating with Seerr.
    /// </summary>
    [Route("/Inseerrtion/Auth", "POST")]
    public class AuthRequest : IReturn<AuthResponse>
    {
        /// <summary>
        /// Gets or sets the Emby user ID.
        /// </summary>
        public string? EmbyUserId { get; set; }
    }

    /// <summary>
    /// Response DTO for authentication.
    /// </summary>
    public class AuthResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether authentication was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the Seerr user ID.
        /// </summary>
        public int? SeerrUserId { get; set; }

        /// <summary>
        /// Gets or sets the Seerr username.
        /// </summary>
        public string? SeerrUsername { get; set; }

        /// <summary>
        /// Gets or sets the Seerr permissions.
        /// </summary>
        public int? Permissions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is an admin.
        /// </summary>
        public bool? IsAdmin { get; set; }

        /// <summary>
        /// Gets or sets the error message if authentication failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request DTO for getting current user info.
    /// </summary>
    [Route("/Inseerrtion/Auth/Me", "GET")]
    public class GetAuthMe : IReturn<AuthResponse>
    {
    }

    #endregion

    /// <summary>
    /// Service that handles user authentication and mapping between Emby and Seerr.
    /// </summary>
    public class AuthProxyService : IService, IRequiresRequest
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
        /// Initializes a new instance of the <see cref="AuthProxyService"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="userMappingService">The user mapping service.</param>
        /// <param name="userManager">The Emby user manager.</param>
        /// <param name="sessionContext">The session context.</param>
        /// <param name="plugin">The plugin instance.</param>
        public AuthProxyService(
            ILogManager logManager,
            UserMappingService userMappingService,
            IUserManager userManager,
            ISessionContext sessionContext,
            Plugin plugin)
        {
            _logger = logManager?.GetLogger(nameof(AuthProxyService))
                ?? throw new ArgumentNullException(nameof(logManager));
            _userMappingService = userMappingService
                ?? throw new ArgumentNullException(nameof(userMappingService));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Authenticates the current Emby user with Seerr.
        /// </summary>
        public async Task<object> Post(AuthRequest request)
        {
            try
            {
                // Get the current Emby user from the request context
                var embyUser = GetCurrentUser();
                if (embyUser == null)
                {
                    _logger.Warn("Authentication failed: No Emby user in context");
                    return new AuthResponse
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated with Emby"
                    };
                }

                _logger.Info("Processing Seerr auth for Emby user {0} ({1})",
                    embyUser.Name, embyUser.Id);

                // Get or create the mapping
                var mapping = await _userMappingService.GetOrCreateMappingAsync(
                    embyUser.Id.ToString(),
                    embyUser.Name,
                    null, // Email not directly available on User entity
                    default);

                if (mapping == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to authenticate with Seerr. Ensure your Emby account is linked in Seerr settings."
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    SeerrUserId = mapping.SeerrUserId,
                    SeerrUsername = mapping.SeerrUsername,
                    Permissions = mapping.Permissions,
                    IsAdmin = mapping.UserType == 4 // 4 = admin in Seerr
                };
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during authentication", ex);
                return new AuthResponse
                {
                    Success = false,
                    ErrorMessage = "Internal error during authentication"
                };
            }
        }

        /// <summary>
        /// Gets the current authenticated user's Seerr info.
        /// </summary>
        public Task<object> Get(GetAuthMe request)
        {
            try
            {
                var embyUser = GetCurrentUser();
                if (embyUser == null)
                {
                    return Task.FromResult<object>(new AuthResponse
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated"
                    });
                }

                // Check if we have a valid mapping
                if (!_userMappingService.TryGetMapping(embyUser.Id.ToString(), out var mapping))
                {
                    return Task.FromResult<object>(new AuthResponse
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated with Seerr"
                    });
                }

                return Task.FromResult<object>(new AuthResponse
                {
                    Success = true,
                    SeerrUserId = mapping?.SeerrUserId,
                    SeerrUsername = mapping?.SeerrUsername,
                    Permissions = mapping?.Permissions,
                    IsAdmin = mapping?.UserType == 4
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting auth info", ex);
                return Task.FromResult<object>(new AuthResponse
                {
                    Success = false,
                    ErrorMessage = "Internal error"
                });
            }
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
