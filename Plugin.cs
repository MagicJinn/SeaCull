using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;

namespace SeaCull;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private const string CONFIG_FILENAME = "SeaCull.ini";
    public static Dictionary<string, object> ConfigOptions = new();

    private void Awake()
    {
        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        LoadConfig();
    }

    private void Start()
    {
        SeaCuller.Initialize();
    }

    private void LoadConfig(bool loadDefault = false)
    {
        string[] lines;
        if (File.Exists(CONFIG_FILENAME) && !loadDefault)
        {
            lines = File.ReadAllLines(CONFIG_FILENAME);
        }
        else
        {
            Logger.LogWarning("Config not found or corrupt, using default values.");
            string file = ReadTextResource(GetEmbeddedPath() + CONFIG_FILENAME); // Get the default config from the embedded resources

            lines = file.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries); // Split the file into lines
        }

        var optionsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var line in lines)
            {
                if (line.Contains('=')) // Check if the line contains an '=' character so it's a valid config line
                {
                    // Remove all spaces from the line and split it at the first occurrence of '=' into two parts
                    string[] keyValue = line.Replace(" ", "").Split(new[] { '=' }, 2);
                    optionsDict[keyValue[0]] = keyValue[1]; // Add the key and value to the dictionary
                }
            }

            ConfigOptions["CullTiles"] = bool.Parse(optionsDict["CullTiles"]);
            ConfigOptions["CullDistance"] = float.Parse(optionsDict["CullDistance"]);
            ConfigOptions["ZeeAnimationTargetFPS"] = int.Parse(optionsDict["ZeeAnimationTargetFPS"]);
        }
        catch (Exception)
        {
            LoadConfig( /*loadDefault =*/ true); // Load config with default values
        }
    }

    private static string GetEmbeddedPath(string folderName = "") // Get the path of embedded resources
    {
        string projectName = Assembly.GetExecutingAssembly().GetName().Name;
        string fullPath = $"{projectName}.{folderName}";
        return fullPath;
    }

    private string ReadTextResource(string fullResourceName)
    {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullResourceName))
        {
            if (stream == null)
            {
                Logger.LogWarning("Tried to get resource that doesn't exist: " + fullResourceName);
                return null; // Return null if the embedded resource doesn't exist
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd(); // Read and return the embedded resource
        }
    }
}
