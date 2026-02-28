using System;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Plugins.UI.Views.Enums;

namespace Inseerrtion.UI.Base
{
    /// <summary>
    /// Base class for plugin views.
    /// </summary>
    public abstract class PluginViewBase : IPluginUIView, IPluginViewWithOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginViewBase"/> class.
        /// </summary>
        /// <param name="pluginId">The plugin ID.</param>
        protected PluginViewBase(string pluginId)
        {
            PluginId = pluginId;
        }

        /// <inheritdoc />
        public event EventHandler<GenericEventArgs<IPluginUIView>>? UIViewInfoChanged;

        /// <inheritdoc />
        public virtual string Caption => ContentData?.EditorTitle ?? string.Empty;

        /// <inheritdoc />
        public virtual string SubCaption => ContentData?.EditorDescription ?? string.Empty;

        /// <inheritdoc />
        public string PluginId { get; }

        /// <inheritdoc />
        public IEditableObject ContentData
        {
            get => ContentDataCore;
            set => ContentDataCore = value;
        }

        /// <inheritdoc />
        public UserDto? User { get; set; }

        /// <inheritdoc />
        public string? RedirectViewUrl { get; set; }

        /// <summary>
        /// Gets or sets the help URL.
        /// </summary>
        public Uri? HelpUrl { get; set; }

        /// <summary>
        /// Gets or sets the query close action.
        /// </summary>
        public QueryCloseAction QueryCloseAction { get; set; }

        /// <summary>
        /// Gets or sets the wizard hiding behavior.
        /// </summary>
        public WizardHidingBehavior WizardHidingBehavior { get; set; }

        /// <summary>
        /// Gets or sets the compact view appearance.
        /// </summary>
        public CompactViewAppearance CompactViewAppearance { get; set; }

        /// <summary>
        /// Gets or sets the dialog size.
        /// </summary>
        public DialogSize DialogSize { get; set; }

        /// <summary>
        /// Gets or sets the OK button caption.
        /// </summary>
        public string? OKButtonCaption { get; set; }

        /// <summary>
        /// Gets or sets the primary dialog action.
        /// </summary>
        public DialogAction PrimaryDialogAction { get; set; }

        /// <summary>
        /// Gets or sets the core content data.
        /// </summary>
        protected IEditableObject ContentDataCore { get; set; } = null!;

        /// <inheritdoc />
        public virtual bool IsCommandAllowed(string commandKey)
        {
            return true;
        }

        /// <inheritdoc />
        public virtual Task<IPluginUIView?> RunCommand(string itemId, string commandId, string data)
        {
            return Task.FromResult<IPluginUIView?>(null);
        }

        /// <inheritdoc />
        public virtual Task Cancel()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual void OnDialogResult(IPluginUIView? dialogView, bool completedOk, object? data)
        {
        }

        /// <summary>
        /// Raises the UIViewInfoChanged event.
        /// </summary>
        protected virtual void RaiseUIViewInfoChanged()
        {
            UIViewInfoChanged?.Invoke(this, new GenericEventArgs<IPluginUIView>(this));
        }

        /// <summary>
        /// Raises the UIViewInfoChanged event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void RaiseUIViewInfoChanged(GenericEventArgs<IPluginUIView> e)
        {
            UIViewInfoChanged?.Invoke(this, e);
        }

        /// <inheritdoc />
        public virtual PluginViewOptions ViewOptions
        {
            get
            {
                return new PluginViewOptions
                {
                    HelpUrl = HelpUrl,
                    CompactViewAppearance = CompactViewAppearance,
                    QueryCloseAction = QueryCloseAction,
                    DialogSize = DialogSize,
                    OKButtonCaption = OKButtonCaption,
                    PrimaryDialogAction = PrimaryDialogAction,
                    WizardHidingBehavior = WizardHidingBehavior
                };
            }
        }
    }
}
