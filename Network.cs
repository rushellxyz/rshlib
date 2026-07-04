using System.Collections.Generic;
using System;
using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using Together;

namespace RshLib
{
    public class Network
    {
        public const byte TOGETHER_RECIVER = 226;

        public static Dictionary<ushort, Multiplayer.HandleNamedMessageDelegate> clientReceivers;
        public static Dictionary<ushort, Multiplayer.HandleNamedMessageDelegate> serverReceivers;
        public static HashSet<ushort> missingIds;

        public static NetDataWriter CreateWriter(ushort msgId)
        {
            NetDataWriter writer = Net.CreateWriter(TOGETHER_RECIVER);
            writer.Put((ushort)msgId);
            return writer;
        }

        internal static void Awake(Harmony harmony)
        {
            Plugin.PatchPostfix(harmony, "Together.Net", "ShutdownReset", "RshLib.ShutdownResetPostfix");
            clientReceivers = new Dictionary<ushort, Multiplayer.HandleNamedMessageDelegate>();
            serverReceivers = new Dictionary<ushort, Multiplayer.HandleNamedMessageDelegate>();
        }

        internal static void ServerReciver(knetid clientid, ref NetDataReader reader)
        {
            reader.Get(out ushort msgId);
            if (serverReceivers.TryGetValue(msgId, out var action))
                action(clientid, ref reader);
       else if (missingIds.Add(msgId))
                log.error($"Server reciver is not registred: {msgId}");
        }

        internal static void ClientReciver(knetid servid, ref NetDataReader reader)
        {
            reader.Get(out ushort msgId);
            if (clientReceivers.TryGetValue(msgId, out var action))
                action(servid, ref reader);
       else if (missingIds.Add(msgId))
                log.error($"Client reciver is not registred: {msgId}");
        }
    }

    internal class ShutdownResetPostfix
    {
        static void Postfix()
        {
            Network.missingIds = new HashSet<ushort>();
            Net.RegisterClientReceiver(Network.TOGETHER_RECIVER, Network.ClientReciver);
            Net.RegisterServerReceiver(Network.TOGETHER_RECIVER, Network.ServerReciver);
        }
    }
}
