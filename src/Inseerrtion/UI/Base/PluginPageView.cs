using System.Threading.Tasks;
using MediaBrowser.Model.Plugins.UI.Views;

namespace Inseerrtion.UI.Base
{
    /// <summary>
    /// Base class for plugin page views.
    /// </summary>
    public abstract class PluginPageView : PluginViewBase, IPluginPageView
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginPageView"/> class.
        /// </summary>
        /// <param name="pluginId">The plugin ID.</param>
        protected PluginPageView(string pluginId)
            : base(pluginId)
        {
        }

        /// <inheritdoc />
        public bool ShowSave { get; set; } = true;

        /// <inheritdoc />
        public bool ShowBack { get; set; } = false;

        /// <inheritdoc />
        public bool AllowSave { get; set; } = true;

        /// <inheritdoc />
        public bool AllowBack { get; set; } = true;

        /// <inheritdoc />
        public virtual Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            return Task.FromResult((IPluginUIView)this);
        }
    }
}
