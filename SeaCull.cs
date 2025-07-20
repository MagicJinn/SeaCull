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
using Sunless.Game.Entities;
using System.Drawing.Text;
using JetBrains.Annotations;

namespace SeaCull;

public class SeaCuller : MonoBehaviour
{
    private static SeaCuller _instance;
    public static SeaCuller Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(SeaCuller));
                _instance = go.AddComponent<SeaCuller>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private static Coroutine cullCoroutine;

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
            SceneManager.sceneLoaded += Instance.OnSceneLoaded;
        }

        // Harmony.CreateAndPatchAll(typeof(FindAndJumpToPortPatch));
        Harmony.CreateAndPatchAll(typeof(OceanAnimationPatch));
        // Harmony.CreateAndPatchAll(typeof(TargetFrameRatePatch));
        // Harmony.CreateAndPatchAll(typeof(VSyncPatch));
    }


    private void OnDestroy()
    {
        if (cullCoroutine != null)
            StopCoroutine(cullCoroutine);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Stop any existing coroutine when scene changes
        if (cullCoroutine != null)
        {
            StopCoroutine(cullCoroutine);
            cullCoroutine = null;
        }

        // Clear references to objects from previous scene
        _playerBoat = _seaRoot = null;
        _seaTiles = null;

        // Wait until we're at Zee
        if (scene.name == "Sailing")
        {
#if !DEBUG
            // These don't work as patches, so we do them here:
            Application.targetFrameRate = -1; // -1 means "don't care"
            QualitySettings.vSyncCount = 1; // Since we disable the 60 FPS limit, it would be irresponsible to leave VSync off.
#endif

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
        cullCoroutine = StartCoroutine(HandleSeaTiles());
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
            Vector2 playerBoatPosition = _playerBoat.transform.position;

            // Use a for loop with index to handle potential null tiles
            for (int i = 0; i < _seaTiles.Count; i++)
            {
                GameObject tile = _seaTiles[i];

                Vector2 tilePos = tile.transform.position;
                // Adjust for the origin being in the top left
                tilePos.x += TILE_SIZE / 2;
                tilePos.y += TILE_SIZE / 2;

                float xDiff = playerBoatPosition.x - tilePos.x;
                float yDiff = playerBoatPosition.y - tilePos.y;
                // Calculate the squared distance
                float distSqr = xDiff * xDiff + yDiff * yDiff;

                bool shouldBeActive = GameProvider.Instance.CurrentUIState.InZubTransit ||
                 distSqr <= _cullDistance * _cullDistance; // Cull if too far away

                if (tile.activeSelf != shouldBeActive)
                {
                    tile.SetActive(shouldBeActive);
                }
                yield return null; // Wait for the next frame after each tile
            }
        }
    }

    // private static void ForceLoadBriefly()
    // {
    //     foreach (GameObject tile in _seaTiles)
    //     {
    //         tile?.SetActive(true);
    //     }
    // }

    // private static class EventOnEnterPatch
    // {
    //     [HarmonyPatch(typeof(EventOnEnter), "OnTriggerEnter2D")]
    //     private static bool Prefix(EventOnEnter __instance)
    //     {
    //         if (__instance.SwitchArea) // Only do this if the player is entering a trigger that could move him
    //         {
    //             ForceLoadBriefly();
    //         }
    //         return true; // Run the original method
    //     }
    // }

    // // Moving to a tile that's unloaded will cause issues. We enable all tiles whenever the player enters a trigger that could move him.
    // private static class FindAndJumpToPortPatch
    // {
    //     [HarmonyPatch(typeof(NavigationProvider), "FindAndJumpToPort")]
    //     private static bool Prefix()
    //     {
    //         Debug.Log("did the thing");
    //         ForceLoadBriefly();
    //         return true; // Run the original method
    //     }
    // }

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
}