// Actual culling logic goes here, since we need to create a gameObject to interact with Unity's methods and such. Better to keep it separate.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using Sunless.Game.Phenomena.StoryletEffects;
using Sunless.Game.ApplicationProviders;
using Sunless.Game.Scripts.Animation;
using Sunless.Game.UI.Menus.Options;

namespace SeaCull;

public class SeaCuller : MonoBehaviour
{
    private static bool _cullTiles;
    private static float _cullDistance;
    private const float TILE_SIZE = 1500f;

    private static GameObject _playerBoat;
    private static GameObject _seaRoot;
    private static List<GameObject> _seaTiles;

    public static void Initialize()
    {
        _cullTiles = (bool)Plugin.ConfigOptions["CullTiles"];
        _cullDistance = (float)Plugin.ConfigOptions["CullDistance"];

        if (_cullTiles)
        {
            // Create a new GameObject to hold the SeaCuller.
            GameObject _seaCuller = new GameObject("SeaCull");
            _seaCuller.AddComponent<SeaCuller>();
            DontDestroyOnLoad(_seaCuller);
        }

        Harmony.CreateAndPatchAll(typeof(EventOnEnterPatch));
        Harmony.CreateAndPatchAll(typeof(OceanAnimationPatch));
        // Harmony.CreateAndPatchAll(typeof(TargetFrameRatePatch));
        // Harmony.CreateAndPatchAll(typeof(VSyncPatch));
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Wait until we're at Zee
        if (scene.name == "Sailing")
        {
            // These don't work as patches, so we do them here:
            Application.targetFrameRate = -1; // -1 means "don't care"
            QualitySettings.vSyncCount = 1; // Since we disable the 60 FPS limit, it would be irresponsible to leave VSync off.

            StartCoroutine(WaitForGameObjects());
        }
    }

    private IEnumerator WaitForGameObjects()
    {
        // Wait until the necessary GameObjects are available
        while (true)
        {
            // Since we assign first and check later, this should be resilient to multiple loads, on death for example
            _seaRoot = GameObject.Find("Sea");
            _playerBoat = GameObject.Find("PlayerBoat");

            if (_seaRoot != null && _playerBoat != null) break;

            yield return new WaitForSeconds(0.1f);
        }
        FindSeaTiles();
        StartCoroutine(HandleSeaTiles());
    }

    private void FindSeaTiles()
    {
        Transform parent = _seaRoot.transform;
        _seaTiles = new List<GameObject>(); // Make a new list to avoid having dead tiles in the list on death and such

        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject tile = parent.GetChild(i).gameObject;

            if (tile.activeInHierarchy)
            {
                _seaTiles.Add(tile);
            }
        }
    }

    private IEnumerator HandleSeaTiles()
    {
        while (true)
        {
            foreach (GameObject tile in _seaTiles)
            {
                Vector2 tilePos = tile.transform.position;
                // Adjust for the origin being in the top left
                tilePos.x += TILE_SIZE / 2;
                tilePos.y += TILE_SIZE / 2;

                // Calculate the squared distance
                float xDiff = _playerBoat.transform.position.x - tilePos.x;
                float yDiff = _playerBoat.transform.position.y - tilePos.y;
                float distSqr = xDiff * xDiff + yDiff * yDiff;

                bool shouldBeActive =
                    GameProvider.Instance.CurrentUIState.IsPaused || // If the game is paused, forceload tiles to avoid problems
                    distSqr <= _cullDistance * _cullDistance; // Cull if too far away

                if (tile.activeSelf != shouldBeActive)
                {
                    tile.SetActive(shouldBeActive);
                }
                yield return null; // Wait for the next frame
            }
        }
    }

    // Moving to a tile that's unloaded will cause issues. We enable all tiles whenever the player enters a trigger that could move him.
    private static class EventOnEnterPatch
    {
        [HarmonyPatch(typeof(EventOnEnter), "OnTriggerEnter2D")]
        private static bool Prefix()
        {
            foreach (GameObject tile in _seaTiles)
            {
                if (!tile.activeSelf)
                {
                    tile.SetActive(true);
                }
            }
            return true; // Run the original method
        }
    }


    // The ocean animation is tied to FPS. This is a problem, but especially so because this mod increases FPS and disables VSync.
    // We set the FPS to 60, as this was a number programmed in by the devs, but was accidentally disabled, sort of.
    private static class OceanAnimationPatch
    {
        private static float _timeSinceLastOceanFrame = 0f;
        private static int TARGET_FPS = (int)Plugin.ConfigOptions["ZeeAnimationTargetFPS"];
        private static float _targetDelta = 1f / TARGET_FPS;

        [HarmonyPatch(typeof(OceanAnimation), "Update")]
        private static bool Prefix()
        {
            _timeSinceLastOceanFrame += Time.deltaTime;
            if (_timeSinceLastOceanFrame > _targetDelta)
            {
                _timeSinceLastOceanFrame -= _targetDelta; // "reset" the timer
                return true; // Run the Update method
            }
            return false; // Don't run the Update method
        }
    }

    // private static class TargetFrameRatePatch
    // {
    //     [HarmonyPatch(typeof(GameProvider), "CheckAndSetCurrentResolution")]
    //     public static void Postfix()
    //     {
    //         Application.targetFrameRate = -1; // -1 means "don't care"
    //         Debug.Log("Setting target frame rate to -1" + (Application.targetFrameRate == -1 ? " (enabled)" : " (disabled)"));
    //     }
    // }

    // private static class VSyncPatch
    // {
    //     [HarmonyPatch(typeof(VideoOptionsPanel), "AcceptSettings")]
    //     public static void Postfix()
    //     {
    //         QualitySettings.vSyncCount = (bool)Plugin.ConfigOptions["EnableVSync"] ? 1 : 0;
    //         Debug.Log("Disabling VSync" + (QualitySettings.vSyncCount == 1 ? " (enabled)" : " (disabled)"));
    //     }
    // }
}