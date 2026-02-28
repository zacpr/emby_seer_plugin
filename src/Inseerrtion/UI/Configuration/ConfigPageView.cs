using System;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Elements;
using Inseerrtion.Configuration;
using Inseerrtion.Services;
using Inseerrtion.UI.Base;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;

namespace Inseerrtion.UI.Configuration
{
    /// <summary>
    /// View for the configuration page.
    /// </summary>
    internal class ConfigPageView : PluginPageView
    {
        private readonly PluginInfo _pluginInfo;
        private readonly PluginConfiguration _configuration;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPageView"/> class.
        /// </summary>
        /// <param name="pluginInfo">The plugin info.</param>
        /// <param name="configuration">The plugin configuration.</param>
        /// <param name="logger">The logger.</param>
        public ConfigPageView(PluginInfo pluginInfo, PluginConfiguration configuration, ILogger logger)
            : base(pluginInfo.Id)
        {
            _pluginInfo = pluginInfo ?? throw new ArgumentNullException(nameof(pluginInfo));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration into the UI model
            ContentData = CreateUIFromConfig();
        }

        /// <summary>
        /// Gets the configuration UI model.
        /// </summary>
        public ConfigPageUI ConfigUI => (ConfigPageUI)ContentData;

        /// <inheritdoc />
        public override async Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            _logger.Debug("Configuration save command received");

            // Update configuration from UI model
            UpdateConfigFromUI(ConfigUI);

            // Update status
            await UpdateConnectionStatusAsync();

            return await base.OnSaveCommand(itemId, commandId, data);
        }

        /// <summary>
        /// Creates the UI model from the current configuration.
        /// </summary>
        /// <returns>The configuration UI model.</returns>
        private ConfigPageUI CreateUIFromConfig()
        {
            var ui = new ConfigPageUI
            {
                SeerrBaseUrl = _configuration.SeerrBaseUrl ?? string.Empty,
                SeerrApiKey = _configuration.SeerrApiKey ?? string.Empty,
                EnableDebugLogging = _configuration.EnableDebugLogging,
                DefaultRequestQuota = _configuration.DefaultRequestQuota,
                AutoApproveAdminRequests = _configuration.AutoApproveAdminRequests
            };

            // Set initial connection status
            UpdateStatusDisplay(ui);

            return ui;
        }

        /// <summary>
        /// Updates the configuration from the UI model.
        /// </summary>
        /// <param name="ui">The UI model.</param>
        private void UpdateConfigFromUI(ConfigPageUI ui)
        {
            _configuration.SeerrBaseUrl = ui.SeerrBaseUrl?.Trim() ?? string.Empty;
            _configuration.SeerrApiKey = ui.SeerrApiKey?.Trim() ?? string.Empty;
            _configuration.EnableDebugLogging = ui.EnableDebugLogging;
            _configuration.DefaultRequestQuota = ui.DefaultRequestQuota;
            _configuration.AutoApproveAdminRequests = ui.AutoApproveAdminRequests;

            _logger.Info("Configuration updated");
        }

        /// <summary>
        /// Updates the connection status display.
        /// </summary>
        /// <param name="ui">The UI model.</param>
        private void UpdateStatusDisplay(ConfigPageUI ui)
        {
            var isConfigured = !string.IsNullOrWhiteSpace(_configuration.SeerrBaseUrl)
                && !string.IsNullOrWhiteSpace(_configuration.SeerrApiKey);

            if (!isConfigured)
            {
                ui.ConnectionStatus.StatusText = "Not configured";
                ui.ConnectionStatus.Status = ItemStatus.Unavailable;
            }
            else
            {
                ui.ConnectionStatus.StatusText = "Configured - Save to test connection";
                ui.ConnectionStatus.Status = ItemStatus.Warning;
            }
        }

        /// <summary>
        /// Updates the connection status by testing the Seerr connection.
        /// </summary>
        private async Task UpdateConnectionStatusAsync()
        {
            try
            {
                using var client = new SeerrClient(_logger, _configuration);
                var status = await client.GetStatusAsync();

                if (status != null)
                {
                    ConfigUI.ConnectionStatus.StatusText = $"Connected to Seerr v{status.Version}";
                    ConfigUI.ConnectionStatus.Status = ItemStatus.Succeeded;
                    _logger.Info("Connection test successful - Seerr v{0}", status.Version);
                }
                else
                {
                    ConfigUI.ConnectionStatus.StatusText = "Failed to connect - check URL and API key";
                    ConfigUI.ConnectionStatus.Status = ItemStatus.Failed;
                    _logger.Warn("Connection test failed");
                }
            }
            catch (Exception ex)
            {
                ConfigUI.ConnectionStatus.StatusText = $"Error: {ex.Message}";
                ConfigUI.ConnectionStatus.Status = ItemStatus.Failed;
                _logger.ErrorException("Connection test error", ex);
            }
        }
    }
}
