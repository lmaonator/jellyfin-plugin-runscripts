using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        _jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };
    }

    /// <inheritdoc />
    public Task RunAsync()
    {
        // Bind events
        _sessionManager.PlaybackStart += PlaybackStart;
        _sessionManager.PlaybackStopped += PlaybackStopped;
        return Task.CompletedTask;
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

            try
            {
                var command = Command.Run(
                    userConfig.CmdPlaybackStart.Split(" ")[0],
                    userConfig.CmdPlaybackStart.Split(" ")[1..],
                    options => options
                        .Timeout(TimeSpan.FromMinutes(10))
                        .EnvironmentVariable("EVENT_ARGS", StripPasswords(JsonSerializer.Serialize(e, _jsonOptions))));
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

            try
            {
                var command = Command.Run(
                    userConfig.CmdPlaybackStopped.Split(" ")[0],
                    userConfig.CmdPlaybackStopped.Split(" ")[1..],
                    options => options
                        .Timeout(TimeSpan.FromMinutes(10))
                        .EnvironmentVariable("EVENT_ARGS", StripPasswords(JsonSerializer.Serialize(e, _jsonOptions))));
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

    private string StripPasswords(string eventArgs)
    {
        return Regex.Replace(eventArgs, "\"Password\":\".+?\",", string.Empty);
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
