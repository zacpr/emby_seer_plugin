using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Inseerrtion.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;

namespace Inseerrtion.Services
{
    /// <summary>
    /// Service for mapping Emby users to Seerr users and managing authentication tokens.
    /// </summary>
    public class UserMappingService
    {
        private readonly ILogger _logger;
        private readonly PluginConfiguration _configuration;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ConcurrentDictionary<string, UserMapping> _userMappings;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string _mappingsFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMappingService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The plugin configuration.</param>
        /// <param name="applicationPaths">The application paths.</param>
        public UserMappingService(
            ILogger logger,
            PluginConfiguration configuration,
            IApplicationPaths applicationPaths)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
            
            _userMappings = new ConcurrentDictionary<string, UserMapping>();
            _mappingsFilePath = Path.Combine(
                applicationPaths.PluginConfigurationsPath,
                "Inseerrtion",
                "user-mappings.json");
            
            // Load existing mappings
            LoadMappings();
        }

        /// <summary>
        /// Gets or creates a Seerr user mapping for an Emby user.
        /// </summary>
        /// <param name="embyUserId">The Emby user ID.</param>
        /// <param name="embyUsername">The Emby username.</param>
        /// <param name="embyEmail">The Emby email (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The user mapping, or null if authentication failed.</returns>
        public async Task<UserMapping?> GetOrCreateMappingAsync(
            string embyUserId,
            string embyUsername,
            string? embyEmail = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(embyUserId))
                throw new ArgumentException("Emby user ID is required", nameof(embyUserId));
            if (string.IsNullOrWhiteSpace(embyUsername))
                throw new ArgumentException("Emby username is required", nameof(embyUsername));

            // Check if we have a valid cached mapping
            if (_userMappings.TryGetValue(embyUserId, out var existingMapping))
            {
                if (existingMapping.IsValid)
                {
                    _logger.Debug("Using cached Seerr token for user {0}", embyUsername);
                    return existingMapping;
                }
                else
                {
                    _logger.Debug("Cached token expired for user {0}, re-authenticating", embyUsername);
                }
            }

