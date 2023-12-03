using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS_Server.Client;
using TS_Server.DataTools.Eve;

namespace TS_Server.Server
{
    public class TSMap
    {
        public TSWorld world;
        public ushort mapid;
        public ConcurrentDictionary<uint, TSClient> listPlayers;
        private ushort mapId;

        public ushort MapID { get; internal set; }

        public TSMap(TSWorld w, ushort id)
        {
            world = w;
            mapid = id;
            listPlayers = new ConcurrentDictionary<uint, TSClient>();
        }

        public void addPlayerWarp(TSClient client, ushort x, ushort y)
        {
            if (client == null) return;

            TSCharacter chr = client.getChar();
            if (chr == null) return;

            // Inform client about warp completion
            PacketCreator warpCompletedPacket = new PacketCreator(new byte[] { 0x29, 0x0E });
            chr.reply(warpCompletedPacket.send());

            // Prepare packet data for client
            PacketCreator p = new PacketCreator(0x0c);
            p.add32(client.accID);
            p.add16(mapid);
            p.add16(x);
            p.add16(y);

            byte w = 0;
            if (chr.party == null || chr.isTeamLeader())
                w = client.warpPrepare;
            else if (chr.isChildInTeam())
            {
                TSClient client_leader = chr.party.getClientLeader();
                if (client_leader != null)
                    w = client_leader.warpPrepare;
            }
            p.add8(w);
            p.add8(0); // Orient
            client.reply(p.send());

            // Handle team leader actions and map changes
            if (chr.isTeamLeader())
            {
                client.AllowMove();
            }
            client.map.RemovePlayer(client);
            this.addPlayer(client);
            client.map = this;

            // End any active event talks for character
            chr.myEvent?.endTalk();
        }


        public void addPlayer(TSClient client)
        {
            if (client == null) return;

            // Check if player is already in map
            if (listPlayers.ContainsKey(client.accID))
            {
                Logger.Error($"Bug: Player with ID {client.accID} already in map {mapid}. Removing before re-adding.");
                listPlayers.TryRemove(client.accID, out _);
            }

            // Add player to map
            listPlayers.TryAdd(client.accID, client);
            client.map = this;

            // Handle specific map actions
            SendMapSpecificPacket(client);

            // If map is available in data list
            if (EveData.mapList.ContainsKey(mapid))
            {
                SendHeepInfoToClient(client);
                SendPlayerInfoToClients(client);
            }
        }

        private void SendMapSpecificPacket(TSClient client)
        {
            PacketCreator p;
            switch (mapid)
            {
                case 10991:
                    p = new PacketCreator(0x16, 3);
                    p.add16(5);
                    p.add16(0);
                    client.reply(p.send());
                    break;
                case 10990:
                    p = new PacketCreator(0x16, 3);
                    p.add16(1);
                    p.add16(0);
                    client.reply(p.send());
                    break;
            }
        }

        private void SendHeepInfoToClient(TSClient client)
        {
            ushort[] heep_ids = { 20001, 20002 };
            var heeps = EveData.mapList[mapid].npcList
                .Where(elm => heep_ids.Contains(elm.Value.npcId))
                .ToDictionary(k => k.Key, v => v.Value);

            foreach (var heep in heeps)
            {
                PacketCreator p = new PacketCreator(0x16, 1);
                TSServer.Item_Pickup_Key heepKey = new TSServer.Item_Pickup_Key
                {
                    map_id = mapid,
                    click_id = heep.Key
                };
                byte status = TSServer.getInstance().heepList.ContainsKey(heepKey) ? (byte)1 : (byte)0;
                p.addByte(heep.Key);
                p.addByte(0);
                p.addByte(status);
                client.reply(p.send());
            }
        }

        private void SendPlayerInfoToClients(TSClient client)
        {
            TSCharacter chr_me = client.getChar();
            PacketCreator p_army = new PacketCreator(0x27, 0x09);
            foreach (TSClient c in listPlayers.Values)
            {
                if (c != null && c.accID != client.accID)
                {
                    TSCharacter chr_current = c.getChar();
                    if (chr_current != null)
                    {
                        chr_me.reply(chr_current.sendBasicInfo());
                        chr_current.reply(chr_me.sendBasicInfo());

                        chr_me.reply(chr_current.sendEquipmentInfo());
                        chr_current.reply(chr_me.sendEquipmentInfo());
                    }
                }
            }
        }

        public void RemovePlayer(TSClient client)
        {
            if (client == null || client.getChar() == null || client.map == null)
            {
                // Handle error (e.g., log it, throw an exception, or return early)
                Logger.Error("Client or required client data is null");
                return;
            }

            TSCharacter chr = client.getChar();
            ushort oldMapId = client.map.mapId;
            ushort warpId = client.warpPrepare;
            ushort destinationMapId = 0;
            const int PacketType = 0x0C;

            if (EveData.mapList.ContainsKey(oldMapId) && EveData.mapList[oldMapId].warpList.ContainsKey((byte)warpId))
            {
                destinationMapId = EveData.mapList[oldMapId].warpList[(byte)warpId].destId;
            }

            PacketCreator p = new PacketCreator(PacketType);
            p.add32(client.accID);
            p.add16(destinationMapId);
            p.add16(chr.mapX);
            p.add16(chr.mapY);
            p.add16(chr.orient);

            BroadCast(client, p.send(), false);

            if (listPlayers.TryRemove(client.accID, out _))
            {
               //Logger.Info($"Removed player {client.accID} from map {oldMapId}. Remaining player count: {listPlayers.Count}");
            }

            if (listPlayers.Count == 0)
            {
                TSWorld.getInstance().listMap.Remove(oldMapId);
               // Logger.Info($"Removed map {oldMapId} as it has no players left.");
            }
        }

