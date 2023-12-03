using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS_Server.DataTools;

namespace TS_Server.Server.BattleClasses
{
    public class BattleNpcAI
    {
        public ushort npcid;
        public ushort hpmax, spmax, hp, sp, atk, mag, def, agi;
        public ushort[] skill;
        public byte level, elem, reborn;
        public int disable;
        public ushort count;
        public ushort drop;
        public List<Tuple<byte, byte>> attacker;
        public List<Tuple<byte, byte>> killer;
        public List<ushort> reinforcement;
        public int reinIndex = 0;
        public TSBattleNPC battleNPC;

        public BattleNpcAI(TSBattleNPC b, ushort cnt, ushort id)
        {
            battleNPC = b;
            npcid = id;
            NpcInfo npcinfo = NpcData.npcList[id];
            hpmax = (ushort)npcinfo.hpmax;
            spmax = (ushort)npcinfo.spmax;
            hp = (ushort)npcinfo.hpmax;
            sp = (ushort)npcinfo.spmax;
            level = npcinfo.level;
            elem = npcinfo.element;
            reborn = npcinfo.reborn;
            mag = npcinfo.mag;
            atk = npcinfo.atk;
            def = npcinfo.def;
            agi = npcinfo.agi;
            if (npcinfo.skill4 != 0)
            {
                skill = new ushort[4];
                skill[3] = npcinfo.skill4;
            }
            else skill = new ushort[3];
            skill[0] = npcinfo.skill1;
            skill[1] = npcinfo.skill2;
            skill[2] = npcinfo.skill3;
            count = cnt;

            attacker = new List<Tuple<byte, byte>>();
            killer = new List<Tuple<byte, byte>>();
        }

        public ushort generateDrop()
        {
            //Console.WriteLine("generateDrop");
            NpcInfo npcinfo = NpcData.npcList[npcid];
            ushort result = 0;
            ushort drop1 = npcinfo.drop1;
            ushort drop2 = npcinfo.drop2;
            ushort drop3 = npcinfo.drop3;
            ushort drop4 = npcinfo.drop4;
            ushort drop5 = npcinfo.drop5;
            ushort drop6 = npcinfo.drop6;

            TSConfig config = TSServer.config;
            int per0 = config.drop.no_drop;
            int per1 = config.drop.item1;
            int per2 = config.drop.item2;
            int per3 = config.drop.item3;
            int per4 = config.drop.item4;
            int per5 = config.drop.item5;
            int per6 = config.drop.item6;
            int perCarpet = config.drop.carpet;
            int perSummon = config.drop.summon;
            int perPearl = config.drop.pearl;

            int totstat = per0 + per1 + per2 + per3 + per4 + per5 + per6 + perCarpet + perSummon + perPearl;
            int randomNumber = battleNPC.randomize.getInt(0, (totstat + 1));
            //string txt = "";

            if (randomNumber < per0)
            {
                result = 0;
                //txt = "No drop";
            }
            else if (randomNumber < (per0 + per1))
            {
                result = drop1;
                //txt = "Item 1";
            }
            else if (randomNumber < (per0 + per1 + per2))
            {
                result = drop2;
                //txt = "Item 2";
            }
            else if (randomNumber < (per0 + per1 + per2 + per3))
            {
                result = drop3;
                //txt = "Item 3";
            }
            else if (randomNumber < (per0 + per1 + per2 + per3 + per4))
            {
                result = drop4;
                //txt = "Item 4";
            }
            else if (randomNumber < (per0 + per1 + per2 + per3 + per4 + per5))
            {
                result = drop5;
                //txt = "Item 5";
            }
            else if (randomNumber < (per0 + per1 + per2 + per3 + per4 + per5 + per6))
            {
                result = drop6;
                //txt = "Item 6";
            }
            else if (randomNumber < (per0 + per1 + per2 + per3 + per4 + per5 + per6 + perCarpet))
            {
                if (this.isNpcOre()) result = 0;
                else
                {
                    ushort[] carpets = new ushort[]
                    {
                        46027, //พรมจัวจวิ้น
                        46016, //พรมเทพท่อง
                        46386, //กล่องฮาโลวีน
                    };
                    int rnd = battleNPC.randomize.getInt(0, carpets.Length);

                    result = rnd < carpets.Length ? carpets[rnd] : (ushort)0;
                }
            }
            else if (randomNumber < (per0 + per1 + per2 + per3 + per4 + per5 + per6 + perCarpet + perSummon))
            {
                if (npcinfo.level >= 80 && npcinfo.element >= 1 && npcinfo.element <= 4 && !this.isNpcOre())
                {
                    int k = npcinfo.element - 1;
                    ushort[] summons = new ushort[]
                    {
                        23086, //ตราหินผา
                        23087, //ตราเทพวารี
                        23088, //ตราหงส์
                        23089, //ตรามังกรเขียว
                    };
                    result = k < summons.Length ? summons[k] : (ushort)0;
                }
                else
                    result = 0;
            }
            else if (randomNumber < (per0 + per1 + per2 + per3 + per4 + per5 + per6 + perCarpet + perSummon + perPearl))
            {
                if (npcinfo.type == 37)
                {
                    ushort[] pearls = new ushort[]
                    {
                        30083,   //เศษมุกฟ้าพลัง
                        30084,   //เศษมุกฟ้าปัญญา
                        30085,   //เศษไข่มุกฟ้า
                    };
                    int rnd = battleNPC.randomize.getInt(0, pearls.Length);

                    result = rnd < pearls.Length ? pearls[rnd] : (ushort)0;
                }
                else
                    result = 0;
            }

            if (ItemData.itemList.ContainsKey(result))
            {
                //type ที่ไม่ให้ดรอบ
                ushort[] no_drop_by_types = new ushort[]
                {
                    //51, //ดาวขุนพล
                    //เพิ่ม type ที่ไม่ให้ดรอบต่อจากตรงนี้
                };

                //item id ที่ไม่ให้ดรอบ
                ushort[] no_drop_by_id = new ushort[]
                {
                    //30083, //เศษมุกฟ้าพลัง
                    //30084, //เศษมุกฟ้าปัญญา
                    //30085, //เศษไข่มุกฟ้า
                    //เพิ่ม item id ที่ไม่ให้ดรอบต่อจากตรงนี้

                };
                ItemInfo itemInfo = ItemData.itemList[result];
                if (no_drop_by_types.Contains(itemInfo.type) || no_drop_by_id.Contains(itemInfo.id))
                    result = 0;
            }

            return result;
        }

        public NpcInfo getNpcInfo()
        {
            if (NpcData.npcList.ContainsKey(this.npcid))
            {
                return NpcData.npcList[this.npcid];
            }
            return new NpcInfo();
        }

        public bool isNpcOre()
        {
            NpcInfo npcInfo = getNpcInfo();
            return npcInfo.id > 0 ? npcInfo.type == 16 : false;
        }
    }
}
