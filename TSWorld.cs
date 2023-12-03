using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS_Server.DataTools.Eve;
using TS_Server.Client;

namespace TS_Server.Server
{
    public class TSWorld
    {
        private static TSWorld instance = null;
        public TSServer server;
        public Dictionary<ushort, TSMap> listMap;

        public TSWorld(TSServer s)
        {
            server = s;
            instance = this;
            listMap = new Dictionary<ushort, TSMap>();
        }

        public static TSWorld getInstance()
        {
            return instance;
        }

        public TSMap initMap(ushort mapid) //มีปัญหาเด้ง
        {
            //Console.WriteLine("TSWorld.initMap");
            if (listMap == null) 
                return null;

            TSMap m = new TSMap(this, mapid);
            if (m == null) 
                return null;

            listMap.Add(mapid, m);//มีปัญหา
            return m;
        }

        #region # warp(...)ของเดิม
        //public void warp(TSClient client, ushort warpid)
        //{
        //    client.reply(new PacketCreator(new byte[] { 20, 0x07 }).send());
        //    client.reply(new PacketCreator(new byte[] { 0x29, 0x0E }).send());

        //    ushort start = client.map.mapid;
        //    if (WarpData.warpList.ContainsKey(start))
        //    {
        //        if (WarpData.warpList[start].ContainsKey(warpid))
        //        {
        //            ushort[] dest = WarpData.warpList[start][warpid];

        //            if (!listMap.ContainsKey(dest[0]))
        //            {
        //                listMap.Add(dest[0], new TSMap(this, dest[0]));
        //            }

        //            client.getChar().mapID = dest[0];
        //            client.getChar().mapX = dest[1];
        //            client.getChar().mapY = dest[2];
        //            //client.map.removePlayer(client.accID);

        //            listMap[dest[0]].addPlayerWarp(client, dest[1], dest[2]);
        //            return;
        //        }
        //        else
        //        {
        //            //Console.WriteLine("Warp data helper : warpid " + warpid + " not found");
        //            //EveData.loadCoor(start, 12000);
        //        }
        //    }
        //    else
        //    {
        //        //Console.WriteLine("Warp data helper : mapid " + start + " warpid " + warpid + " not found");
        //        //EveData.loadCoor(start, 12000);
        //    }
        //    client.AllowMove();
        //}

        //public void warp(TSClient client, ushort warpid)//อันนี้ใช้วาปจาก eve เพื่อทดแทบข้างบน เด่วค่อยมาทำ
        //{
        //    //step 1 เอา click_id ไปหาใน eve -> gates
        //    EveData.EveInfo eveInfo = EveData.eveList[client.map.mapid];
        //    EveData.Gate gate = eveInfo.gates[warpid];
        //    byte eve_key = gate.events[0]; //ก็จะได้ key ของ event_list

        //    //step 2 เอา event ตาม key จาก step 1 
        //    EveData.Event eve_event = eveInfo.event_list[eve_key];

        //    //ดึงค่า talk ตัวแรก
        //    EveData.Talk talk = eve_event.talks.First().Value;

        //    //เอา dialog ที่ 0
        //    byte[] d = talk.dialogs[0]; //00-00-01-02-07-06-00-00-00-00-00-00-00-00

        //    //index ที่ 5 ของ dialog[0] คือ warp id นั่นเอง
        //    ushort warp_key = (ushort)((int)d[5] + ((int)d[6] << 8)); //read16
        //    EveData.Warp warp = eveInfo.warps[warp_key];
        //    Logger.Warning("dest id " + warp.dest_id);
        //    Logger.Warning("dest x " + warp.dest_x);
        //    Logger.Warning("dest y " + warp.dest_y);
        //}
        #endregion

        /// <summary>
        /// วาร์โดยระบุแมะและพิกัดไปได้เลยโดยไม่อาศัยข้อมูลจาก Warps.txt
        /// </summary>
        /// <param name="client"></param>
        /// <param name="warpid"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void warp(TSClient client, ushort mapid, ushort x, ushort y)
        {
            var p = new PacketCreator(0x0C);
            p.add32(client.accID);
            p.add16(mapid);
            p.add16(client.getChar().mapX);
            p.add16(client.getChar().mapY);
            p.add16(client.getChar().orient);

            client.map.listPlayers.TryRemove(client.accID, out _);
            client.map.BroadCast(client, p.send(), false);

            client.reply(new PacketCreator(new byte[] { 20, 0x07 }).send());
            client.reply(new PacketCreator(new byte[] { 0x29, 0x0E }).send());


            if (!listMap.ContainsKey(mapid))
            {
                TSMap m = new TSMap(this, mapid);
                if (m != null)
                    listMap.Add(mapid, new TSMap(this, mapid));
            }

            client.getChar().mapID = mapid;
            client.getChar().mapX = x;
            client.getChar().mapY = y;
            //client.map.removePlayer(client.accID);

            listMap[mapid].addPlayerWarp(client, x, y);
            return;
            //client.AllowMove();
        }

        public void eveWarp(TSClient client, byte warp_id)
        {
            //Console.WriteLine("eveWarp: " + client.accID);
            TSCharacter chr = client.getChar();
            ushort start = client.map.mapid;
            //Logger.Info("old map " + start);
            if (EveData.mapList[start].warpList.ContainsKey(warp_id))
            {
                EveWarp dest = EveData.mapList[start].warpList[warp_id];

                chr.reply(new PacketCreator(new byte[] { 0x14, 0x07 }).send());
                //chr.reply(new PacketCreator(new byte[] { 0x29, 0x0E }).send());
                if (dest.destId > 0)
                {
                    if (!listMap.ContainsKey(dest.destId))
                    {
                        listMap.Add(dest.destId, new TSMap(this, dest.destId));
                    }

                    chr.mapID = dest.destId;
                    chr.mapX = dest.destX;
                    chr.mapY = dest.destY;
                    //client.map.removePlayer(client.accID);

                    listMap[dest.destId].addPlayerWarp(client, dest.destX, dest.destY);

                    client.continueMoving();

                    return;
                }
            }
            else
            {
                Logger.Warning(client.accID + " map:" + start + " warping: " + warp_id + " fail. Not Contain");
            }
            client.AllowMove();
        }
    }
}