            // Authenticate with Seerr
            return await AuthenticateWithSeerrAsync(embyUserId, embyUsername, embyEmail, cancellationToken);
        }

        /// <summary>
        /// Authenticates an Emby user with Seerr using the jellyfin/Emby auth endpoint.
        /// </summary>
        private async Task<UserMapping?> AuthenticateWithSeerrAsync(
            string embyUserId,
            string embyUsername,
            string? embyEmail,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("Authenticating Emby user {0} with Seerr", embyUsername);

                using var client = new SeerrClient(_logger, _configuration);
                
                // For now, we use the admin API key to look up the user
                // In a production scenario, we might want to use password-based auth
                // or have the user link their account explicitly
                var seerrUser = await GetSeerrUserByEmbyIdAsync(client, embyUserId);
                
                if (seerrUser == null)
                {
                    _logger.Warn("No Seerr user found for Emby user {0} (ID: {1})", embyUsername, embyUserId);
                    return null;
                }

                // Create the mapping
                var mapping = new UserMapping
                {
                    EmbyUserId = embyUserId,
                    EmbyUsername = embyUsername,
                    EmbyEmail = embyEmail,
                    SeerrUserId = seerrUser.Id,
                    SeerrUsername = seerrUser.Username ?? seerrUser.JellyfinUsername ?? embyUsername,
                    SeerrEmail = seerrUser.Email,
                    Permissions = seerrUser.Permissions,
                    UserType = seerrUser.UserType,
                    CreatedAt = DateTime.UtcNow,
                    LastAuthenticatedAt = DateTime.UtcNow,
                    IsValid = true
                };

                // Store in cache
                _userMappings[embyUserId] = mapping;
                
                // Persist to disk
                SaveMappings();

                _logger.Info("Successfully mapped Emby user {0} to Seerr user {1} (ID: {2})",
                    embyUsername, mapping.SeerrUsername, mapping.SeerrUserId);

                return mapping;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to authenticate user with Seerr", ex);
                return null;
            }
        }

        /// <summary>
        /// Looks up a Seerr user by their linked Emby user ID.
        /// </summary>
        private async Task<SeerrUser?> GetSeerrUserByEmbyIdAsync(SeerrClient client, string embyUserId)
        {
            // Get current user (if API key is for a linked user)
            var currentUser = await client.GetCurrentUserAsync();
            
            // If the current user matches the Emby ID, return it
            if (currentUser != null && currentUser.JellyfinUserId == embyUserId)
            {
                return currentUser;
            }

            // If current user is admin, search all users for the matching jellyfinUserId
            if (currentUser?.UserType == 4) // 4 = admin
            {
                _logger.Debug("Current user is admin, searching all users for Emby ID: {0}", embyUserId);
                
                var allUsers = await client.GetAllUsersAsync();
                if (allUsers != null)
                {
                    foreach (var user in allUsers)
                    {
                        if (user.JellyfinUserId == embyUserId)
                        {
                            _logger.Debug("Found matching Seerr user: {0} (ID: {1})", 
                                user.Username ?? user.JellyfinUsername, user.Id);
                            return user;
                        }
                    }
                }
            }
            else
            {
                _logger.Warn("Current API key user is not an admin, cannot search all users");
            }

            return null;
        }

        /// <summary>
        /// Gets a user mapping by Emby user ID if it exists and is valid.
        /// </summary>
        public bool TryGetMapping(string embyUserId, out UserMapping? mapping)
        {
            if (_userMappings.TryGetValue(embyUserId, out mapping))
            {
                return mapping?.IsValid ?? false;
            }
            mapping = null;
            return false;
        }

        /// <summary>
        /// Invalidates a user's cached mapping.
        /// </summary>
        public void InvalidateMapping(string embyUserId)
        {
            if (_userMappings.TryGetValue(embyUserId, out var mapping))
            {
                mapping.IsValid = false;
                _logger.Debug("Invalidated mapping for user {0}", embyUserId);
            }
        }

        /// <summary>
        /// Loads user mappings from disk.
        /// </summary>
        private void LoadMappings()
        {
            try
            {
                _lock.Wait();
                
                if (!File.Exists(_mappingsFilePath))
                {
                    _logger.Debug("No user mappings file found at {0}", _mappingsFilePath);
                    return;
                }

                var json = File.ReadAllText(_mappingsFilePath);
                var mappings = JsonSerializer.Deserialize<UserMapping[]>(json);
                
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        // Mark all loaded mappings as invalid (they need re-auth)
                        mapping.IsValid = false;
                        _userMappings[mapping.EmbyUserId] = mapping;
                    }
                    _logger.Info("Loaded {0} user mappings from disk", mappings.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to load user mappings", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Saves user mappings to disk.
        /// </summary>
        private void SaveMappings()
        {
            try
            {
                _lock.Wait();
                
                var directory = Path.GetDirectoryName(_mappingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var mappings = _userMappings.Values.ToArray();
                var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(_mappingsFilePath, json);
                _logger.Debug("Saved {0} user mappings to disk", mappings.Length);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to save user mappings", ex);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// Represents a mapping between an Emby user and a Seerr user.
    /// </summary>
    public class UserMapping
    {
        /// <summary>
        /// Gets or sets the Emby user ID.
        /// </summary>
        public string EmbyUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Emby username.
        /// </summary>
        public string EmbyUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Emby email.
        /// </summary>
        public string? EmbyEmail { get; set; }

        /// <summary>
        /// Gets or sets the Seerr user ID.
        /// </summary>
        public int SeerrUserId { get; set; }

        /// <summary>
        /// Gets or sets the Seerr username.
        /// </summary>
        public string SeerrUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Seerr email.
        /// </summary>
        public string? SeerrEmail { get; set; }

        /// <summary>
        /// Gets or sets the Seerr permissions.
        /// </summary>
        public int Permissions { get; set; }

        /// <summary>
        /// Gets or sets the Seerr user type.
        /// </summary>
        public int UserType { get; set; }

        /// <summary>
        /// Gets or sets the date the mapping was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date of last authentication.
        /// </summary>
        public DateTime LastAuthenticatedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mapping is valid (has active token).
        /// </summary>
        public bool IsValid { get; set; }
    }
}
