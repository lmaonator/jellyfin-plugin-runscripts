using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Plugin.RunScripts.Configuration;
using Medallion.Shell;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RunScripts;

/// <summary>
/// RunScripts class.
/// </summary>
public class RunScripts : IServerEntryPoint
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<RunScripts> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunScripts"/> class.
    /// </summary>
    /// <param name="sessionManager">The <see cref="ISessionManager"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
    public RunScripts(ISessionManager sessionManager, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<RunScripts>();
        _sessionManager = sessionManager;
        _jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    }

    /// <inheritdoc />
    public Task RunAsync()
    {
        // Bind events
        _sessionManager.PlaybackStart += PlaybackStart;
        _sessionManager.PlaybackStopped += PlaybackStopped;
        return Task.CompletedTask;
    }

    /// <summary>
    /// MediaInfo.Path is incorrect for multi-version files, grab the real MediaSource from playlist.
    /// </summary>
    /// <param name="e">Event info.</param>
    /// <returns>The currently playing MediaSource or null.</returns>
    private MediaBrowser.Model.Dto.MediaSourceInfo? GetPlayingVersion(PlaybackProgressEventArgs e)
    {
        var mediaSourceId = e.MediaSourceId;
        foreach (var queueItem in e.Session.NowPlayingQueueFullItems)
        {
            foreach (var mediaSource in queueItem.MediaSources)
            {
                if (mediaSource.Id == mediaSourceId)
                {
                    return mediaSource;
                }
            }
        }

        return null;
    }

    private RunScriptsEnv GetScriptEnvStart(PlaybackProgressEventArgs e)
    {
        double? playbackPercentage = null;
        if (e.PlaybackPositionTicks != null && e.MediaInfo.RunTimeTicks != null && e.PlaybackPositionTicks > 0)
        {
            playbackPercentage = (double)e.PlaybackPositionTicks / (double)e.MediaInfo.RunTimeTicks;
        }

        var scriptEnv = new RunScriptsEnv
        {
            UserId = e.Session.UserId,
            UserName = e.Session.UserName,
            SessionId = e.Session.Id,
            DeviceId = e.Session.DeviceId,
            DeviceName = e.Session.DeviceName,
            ClientName = e.Session.Client,
            MediaSource = GetPlayingVersion(e),
            MediaInfo = e.MediaInfo,
            PlaybackPositionTicks = e.PlaybackPositionTicks,
            PlaybackPercentage = playbackPercentage,
        };
        return scriptEnv;
    }

    private RunScriptsEnv GetScriptEnvStop(PlaybackStopEventArgs e)
    {
        var scriptEnv = GetScriptEnvStart(e);
        scriptEnv.PlayedToCompletion = e.PlayedToCompletion;
        return scriptEnv;
    }

    private async void PlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        foreach (var user in e.Users)
        {
            var userConfig = GetUserConfig(user.Id);
            if (userConfig == null)
            {
                _logger.LogDebug("{Username}: No configuration", user.Username);
                continue;
            }

            if (string.IsNullOrEmpty(userConfig.CmdPlaybackStart))
            {
                _logger.LogDebug("{Username}: No configured PlaybackStart command", user.Username);
                continue;
            }

            _logger.LogInformation("{Username}: Running command \"{CmdPlaybackStart}\"", user.Username, userConfig.CmdPlaybackStart);

            var scriptEnv = GetScriptEnvStart(e);

            try
            {
                var command = Command.Run(
                    userConfig.CmdPlaybackStart.Split(" ")[0],
                    userConfig.CmdPlaybackStart.Split(" ")[1..],
                    options => options
                        .Timeout(TimeSpan.FromMinutes(10))
                        .EnvironmentVariable("EVENT_ARGS", JsonSerializer.Serialize(scriptEnv)));
                var result = await command.Task.ConfigureAwait(false);

                if (!result.Success)
                {
                    _logger.LogError("{Username}: Command failed with with exit code {ExitCode}: {StandardError}", user.Username, result.ExitCode, result.StandardError);
                }

                _logger.LogInformation("{Username}: Command output: {StandardOutput}", user.Username, result.StandardOutput);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "{Username}: Error running \"{CmdPlaybackStart}\"", user.Username, userConfig.CmdPlaybackStart);
            }
        }
    }

    private async void PlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        foreach (var user in e.Users)
        {
            var userConfig = GetUserConfig(user.Id);
            if (userConfig == null)
            {
                _logger.LogDebug("{Username}: No configuration", user.Username);
                continue;
            }

            if (string.IsNullOrEmpty(userConfig.CmdPlaybackStopped))
            {
                _logger.LogDebug("{Username}: No configured PlaybackStopped command", user.Username);
                continue;
            }

            _logger.LogInformation("{Username}: Running command \"{CmdPlaybackStopped}\"", user.Username, userConfig.CmdPlaybackStopped);

            var scriptEnv = GetScriptEnvStop(e);

            try
            {
                var command = Command.Run(
                    userConfig.CmdPlaybackStopped.Split(" ")[0],
                    userConfig.CmdPlaybackStopped.Split(" ")[1..],
                    options => options
                        .Timeout(TimeSpan.FromMinutes(10))
                        .EnvironmentVariable("EVENT_ARGS", JsonSerializer.Serialize(scriptEnv)));
                var result = await command.Task.ConfigureAwait(false);

                if (!result.Success)
                {
                    _logger.LogError("{Username}: Command failed with with exit code {ExitCode}: {StandardError}", user.Username, result.ExitCode, result.StandardError);
                }

                _logger.LogInformation("{Username}: Command output: {StandardOutput}", user.Username, result.StandardOutput);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "{Username}: Error running \"{CmdPlaybackStopped}\"", user.Username, userConfig.CmdPlaybackStopped);
            }
        }
    }

    private RunScriptsUser? GetUserConfig(Guid userGuid)
    {
        if (Plugin.Instance == null || Plugin.Instance.Configuration.RunScriptsUsers == null)
        {
            return null;
        }

        return Plugin.Instance.Configuration.RunScriptsUsers.FirstOrDefault(u => u.UserId.Equals(userGuid));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Removes event subscriptions on dispose.
    /// </summary>
    /// <param name="disposing"><see cref="bool"/> indicating if object is currently disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionManager.PlaybackStart -= PlaybackStart;
            _sessionManager.PlaybackStopped -= PlaybackStopped;
        }
    }
}
