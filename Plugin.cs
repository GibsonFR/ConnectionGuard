global using BepInEx;
global using BepInEx.IL2CPP;
global using HarmonyLib;
global using System.Collections.Generic;
global using SteamworksNative;
using System;
using UnityEngine;
using System.Numerics;
using TMPro;
using UnityEngine.UI;
using UnhollowerRuntimeLib;

namespace ConnectionGuard
{
    [BepInPlugin("A5269350-6D4B-4954-BF65-7D58981B5CFB", "ConnectionGuard", "1.0.0")]
    public class Plugin : BasePlugin
    {
        public static ulong ClientId = 0UL;
        public static Dictionary<ulong, DateTime> ConnectingPlayers = []; // Correct initialization
        public static List<ulong> playersList = [];
        public static Dictionary<ulong, GameObject> loadingPlayersList = [];
        public static bool IsConnectionGuardOn = false;
        public static float elapsedLoadingPlayerUpdate = 0f;

        public override void Load()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Log.LogInfo("ConnectionGuard loaded. Created by Gibson, discord: gib_son");
        }

        // Set ClientId
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Awake))]
        [HarmonyPostfix]
        public static void OnSteamManagerAwake(SteamManager __instance)
        {
            if (ClientId == 0UL) ClientId = (ulong)__instance.field_Private_CSteamID_0;

        }

        // Toggle ConnectionGuard with Key
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Update))]
        [HarmonyPostfix]
        public static void OnSteamManagerUpdate(SteamManager __instance)
        {
            if (Input.GetKeyDown(KeyCode.Alpha7) && SteamManager.Instance.IsLobbyOwner())
            {
                IsConnectionGuardOn = !IsConnectionGuardOn;
                ChatBox.Instance.ForceMessage(IsConnectionGuardOn ? "Connection Guard ON" : "Connection Guard OFF");
            }

            if (__instance.currentLobby == (CSteamID)0) IsConnectionGuardOn = false;

            if (IsConnectionGuardOn) HandleTimedOutPlayers();

            elapsedLoadingPlayerUpdate += Time.deltaTime;

            if (elapsedLoadingPlayerUpdate > 1f && IsConnectionGuardOn)
            {
                elapsedLoadingPlayerUpdate = 0f;
                HandleLoadingPlayers();
            }


        }

        // Manage player connection state when requesting all players
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.GameRequestAllPlayers))]
        [HarmonyPrefix]
        public static void OnGameRequestAllPlayers(ulong __0)
        {
            if (__0 == ClientId) return;

            RemovePlayerConnecting(__0);
            RemovePlayerFromLoadingPlayersList(__0);
            AddPlayerToPlayersList(__0);
        }



        // Manage player join update
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.AddPlayerToLobby))]
        [HarmonyPostfix]
        public static void LobbyManagerAddPlayerToLobby(CSteamID __0)
        {
            ulong playerId = (ulong)__0;

            AddPlayerConnecting(playerId);
        }

        // Manage player leave update
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.RemovePlayerFromLobby))]
        [HarmonyPostfix]
        public static void LobbyManagerRemovePlayerFromLobby(CSteamID __0)
        {
            ulong playerId = (ulong)__0;

            RemovePlayerConnecting(playerId);
            RemovePlayerFromPlayersList(playerId);
            RemovePlayerFromLoadingPlayersList(playerId);
        }

        private static void HandleTimedOutPlayers()
        {
            var playersToKick = new List<ulong>();

            foreach (var player in ConnectingPlayers)
            {
                if ((DateTime.Now - player.Value).TotalSeconds > 30f)
                {
                    playersToKick.Add(player.Key);
                }
            }

            foreach (var playerId in playersToKick)
            {
                KickPlayer(playerId);
            }
        }

        private static void HandleLoadingPlayers()
        {
            List<CSteamID> allLobbyMembers = [];
            List<CSteamID> loadingPlayers = [];
            int memberIndex = 0;

            for (int i = 0; i < SteamMatchmaking.GetNumLobbyMembers(SteamManager.Instance.currentLobby); i++)
            {
                CSteamID currentLobbyMember = SteamMatchmaking.GetLobbyMemberByIndex(SteamManager.Instance.currentLobby, memberIndex);
                if (!allLobbyMembers.Contains(currentLobbyMember)) allLobbyMembers.Add(currentLobbyMember);

                memberIndex++;
            }

            foreach (var player in allLobbyMembers)
            {
                if (player == (CSteamID)0 || player == (CSteamID)ClientId || playersList.Contains((ulong)player)) continue;

                loadingPlayers.Add(player);
            }

            foreach (var player in loadingPlayers)
            {
                if (loadingPlayersList.ContainsKey((ulong)player)) continue;

                GameObject originalPlayerListing = GameObject.Find("GameUI/PlayerList/WindowUI/Tab0/Container/Content/PlayerListing(Clone)");
                if (originalPlayerListing != null)
                {
                    GameObject newPlayerListing = UnityEngine.Object.Instantiate(originalPlayerListing);

                    loadingPlayersList.Add((ulong)player, newPlayerListing);

                    newPlayerListing.SetActive(true);
                    newPlayerListing.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = $"#? {SteamFriends.GetFriendPersonaName(player)}";
                    newPlayerListing.transform.Find("Icon").gameObject.GetComponent<RawImage>().color = Color.black;
                    newPlayerListing.GetComponent<RawImage>().color = Color.red;
                    newPlayerListing.GetComponent<MonoBehaviourPublicRabaicRaTeusscTepiObUnique>().field_Private_UInt64_0 = (ulong)player;

                    newPlayerListing.transform.parent = originalPlayerListing.transform.parent;
                }

            }

            List<ulong> playersToRemove = [];
            foreach (var playerId in loadingPlayersList.Keys)
            {
                if (!allLobbyMembers.Contains((CSteamID)playerId)) playersToRemove.Add(playerId);  
            }

            foreach (var playerId in playersToRemove)
            {
                RemovePlayerFromLoadingPlayersList(playerId);
            }


        }


        // Add player to connecting list
        public static void AddPlayerConnecting(ulong playerId)
        {
            if (!ConnectingPlayers.ContainsKey(playerId)) ConnectingPlayers.Add(playerId, DateTime.Now);

        }


        // Remove player from connecting list
        public static void RemovePlayerConnecting(ulong playerId)
        {
            if (ConnectingPlayers.ContainsKey(playerId))
            {
                ConnectingPlayers.Remove(playerId);
            }
        }

        // Add player to playerList
        public static void AddPlayerToPlayersList(ulong playerId)
        {
            if (!playersList.Contains(playerId)) playersList.Add(playerId);

        }

        // Remove player to playerList
        public static void RemovePlayerFromPlayersList(ulong playerId)
        {
            if (playersList.Contains(playerId)) playersList.Remove(playerId);

        }

        public static void RemovePlayerFromLoadingPlayersList(ulong playerId)
        {
            if (loadingPlayersList.ContainsKey(playerId))
            {
                GameObject.Destroy(loadingPlayersList[playerId]);
                loadingPlayersList.Remove(playerId);
            }
        }

        // Kick player from server
        public static void KickPlayer(ulong playerId)
        {
            if (ConnectingPlayers.ContainsKey(playerId))
            {
                ConnectingPlayers.Remove(playerId);
            }

            if (playerId == ClientId) return;

            LobbyManager.Instance.KickPlayer(playerId);
        }

        // Disable certain anti-cheat methods
        [HarmonyPatch(typeof(EffectManager), "Method_Private_Void_GameObject_Boolean_Vector3_Quaternion_0")]
        [HarmonyPatch(typeof(LobbyManager), "Method_Private_Void_0")]
        [HarmonyPatch(typeof(MonoBehaviourPublicVesnUnique), "Method_Private_Void_0")]
        [HarmonyPatch(typeof(LobbySettings), "Method_Public_Void_PDM_2")]
        [HarmonyPatch(typeof(MonoBehaviourPublicTeplUnique), "Method_Private_Void_PDM_32")]
        [HarmonyPrefix]
        public static bool DisableAntiCheatMethods(System.Reflection.MethodBase __originalMethod)
        {
            return false;
        }
    }
}