        public TSClient getPlayerById(uint id)
        {
            if (listPlayers.ContainsKey(id))
                return listPlayers[id];
            return null;
        }

        public void movePlayer(TSClient client, ushort x, ushort y)
        {
            if (client.warpPrepare > 0)
                return;
            TSCharacter chr = client.getChar();
            var p = new PacketCreator(0x06);
            p.add8(0x01);
            p.add32(client.accID);
            p.add8(chr.orient); // Orientation
            p.add16(x);
            p.add16(y);
            byte[] packet = p.send();

            chr.replyToMap(packet, true);
            chr.mapX = x;
            chr.mapY = y;

            if (chr.party != null)
            {
                foreach (TSCharacter c in chr.party.member)
                {
                    if (c != chr)
                    {
                        c.mapX = x;
                        c.mapY = y;
                    }
                }
            }
        }
        public void announceAppear(TSClient client)
        {
            TSCharacter chr = client.getChar();
            PacketCreator p_army = new PacketCreator(0x27, 0x09);

            byte[] event10991 = chr.sendEventOfMap10991(true);
            if (event10991 != null)
                chr.reply(event10991);

            if (chr.isTeamLeader())
                chr.reply(chr.sendPartyForMap(true));

            // สร้าง list ของ info ของ chr ก่อนเข้าลูป
            List<byte[]> chr_info_list = chr.sendInfoListForMap();

            foreach (TSClient client_player in listPlayers.Values.ToList())
            {
                if (client_player != null && !client_player.accID.Equals(client.accID))
                {
                    TSCharacter chr_other = client_player.getChar();
                    if (chr_other != null)
                    {
                        if (chr_other.army != null)
                            p_army.addBytes(chr_other.army.getNameOnHead());

                        // เอา info ของคนอื่นให้เราดู
                        foreach (byte[] info in chr_other.sendInfoListForMap())
                        {
                            if (info != null)
                                chr.reply(info);
                        }

                        // เอา info ของเราให้คนอื่นดู
                        foreach (byte[] info in chr_info_list)
                        {
                            if (info != null)
                                chr_other.reply(info);
                        }

                        if (chr_other.outfitId > 0 && DataTools.NpcData.npcList.ContainsKey(chr_other.outfitId))
                            chr.reply(chr_other.sendOutfit());
                    }
                }
            }

            refreshItemOnMap(client);

            client.reply(p_army.send());
        }

        public void BroadCast(TSClient client, byte[] data, bool self)
        {
            if (self && listPlayers.ContainsKey(client.accID) && !client.disconnecting)
            {
                client.reply(data);
            }
            try
            {
                foreach (var pair in listPlayers)
                {
                    uint current_id = pair.Key;
                    TSClient c = pair.Value;
                    if (
                        c != null
                        && c.accID != client.accID
                        && !c.disconnecting
                    )
                    {
                        c.reply(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Map.BroadCast Exception: " + ex.Message + "\n" + ex.StackTrace);
                Console.WriteLine(ex.ToString());
            }
        }

        public void refreshItemOnMap(TSClient client)
        {
            if (
                !EveData.mapList.ContainsKey(client.map.mapid)
                || EveData.mapList[client.map.mapid].itemList == null
            )
            {
                return;
            }
            ushort map_id = client.map.mapid;
            foreach (
                KeyValuePair<byte, EveItem> item in EveData.mapList[
                    client.map.mapid
                ].itemList.ToList()
            )
            {
                TSServer.Item_Pickup_Key key = new TSServer.Item_Pickup_Key();
                key.map_id = map_id;
                key.click_id = (byte)item.Value.clickId;

                if (!TSServer.getInstance().itemOnMapPickup.ContainsKey(key))
                {
                    PacketCreator p = new PacketCreator(0x17, 0x04);
                    p.addByte(0x03);
                    p.add16(item.Value.clickId);
                    p.add16(item.Value.itemId);
                    p.addZero(2);
                    p.add16(item.Value.posX);
                    p.add16(item.Value.posY);
                    client.reply(p.send());
                }
            }
        }

        public void setPopNpcOnMap(
            TSClient client,
            string pop_type,
            ushort click_id,
            int delay,
            string reply_type,
            bool self
        )
        {
            EveMap eveMap = EveData.mapList[mapid];
            byte id = (byte)click_id;
            byte[] hex = new byte[0];
            switch (pop_type.ToUpper())
            {
                case "HIDE":

                    {
                        if (eveMap.npcList[id].hideDelay < 0)
                        {
                            eveMap.npcList[id].hideDelay = delay;
                            PacketCreator p = new PacketCreator(0x14, 1);
                            p.addZero(3);
                            p.add16(1);
                            p.addByte(3);
                            p.add16(id);
                            p.addByte(3);
                            p.add32((uint)eveMap.npcList[id].hideDelay);
                            p.addZero(2);
                            hex = p.getData();
                        }
                    }
                    break;
                default:
                    break;
            }

            if (hex.Length > 0)
            {
                PacketCreator p = new PacketCreator(hex);
                switch (reply_type.ToUpper())
                {
                    case "ME":
                        client.getChar().reply(p.send());
                        break;
                    case "TEAM":
                        client.getChar().replyToTeam(p.send());
                        break;
                    case "MAP":
                        client.getChar().replyToMap(p.send(), self);
                        break;
                    case "ALL":
                        client.getChar().replyToAll(p.send(), self);
                        break;
                    default:
                        Logger.Error("TSMap.setPopNpcOnMap unknown reply_type");
                        break;
                }
            }
        }
    }
}
