using System;
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

        [HarmonyPatch]
        public class protube_Reload
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(AssaultRifleController), "Load")]
            [HarmonyPatch(typeof(BouncerController), "Load")]
            [HarmonyPatch(typeof(GrenadeLaucher), "Load")]
            [HarmonyPatch(typeof(LaserRifle), "Load")]
            [HarmonyPatch(typeof(RailgunController), "Load")]
            [HarmonyPatch(typeof(RocketLauncher), "Load")]
            [HarmonyPatch(typeof(SonicPulseGenerator), "Load")]
            [HarmonyPatch(typeof(Revolver), "Load")]
            [HarmonyPatch(typeof(DartGun), "Load")]
            [HarmonyPatch(typeof(NewDoubleShotgun), "Load")]
            [HarmonyPatch(typeof(ShotgunController), "Load")]
            [HarmonyPatch(typeof(StickyLauncher), "Load")]
            public static void Postfix(GunController __instance)
            {
                ForceTubeVRChannel myChannel = (Traverse.Create(__instance).Property("IsLeftHandedGun").GetValue<Boolean>())
                    ?
                    ForceTubeVRChannel.pistol2
                    :
                    ForceTubeVRChannel.pistol1;
                ForceTubeVRInterface.Rumble(126, 20f, myChannel);
            }
        }
        
        [HarmonyPatch]
        public class protube_ReloadMachinePistol
        {
            public static bool reloaded = false;
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MachinePistol), "PlayPressButtonAnim")]
            public static void Postfix(MachinePistol __instance, bool __result)
            {
                if (__result && !reloaded)
                {
                    ForceTubeVRChannel myChannel = (Traverse.Create(__instance).Property("IsLeftHandedGun").GetValue<Boolean>())
                        ?
                        ForceTubeVRChannel.pistol2
                        :
                        ForceTubeVRChannel.pistol1;
                    ForceTubeVRInterface.Rumble(126, 20f, myChannel);
                    reloaded = true;
                }
                if( !__result && reloaded)
                {
                    reloaded = false;
                }
            }
        }

        [HarmonyPatch(typeof(LaserRifle), "Update")]
        public class protube_laserRifle
        {
            [HarmonyPostfix]
            public static void Postfix(LaserRifle __instance)
            {
                ForceTubeVRChannel myChannel = (Traverse.Create(__instance).Property("IsLeftHandedGun").GetValue<Boolean>())
                    ?
                    ForceTubeVRChannel.pistol2
                    :
                    ForceTubeVRChannel.pistol1;

                if(Traverse.Create(__instance).Field("IsFiringThisFrame").GetValue<Boolean>())
                {
                    ForceTubeVRInterface.Rumble(255, 100f, myChannel);
                }
                else if (Traverse.Create(__instance).Field("Charge").GetValue<float>() > 0)
                {
                    ForceTubeVRInterface.Rumble(100, 100f, myChannel);
                }
            }
        }

        [HarmonyPatch(typeof(GunController), "Fire", new Type[] { })]
        public class protube_Fire
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
                switch (__instance.WeaponID)
                {
                    case SaveFile.WeaponUnlockFlags.Bouncer:
                        kickPower = 230;
                        break;
                    case SaveFile.WeaponUnlockFlags.RocketLauncher:
                        ForceTubeVRInterface.Shoot(255, 230, 200f, myChannel);
                        return;
                    case SaveFile.WeaponUnlockFlags.DoubleShotgun:
                        ForceTubeVRInterface.Shoot(255, 200, 100f, myChannel);
                        return;
                    case SaveFile.WeaponUnlockFlags.Revolver:
                        kickPower = 245;
                        break;
                    case SaveFile.WeaponUnlockFlags.DartGun:
                        kickPower = 200;
                        break;
                    case SaveFile.WeaponUnlockFlags.SonicPulseGenerator:
                        kickPower = 200;
                        break;
                    case SaveFile.WeaponUnlockFlags.StickyLauncher:
                        kickPower = 200;
                        break;
                    case SaveFile.WeaponUnlockFlags.LaserPistol:
                        kickPower = 200;
                        break;
                    case SaveFile.WeaponUnlockFlags.Railgun:
                        ForceTubeVRInterface.Shoot(220, 200, 100f, myChannel);
                        return;
                    case SaveFile.WeaponUnlockFlags.GrenadePistol:
                        kickPower = 220;
                        break;
                    case SaveFile.WeaponUnlockFlags.Minigun:
                        kickPower = 230;
                        break;
                    default:
                        kickPower = 210;
                        break;
                }
                ForceTubeVRInterface.Kick(kickPower, myChannel);

            }
        }
    }
}

