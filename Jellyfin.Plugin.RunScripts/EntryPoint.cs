using System;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using System.Threading.Tasks;
using Medallion.Shell;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.RunScripts.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.RunScripts
{
    public class RunScripts : IServerEntryPoint
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<RunScripts> _logger;
        private readonly IJsonSerializer _jsonSerializer;

        public RunScripts(
            ISessionManager sessionManager,
            ILoggerFactory loggerFactory,
            IJsonSerializer jsonSerializer
        )
        {
            _logger = loggerFactory.CreateLogger<RunScripts>();
            _sessionManager = sessionManager;
            _jsonSerializer = jsonSerializer;
        }

        /// <inheritdoc />
        public Task RunAsync()
        {
            //Bind events
            _sessionManager.PlaybackStart += PlaybackStart;
            _sessionManager.PlaybackStopped += PlaybackStopped;
            return Task.CompletedTask;
        }

        private async void PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            foreach (var user in e.Users)
            {
                var userConfig = getUserConfig(user.Id);
                if (userConfig == null)
                {
                    _logger.LogDebug($"{user.Username}: No configuration");
                    continue;
                }

                if (String.IsNullOrEmpty(userConfig.CmdPlaybackStart))
                {
                    _logger.LogDebug($"{user.Username}: No configured PlaybackStart command");
                    continue;
                }

                _logger.LogInformation($"{user.Username}: Running command \"{userConfig.CmdPlaybackStart}\"");

                try
                {
                    var command = Command.Run(
                        userConfig.CmdPlaybackStart.Split(" ")[0],
                        userConfig.CmdPlaybackStart.Split(" ")[1..],
                        options => options
                            .Timeout(TimeSpan.FromMinutes(10))
                            .EnvironmentVariable("EVENT_ARGS", stripPasswords(_jsonSerializer.SerializeToString(e)))
                    );
                    var result = await command.Task;

                    if (!result.Success)
                    {
                        _logger.LogError($"{user.Username}: Command failed with with exit code {result.ExitCode}: {result.StandardError}");
                    }

                    _logger.LogInformation($"{user.Username}: Command output: {result.StandardOutput}");
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, $"{user.Username}: Error running \"{userConfig.CmdPlaybackStart}\"");
                }
            }
        }

        private async void PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            foreach (var user in e.Users)
            {
                var userConfig = getUserConfig(user.Id);
                if (userConfig == null)
                {
                    _logger.LogDebug($"{user.Username}: No configuration");
                    continue;
                }

                if (String.IsNullOrEmpty(userConfig.CmdPlaybackStopped))
                {
                    _logger.LogDebug($"{user.Username}: No configured PlaybackStopped command");
                    continue;
                }

                _logger.LogInformation($"{user.Username}: Running command \"{userConfig.CmdPlaybackStopped}\"");

                try
                {
                    var command = Command.Run(
                        userConfig.CmdPlaybackStopped.Split(" ")[0],
                        userConfig.CmdPlaybackStopped.Split(" ")[1..],
                        options => options
                            .Timeout(TimeSpan.FromMinutes(10))
                            .EnvironmentVariable("EVENT_ARGS", stripPasswords(_jsonSerializer.SerializeToString(e)))
                    );
                    var result = await command.Task;

                    if (!result.Success)
                    {
                        _logger.LogError($"{user.Username}: Command failed with with exit code {result.ExitCode}: {result.StandardError}");
                    }

                    _logger.LogInformation($"{user.Username}: Command output: {result.StandardOutput}");
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, $"{user.Username}: Error running \"{userConfig.CmdPlaybackStopped}\"");
                }
            }
        }

        private RunScriptsUser getUserConfig(Guid userGuid)
        {
            if (Plugin.Instance.Configuration.RunScriptsUsers == null)
            {
                return null;
            }

            return Plugin.Instance.Configuration.RunScriptsUsers.FirstOrDefault(
                u => u.UserId.Equals(userGuid)
            );
        }

        private string stripPasswords(string eventArgs)
        {
            return Regex.Replace(eventArgs, "\"Password\":\".+?\",", "");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _sessionManager.PlaybackStart -= PlaybackStart;
            _sessionManager.PlaybackStopped -= PlaybackStopped;
        }
    }
}
