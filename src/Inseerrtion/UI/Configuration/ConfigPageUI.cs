using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using MediaBrowser.Model.Attributes;

namespace Inseerrtion.UI.Configuration
{
    /// <summary>
    /// Configuration UI model for Inseerrtion plugin using Emby's declarative UI framework.
    /// </summary>
    public class ConfigPageUI : EditableOptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPageUI"/> class.
        /// </summary>
        public ConfigPageUI()
        {
            SeerrBaseUrl = string.Empty;
            SeerrApiKey = string.Empty;
            EnableDebugLogging = false;
            DefaultRequestQuota = 0;
            AutoApproveAdminRequests = true;
        }

        /// <inheritdoc />
        public override string EditorTitle => "Inseerrtion Configuration";

        /// <inheritdoc />
        public override string EditorDescription => "Configure your Seerr integration settings. "
            + "Enter your Seerr server URL and API key to enable media request functionality within Emby.";

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public StatusItem ConnectionStatus { get; set; } = new StatusItem("Connection Status", "Not configured");

        public SpacerItem Spacer1 { get; set; } = new SpacerItem();

        public CaptionItem CaptionConnection { get; set; } = new CaptionItem("Seerr Connection");

        /// <summary>
        /// Gets or sets the Seerr base URL.
        /// </summary>
        [DisplayName("Seerr URL")]
        [Description("The base URL of your Seerr instance (e.g., http://localhost:5055 or https://seerr.example.com)")]
        [Required]
        public string SeerrBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the Seerr API key.
        /// </summary>
        [DisplayName("API Key")]
        [Description("Your Seerr admin API key. Found in Settings > General > API Key")]
        [Required]
        [IsPassword]
        public string SeerrApiKey { get; set; }

        public SpacerItem Spacer2 { get; set; } = new SpacerItem();

        public CaptionItem CaptionSettings { get; set; } = new CaptionItem("Plugin Settings");

        /// <summary>
        /// Gets or sets a value indicating whether to enable debug logging.
        /// </summary>
        [DisplayName("Enable Debug Logging")]
        [Description("Enable verbose logging for troubleshooting. Logs will appear in Emby's server logs.")]
        public bool EnableDebugLogging { get; set; }

        /// <summary>
        /// Gets or sets the default request quota per user.
        /// </summary>
        [DisplayName("Default Request Quota")]
        [Description("Maximum number of requests per user (0 = unlimited). This is a local quota managed by the plugin.")]
        public int DefaultRequestQuota { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to auto-approve admin requests.
        /// </summary>
        [DisplayName("Auto-approve Admin Requests")]
        [Description("Automatically approve media requests from users with admin privileges.")]
        public bool AutoApproveAdminRequests { get; set; }

        public SpacerItem Spacer3 { get; set; } = new SpacerItem();

        public CaptionItem CaptionActions { get; set; } = new CaptionItem("Actions");

        /// <summary>
        /// Gets or sets the test connection button.
        /// </summary>
        [DisplayName("Test Connection")]
        [Description("Click to test the connection to your Seerr instance")]
        public ButtonItem TestConnectionButton { get; set; } = new ButtonItem("Test Connection");

        public SpacerItem Spacer4 { get; set; } = new SpacerItem();

        public CaptionItem CaptionAbout { get; set; } = new CaptionItem("About");

        public LabelItem AboutLabel { get; set; } = new LabelItem(
            "Inseerrtion integrates Seerr media request functionality directly into Emby. "
            + "Users can search for and request movies and TV shows without leaving the Emby interface.");
    }
}
