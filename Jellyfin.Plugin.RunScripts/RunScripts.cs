using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RunScripts.Configuration;
using Medallion.Shell;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RunScripts;

/// <summary>
/// RunScripts class.
/// </summary>
public class RunScripts : IHostedService, IDisposable
{
    private const int _lastRunSeconds = 10;

    private readonly ISessionManager _sessionManager;
    private readonly ILogger<RunScripts> _logger;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, (Guid Id, DateTime Dt)> _lastRun;

    private int _eventCount = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunScripts"/> class.
    /// </summary>
    /// <param name="sessionManager">The <see cref="ISessionManager"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
    /// <param name="mediaSourceManager">The IMediaSourceManager.</param>
    public RunScripts(ISessionManager sessionManager, ILoggerFactory loggerFactory, IMediaSourceManager mediaSourceManager)
    {
        _logger = loggerFactory.CreateLogger<RunScripts>();
        _sessionManager = sessionManager;
        _mediaSourceManager = mediaSourceManager;
        _jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        _lastRun = new Dictionary<string, (Guid Id, DateTime Dt)>();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Bind events
        _sessionManager.PlaybackStart += PlaybackStart;
        _sessionManager.PlaybackStopped += PlaybackStopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= PlaybackStart;
        _sessionManager.PlaybackStopped -= PlaybackStopped;
        return Task.CompletedTask;
    }

