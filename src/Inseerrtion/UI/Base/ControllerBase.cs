using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;

namespace Inseerrtion.UI.Base
{
    /// <summary>
    /// Base class for plugin UI page controllers.
    /// </summary>
    public abstract class ControllerBase : IPluginUIPageController
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ControllerBase"/> class.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        protected ControllerBase(string pluginId)
        {
            PluginId = pluginId;
        }

        /// <summary>
        /// Gets the page info.
        /// </summary>
        public abstract PluginPageInfo PageInfo { get; }

        /// <summary>
        /// Gets the plugin ID.
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// Initializes the controller.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        public virtual Task Initialize(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates the default page view.
        /// </summary>
        /// <returns>The plugin UI view.</returns>
        public abstract Task<IPluginUIView> CreateDefaultPageView();
    }
}
