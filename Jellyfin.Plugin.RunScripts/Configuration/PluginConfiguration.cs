#pragma warning disable CA1819

using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RunScripts.Configuration;

/// <summary>
/// Class for RunScripts plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        RunScriptsUsers = Array.Empty<RunScriptsUser>();
    }

    /// <summary>
    /// Gets or sets the RunScriptsUsers array.
    /// </summary>
    public RunScriptsUser[] RunScriptsUsers { get; set; }
}
