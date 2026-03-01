using System;
using System.Collections.Generic;
using System.IO;
using Inseerrtion.Configuration;
using Inseerrtion.Services;
using Inseerrtion.UI.Configuration;
using Inseerrtion.UI.Search;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Serialization;

namespace Inseerrtion
{
    /// <summary>
    /// Main plugin class for Inseerrtion - Emby plugin for Seerr integration.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasUIPages
    {
        private readonly ILogger _logger;
        private UserMappingService? _userMappingService;
        private readonly IServerApplicationHost _applicationHost;
        private readonly IApplicationPaths _applicationPaths;
        private List<IPluginUIPageController>? _pages;

        // Plugin ID - MUST be unique and stable
        private readonly Guid _id = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <param name="xmlSerializer">The XML serializer.</param>
        /// <param name="logManager">The log manager.</param>
        /// <param name="applicationHost">The application host.</param>
        public Plugin(
            IApplicationPaths? applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogManager logManager,
            IServerApplicationHost applicationHost)
            : base(applicationPaths ?? CreateApplicationPaths(applicationHost), xmlSerializer)
        {
            try
            {
                _applicationHost = applicationHost ?? throw new ArgumentNullException(nameof(applicationHost));
                _applicationPaths = applicationPaths ?? CreateApplicationPaths(applicationHost);
                _logger = logManager?.GetLogger(Name) ?? throw new ArgumentNullException(nameof(logManager));
                
                _logger.Info("Inseerrtion plugin loaded - version {0}", Version.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR in Inseerrtion plugin constructor: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Creates an IApplicationPaths instance from the application host using reflection.
        /// </summary>
        private static IApplicationPaths CreateApplicationPaths(IServerApplicationHost host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            // Try to get paths from the host via reflection
            var hostType = host.GetType();
            
            // Try to find a property that returns IApplicationPaths
            var pathsProperty = hostType.GetProperty("ApplicationPaths") 
                ?? hostType.GetProperty("Paths")
                ?? hostType.GetProperty("ApplicationHost");
                
            if (pathsProperty != null)
            {
                var value = pathsProperty.GetValue(host);
                if (value is IApplicationPaths paths)
                    return paths;
            }

            // Try to cast the host itself
            if (host is IApplicationPaths hostPaths)
                return hostPaths;

            // Create a wrapper using reflection to get path properties from the host
            return new ApplicationPathsFromHost(host);
        }

        /// <summary>
        /// Wrapper that extracts IApplicationPaths properties from IServerApplicationHost via reflection.
        /// </summary>
        private class ApplicationPathsFromHost : IApplicationPaths
        {
            private readonly IServerApplicationHost _host;
            private readonly string _programDataPath;

            public ApplicationPathsFromHost(IServerApplicationHost host)
            {
                _host = host ?? throw new ArgumentNullException(nameof(host));
                
                // Try to get ProgramDataPath from host via reflection
                var hostType = host.GetType();
                
                _programDataPath = GetPropertyValue(host, "ProgramDataPath") 
                    ?? GetPropertyValue(host, "DataPath")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Emby-Server", "programdata");

                // Ensure plugin config directory exists
                var configPath = Path.Combine(_programDataPath, "plugins", "configurations");
                if (!Directory.Exists(configPath))
                    Directory.CreateDirectory(configPath);
            }

            private static string? GetPropertyValue(object obj, string propertyName)
            {
                var prop = obj.GetType().GetProperty(propertyName);
                return prop?.GetValue(obj)?.ToString();
            }

            public string ProgramDataPath => _programDataPath;
            public string DataPath => _programDataPath;
            public string ProgramSystemPath => Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty;
            public string PluginConfigurationsPath => Path.Combine(_programDataPath, "plugins", "configurations");
            public string PluginsPath => Path.Combine(ProgramSystemPath, "plugins");
            public string TempUpdatePath => Path.Combine(_programDataPath, "temp", "update");
            public string SystemLogFilePath => Path.Combine(_programDataPath, "logs");
            public string LogDirectoryPath => Path.Combine(_programDataPath, "logs");
            public string ConfigurationFilePath => Path.Combine(_programDataPath, "config");
            public string ApplicationResourcesPath => ProgramSystemPath;
            public string VisualStudioPath => string.Empty;
            public string ImageCachePath => Path.Combine(_programDataPath, "cache", "images");
            public string ImageCaptureLocationsPath => Path.Combine(_programDataPath, "config", "imagecapture");
            public string RootFolderPath => _programDataPath;
            public string DefaultMetadataPath => Path.Combine(_programDataPath, "metadata");
            public string VirtualDataPath => "/";
            public string PlaylistFolderPath => Path.Combine(_programDataPath, "playlists");
            public string SubtitlesPath => Path.Combine(_programDataPath, "subtitles");
            public string FontsPath => Path.Combine(_programDataPath, "fonts");
            public string ArtistsFolderPath => Path.Combine(_programDataPath, "artists");
            public string GenreFolderPath => Path.Combine(_programDataPath, "genres");
            public string StudioFolderPath => Path.Combine(_programDataPath, "studios");
            public string YearFolderPath => Path.Combine(_programDataPath, "years");
            public string GeneralCachePath => Path.Combine(_programDataPath, "cache");
            public string MusicBrainzArtistPath => Path.Combine(_programDataPath, "musicbrainz", "artists");
            public string MusicBrainzReleasePath => Path.Combine(_programDataPath, "musicbrainz", "releases");
            public string BifTrashPath => Path.Combine(_programDataPath, "bif");
            public string LavFiltersPath => Path.Combine(_programDataPath, "lavfilters");
            public string TempDirectory => Path.Combine(_programDataPath, "temp");
            public string ShutterEncoderPath => Path.Combine(_programDataPath, "shutterencoder");
            public string DVDAssPath => Path.Combine(_programDataPath, "dvdass");
            public string ConfigurationDirectoryPath => Path.Combine(_programDataPath, "config");
            public string SystemConfigurationFilePath => Path.Combine(_programDataPath, "config", "system.xml");
            public string CachePath => Path.Combine(_programDataPath, "cache");

#pragma warning disable CS8618 // Non-nullable event
            public event EventHandler? CachePathChanged;
#pragma warning restore CS8618

            public ReadOnlySpan<char> GetImageCachePath() => ImageCachePath.AsSpan();
            public ReadOnlySpan<char> GetCachePath() => CachePath.AsSpan();
        }

        /// <summary>
        /// Gets the user mapping service (lazy-initialized).
        /// </summary>
        public UserMappingService UserMappingService
        {
            get
            {
                if (_userMappingService == null)
                {
                    _userMappingService = new UserMappingService(_logger, Configuration, _applicationPaths);
                }
                return _userMappingService;
            }
        }

        /// <inheritdoc />
        public override string Name => "Inseerrtion";

        /// <inheritdoc />
        public override string Description => "Integrate Seerr media requests directly into Emby";

        /// <inheritdoc />
        public override Guid Id => _id;

        /// <inheritdoc />
        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        /// <inheritdoc />
        public IReadOnlyCollection<IPluginUIPageController> UIPageControllers
        {
            get
            {
                if (_pages == null)
                {
                    try
                    {
                        _pages = new List<IPluginUIPageController>();
                        _pages.Add(new ConfigPageController(CreatePluginInfo(), Configuration, _logger));
                        _pages.Add(new SearchPageController(CreatePluginInfo(), _logger, this));
                        _logger.Debug("UIPageControllers initialized with {0} pages", _pages.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("Failed to initialize UIPageControllers", ex);
                        _pages = new List<IPluginUIPageController>();
                    }
                }

                return _pages.AsReadOnly();
            }
        }

        /// <inheritdoc />
        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Resources.ThumbImage.png")
                ?? Stream.Null;
        }

        /// <inheritdoc />
        public override void OnUninstalling()
        {
            _logger.Info("Inseerrtion plugin is being uninstalled");
            base.OnUninstalling();
        }

        /// <summary>
        /// Creates the plugin info.
        /// </summary>
        /// <returns>The plugin info.</returns>
        private PluginInfo CreatePluginInfo()
        {
            return new PluginInfo
            {
                Id = Id.ToString(),
                Name = Name,
                Version = Version.ToString(),
                Description = Description
            };
        }
    }
}
