using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;
using LiteNetLib;

namespace RshLib
{
    internal class MpValidator
    {
        public static void SetupMpValidator()
        {
            NetPlayer.OnPlayerJoined += delegate(NetPlayer plr)
            {
                if (plr.is_local)
                    return;
                if (Net.is_client)
                {
                    NetDataWriter writer = KrokoshaCasualtiesMP.Net.CreateWriter(31800);
                    writer.Put((bool)true);
                    writer.Put((ushort)3);
                    writer.Put((ushort)1);
                    KrokoshaCasualtiesMP.Net.Client_Send(LiteNetLib.DeliveryMethod.ReliableUnordered, in writer);
                }
           else if (0 < Plugin.itemRegistry.Count)
                {
                    plr.CUSTOM_LOCAL_DATA["RshLib_Alert_Countdown"] = (int)10;
                }
            };
        }

        public static void HReciveVersionInformation(knetid clientId, ref NetDataReader reader)
        {
            NetPlayer.GetNetPlayerFromClientId(clientId).CUSTOM_LOCAL_DATA.Remove("RshLib_Alert_Countdown");
        }
    }

    internal class AlertTracker
    {
        static void Postfix(NetPlayer __instance)
        {
            if (Net.is_client || !__instance.CUSTOM_LOCAL_DATA.TryGetValue("RshLib_Alert_Countdown", out object value))
                return;
            int newValue = (int)value - 1;
            if (0 >= newValue)
            {
                __instance.CUSTOM_LOCAL_DATA.Remove("RshLib_Alert_Countdown");
                __instance.Server_DoAlertSingle("You are missing or using an older version of RshLib");
            }
       else     __instance.CUSTOM_LOCAL_DATA["RshLib_Alert_Countdown"] = newValue;
        }
    }

    internal class NetworkMsgReigister
    {
        static void Postfix()
        {
            Net.RegisterServerReceiver(31800, MpValidator.HReciveVersionInformation);
        }
    }
}
