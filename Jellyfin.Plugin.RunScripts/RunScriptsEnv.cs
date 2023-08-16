using System;

namespace Jellyfin.Plugin.RunScripts;

/// <summary>
/// Contains all the fields that will be JSON serialized and added to the script environment.
/// </summary>
public class RunScriptsEnv
{
    /// <summary>Gets or sets User Guid.</summary>
    public Guid? UserId { get; set; } = null;

    /// <summary>Gets or sets User Name.</summary>
    public string? UserName { get; set; } = null;

    /// <summary>Gets or sets Session Id.</summary>
    public string? SessionId { get; set; } = null;

    /// <summary>Gets or sets Device Id.</summary>
    public string? DeviceId { get; set; } = null;

    /// <summary>Gets or sets Device Name.</summary>
    public string? DeviceName { get; set; } = null;

    /// <summary>Gets or sets Client Name.</summary>
    public string? ClientName { get; set; } = null;

    /// <summary>Gets or sets Media Source.</summary>
    public MediaBrowser.Model.Dto.MediaSourceInfo? MediaSource { get; set; } = null;

    /// <summary>Gets or sets Media Info.</summary>
    public MediaBrowser.Model.Dto.BaseItemDto? MediaInfo { get; set; } = null;

    /// <summary>Gets or sets a value indicating whether [played to completion].</summary>
    public bool? PlayedToCompletion { get; set; } = null;

    /// <summary>Gets or sets Playback Position Ticks.</summary>
    public long? PlaybackPositionTicks { get; set; } = null;

    /// <summary>Gets or sets Playback Percentage.</summary>
    public double? PlaybackPercentage { get; set; } = null;
}
