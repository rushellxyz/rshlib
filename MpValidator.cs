using HarmonyLib;
using Together;
using LiteNetLib.Utils;
using LiteNetLib;

namespace RshLib
{
    internal class MpValidator
    {
        public const ushort CLIENT_SEND_ITEM_REGISTRY = 0;

        public static void Awake(Harmony harmony)
        {
            Plugin.PatchPostfix(harmony, "Together.ScavPlayer", "SlowUpdate", "RshLib.AlertTracker");
            Plugin.PatchPrefix(harmony, "Together.KrokoshaTraderTrackerComponent", "Server_SendTraderInventory", "RshLib.FixTradersForClientsWithoutRshlib");

            Network.serverRecivers.Add(CLIENT_SEND_ITEM_REGISTRY, ReciveItemRegistryInformation);

            ScavPlayer.OnPlayerJoined += delegate(ScavPlayer plr)
            {
                if (plr.IsLocal && Net.IsClient)
                {
                    NetDataWriter writer = Network.CreateWriter(CLIENT_SEND_ITEM_REGISTRY);
                    writer.Put((ushort)3);
                    writer.Put((ushort)2);
                    writer.Put((ushort)Plugin.itemRegistry.Count);
                    Net.Client_Send(LiteNetLib.DeliveryMethod.ReliableUnordered, in writer);
                }
           else if (Net.IsHost && Plugin.anyItemIsRegistred)
                {
                    if (plr.IsLocal)
                        plr.CUSTOM_LOCAL_DATA["RshLib_present"] = (bool)true;
               else     plr.CUSTOM_LOCAL_DATA["RshLib_AlertCountdown"] = (byte)11;
                }
            };
        }

        public static void ReciveItemRegistryInformation(knetid clientId, ref NetDataReader reader)
        {
            reader.Get(out ushort major);
            reader.Get(out ushort middle);
            if (3 != major || 2 != middle)
                UnityEngine.Debug.LogWarning($"[RshLib] {clientId} has a different version {major}.{middle}");
            ScavPlayer player = ScavPlayer.GetNetPlayerFromClientId(clientId);
            player.CUSTOM_LOCAL_DATA.Remove("RshLib_AlertCountdown");
            player.CUSTOM_LOCAL_DATA["RshLib_present"] = (bool)true;
        }
    }

    internal class AlertTracker
    {
        static void Postfix(ScavPlayer __instance)
        {
            if (Net.IsClient || !__instance.CUSTOM_LOCAL_DATA.TryGetValue("RshLib_AlertCountdown", out object v))
                return;
            byte newValue = (byte)((byte)v - (byte)1);
            if (0 >= newValue)
            {
                __instance.CUSTOM_LOCAL_DATA.Remove("RshLib_AlertCountdown");
                __instance.CUSTOM_LOCAL_DATA["RshLib_present"] = (bool)false;
                Con.Server_SendConsoleLog("You are missing RshLib\nYou wont be able to see custom items", __instance);
                __instance.Server_DoAlertSingle("You are missing RshLib\nYou wont be able to see custom items");
            }
       else     __instance.CUSTOM_LOCAL_DATA["RshLib_AlertCountdown"] = newValue;
        }
    }
}
