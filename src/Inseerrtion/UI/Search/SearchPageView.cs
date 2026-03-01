using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Elements;
using Inseerrtion.Services;
using Inseerrtion.UI.Base;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;

namespace Inseerrtion.UI.Search
{
    /// <summary>
    /// View for the search page.
    /// </summary>
    internal class SearchPageView : PluginPageView
    {
        private readonly PluginInfo _pluginInfo;
        private readonly ILogger _logger;
        private readonly Plugin _plugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchPageView"/> class.
        /// </summary>
        /// <param name="pluginInfo">The plugin info.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="plugin">The plugin instance.</param>
        public SearchPageView(PluginInfo pluginInfo, ILogger logger, Plugin plugin)
            : base(pluginInfo.Id)
        {
            _pluginInfo = pluginInfo ?? throw new ArgumentNullException(nameof(pluginInfo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

            ContentData = new SearchPageUI
            {
                SearchResults = new List<SearchResultItemUI>()
            };
        }

        /// <summary>
        /// Gets the search UI model.
        /// </summary>
        public SearchPageUI SearchUI => (SearchPageUI)ContentData;

        /// <inheritdoc />
        public override async Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            _logger.Debug("Search page command: itemId={0}, commandId={1}", itemId, commandId);

            // Handle search button
            if (commandId == nameof(SearchPageUI.SearchButton) || 
                (itemId == nameof(SearchPageUI.SearchQuery) && !string.IsNullOrWhiteSpace(SearchUI.SearchQuery)))
            {
                await PerformSearchAsync();
            }
            // Handle request button on a result item
            else if (commandId != null && commandId.StartsWith("Request_"))
            {
                if (int.TryParse(commandId.Replace("Request_", ""), out var mediaId))
                {
                    await RequestMediaAsync(mediaId);
                }
                else
                {
                    _logger.Error("Invalid media ID format in command: {0}", commandId);
                }
            }

            RaiseUIViewInfoChanged();
            return await base.OnSaveCommand(itemId, commandId, data);
        }

        /// <inheritdoc />
        public override async Task<IPluginUIView?> RunCommand(string itemId, string commandId, string data)
        {
            _logger.Debug("RunCommand: itemId={0}, commandId={1}", itemId, commandId);

            // Handle button clicks
            if (commandId == nameof(SearchPageUI.SearchButton))
            {
                await PerformSearchAsync();
                RaiseUIViewInfoChanged();
                return this;
            }

            // Check if it's a request button click from a result item
            if (itemId?.StartsWith("SearchResults[") == true && commandId == nameof(SearchResultItemUI.RequestButton))
            {
                // Extract index from itemId (format: "SearchResults[0]")
                var indexStr = itemId.Replace("SearchResults[", "").Replace("]", "");
                if (int.TryParse(indexStr, out var index) && index < SearchUI.SearchResults.Count)
                {
                    var item = SearchUI.SearchResults[index];
                    await RequestMediaAsync(item.MediaId, item.MediaType);
                    RaiseUIViewInfoChanged();
                    return this;
                }
            }

            return await base.RunCommand(itemId, commandId, data);
        }

        /// <summary>
        /// Performs the search against Seerr.
        /// </summary>
        private async Task PerformSearchAsync()
        {
            var query = SearchUI.SearchQuery?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchUI.SearchStatus.StatusText = "Please enter a search term";
                SearchUI.SearchStatus.Status = ItemStatus.Warning;
                return;
            }

            SearchUI.SearchStatus.StatusText = "Searching...";
            SearchUI.SearchStatus.Status = ItemStatus.InProgress;
            SearchUI.SearchResults.Clear();
            RaiseUIViewInfoChanged();

            try
            {
                using var client = new SeerrClient(_logger, _plugin.Configuration);
                var results = await client.SearchAsync(query, 1);

                if (results?.Results == null || results.Results.Length == 0)
                {
                    SearchUI.SearchStatus.StatusText = "No results found";
                    SearchUI.SearchStatus.Status = ItemStatus.Warning;
                    return;
                }

                // Map results to UI model
                foreach (var result in results.Results)
                {
                    int? year = null;
                    if (!string.IsNullOrEmpty(result.ReleaseDate) && result.ReleaseDate.Length >= 4)
                    {
                        if (int.TryParse(result.ReleaseDate.Substring(0, 4), out var y))
                        {
                            year = y;
                        }
                    }

                    var item = new SearchResultItemUI
                    {
                        MediaId = result.Id,
                        MediaType = result.MediaType?.ToLowerInvariant() == "tv" ? "tv" : "movie",
                        Title = result.Title ?? "Unknown",
                        Overview = result.Overview ?? "",
                        PosterUrl = result.PosterPath ?? "",
                        Year = year,
                        IsRequested = result.Requested,
                        IsAvailable = false, // TODO: Check availability
                        RequestButton = new ButtonItem("Request")
                        {
                            Icon = IconNames.add_circle,
                            Data1 = $"Request_{result.Id}"
                        }
                    };

                    // Update button text if already requested
                    if (item.IsRequested || item.IsAvailable)
                    {
                        item.RequestButton = new ButtonItem(item.IsAvailable ? "✓ Available" : "⏳ Requested");
                    }

                    SearchUI.SearchResults.Add(item);
                }

                SearchUI.SearchStatus.StatusText = $"Found {SearchUI.SearchResults.Count} results";
                SearchUI.SearchStatus.Status = ItemStatus.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Search failed", ex);
                SearchUI.SearchStatus.StatusText = $"Search failed: {ex.Message}";
                SearchUI.SearchStatus.Status = ItemStatus.Failed;
            }
        }

        /// <summary>
        /// Requests a media item.
        /// </summary>
        private async Task RequestMediaAsync(int mediaId, string? mediaType = null)
        {
            try
            {
                SearchUI.SearchStatus.StatusText = "Creating request...";
                SearchUI.SearchStatus.Status = ItemStatus.InProgress;
                RaiseUIViewInfoChanged();

                // Find the item in results to get media type if not provided
                var item = SearchUI.SearchResults.FirstOrDefault(r => r.MediaId == mediaId);
                var type = mediaType ?? item?.MediaType ?? "movie";

                using var client = new SeerrClient(_logger, _plugin.Configuration);

                // For now, we need a user mapping to make requests
                // This is a simplified version - in production, we'd get the current user
                var requestData = new CreateMediaRequest
                {
                    MediaType = type,
                    MediaId = mediaId
                };

                var result = await client.CreateRequestAsync(requestData);

                if (result != null)
                {
                    SearchUI.SearchStatus.StatusText = $"Request created successfully! Status: {result.StatusString}";
                    SearchUI.SearchStatus.Status = ItemStatus.Succeeded;

                    // Update the item in the list
                    if (item != null)
                    {
                        item.IsRequested = true;
                        item.RequestButton = new ButtonItem("⏳ Requested");
                    }
                }
                else
                {
                    SearchUI.SearchStatus.StatusText = "Failed to create request";
                    SearchUI.SearchStatus.Status = ItemStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Request failed", ex);
                SearchUI.SearchStatus.StatusText = $"Request failed: {ex.Message}";
                SearchUI.SearchStatus.Status = ItemStatus.Failed;
            }
        }
    }
}