    private async Task<RunScriptsEnv> GetScriptEnvStart(PlaybackProgressEventArgs e)
    {
        double? playbackPercentage = null;
        if (e.PlaybackPositionTicks != null && e.MediaInfo.RunTimeTicks != null && e.PlaybackPositionTicks > 0 && e.MediaInfo.RunTimeTicks > 0)
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
            MediaInfo = e.MediaInfo,
            // MediaInfo is incorrect for multi-version files, it always constains the first version, grab the real MediaSource
            MediaSource = await _mediaSourceManager.GetMediaSource(e.Item, e.MediaSourceId, string.Empty, false, default).ConfigureAwait(false),
            PlaybackPositionTicks = e.PlaybackPositionTicks,
            PlaybackPercentage = playbackPercentage,
        };
        return scriptEnv;
    }

    private async Task<RunScriptsEnv> GetScriptEnvStop(PlaybackStopEventArgs e)
    {
        var scriptEnv = await GetScriptEnvStart(e).ConfigureAwait(false);
        scriptEnv.PlayedToCompletion = e.PlayedToCompletion;
        return scriptEnv;
    }

    private bool AlreadyRan(string eventName, SessionInfo session, MediaBrowser.Model.Dto.BaseItemDto mediaInfo)
    {
        if (_lastRun.TryGetValue(eventName + session.Id, out var v) && v.Id == mediaInfo.Id && v.Dt.AddSeconds(_lastRunSeconds) > DateTime.UtcNow)
        {
            _logger.LogDebug("Already ran {EventName} command for item {Name} for user {UserName}", eventName, mediaInfo.Name, session.UserName);
            return true;
        }

        return false;
    }

    private void AddLastRunEntry(string eventName, SessionInfo session, MediaBrowser.Model.Dto.BaseItemDto mediaInfo)
    {
        _lastRun[eventName + session.Id] = (Id: mediaInfo.Id, Dt: DateTime.UtcNow);
    }

    private void RemoveOldLastRunEntries()
    {
        foreach (var item in _lastRun.Where(item => item.Value.Dt.AddSeconds(_lastRunSeconds) <= DateTime.UtcNow).ToList())
        {
            _lastRun.Remove(item.Key);
        }
    }

    private async void RunCommand(string username, string commandStr, RunScriptsEnv env, int eventNum)
    {
        var commandLine = ParseCommandLine(commandStr);
        _logger.LogInformation("[{EventNum}] Running command for {Username}: {CommandLine}", eventNum, username, commandLine);
        try
        {
            var command = Command.Run(
                commandLine[0],
                commandLine.Count > 1 ? commandLine.GetRange(1, commandLine.Count - 1) : null,
                options => options
                    .Timeout(TimeSpan.FromMinutes(10))
                    .EnvironmentVariable("EVENT_ARGS", JsonSerializer.Serialize(env)));
            var result = await command.Task.ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogError("[{EventNum}] Command failed with with exit code {ExitCode}: {StandardError}", eventNum, result.ExitCode, result.StandardError.Trim());
            }

            _logger.LogInformation("[{EventNum}] Command output: {StandardOutput}", eventNum, result.StandardOutput.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{EventNum}] Error running command", eventNum);
        }
    }

    private async void PlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        if (!AlreadyRan(nameof(PlaybackStart), e.Session, e.MediaInfo))
        {
            AddLastRunEntry(nameof(PlaybackStart), e.Session, e.MediaInfo);

            var eventNum = _eventCount++;

            var userConfig = GetUserConfig(e.Session.UserId);
            if (userConfig == null)
            {
                _logger.LogDebug("[{EventNum}] No configuration for {Username}", eventNum, e.Session.UserName);
                return;
            }

            if (string.IsNullOrEmpty(userConfig.CmdPlaybackStart))
            {
                _logger.LogDebug("[{EventNum}] No configured PlaybackStart command for {Username}", eventNum, e.Session.UserName);
                return;
            }

            var scriptEnv = await GetScriptEnvStart(e).ConfigureAwait(false);
            RunCommand(e.Session.UserName, userConfig.CmdPlaybackStart, scriptEnv, eventNum);
        }

        RemoveOldLastRunEntries();
    }

    private async void PlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        if (!AlreadyRan(nameof(PlaybackStopped), e.Session, e.MediaInfo))
        {
            AddLastRunEntry(nameof(PlaybackStopped), e.Session, e.MediaInfo);

            var eventNum = _eventCount++;

            var userConfig = GetUserConfig(e.Session.UserId);
            if (userConfig == null)
            {
                _logger.LogDebug("[{EventNum}] No configuration for {Username}", eventNum, e.Session.UserName);
                return;
            }

            if (string.IsNullOrEmpty(userConfig.CmdPlaybackStopped))
            {
                _logger.LogDebug("[{EventNum}] No configured PlaybackStopped command for {Username}", eventNum, e.Session.UserName);
                return;
            }

            var scriptEnv = await GetScriptEnvStop(e).ConfigureAwait(false);
            RunCommand(e.Session.UserName, userConfig.CmdPlaybackStopped, scriptEnv, eventNum);
        }

        RemoveOldLastRunEntries();
    }

    private RunScriptsUser? GetUserConfig(Guid userGuid)
    {
        if (Plugin.Instance == null || Plugin.Instance.Configuration.RunScriptsUsers == null)
        {
            return null;
        }

        return Plugin.Instance.Configuration.RunScriptsUsers.FirstOrDefault(u => u.UserId.Equals(userGuid));
    }

    private static List<string> ParseCommandLine(string input)
    {
        List<string> arguments = new List<string>();
        bool insideQuotes = false;
        bool escapeNextChar = false;
        string currentArgument = string.Empty;

        foreach (char c in input)
        {
            if (escapeNextChar)
            {
                currentArgument += c;
                escapeNextChar = false;
            }
            else if (c == '\\')
            {
                escapeNextChar = true;
            }
            else if (c == '\"')
            {
                if (insideQuotes && !escapeNextChar)
                {
                    insideQuotes = false;
                }
                else
                {
                    insideQuotes = true;
                }
            }
            else if (c == ' ' && !insideQuotes)
            {
                if (!string.IsNullOrEmpty(currentArgument))
                {
                    arguments.Add(currentArgument);
                    currentArgument = string.Empty;
                }
            }
            else
            {
                currentArgument += c;
            }
        }

        if (!string.IsNullOrEmpty(currentArgument))
        {
            arguments.Add(currentArgument);
        }

        return arguments;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    }

    /// <summary>
    /// Removes event subscriptions on dispose.
    /// </summary>
    /// <param name="disposing"><see cref="bool"/> indicating if object is currently disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
    }
}
