using MediaBrowser.Model.Plugins;

namespace Inseerrtion.Configuration
{
    /// <summary>
    /// Plugin configuration class for Inseerrtion.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the Seerr base URL.
        /// </summary>
        public string SeerrBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Seerr admin API key.
        /// </summary>
        public string SeerrApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to enable debug logging.
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets the default request quota per user (0 = unlimited).
        /// </summary>
        public int DefaultRequestQuota { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether to auto-approve requests from admin users.
        /// </summary>
        public bool AutoApproveAdminRequests { get; set; } = true;
    }
}
