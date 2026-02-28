using System;
using System.Threading.Tasks;
using Inseerrtion.UI.Base;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;

namespace Inseerrtion.UI.Search
{
    /// <summary>
    /// Controller for the search page.
    /// </summary>
    internal class SearchPageController : ControllerBase
    {
        private readonly PluginInfo _pluginInfo;
        private readonly ILogger _logger;
        private readonly Plugin _plugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchPageController"/> class.
        /// </summary>
        /// <param name="pluginInfo">The plugin info.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="plugin">The plugin instance.</param>
        public SearchPageController(PluginInfo pluginInfo, ILogger logger, Plugin plugin)
            : base(pluginInfo.Id)
        {
            _pluginInfo = pluginInfo ?? throw new ArgumentNullException(nameof(pluginInfo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

            PageInfo = new PluginPageInfo
            {
                Name = "InseerrtionSearch",
                EnableInMainMenu = true,
                DisplayName = "Request Media",
                MenuIcon = "search",
                IsMainConfigPage = false
            };

            _logger.Debug("SearchPageController initialized");
        }

        /// <inheritdoc />
        public override PluginPageInfo PageInfo { get; }

        /// <inheritdoc />
        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new SearchPageView(_pluginInfo, _logger, _plugin);
            return Task.FromResult(view);
        }
    }
}
