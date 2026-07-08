using System.Collections.Generic;
using LiteNetLib.Utils;
using LiteNetLib;
using Together;

namespace RshLib
{
    internal class FixTradersForClientsWithoutRshlib
    {
        static bool Prefix(ref TrackerTrader __instance, List<knetid> clients_to_enlighten)
        {
            if (!Plugin.anyItemIsRegistred)
                return true;

            if (clients_to_enlighten.Count == 0)
            {
                return false;
            }
            TraderScript traderScript = __instance.og;
            if (!NetObjectRegistry.IsRegistered(__instance.gameObject))
            {
                NetObjectRegistry.NewGO(__instance.gameObject);
            }
            foreach (knetid client in clients_to_enlighten)
            {
                ScavPlayer player = ScavPlayer.GetNetPlayerFromClientId(client);
                bool clientHaveRshlib = !player.CUSTOM_LOCAL_DATA.TryGetValue("RshLib_present", out object v) || (bool)v;

                NetDataWriter writer = Net.CreateWriter((byte)NetmsgId.CLIENT_TraderSync_Inventory);
                writer.Put((ushort)__instance.si.syncId);
                writer.Put((short)traderScript.MoveRange.min);
                writer.Put((short)traderScript.MoveRange.max);
                writer.Put((byte)traderScript.items.Count);
                foreach (TraderItem item in traderScript.items)
                {
                    if (clientHaveRshlib || (!clientHaveRshlib && !Plugin.itemRegistry.ContainsKey(item.id)))
                        writer.Put(item.id, oneByteChars: true);
               else     writer.Put("craftingbottle", oneByteChars: true);

                    writer.Put(item.bought);
                    writer.Put((ushort)item.value);
                    writer.Put((byte)item.preference);
                }

                Net.Server_SendTo(DeliveryMethod.ReliableUnordered, in writer, client);
            }

            IReadOnlyList<knetid> to_who = clients_to_enlighten;
            __instance.Server_AnnounceTraderReputationState(in to_who);

            return false;
        }
    }
}

