using System;
using System.Threading.Tasks;
using Inseerrtion.Configuration;
using Inseerrtion.UI.Base;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;

namespace Inseerrtion.UI.Configuration
{
    /// <summary>
    /// Controller for the configuration page.
    /// </summary>
    internal class ConfigPageController : ControllerBase
    {
        private readonly PluginInfo _pluginInfo;
        private readonly PluginConfiguration _configuration;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPageController"/> class.
        /// </summary>
        /// <param name="pluginInfo">The plugin info.</param>
        /// <param name="configuration">The plugin configuration.</param>
        /// <param name="logger">The logger.</param>
        public ConfigPageController(PluginInfo pluginInfo, PluginConfiguration configuration, ILogger logger)
            : base(pluginInfo.Id)
        {
            _pluginInfo = pluginInfo ?? throw new ArgumentNullException(nameof(pluginInfo));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PageInfo = new PluginPageInfo
            {
                Name = "InseerrtionConfig",
                EnableInMainMenu = true,
                DisplayName = "Inseerrtion",
                MenuIcon = "settings",
                IsMainConfigPage = true
            };

            _logger.Debug("ConfigPageController initialized");
        }

        /// <inheritdoc />
        public override PluginPageInfo PageInfo { get; }

        /// <inheritdoc />
        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new ConfigPageView(_pluginInfo, _configuration, _logger);
            return Task.FromResult(view);
        }
    }
}
