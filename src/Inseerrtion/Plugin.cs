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
        /// <param name="xmlSerializer">The XML serializer.</param>
        /// <param name="logManager">The log manager.</param>
        /// <param name="applicationHost">The application host (also provides IApplicationPaths).</param>
        public Plugin(
            IXmlSerializer xmlSerializer,
            ILogManager logManager,
            IServerApplicationHost applicationHost)
            : base(GetApplicationPaths(applicationHost), xmlSerializer)
        {
            try
            {
                _applicationHost = applicationHost ?? throw new ArgumentNullException(nameof(applicationHost));
                _applicationPaths = (applicationHost as IApplicationPaths) 
                    ?? throw new InvalidOperationException("IServerApplicationHost must implement IApplicationPaths");
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
        /// Gets IApplicationPaths from the application host.
        /// </summary>
        private static IApplicationPaths GetApplicationPaths(IServerApplicationHost host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            
            // Try to cast IServerApplicationHost to IApplicationPaths
            if (host is IApplicationPaths paths)
                return paths;
            
            throw new InvalidOperationException(
                "IServerApplicationHost does not implement IApplicationPaths. " +
                "This plugin requires an Emby version where the application host provides path information.");
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
