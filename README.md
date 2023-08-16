# RunScripts

Run Scripts is a Jellyfin Plugin that let's you run custom scripts
after events like playback start and stop.

Currently only `PlaybackStart` and `PlaybackStopped` are implemented.

## Plugin Repository

Add the repository to Jellyfin under Dashboard -> Plugins -> Repositories.

- Name: RunScripts Stable
- URL: <https://lmaonator.github.io/jellyfin-plugin-runscripts/manifest.json>

## Usage

Go to the plugin configuration page and set commands for users.

Commands support quoted strings which can contain spaces and backslash escape for quotes.  
Note: Only double-quotes ( `"` ) work, don't use single-quotes.

For example:  
`"/path with/spaces/script.py" arg1 "arg 2" "arg3 with \" quote"`  
is parsed into:

- `/path with/spaces/script.py`
- `arg1`
- `arg 2`
- `arg3 with " quote`

The plugin passes only a select subset of attributes from the event arguments
(eg. `PlaybackStopEventArgs`) to the script as a JSON serialized environment
variable `EVENT_ARGS`.  
This is necessary because lage play queues fail with `System.ComponentModel.Win32Exception: Argument list too long` and the script would not be executed.

The attributes are:

- UserId
- UserName
- SessionId
- DeviceId
- DeviceName
- ClientName
- MediaSource
- MediaInfo
- PlaybackPositionTicks
- PlaybackPercentage (calculated by the Plugin for convenience)

Note: If you play a file with multiple versions then data in `MediaInfo` (like `Path`)
will often be wrong and from a different version than the currently playing one.
Use the data from `MediaSource` instead when possible.
The Plugin retrieves `MediaSource` by searching for `e.MediaSourceId` in `e.Session.NowPlayingQueueFullItems[].MediaSources[].id`.

If you would like other fields from `PlaybackEventArgs` please file an issue.

You can see the available fields by using a python script like this
and checking the Jellyfin log:

```python
#!/usr/bin/env python3
import os
import json

data = json.loads(os.environ["EVENT_ARGS"])
print(json.dumps(data, indent=2))
```

### Use with Docker

The Jellyfin Docker container doesn't come with python or most other
scripting languages.

You will have to compile your scripts into standalone executables or
mount a portable interpreter into docker and run it with that.

An easy way for python scripts is to make your script standalone with
PyInstaller.

## Build Process

1. Clone or download this repository

2. Ensure you have .NET Core SDK setup and installed

3. Build the plugin with the following command:

    ```sh
    dotnet publish --configuration Release --output bin
    ```

4. Place `Jellyfin.Plugin.RunScripts.dll` and `MedallionShell.dll` into
    a subdirectory `RunScripts_2.0.0.0` in the Jellyfin `plugins` directory
