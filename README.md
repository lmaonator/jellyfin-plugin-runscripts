# RunScripts

Run Scripts is a Jellyfin Plugin that let's you run custom scripts
after events like playback start and stop.

Currently only `PlaybackStart` and `PlaybackStopped` are implemented.


## Usage
Go to the plugin configuration page and set commands for users.

Commands have to be set using absolute paths, eg.
`/home/user/scripts/jellyfin.py`.

Commands are simply split by spaces and everything after the
first space is passed as arguments. Escaping spaces or using quotes
will not work, you should handle everything in the script itself
using the environment variable `EVENT_ARGS`.

Currently the plugin simply passes the JSON serialized event arguments
(eg `PlaybackStopEventArgs`) as an environment variable `EVENT_ARGS`.

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
a subdirectory `Jellyfin_1.0.0.0` in the Jellyfin `plugins` directory
