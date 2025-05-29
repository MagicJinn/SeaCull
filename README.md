# SeaCull

A performance optimization mod for Sunless Sea that significantly improves FPS through intelligent culling optimization, as well as fixing some bugs.

## Performance Improvements

- **40-70% FPS Increase with Smart Tile Culling**: SeaCull can dramatically improve your game's performance by automatically culling distant tiles to reduce computational overhead.
- **Zee Animation Fix**: Limits the Zee animation to 60 FPS (configurable), as it was originally tied to FPS, and the devs clearly intended it to be limited to 60 FPS (as there are hints in the code to that effect).

## Features

- Automatic tile culling
- Configurable culling distance (though you probably don't wanna touch this)
- Adjustable Zee animation target FPS
- Attempt to disable FPS limit for the lowest difficulty (not always successful)
- Zero impact on gameplay or visuals

## Configuration

Edit `SeaCull.ini` in your Sunless Sea **install** folder to customize:

- `Cull Tiles = true/false` - Enable/disable tile culling
- `Cull Distance = float` - Distance at which tiles are culled
- `Zee Animation Target FPS = integer` - Target FPS for Zee animations

## **Compiling the plugin**

To develop and build the plugin, there are a couple of prerequisites. Clone the repository:

```bash
git clone https://github.com/MagicJinn/SeaCull.git
cd SeaCull
```

After this, you need to acquire a DLL SeaCull relies on. Create a `dependencies` folder, and find `Sunless.Game.dll` in your `SunlessSea\Sunless Sea_Data\Managed` folder. Copy it into the `dependencies` folder. After this, you should be able to compile the project with the following command:

```bash
dotnet build -c Release -p:Optimize=true
```

The DLL should be located in `bin/Release/net35`.
