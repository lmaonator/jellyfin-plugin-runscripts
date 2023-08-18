using System;
using System.Collections.Generic;
using Jellyfin.Plugin.RunScripts.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RunScripts;

/// <summary>
/// Plugin class for the RunScripts plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The <see cref="IApplicationPaths"/>.</param>
    /// <param name="xmlSerializer">The <see cref="IXmlSerializer"/>.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc/>
    public override string Name => "RunScripts";

    /// <inheritdoc/>
    public override Guid Id => Guid.Parse("616552f9-8355-4737-bbe0-8217f9e8ea14");

    /// <summary>
    /// Gets the instance of the RunScripts plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Return the plugin configuration page.
    /// </summary>
    /// <returns>PluginPageInfo.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
            },
            new PluginPageInfo
            {
                Name = "runscriptsjs",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.runscripts.js"
            }
        };
    }
}
