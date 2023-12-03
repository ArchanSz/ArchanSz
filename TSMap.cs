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

        public TSMap(TSWorld w, ushort id)
        {
            world = w;
            mapid = id;
            listPlayers = new ConcurrentDictionary<uint, TSClient>();
        }

        public void addPlayerWarp(TSClient client, ushort x, ushort y)
        {
            PacketCreator p;
            TSCharacter chr = client.getChar();

            chr.reply(new PacketCreator(new byte[] { 0x29, 0x0E }).send());

            //warp done, reply to client
            p = new PacketCreator(0x0c);
            p.add32(client.accID);
            p.add16(mapid);
            p.add16(x);
            p.add16(y);
            // Group
            byte w = 0;
            if (chr.party == null || chr.isTeamLeader())
                w = client.warpPrepare;
            else if (chr.isChildInTeam())
            {
                TSClient client_leader = chr.party.getClientLeader();
                if (client_leader != null)
                    w = client_leader.warpPrepare;
            }
            //p.add8((byte)(chr.isTeamLeader() ? client.warpPrepare : (!chr.isJoinedTeam() ? client.warpPrepare : (chr.party.getClientLeader() != null ? chr.party.getClientLeader().warpPrepare : 0))));
            p.add8(w);
            p.add8(0); // Orient
            client.reply(p.send());
            //BroadCast(client, p.send(), false);

            if (chr.isTeamLeader())
            {
                client.AllowMove();
            }

            //client.reply(new PacketCreator(0x17, 4).send());
            //client.map.removePlayer(client.accID); //<<< ของเดิม

            //Logger.Info("old map " + client.map.mapid);
            //Logger.Info("new map " + mapid);
            client.map.removePlayer(client);
            this.addPlayer(client);
            client.map = this;

            if (chr.myEvent != null)
            {
                chr.myEvent.endTalk();
            }
            //announceAppear(client); //เปลี่ยนไปใส่ที่ RelocateHandler แล้ว
        }

        //public void addPlayer(TSClient client)
        //{
        //    listPlayers.Add(client.accID, client);
        //    client.map = this;

        //    //packets for client
        //    foreach (TSClient c in listPlayers.Values)
        //    {
        //        if (c.accID != client.accID)
        //        {
        //            // packets For players in the same map
        //            client.reply(c.getChar().sendLookForOther());
        //            c.reply(client.getChar().sendLookForOther()); //nice line :))

        //            // Update team visible or pet list from other
        //            c.getChar().sendUpdateTeam();
        //            client.getChar().sendUpdateTeam();
        //        }
        //    }
        //    client.getChar().sendUpdateTeam();
        //}

        public void addPlayer(TSClient client)
        {
            //Console.WriteLine("TSMap.addPalyer");
            if (listPlayers.ContainsKey(client.accID))
            {
                Logger.Error(
                    "Bug: มี "
                        + client.accID
                        + " ในแมพ "
                        + mapid//มีปัญหา
                        + " แล้ว -> จึงลบออกจากแมพไปก่อนแล้วค่อยแอดใหม่ ไม่รู้วิธีนี้จะดีป่าว"
                );
                listPlayers.TryRemove(client.accID, out _);
            }

            listPlayers.TryAdd(client.accID, client); //มีปัญหา
            client.map = this;

            switch (mapid)
            {
                case 10991:
                {
                    PacketCreator p = new PacketCreator(0x16, 3); //ให้โชว์จ้าวเวทีชิงชัย
                    p.add16(5);
                    p.add16(0);
                    client.reply(p.send());
                    break;
                }
                case 10990:
                {
                    PacketCreator p = new PacketCreator(0x16, 3); //ให้โชว์จ้าวเวทีชิงชัย
                    p.add16(1);
                    p.add16(0);
                    client.reply(p.send());
                    break;
                }
            }

            if (EveData.mapList.ContainsKey(mapid))
            {
                ushort[] heep_ids = new ushort[]
                {
                    20001, //หีบ
                    20002 //กองเงิน
                };
                Dictionary<byte, EveNpc> heeps = EveData.mapList[mapid].npcList
                    .Where(elm => heep_ids.Contains(elm.Value.npcId))
                    .ToDictionary(k => k.Key, v => v.Value);
                if (heeps.Count > 0)
                {
                    foreach (KeyValuePair<byte, EveNpc> heep in heeps.ToList())
                    {
                        PacketCreator p = new PacketCreator(0x16, 1);
                        TSServer.Item_Pickup_Key heepKey = new TSServer.Item_Pickup_Key();
                        heepKey.map_id = mapid;
                        heepKey.click_id = heep.Key;
                        byte status = TSServer.getInstance().heepList.ContainsKey(heepKey)
                            ? (byte)1
                            : (byte)0;

                        p.addByte(heep.Key);
                        p.addByte(0);
                        p.addByte(status);
                        //Logger.Warning(BitConverter.ToString(p.getData()));
                        client.reply(p.send());
                    }
                }

                //packets for client
                TSCharacter chr_me = client.getChar();
                PacketCreator p_army = new PacketCreator(0x27, 0x09);
                foreach (TSClient c in listPlayers.Values.ToList())
                {
                    if (c != null && c.accID != client.accID)
                    {
                        TSCharacter chr_current = c.getChar();
                        // packets For players in the same map
                        chr_me.reply(chr_current.sendBasicInfo());
                        chr_current.reply(chr_me.sendBasicInfo());

                        chr_me.reply(chr_current.sendEquipmentInfo());
                        chr_current.reply(chr_me.sendEquipmentInfo());
                    }
                } //end foreach (TSClient c in listPlayers.Values.ToList())
            }
        }

        //public void removePlayer(uint id)
        //{
        //    TSClient client = getPlayerById(id);

        //    var p = new PacketCreator(0x0C);
        //    p.add32(client.accID);
        //    p.add16(mapid);
        //    p.add16(client.getChar().mapX);
        //    p.add16(client.getChar().mapY);
        //    p.add16(0x0100);

        //    listPlayers.Remove(id);
        //    BroadCast(client, p.send(), false);
        //}
        public void removePlayer(TSClient client)
        {
            TSCharacter chr = client.getChar();
            
            PacketCreator p = new PacketCreator(0x0C);
            p.add32(client.accID);
            p.add16(chr.mapID);
            p.add16(chr.mapX);
            p.add16(chr.mapY);
            p.add16(0x0100);

            BroadCast(client, p.send(), false);
            listPlayers.TryRemove(client.accID, out _);
            //Logger.Info("old map " + mapid + " count " + listPlayers.Count);
            //if (listPlayers.Count == 0) //อันนี้เพิ่มเอง
            //{
            //    TSWorld.getInstance().listMap.Remove(oldMapId);
            //}
            //Logger.Error(warpId.ToString());
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
            //Logger.Warning("movePlayer");
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

        //public void announceBattle(TSClient client)
        //{
        //    TSCharacter chr = client.getChar();
        //    // Update Smoke
        //    PacketCreator p = new PacketCreator(0x0B, 0x04);
        //    p.add8(0x02);
        //    p.add32(client.accID);
        //    p.add16(0); // Guess
        //    p.add8((byte)(chr.isTeamLeader() ? 3 : 5)); // Guessing

        //    BroadCast(client, p.send(), true);
        //}

        public void announceAppear(TSClient client)
        {
            TSCharacter chr = client.getChar();
            PacketCreator p_army = new PacketCreator(0x27, 0x09);

            //PacketCreator p;

            byte[] event10991 = chr.sendEventOfMap10991(true);
            if (event10991 != null)
                chr.reply(event10991);

            if (chr.isTeamLeader())
                chr.reply(chr.sendPartyForMap(true));
            foreach (TSClient client_player in listPlayers.Values.ToList())
            {
                if (client_player != null && client_player.accID != client.accID) // แพคที่จะส่งให้คนอื่น
                {
                    TSCharacter chr_other = client_player.getChar();
                    if (chr_other != null)
                    {
                        if (chr_other.army != null)
                            p_army.addBytes(chr_other.army.getNameOnHead());

                        //////////เอา info ของคนอื่นให้เราดู ////////////
                        List<byte[]> chr_other_info_list = chr_other.sendInfoListForMap();
                        for (int i = 0; i < chr_other_info_list.Count; i++)
                        {
                            byte[] info = chr_other_info_list[i];
                            if (info != null)
                                chr.reply(info);
                        }

                        //////////เอา info ของเราคนอื่นให้ดู ////////////
                        List<byte[]> chr_info_list = chr.sendInfoListForMap();
                        for (int i = 0; i < chr_info_list.Count; i++)
                        {
                            byte[] info = chr_info_list[i];
                            if (info != null)
                                chr_other.reply(info);
                        }

                        if (
                            chr_other.outfitId > 0
                            && DataTools.NpcData.npcList.ContainsKey(chr_other.outfitId)
                        )
                            chr.reply(chr_other.sendOutfit());
                    }
                }
            }

            refreshItemOnMap(client);

            client.reply(p_army.send());
        }

        public void BroadCast(TSClient client, byte[] data, bool self)
        {
            //if (self && listPlayers.ContainsKey(client.accID))
            if (self && listPlayers.ContainsKey(client.accID))
            {
                //Console.WriteLine(client.accID.ToString());

                //if(client != null && client.getSocket().Connected)
                client.reply(data);
            }
            try
            {
                uint[] client_ids = listPlayers.Keys.ToArray();
                for (int i = 0; i < client_ids.Length; i++)
                {
                    uint current_id = client_ids[i];
                    TSClient c = getPlayerById(current_id);
                    if (
                        c != null
                        && c.accID != client.accID
                        && listPlayers.ContainsKey(c.accID)
                    )
                    {
                        c.reply(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Map.BroadCast " + ex.Message);
                Console.WriteLine(ex.ToString());
            }

            //foreach (TSClient c in listPlayers.Values.ToList())
            //{
            //    //Console.WriteLine(c.accID);
            //    //if (c != null && c.accID != client.accID && listPlayers.ContainsKey(c.accID))
            //    if (c != null && c.accID != client.accID && listPlayers.ContainsKey(c.accID) && !c.disconnecting)
            //    {
            //        c.reply(data);
            //    }
            //}
        }

        //public bool isWarpChangeMap(ushort warpId)
        //{
        //    if (WarpData.warpList.ContainsKey(mapid))
        //    {
        //        Dictionary<ushort, ushort[]> warping = WarpData.warpList[mapid];
        //        //Logger.Debug("WarpData.warpList.ContainsKey(mapId)"+ warping[talkingId]);
        //        //foreach (KeyValuePair<ushort, ushort[]> m in warping) Logger.Log(m.Key.ToString());
        //        return warping.ContainsKey(warpId);
        //        //if (warping.ContainsKey(talkingId))
        //        //{
        //        //    ushort[] dest = warping[talkingId];
        //        //    Logger.Log("mapId: " + mapId + " dest " + dest[0]);
        //        //    if (dest[0] != mapId)
        //        //        return false;
        //        //    else
        //        //        return true;
        //        //}
        //    }
        //    return false;
        //}

        //public ushort getWarpToMapId(ushort warpId)
        //{
        //    ushort result = 0;
        //    if (isWarpChangeMap(warpId))
        //    {
        //        Dictionary<ushort, ushort[]> warping = WarpData.warpList[mapid];
        //        if (warping.ContainsKey(warpId))
        //        {
        //            ushort[] dest = warping[warpId];
        //            //Logger.Log("mapId: " + mapId + " dest " + dest[0]);
        //            if (dest[0] != mapid)
        //                return dest[0];
        //        }
        //    }
        //    return result;
        //}

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
                    //Console.WriteLine(BitConverter.ToString(p.getData()));
                    client.reply(p.send());

                    //PacketCreator p = new PacketCreator(0x17, 0x03);
                    //p.add16(item.Value.itemId);
                    //p.add16(item.Value.posX);
                    //p.add16(item.Value.posY);

                    ////Logger.Error("item " + item.Value.item_id + ", x:" + item.Value.pos_x + ", y:" + item.Value.pos_y);
                    //client.reply(p.send());
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
            //00-00-01-00-03-01-00-03-05-00-00-00-00-00
            //00-00-01-00-03-06-00-03-0A-00-00-00-00-00
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

                            //PacketCreator p = new PacketCreator(0x16, 3);
                            //p.add16(id);
                            //p.add16((uint)eveMap.npcList[id].hideDelay);
                            //client.reply(p.send());
                        }
                    }
                    break;
                default:
                    break;
            }

            if (hex.Length > 0)
            {
                //Logger.Error(BitConverter.ToString(hex));
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
