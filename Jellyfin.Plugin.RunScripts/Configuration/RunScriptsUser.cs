using System;

namespace Jellyfin.Plugin.RunScripts.Configuration;

/// <summary>
/// Class for RunScripts User settings.
/// </summary>
public class RunScriptsUser
{
    /// <summary>Gets or sets User Guid.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets command to run after playback starts.
    /// </summary>
    public string? CmdPlaybackStart { get; set; }

    /// <summary>
    /// Gets or sets command to run after playback is stopped.
    /// </summary>
    public string? CmdPlaybackStopped { get; set; }
}
