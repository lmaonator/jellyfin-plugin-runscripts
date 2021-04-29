using MediaBrowser.Model.Plugins;
using System;

namespace Jellyfin.Plugin.RunScripts.Configuration
{
    public class RunScriptsUser
    {
        public Guid UserId { get; set; }
        public string CmdPlaybackStart { get; set; }
        public string CmdPlaybackStopped { get; set; }
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public RunScriptsUser[] RunScriptsUsers { get; set; }

        public PluginConfiguration()
        {
            RunScriptsUsers = new RunScriptsUser[] { };
        }
    }
}
