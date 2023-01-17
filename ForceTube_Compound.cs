﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Valve.Newtonsoft.Json;

namespace ForceTube_Compound
{
    [BepInPlugin("org.bepinex.plugins.ProTube_Compound", "Compound protube integration", "1.0")]
    public class ForceTube_Compound : BaseUnityPlugin
    {
        public static bool rightGunHasAmmo = true;
        public static bool leftGunHasAmmo = true;
        public static bool reloadHip = true;
        public static bool reloadShoulder = false;
        public static bool reloadTrigger = false;
        public static bool justKilled = false;
        public static string configPath = Directory.GetCurrentDirectory() + "\\UserData\\";
        public static bool dualWield = false;
        internal static ManualLogSource Log;

        public void Awake()
        {
            Log = base.Logger;
            InitializeProTube();
            // patch all functions
            var harmony = new Harmony("protube.patch.compound");
            harmony.PatchAll();
        }

        public static void saveChannel(string channelName, string proTubeName)
        {
            string fileName = configPath + channelName + ".pro";
            File.WriteAllText(fileName, proTubeName, Encoding.UTF8);
        }

        public static string readChannel(string channelName)
        {
            string fileName = configPath + channelName + ".pro";
            if (!File.Exists(fileName)) return "";
            return File.ReadAllText(fileName, Encoding.UTF8);
        }

        public static void dualWieldSort()
        {
            ForceTubeVRInterface.FTChannelFile myChannels = JsonConvert.DeserializeObject<ForceTubeVRInterface.FTChannelFile>(ForceTubeVRInterface.ListChannels());
            var pistol1 = myChannels.channels.pistol1;
            var pistol2 = myChannels.channels.pistol2;
            if ((pistol1.Count > 0) && (pistol2.Count > 0))
            {
                dualWield = true;
                Log?.LogMessage("Two ProTube devices detected, player is dual wielding.");
                if ((readChannel("rightHand") == "") || (readChannel("leftHand") == ""))
                {
                    Log?.LogMessage("No configuration files found, saving current right and left hand pistols.");
                    saveChannel("rightHand", pistol1[0].name);
                    saveChannel("leftHand", pistol2[0].name);
                }
                else
                {
                    string rightHand = readChannel("rightHand");
                    string leftHand = readChannel("leftHand");
                    Log?.LogMessage("Found and loaded configuration. Right hand: " + rightHand + ", Left hand: " + leftHand);
                    // Channels 4 and 5 are ForceTubeVRChannel.pistol1 and pistol2
                    ForceTubeVRInterface.ClearChannel(4);
                    ForceTubeVRInterface.ClearChannel(5);
                    ForceTubeVRInterface.AddToChannel(4, rightHand);
                    ForceTubeVRInterface.AddToChannel(5, leftHand);
                }
            }
            else
            {
                Log.LogMessage("SINGLE WIELD");
            }
        }


        private async void InitializeProTube()
        {
            Log?.LogMessage("Initializing ProTube gear...");
            await ForceTubeVRInterface.InitAsync(true);
            Thread.Sleep(10000);
            dualWieldSort();
        }


        private static void setAmmo(bool hasAmmo, bool isRight)
        {
            if (isRight) { rightGunHasAmmo = hasAmmo; }
            else { leftGunHasAmmo = hasAmmo; }
        }


        private static bool checkIfRightHand(string controllerName)
        {
            if (controllerName.Contains("Right") | controllerName.Contains("right"))
            {
                return true;
            }
            else { return false; }
        }

        [HarmonyPatch(typeof(GunController), "Fire", new Type[] { })]
        public class bhaptics_Fire
        {
            [HarmonyPostfix]
            public static void Postfix(GunController __instance)
            {
                ForceTubeVRChannel myChannel = (Traverse.Create(__instance).Property("IsLeftHandedGun").GetValue<Boolean>())
                    ?
                    ForceTubeVRChannel.pistol2
                    :
                    ForceTubeVRChannel.pistol1;
                byte kickPower = 210;
                switch (__instance.name)
                {
                    /*
                    case 0:
                        // Pistol
                        kickPower = 210;
                        break;
                    case 1:
                        // Revolver
                        kickPower = 230;
                        break;
                    case 2:
                        // Burstfire
                        kickPower = 180;
                        break;
                    case 3:
                        // Boomstick (Shotgun)
                        ForceTubeVRInterface.Shoot(255, 200, 100f, myChannel);
                        return;
                    case 4:
                        // Knuckles
                        ForceTubeVRInterface.Rumble(255, 200f, myChannel);
                        return;
                    */
                    default:
                        kickPower = 210;
                        break;
                }
                ForceTubeVRInterface.Kick(kickPower, myChannel);
            }
        }
        /*
        [HarmonyPatch(typeof(SyringeController), "Inject")]
        public class bhaptics_SyringeController
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (Plugin.tactsuitVr.suitDisabled)
                {
                    return;
                }
                Plugin.tactsuitVr.PlaybackHaptics("SuperPower");
                Plugin.tactsuitVr.PlaybackHaptics("superpower_L");
                Plugin.tactsuitVr.PlaybackHaptics("superpower_R");
            }
        }
        */
    }
}

