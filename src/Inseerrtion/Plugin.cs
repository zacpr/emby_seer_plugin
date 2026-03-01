using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
        private IApplicationPaths _applicationPaths;
        private List<IPluginUIPageController>? _pages;

        // Plugin ID - MUST be unique and stable
        private readonly Guid _id = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogManager logManager,
            IServerApplicationHost applicationHost)
            : base(ResolveApplicationPaths(applicationPaths, applicationHost), xmlSerializer)
        {
            try
            {
                _applicationHost = applicationHost ?? throw new ArgumentNullException(nameof(applicationHost));
                
                // Store the resolved paths
                _applicationPaths = applicationPaths ?? FallbackApplicationPaths.Create();
                
                // Log where configuration is being stored
                Console.WriteLine($"Inseerrtion: PluginConfigurationsPath = {_applicationPaths.PluginConfigurationsPath}");
                Console.WriteLine($"Inseerrtion: Configuration will be saved to: {Path.Combine(_applicationPaths.PluginConfigurationsPath, "Inseerrtion.xml")}");
                
                // Initialize logger
                if (logManager == null)
                {
                    throw new ArgumentNullException(nameof(logManager));
                }
                // Use hardcoded name since base class properties may not be initialized yet
                _logger = logManager.GetLogger("Inseerrtion");
                if (_logger == null)
                {
                    throw new InvalidOperationException("Logger is null");
                }
                
                _logger.Info("Inseerrtion plugin loaded - version 0.1.0");
                
                // Log configuration status
                var config = Configuration;
                if (config != null)
                {
                    _logger.Info("Configuration loaded - Seerr URL: {0}, Has API Key: {1}", 
                        string.IsNullOrEmpty(config.SeerrBaseUrl) ? "(not set)" : config.SeerrBaseUrl,
                        !string.IsNullOrEmpty(config.SeerrApiKey));
                }
                else
                {
                    _logger.Warn("Configuration is null after loading");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR in Inseerrtion plugin constructor: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Resolves IApplicationPaths, using fallback if needed.
        /// </summary>
        private static IApplicationPaths ResolveApplicationPaths(IApplicationPaths? provided, IServerApplicationHost host)
        {
            if (provided != null)
            {
                Console.WriteLine("Inseerrtion: Using provided IApplicationPaths");
                return provided;
            }

            try
            {
                if (host != null)
                {
                    // Try to get from host properties
                    var hostType = host.GetType();
                    var prop = hostType.GetProperty("ApplicationPaths");
                    if (prop != null)
                    {
                        var val = prop.GetValue(host);
                        if (val is IApplicationPaths paths)
                        {
                            Console.WriteLine("Inseerrtion: Found IApplicationPaths via reflection");
                            return paths;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Inseerrtion: Reflection failed: {ex.Message}");
            }

            Console.WriteLine("Inseerrtion: Using fallback application paths");
            return FallbackApplicationPaths.Create();
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

        /// <summary>
        /// Fallback implementation for when IApplicationPaths is not available.
        /// </summary>
        private class FallbackApplicationPaths : IApplicationPaths
        {
            private readonly string _dataPath;

            public static FallbackApplicationPaths Create()
            {
                return new FallbackApplicationPaths();
            }

            public FallbackApplicationPaths()
            {
                _dataPath = FindEmbyDataPath();
                Console.WriteLine($"Inseerrtion FallbackApplicationPaths: Using {_dataPath}");
                
                try
                {
                    var configDir = Path.Combine(_dataPath, "plugins", "configurations");
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Inseerrtion: Warning - could not create config dir: {ex.Message}");
                }
            }

            private static string FindEmbyDataPath()
            {
                // Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    
                    var candidates = new[]
                    {
                        Path.Combine(appData, "Emby-Server", "programdata"),
                        Path.Combine(localAppData, "Emby-Server", "programdata"),
                        @"C:\ProgramData\Emby-Server\programdata",
                    };
                    
                    foreach (var c in candidates)
                    {
                        if (Directory.Exists(c))
                        {
                            Console.WriteLine($"Inseerrtion: Found existing data path: {c}");
                            return c;
                        }
                    }
                    
                    return candidates[0];
                }
                
                // Linux
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var linuxCandidates = new[]
                {
                    "/var/lib/emby/programdata",
                    "/var/lib/emby-server/programdata",
                    Path.Combine(home, ".config", "emby-server", "programdata"),
                };
                
                foreach (var c in linuxCandidates)
                {
                    if (Directory.Exists(c))
                        return c;
                }
                
                return linuxCandidates[2];
            }

            public string ProgramDataPath => _dataPath;
            public string DataPath => _dataPath;
            public string ProgramSystemPath => Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? "";
            public string PluginConfigurationsPath => Path.Combine(_dataPath, "plugins", "configurations");
            public string PluginsPath => Path.Combine(ProgramSystemPath, "plugins");
            public string TempUpdatePath => Path.Combine(_dataPath, "temp", "update");
            public string SystemLogFilePath => Path.Combine(_dataPath, "logs");
            public string LogDirectoryPath => Path.Combine(_dataPath, "logs");
            public string ConfigurationFilePath => Path.Combine(_dataPath, "config");
            public string ApplicationResourcesPath => ProgramSystemPath;
            public string VisualStudioPath => "";
            public string ImageCachePath => Path.Combine(_dataPath, "cache", "images");
            public string ImageCaptureLocationsPath => Path.Combine(_dataPath, "config", "imagecapture");
            public string RootFolderPath => _dataPath;
            public string DefaultMetadataPath => Path.Combine(_dataPath, "metadata");
            public string VirtualDataPath => "/";
            public string PlaylistFolderPath => Path.Combine(_dataPath, "playlists");
            public string SubtitlesPath => Path.Combine(_dataPath, "subtitles");
            public string FontsPath => Path.Combine(_dataPath, "fonts");
            public string ArtistsFolderPath => Path.Combine(_dataPath, "artists");
            public string GenreFolderPath => Path.Combine(_dataPath, "genres");
            public string StudioFolderPath => Path.Combine(_dataPath, "studios");
            public string YearFolderPath => Path.Combine(_dataPath, "years");
            public string GeneralCachePath => Path.Combine(_dataPath, "cache");
            public string MusicBrainzArtistPath => Path.Combine(_dataPath, "musicbrainz", "artists");
            public string MusicBrainzReleasePath => Path.Combine(_dataPath, "musicbrainz", "releases");
            public string BifTrashPath => Path.Combine(_dataPath, "bif");
            public string LavFiltersPath => Path.Combine(_dataPath, "lavfilters");
            public string TempDirectory => Path.Combine(_dataPath, "temp");
            public string ShutterEncoderPath => Path.Combine(_dataPath, "shutterencoder");
            public string DVDAssPath => Path.Combine(_dataPath, "dvdass");
            public string ConfigurationDirectoryPath => Path.Combine(_dataPath, "config");
            public string SystemConfigurationFilePath => Path.Combine(_dataPath, "config", "system.xml");
            public string CachePath => Path.Combine(_dataPath, "cache");

            public event EventHandler? CachePathChanged;

            public ReadOnlySpan<char> GetImageCachePath() => ImageCachePath.AsSpan();
            public ReadOnlySpan<char> GetCachePath() => CachePath.AsSpan();
        }
    }
}
