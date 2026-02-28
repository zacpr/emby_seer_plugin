using System.Collections.Generic;
using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using MediaBrowser.Model.Attributes;

namespace Inseerrtion.UI.Search
{
    /// <summary>
    /// Search UI model for Inseerrtion plugin.
    /// </summary>
    public class SearchPageUI : EditableOptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchPageUI"/> class.
        /// </summary>
        public SearchPageUI()
        {
            SearchQuery = string.Empty;
            SearchResults = new List<SearchResultItemUI>();
        }

        /// <inheritdoc />
        public override string EditorTitle => "Request Media";

        /// <inheritdoc />
        public override string EditorDescription => "Search for movies and TV shows to request. "
            + "Results will show availability and request status from your Seerr instance.";

        public SpacerItem Spacer1 { get; set; } = new SpacerItem();

        public CaptionItem CaptionSearch { get; set; } = new CaptionItem("Search");

        /// <summary>
        /// Gets or sets the search query.
        /// </summary>
        [DisplayName("Search Query")]
        [Description("Enter a movie or TV show title to search for")]
        public string SearchQuery { get; set; }

        /// <summary>
        /// Gets or sets the search button.
        /// </summary>
        [DisplayName("Search")]
        [Description("Click to search for media")]
        public ButtonItem SearchButton { get; set; } = new ButtonItem("Search") { Icon = IconNames.search };

        public SpacerItem Spacer2 { get; set; } = new SpacerItem();

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public StatusItem SearchStatus { get; set; } = new StatusItem("Status", "Enter a search term and click Search", ItemStatus.None);

        public SpacerItem Spacer3 { get; set; } = new SpacerItem();

        public CaptionItem CaptionResults { get; set; } = new CaptionItem("Results");

        /// <summary>
        /// Gets or sets the search results.
        /// </summary>
        [DisplayName("Results")]
        [Description("Search results from Seerr")]
        public List<SearchResultItemUI> SearchResults { get; set; }
    }

    /// <summary>
    /// Represents a single search result item in the UI.
    /// </summary>
    public class SearchResultItemUI : EditableOptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchResultItemUI"/> class.
        /// </summary>
        public SearchResultItemUI()
        {
            Title = string.Empty;
            Overview = string.Empty;
            MediaType = string.Empty;
            PosterUrl = string.Empty;
        }

        /// <inheritdoc />
        public override string EditorTitle => Title ?? "Search Result";

        /// <summary>
        /// Gets or sets the media ID.
        /// </summary>
        public int MediaId { get; set; }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        [DisplayName("Type")]
        public string MediaType { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [DisplayName("Title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        [DisplayName("Overview")]
        [EditMultiline(3)]
        public string Overview { get; set; }

        /// <summary>
        /// Gets or sets the poster URL.
        /// </summary>
        public string PosterUrl { get; set; }

        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        [DisplayName("Year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is already requested.
        /// </summary>
        [DisplayName("Requested")]
        public bool IsRequested { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is available.
        /// </summary>
        [DisplayName("Available")]
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the request button.
        /// </summary>
        [DisplayName("")]
        public ButtonItem RequestButton { get; set; } = new ButtonItem("Request") { Icon = IconNames.add_circle };

        /// <summary>
        /// Gets the status text for display.
        /// </summary>
        public string StatusText
        {
            get
            {
                if (IsAvailable)
                    return "✓ Available";
                if (IsRequested)
                    return "⏳ Requested";
                return "Available to request";
            }
        }
    }
}
