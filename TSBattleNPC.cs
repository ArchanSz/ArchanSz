using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS_Server.DataTools;
using TS_Server.Client;
using System.Timers;
using TS_Server.Server.BattleClasses;
using TS_Server.DataTools.Eve;
using MySqlX.XDevAPI.Relational;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using MySqlX.XDevAPI.Common;

namespace TS_Server.Server
{
    public class TSBattleNPC : BattleAbstract
    {
        public ushort npcmapid = 65000; //pk NPC only
        public EveBattle eveMapBattle;

        public TSBattleNPC(TSClient c, byte type, ushort npc_mapid, ushort[] listNPC) : base(c, type)
        {
            //Logger.Info("TSBattleNPC");
            ushort mapId = c.getChar().mapID;
            if (BattleGroundData.battleGroundList.ContainsKey(mapId))
            {
                BattleGroundData.BattleGroundInfo battleGround = BattleGroundData.battleGroundList[mapId];
                ffield = battleGround.ffield > 0 ? battleGround.ffield : ffield;
            }

            initAlly(c);

            npcmapid = npc_mapid;
            initNPCs(listNPC);

            //if (c.getChar().party != null)
            //    foreach (TSCharacter mem in c.getChar().party.member.ToList())
            //        map.BroadCast(mem.client, mem.sendBattleSmoke(true), true);
            //else
            //    map.BroadCast(c, c.getChar().sendBattleSmoke(true), true);
            //start_round();
            beginBattle();
            //c.map.announceBattle(c); //<<--เพิ่มตรงนี้ เพื่อให้คนอื่นเห็นเราคลุกฝุ่นต่อสู้อยู่
        }

        public TSBattleNPC(TSClient c, byte type, ushort npc_mapid, EveBattle eve_map_battle) : base(c, type)
        {
            eveMapBattle = eve_map_battle;
            ushort mapId = c.getChar().mapID;
            if (eveMapBattle != null)
            {
                if (BattleGroundData.battleGroundList.ContainsKey(mapId))
                {
                    BattleGroundData.BattleGroundInfo battleGround = BattleGroundData.battleGroundList[mapId];
                    ffield = battleGround.ffield > 0 ? battleGround.ffield : ffield;
                }

                initAlly(c);


                npcmapid = npc_mapid;
                initNPCs(eveMapBattle.enemy);

                //start_round();
                beginBattle();
                //c.map.announceBattle(c); //<<--เพิ่มตรงนี้ เพื่อให้คนอื่นเห็นเราคลุกฝุ่นต่อสู้อยู่
            }
        }
        /// <summary>
        /// สู้ NPC โดยสามารถตั้งค่าฉากการต่อสู้ได้
        /// </summary>
        /// <param name="c"></param>
        /// <param name="type"></param>
        /// <param name="npc_mapid"></param>
        /// <param name="ground"></param>
        /// <param name="listNPC"></param>
        public TSBattleNPC(TSClient c, byte type, ushort npc_mapid, ushort[] listNPC, byte ffield) : base(c, type)
        {
            this.ffield = ffield;

            initAlly(c);

            npcmapid = npc_mapid;
            initNPCs(listNPC);

            //start_round();
            beginBattle();
            //c.map.announceBattle(c); //<<--เพิ่มตรงนี้ เพื่อให้คนอื่นเห็นเราคลุกฝุ่นต่อสู้อยู่
        }

        public void initAlly(TSClient c) //rewrite later for party
        {
            TSCharacter chr = c.getChar();
            if (chr != null)
            {
                PacketCreator p = new PacketCreator();
                p.addBytes(announceStart(chr, 3, 2, 3, null));

                TSParty party = chr.party;
                if (party != null)
                {
                    if (party.member.Count > 1) p.addBytes(announceStart(party.member[1], 3, 1, 5, p));
                    if (party.member.Count > 2) p.addBytes(announceStart(party.member[2], 3, 3, 5, p));
                    if (party.member.Count > 3) p.addBytes(announceStart(party.member[3], 3, 0, 5, p));
                    if (party.member.Count > 4) p.addBytes(announceStart(party.member[4], 3, 4, 5, p));
                }
            }
        }

        public byte[] announceStart(TSCharacter c, byte row, byte col, byte type, PacketCreator prefix)
        {
            if (prefix != null) //ใส่ battle ให้ลูกตี้
            {
                //if(c.client.battle != null)
                //{
                //    c.client.battle.destroyBattle();
                //    c.client.battle = null;
                //}

                c.client.battle = this;//มีปัญหาเด้ง
            }

            position[row][col].charIn(c);
            countAlly++;
            PacketCreator ret = position[row][col].announce(type, 0); //get the battle info of char
            byte[] ret_array = ret.getData();

            PacketCreator p = new PacketCreator(0xb, 0xfa); //announce battle
            //PacketCreator p = new PacketCreator(0x41, 0x01); //boss ep13
            //ushort rand1 = RandomGen.getUShort(0, 65535);
            //Console.WriteLine("combat gen " + rand1);
            //p.add8(0x70); p.add8(0); //<<--ของเดิม
            p.add8(ffield); p.add8(0);
            p.addBytes(ret_array);
            if (prefix != null)
            {
                byte[] prefix_data = prefix.getData();
                int cnt = prefix_data.Length / 24;
                for(int i = 0; i < cnt; i++)
                {
                    //byte[] test = new byte[24];
                    int idx = i * 24;
                    if( i > 1)
                        prefix_data[idx] = 0x64; //100 ตาม nw
                    //Array.Copy(prefix_data, idx, test, 0, test.Length);
                    //Console.WriteLine(">>"+BitConverter.ToString(test));
                }
                p.addBytes(prefix_data); //prefix contains info for next char in party
            }

            //Console.WriteLine(c.client.accID + "\n" + BitConverter.ToString(p.getData()) + "\n-----\n");

            c.reply(p.send());

            c.reply(new PacketCreator(new byte[] { 0xb, 0xa, 1 }).send());
            //c.reply(new PacketCreator(new byte[] { 0xb, 0xa, 0x10, 0, 0, 1 }).send()); //boss ep13
            //c.reply(new PacketCreator(new byte[] { 0x49, 0x04, 0x10, 0, 0, 0, 0, 0x14 }).send()); //boss ep13
            //Logger.Error("asasasaasasasas");

            if (prefix != null)
            {
                PacketCreator p_announce = new PacketCreator(0xb, 5);
                p_announce.addBytes(ret_array);
                byte[] announce = p_announce.send();
                for (int i = 0; i < 5; i++)
                    if (position[3][i].exist && i != col)
                        position[3][i].chr.reply(announce);
            }

            if (c.pet_battle != -1)
            {
                position[row - 1][col].petIn(c.pet[c.pet_battle]);
                countAlly++;

                //TSPet pet = c.pet[c.pet_battle];
                //PacketCreator ret1 = position[row - 1][col].announce(type, countAlly); //<<-ของเดิม
                PacketCreator ret1 = position[row - 1][col].announce(5, countAlly); //<< เพื่อให้ขุนในบอทออกตี ของ nw เป็น 3
                //byte h = (byte)(prefix == null ? 3 : 5);
                //PacketCreator ret1 = position[row - 1][col].announce(h, countAlly); //<< เพื่อให้ขุนในบอทออกตี ของ nw เป็น 3
                byte[] ret1_array = ret1.getData();

                PacketCreator p1 = new PacketCreator(0xb, 5);
                p1.addBytes(ret1_array);
                //Logger.Info(BitConverter.ToString(p1.getData()));
                c.reply(p1.send());

                if (prefix != null)
                {
                    PacketCreator p_announce = new PacketCreator(0xb, 5);
                    p_announce.addBytes(ret1_array);
                    byte[] announce = p_announce.send();
                    for (int i = 0; i < 5; i++)
                        if (position[3][i].exist && i != col)
                            position[3][i].chr.reply(announce);
                }

                ret.addBytes(ret1_array);
            }

            return ret.getData(); //will be used as prefix for next char in party
        }

        public void initNPCs(ushort[] listNPC)
        {
            for (byte i = 0; i < listNPC.Length; i++)
            {
                byte r = (byte)(i % 2);
                byte c = (byte)(i / 2);
                if (!NpcData.npcList.ContainsKey(listNPC[i]))
                {
                    if (listNPC[i] > 0)
                    {
                        string message = "BUG[BT]: แอดมินค๊าบ Add Npc [" + listNPC[i] + "] ด้วยค๊าบ";
                        Logger.Error(message);
                    }
                    listNPC[i] = 0;
                }
                if (listNPC[i] != 0)
                {
                    countEnemy++;

                    BattleNpcAI ai = new BattleNpcAI(this, countEnemy, listNPC[i]);
                    if (eveMapBattle != null && eveMapBattle.reinforcement.Count > 0)
                    {
                        if (eveMapBattle.reinforcement.ContainsKey(i))
                        {
                            ai.reinforcement = eveMapBattle.reinforcement[i];
                        }
                    }
                    position[r][c].npcIn(ai);

                    PacketCreator p = new PacketCreator(0x0b, 0x05);
                    BattleParticipant bp = position[r][c];
                    //note sure, but 3 3 = pk with npcmapid, 3 7 = gate, 1 7 = pk with npcid
                    p.addBytes(bp.announce(3, npcmapid != 65000 ? npcmapid : bp.npc.count).getData());
                    battleBroadcast(p.send());
                }
            }
        }

        public override void start_round()
        {
            //Console.WriteLine("start_round " + countAlly);
            //Logger.Error(btThread.Name);
            int dl = 300;
            System.Threading.Thread.Sleep(dl);
            executing = false;
            roundCount += 1;
            //Logger.Error("roundCount " + roundCount);
            //prepare for new round
            if (finish == 3) //RUN
            {
                endBattle(false);
                return;
            }

            ushort chkAlly = 0;
            ushort chkEnemy = 0;
            ushort chkDisabled = 0;
            int ally_sum_level = 0;
            int enemy_sum_level = 0;
            for (byte i = 0; i < 4; i++)
            {
                for (byte j = 0; j < 5; j++)
                {
                    //Logger.Error("position[" + i + "][" + j + "] = " + (position[i][j].exist ? "exist" : "no"));
                    BattleParticipant bp = position[i][j];

                    bp.purge_status(); //BattleNpc start_round

                    reflectSpSubLeader(bp);

                    if (countAlly > 0)
                        npcReinforcement(bp);
                    
                    if(finish == 0)
                        bp.updateStatus();

                    bp.alreadyCommand = false;

                    //Calc Sum level & reset countEnemy, countAlly, countDisabled
                    if (bp.exist && !bp.death)
                    {
                        /*if (bp.row < 2)
                        {
                            enemy_sum_level += bp.getLvl();
                            chkEnemy += 1;
                        }
                        else
                        {
                            ally_sum_level += bp.getLvl();
                            chkAlly += 1;
                        }
                        if (bp.disable != 0)
                        {
                            chkDisabled += 1;
                        }*/
                        if(bp.disable == 0)
                        {
                            if (bp.row < 2)
                            {
                                enemy_sum_level += bp.getLvl();
                                chkEnemy += 1;
                            }
                            else
                            {
                                ally_sum_level += bp.getLvl();
                                chkAlly += 1;
                            }
                        }
                    }
                    ///

                    // drop
                    if (battle_type == 3)
                    {
                        if (i < 2 && j < 5 && position[i][j].npc != null)
                        {
                            try
                            {
                                foreach (Tuple<byte, byte> k in position[i][j].npc.killer.ToList())
                                {
                                    if (k != null)
                                    {
                                        giveMoral(position[k.Item1][k.Item2], position[i][j]);
                                        if (position[i][j].npc.drop != 0)
                                        {
                                            giveDrop(position[i][j].npc.drop, (byte)i, (byte)j, k.Item1, k.Item2);
                                        }
                                    }
                                }

                                if (position[i][j].npc.drop != 0 && position[i][j].npc.killer.Count > 0)
                                {
                                    position[i][j].npc.drop = 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }
                    }
                }
            }
            countAlly = chkAlly;
            countEnemy = chkEnemy;
            countDisabled= chkDisabled;

            if (countAlly > 0)
                team1AvgLevel = ally_sum_level / countAlly;
            if (countEnemy > 0)
                team2AvgLevel = enemy_sum_level / countEnemy;

            cmdReg.Clear();
            cmdNeeded = countAlly + countEnemy - countDisabled;
            //Console.WriteLine("New round, ally = " + countAlly + ", enemy = " + countEnemy + ", disabled " + countDisabled + ", need " + cmdNeeded);

            //System.Threading.Thread.Sleep(200);
            if (finish != 0)
            {
                endBattle(finish == 1);
                return;
            }

            for (byte i = 0; i < 4; i++)
            {
                for (byte j = 0; j < 5; j++)
                {
                    BattleParticipant bp = position[i][j];
                    if (
                        bp.disable == 0
                        && (bp.debuff_type == 14021 || bp.debuff_type == 20014)) //สับสนวุ่นวาย สี่คนสับสน
                    {
                        pushCommand(i, j, i, j, 0, 10000); //ตีตัวเอง
                        //Logger.Error("\tcmdNeeded " + cmdNeeded, false);

                        battleBroadcast(new PacketCreator(new byte[] { 0x35, 0x05, i, j }).send()); //น่าจะทำให้ตำแหน่งนี้มีลูกศรที่หัวหายไป (ออกตีไม่ได้ละ)
                    }
                }
            }

            battleBroadcast(new PacketCreator(0x34, 0x01).send());

            aTimer.Start();
            //Logger.Error("ally = " + countAlly + ", enemy = " + countEnemy + ", disabled " + countDisabled + " --> cmdNeeded" + cmdNeeded);
            //Console.WriteLine("ally = " + countAlly + ", enemy = " + countEnemy + ", disabled " + countDisabled + " --> cmdNeeded" + cmdNeeded);
            giveCommandAI();
        }

        //public void giveCommandAI()
        //{
        //    for (byte i = 0; i < 2; i++)
        //        for (byte j = 0; j < 5; j++)
        //            if (position[i][j].type == 3 && position[i][j].disable == 0 && !position[i][j].death && position[i][j].debuff_type != 14021 && position[i][j].debuff_type != 20014 && position[i][j].exist)
        //            {
        //                //pushCommand(i, j, i, j, 0, 17001); //def 
        //                seekTarget(i, j);
        //            }
        //}
        public void giveCommandAI()
        {
            List<int[]> monsters = new List<int[]>();
            for (byte i = 0; i < 2; i++)
            {
                for (byte j = 0; j < 5; j++)
                {
                    BattleParticipant bp = position[i][j];
                    if (bp.type == 3 && bp.disable == 0 && !bp.death && bp.exist && !bp.alreadyCommand)
                    {
                        //pushCommand(i, j, i, j, 0, 17001); //def 
                        //seekTarget(i, j);
                        monsters.Add(new int[] { i, j, bp.getAgiSummary() });
                    }
                }
            }
            monsters = monsters.OrderByDescending(n => n[2]).ToList();
            foreach (int[] monster in monsters)
            {
                byte row = (byte)monster[0];
                byte col = (byte)monster[1];
                position[row][col].alreadyCommand = true;
                seekTargetV3(row, col);
            }
        }

        public void seekTarget(byte row, byte col) //AI in position (row, col) seeking for its target :)))
        {
            byte dest_row = 5;
            byte dest_col = 5;
            int skill = 10000;


            ushort id = (ushort)position[row][col].npc.npcid;
            NpcInfo npcInfo = NpcData.npcList[id];
            //List<int[]> skill_list = new List<int[]>();
            //skill_list.Add(new int[] { 10000, 0 }); //มือเปล่า
            List<int[]> npc_filter_skills = new List<int[]>() //[id, skill_type, grade]
            {
                new int[] { 10000, SkillData.skillList[10000].skill_type, SkillData.skillList[10000].grade },
                skillBattleFilter(npcInfo.skill1,npcInfo),
                skillBattleFilter(npcInfo.skill2,npcInfo),
                skillBattleFilter(npcInfo.skill3,npcInfo),
                skillBattleFilter(npcInfo.skill4,npcInfo),
            };

            npc_filter_skills = npc_filter_skills.Where(x => x[0] != 0 && x[1] != 0).ToList(); //เอาสกิล passive ออก

            //if (npcInfo.skill1 > 0) skill_list.Add(new int[] { npcInfo.skill1, 0 });
            //if (npcInfo.skill2 > 0) skill_list.Add(new int[] { npcInfo.skill2, 0 });
            //if (npcInfo.skill3 > 0) skill_list.Add(new int[] { npcInfo.skill3, 0 });
            //if (npcInfo.skill4 > 0) skill_list.Add(new int[] { npcInfo.skill4, 0 });

            bool test = false;
            if (test)//ชั่วคราว เทสให้มอนยิงสกิลเดิมๆ รัวๆ
            {
                skill = 10000;
            }
            else
            {
                skill = seekSkill(npc_filter_skills);
            }
            byte type = SkillData.skillList[(ushort)skill].skill_type;
            List<BattleParticipant> death_list = new List<BattleParticipant>();
            if (type == 8) //ถ้าสุ่มได้สกิลชุบ ให้รีเช็คมอนตัวอื่นอีกทีว่ามีตายมั้ย
            {
                for(int i = 0; i < 2; i++)
                    for(int j = 0; j < 5; j++)
                        if(position[i][j].exist && position[i][j].death)
                            death_list.Add(position[i][j]);

                if(death_list.Count == 0) //ถ้าไม่มีมอนตายเลยก็เอาสกิลชุบออกและสุ่มสกิลใหม่
                {
                    List<int[]> new_skill_list = npc_filter_skills.Where(s => s[0] != 11013).ToList();
                    skill = seekSkill(new_skill_list);
                    //type = SkillData.skillList[(ushort)skill].skill_type;
                }
            }

            //int rand = RandomGen.getInt(0, 100);
            //// 20% skill4, else 25% skill 3, else 33% skill 2, else 50% skill1, else attack
            //if (NpcData.npcList[id].skill4 != 0 && rand % 5 == 0)
            //    skill = NpcData.npcList[id].skill4;
            //else if (NpcData.npcList[id].skill3 != 0 && rand % 4 == 0)
            //    skill = NpcData.npcList[id].skill3;
            //else if (NpcData.npcList[id].skill2 != 0 && rand % 3 == 0)
            //    skill = NpcData.npcList[id].skill2;
            //else if (NpcData.npcList[id].skill1 != 0 && rand % 2 == 0)
            //    skill = NpcData.npcList[id].skill1;
            byte r = 2;
            byte nb_row = 2;
            if (type == 4 || type == 6 || type == 7 || type == 14 || type == 19) r = 0; //4 บัพ, 6 ฮิล SP, 7 ฮิล HP, 14 ฮิล HP/SP, 19 ออร่า แสดงว่าต้องฝั่งเมอนเท่านั้น
            if (type == 5 || type == 18) { nb_row = 4; r = 0; } //พวกสกิลสลาย กับเชิญน้ำ แสดงว่าใส่ได้ทั้ง 2 ฝ่าย

            if (type != 8)
            {
                List<byte[]> pos_ready = new List<byte[]>();
                for (byte i = r; i < (r + nb_row); i++)
                {
                    for (byte j = 0; j < 5; j++)
                    {
                        bool founded = position[i][j].exist && !position[i][j].death;
                        //Logger.Info(string.Format("position[{0}][{1}] {2}", i, j, founded));
                        if (founded)
                            pos_ready.Add(new byte[] { i, j });
                    }
                }
                if (pos_ready.Count > 0)
                {
                    int idx = randomize.getInt(0, pos_ready.Count);
                    dest_row = pos_ready[idx][0];
                    dest_col = pos_ready[idx][1];

                    //Logger.Info(string.Format("[{0}][{1}] ATTACT [{2}][{3}]", row, col, dest_row, dest_col));
                }
                else
                {
                    BattleParticipant bp_chr = position[3][2];
                    string owner_info = string.Empty;
                    if (bp_chr != null && bp_chr.chr != null)
                    {
                        owner_info = bp_chr.chr.client.accID + " " + bp_chr.chr.name;
                    }
                    Logger.Warning(string.Format("{0} {1} seekTarget ({2}){3} ไม่เจอคน แปลกจัง", DateTime.Now.ToString(), owner_info, type, skill));
                    dest_row = row;
                    dest_col = col;
                    skill = 17001; //ป้องกัน
                    //endBattle(false);
                    finish = 2;
                }
            }
            else //type 8 = วิชาฟื้นคืนชีพ
            {
                if (death_list.Count == 0) //เป็นไปไม่ได้หรอก แต่กันไว้ก่อน
                {
                    dest_row = row;
                    dest_col = col;
                    skill = 17001; //ป้องกัน
                }
                else
                {
                    BattleParticipant bp_death;
                    int idx = 0;
                    if (death_list.Count == 1)
                        idx = 0;
                    if (death_list.Count > 1)
                        idx = randomize.getInt(0, death_list.Count);
                    bp_death = death_list[idx];

                    dest_row = bp_death.row;
                    dest_col = bp_death.col;
                }
                //for (int i = 0; i < 2; i++)
                //    for (int j = 0; j < 5; j++)
                //        if (position[i][j].exist && position[i][j].death)
                //        {
                //            dest_row = (byte)(i);
                //            dest_col = (byte)(j);
                //            break;
                //        }
            }
            if (dest_row == 5) { dest_row = 0; dest_col = 0; }

            if (skill == 10016 || skill == 11016 || skill == 12016 || skill == 13015) //NPC trieu goi lvl 10 อัญเชิญ 4 ธาตุ
                skill += 3;

            //if (row == 0) dest_row = 1; //test only
            pushCommand(row, col, dest_row, dest_col, 0, (ushort)skill);
            //pushCommand(row, col, 3, 2, 0, 20014); //test only
        }
        public void seekTargetV2(byte row, byte col) //AI in position (row, col) seeking for its target :)))
        {
            byte dest_row = 5;
            byte dest_col = 5;
            byte type_sk = 0; //0 = skill, 1 = use item
            int skill = 10000;


            ushort id = (ushort)position[row][col].npc.npcid;
            NpcInfo npcInfo = NpcData.npcList[id];
            List<int[]> skill_list = new List<int[]>();
            skill_list.Add(new int[] { 10000, 0 }); //มือเปล่า
            if (npcInfo.skill1 > 0) skill_list.Add(new int[] { npcInfo.skill1, 0 });
            if (npcInfo.skill2 > 0) skill_list.Add(new int[] { npcInfo.skill2, 0 });
            if (npcInfo.skill3 > 0) skill_list.Add(new int[] { npcInfo.skill3, 0 });
            if (npcInfo.skill4 > 0) skill_list.Add(new int[] { npcInfo.skill4, 0 });


            int ai_level = TSServer.config.npc_ai_level;
            int score_base = 100;
            int score_low = score_base / 2;
            int score_meduim = score_base * Math.Max(1, (ai_level / 2));
            int score_meduim2 = (int)(score_meduim * 1.2);
            int score_height = score_base * ai_level;
            int score_height2 = (int)(score_height * 1.2);
            List<int[]> rate_skill_list = new List<int[]>();
            for (int s = 0; s < skill_list.Count; s++)
            {
                ushort sk_id = (ushort)skill_list[s][0];
                //Console.WriteLine("sk_id {0}", sk_id);
                if (SkillData.skillList.ContainsKey(sk_id))
                {
                    SkillInfo skillInfo = SkillData.skillList[sk_id];
                    byte effect = 1;
                    if (sk_id >= 13016 && sk_id <= 13018) { effect = 3; }//เชิญมังกร;
                    else if (sk_id >= 10017 && sk_id <= 10019) { effect = 15; } //เชิญหินผา;
                    else if (sk_id >= 11017 && sk_id <= 11019) effect = 18; //เชิญวารี;
                    //else if (sk_id >= 12017 && sk_id <= 12019) c.dmg = Math.Min((ushort)(c.dmg * (c.skill_lvl / 3)), (ushort)50000); //boost dmg phoenix
                    else if (sk_id >= 20001 && sk_id <= 20003) effect = 20; //ต้นไม้ดูดเลือด พิษเสียเลือด สะท้อนบาดเจ็บ
                    else effect = skillInfo.skill_type;

                    byte skill_type = skillInfo.skill_type;
                    byte r = 2;
                    byte nb_row = 2;
                    byte[] side_npc = new byte[] { 4, 6, 7, 8, 14, 19 };//4 บัพ, 6 ฮิล SP, 7 ฮิล HP, 14 ฮิล HP/SP, 19 ออร่า แสดงว่าต้องฝั่งเมอนเท่านั้น
                    byte[] side_both = new byte[] { 5, 18 };//5 สลาย, 18 อัญเชิญ, แสดงว่าใส่ได้ทั้ง 2 ฝ่าย
                    if (side_npc.Contains(effect)) r = 0;
                    if (side_both.Contains(effect)) { nb_row = 4; r = 0; }

                    ushort[] buff_ugly = new ushort[] {
                        10015, //กระจก
                        10031, //ระฆังทองคุ้มกาย
                        12024, //ตะวันคุ้มกาย
                        13021, //เคลื่อนดาวย้ายดารา
                        14044, //เกราะเทพเบื้องบน
                        14046, //เกราะคุ้มลมปราณ
                        10010, //ม่านคุ้มกัน
                    };

                    for (byte i = r; i < (r + nb_row); i++)
                    {
                        for (byte j = 0; j < 5; j++)
                        {
                            BattleParticipant bp = position[i][j];
                            int skill_grade = skillInfo.grade;
                            bool is_summon = SkillData.skillSummonEarth.Contains(sk_id) || SkillData.skillSummonWater.Contains(sk_id) || SkillData.skillSummonFire.Contains(sk_id) || SkillData.skillSummonWind.Contains(sk_id);
                            if (is_summon)
                                skill_grade = score_height;
                            //ressurrection วิชาฟื้นคืนชีพ
                            if (skill_type == 8)
                            {
                                if (bp.exist && bp.death)
                                    rate_skill_list.Add(new int[] { sk_id, i, j, score_height + skill_grade });
                            }
                            else
                            {
                                if (bp.exist && !bp.death)
                                {
                                    //giai tru สลายสภาวะ, อัญเชิญ
                                    if ((skill_type == 5 || skill_type == 18))
                                    {
                                        if (i < 2) //ฝั่งมอน
                                        {
                                            if (bp.disable != 0)
                                                rate_skill_list.Add(new int[] { sk_id, i, j, score_height + skill_grade });
                                            if (bp.debuff != 0)
                                                rate_skill_list.Add(new int[] { sk_id, i, j, score_meduim2 + skill_grade });
                                        }
                                        else
                                        {
                                            if (bp.getHiding() || buff_ugly.Contains(bp.buff_type) || buff_ugly.Contains(bp.aura_type))
                                            {
                                                rate_skill_list.Add(new int[] { sk_id, i, j, score_height + skill_grade });
                                            }
                                            else if (bp.buff != 0)
                                            {
                                                rate_skill_list.Add(new int[] { sk_id, i, j, score_meduim2 + skill_grade });
                                            }
                                        }
                                    }
                                    else if (!bp.getHiding() && !buff_ugly.Contains(bp.buff_type) && !buff_ugly.Contains(bp.aura_type))
                                    {
                                        switch (skill_type)
                                        {
                                            case 1:
                                            case 2:
                                                {
                                                    bool sk_mag_dest_13003 = skillInfo.unk17 == 0 && bp.buff_type != 13003; //
                                                    //if (buff_ugly.Contains(bp.buff_type) || buff_ugly.Contains(bp.aura_type) || (skillInfo.unk17 == 2 && bp.buff_type == 13003))
                                                    //    rate_skill_list.Add(new int[] { sk_id, i, j, 1 });
                                                    //else
                                                    //    rate_skill_list.Add(new int[] { sk_id, i, j, 50 });
                                                    if (!buff_ugly.Contains(bp.buff_type) || !buff_ugly.Contains(bp.aura_type))
                                                        rate_skill_list.Add(new int[] { sk_id, i, j, sk_id == 10000 ? score_low : (score_meduim + skill_grade) });
                                                    break;
                                                }
                                            case 3://disable
                                                {
                                                    //ถ้าเป้าหมายยังไม่มี disable และไม่มีกระจกศักดิ์สิทธิ์
                                                    if (bp.disable == 0 && bp.buff_type != 10026)
                                                        rate_skill_list.Add(new int[] { sk_id, i, j, score_height2 + skill_grade });
                                                    break;
                                                }
                                            case 4://buff
                                                {
                                                    if (bp.buff == 0)
                                                        rate_skill_list.Add(new int[] { sk_id, i, j, score_meduim2 + skill_grade });
                                                    break;
                                                }
                                            //case 5: //สลายสภาวะ ทำไว้ข้างบนแล้วเพราะต้องสามารถสลายซ่อนได้ด้วย
                                            //case 6: // คืนมารไม่ต้องแอด
                                            case 7: //รักษาบาดเจ็บ | สุดยอดการรักษา
                                                {
                                                    goto case 14;
                                                }
                                            //case 9: //วิชาแบ่งร่าง + ซ่อนกายสายลม
                                            //case 10: //ป้องกัน
                                            //case 11: // จับศัตตรู
                                            //case 12: // run
                                            //case 13: //item, later
                                            case 14: //ฮิว HP/SP
                                                {
                                                    rate_skill_list.Add(new int[] { sk_id, i, j, roundCount }); //ยิ่งหลายเทินยิ่งมีโอกาสฮิล
                                                    break;
                                                }
                                            case 15://debuff
                                                {
                                                    if (bp.debuff == 0)
                                                        rate_skill_list.Add(new int[] { sk_id, i, j, score_meduim2 + skill_grade });
                                                    break;
                                                }
                                            case 16: //สลายคุ้มกัน | สลายกระจก
                                                {
                                                    ushort sk_host = (ushort)(sk_id + 1);
                                                    if (i >= 2 && bp.buff_type == sk_host)
                                                    {
                                                        rate_skill_list.Add(new int[] { sk_id, i, j, score_height + skill_grade });
                                                    }
                                                    break;
                                                }
                                            //case 17: //คุ้มครองนาย | สลายคุ้มครอง
                                            //case 18: //อัญเชิญวารี | สลายสภาวะสี่คน | สลายสภาวะ6คน ทำไว้ข้างบนแล้วเพราะต้องสามารถสลายซ่อนได้ด้วย
                                            case 19://ออร่า
                                                {
                                                    if (bp.aura != 0)
                                                        rate_skill_list.Add(new int[] { sk_id, i, j, score_meduim2 + skill_grade });
                                                    break;
                                                }
                                            default:
                                                {
                                                    rate_skill_list.Add(new int[] { sk_id, i, j, score_low });
                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        ushort item_clear = 25027;
                                        if (rate_skill_list.Where(e => e[0] == item_clear && e[1] == i && e[2] == j).Count() == 0)
                                            if (i < 2 && (bp.disable != 0 || bp.debuff != 0))
                                                rate_skill_list.Add(new int[] { item_clear, i, j, score_low, 1 }); //ใช้ยันต์สลายสภาวะ
                                            else if (i > 1 && (bp.buff != 0 || bp.aura != 0))
                                                rate_skill_list.Add(new int[] { item_clear, i, j, score_low, 1 }); //ใช้ยันต์สลายสภาวะ
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //Console.WriteLine("rate_skill_list: {0}", rate_skill_list.Count);
            if (rate_skill_list.Count == 0)
            {
                //Logger.Warning(row+"\t"+col+"\trate_skill_list.Count == 0");
                dest_row = row;
                dest_col = col;
                skill = 17001; //ป้องกัน
            }
            else
            {
                int total = rate_skill_list.Sum(el => el[3]);
                int formular = 0;
                int rnd = randomize.getInt(0, total);
                //Console.WriteLine("rnd {0}/{1}", rnd, total);
                bool chk = false;
                foreach (int[] pos in rate_skill_list)
                {

                    //Console.WriteLine("{0}{1}", sk_name, string.Join("\t", pos));
                    formular += pos[3];
                    if (!chk && formular >= rnd)
                    {
                        skill = pos[0];
                        dest_row = (byte)pos[1];
                        dest_col = (byte)pos[2];
                        if (pos.Length == 5) //แสดงว่าใช้ยัน
                        {
                            type_sk = (byte)pos[4];
                        }
                        chk = true;
                        break;
                        //Console.Write("> ");
                    }
                }
            }

            //ถ้าเกิน 4 แถว หรือ 5 คอลัมน์ ให้กดกันซะ
            if (dest_row > 3 || dest_col > 4)
            {
                Logger.Warning("BT flood row:" + dest_row + ", col:" + dest_col);
                dest_row = row;
                dest_col = col;
                skill = 17001; //ป้องกัน 
            }

            if (skill == 10016 || skill == 11016 || skill == 12016 || skill == 13015) //NPC trieu goi lvl 10 อัญเชิญ 4 ธาตุ
                skill += 3;

            //สหรับทดสอบฟิกให้มอนยิงสกิล
            {
                //dest_row = row;
                //dest_col = col;
                //type_sk = 0;
                //skill = 13005;
            }


            pushCommand(row, col, dest_row, dest_col, type_sk, (ushort)skill);
            //Console.WriteLine("seekTarget {0}\tpos[{1}][{2}]", skill, dest_row, dest_col);
        }
        public void seekTargetV3(byte row, byte col)
        {
            byte dest_row = 4;
            byte dest_col = 5;
            ushort skill = 10000;
            ushort yun_id = 25027; //talisman ยันสลายสภาวะ
            byte type_sk = 0;//0 = skill, 1 = use item
            byte[] side_npc = new byte[] { 4, 6, 7, 8, 14, 19 };//4 บัพ, 6 ฮิล SP, 7 ฮิล HP, 14 ฮิล HP/SP, 19 ออร่า แสดงว่าต้องฝั่งเมอนเท่านั้น
            byte[] side_both = new byte[] { 5, 18, 99 };//5 สลาย, 18 อัญเชิญ, แสดงว่าใส่ได้ทั้ง 2 ฝ่าย

            BattleParticipant init = position[row][col];

            ushort id = (ushort)position[row][col].npc.npcid;
            NpcInfo npcInfo = NpcData.npcList[id];
            List<ushort> skill_list = new List<ushort>();
            skill_list.Add(10000); //มือเปล่า
            skill_list.Add(yun_id); //ยันต์สลาย
            if (npcInfo.skill1 > 0) skill_list.Add(npcInfo.skill1);
            if (npcInfo.skill2 > 0) skill_list.Add(npcInfo.skill2);
            if (npcInfo.skill3 > 0) skill_list.Add(npcInfo.skill3);
            if (npcInfo.skill4 > 0) skill_list.Add(npcInfo.skill4);

            //Console.WriteLine("{0}", string.Join(", ", skill_list));

            int ai_level = TSServer.config.npc_ai_level;
            int score_base = 100;
            int score_low = score_base / 2;
            int score_meduim = score_base * Math.Max(1, (ai_level / 2));
            int score_meduim2 = (int)(score_meduim * 1.2);
            int score_height = score_base * ai_level;
            int score_height2 = (int)(score_height * 1.2);
            ushort[] buff_ugly = new ushort[] {
                10015, //กระจก
                10031, //ระฆังทองคุ้มกาย
                12024, //ตะวันคุ้มกาย
                13021, //เคลื่อนดาวย้ายดารา
                14044, //เกราะเทพเบื้องบน
                14046, //เกราะคุ้มลมปราณ
                10010, //ม่านคุ้มกัน
            };

            //Dictionary<byte, List<BattleParticipant>> post_list = new Dictionary<byte, List<BattleParticipant>>();
            List<Tuple<BattleParticipant, ushort, int, byte>> dest_list = new List<Tuple<BattleParticipant, ushort, int, byte>>();
            foreach (ushort skill_id in skill_list)
            {
                byte effect = 1;
                byte r = 2;
                byte nb_row = 2;
                if (skill_id == yun_id)
                {
                    List<BattleParticipant> bps = new List<BattleParticipant>();
                    for (int i = 0; i < 4; i++)
                    {
                        Tuple<BattleParticipant, ushort, int, byte> added;
                        //ฝั่งคนมีบัพที่มอนตีไม่ได้
                        bps = position[i].Where(e => e.row >= 2 && (e.getHiding() || buff_ugly.Contains(e.buff_type) || buff_ugly.Contains(e.aura_type))).ToList();
                        added = seekTargetV3AddScore(bps, skill_id, 10, 1);
                        if (bps.Count > 0 && added != null)
                            dest_list.Add(added);

                        //ฝั่งมอนติด disable
                        bps = position[i].Where(e => e.row < 2 && e.disable > 0).ToList();
                        added = seekTargetV3AddScore(bps, skill_id, 10, 1);
                        if (bps.Count > 0 && added != null)
                            dest_list.Add(added);
                    }

                }
                else if (SkillData.skillList.ContainsKey(skill_id))
                {
                    SkillInfo skillInfo = SkillData.skillList[skill_id];
                    if (skill_id >= 13016 && skill_id <= 13018) { effect = 3; }//เชิญมังกร;
                    else if (skill_id >= 10017 && skill_id <= 10019) { effect = 15; } //เชิญหินผา;
                    else if (skill_id >= 11017 && skill_id <= 11019) effect = 18; //เชิญวารี;
                    //else if (sk_id >= 12017 && sk_id <= 12019) c.dmg = Math.Min((ushort)(c.dmg * (c.skill_lvl / 3)), (ushort)50000); //boost dmg phoenix
                    else if (skill_id >= 20001 && skill_id <= 20003) effect = 20; //ต้นไม้ดูดเลือด พิษเสียเลือด สะท้อนบาดเจ็บ
                    else effect = skillInfo.skill_type;

                    if (side_npc.Contains(effect)) r = 0;
                    if (side_both.Contains(effect)) { nb_row = 4; r = 0; }

                    byte skill_grade = skillInfo.grade;

                    for (byte i = r; i < (r + nb_row); i++)
                    {
                        Tuple<BattleParticipant, ushort, int, byte> added;
                        //ชุบ
                        if (effect == 8)
                        {
                            List<BattleParticipant> bps = position[i].Where(e => e.exist && e.death).ToList();
                            //Console.WriteLine("{0} มีตัวที่นาย {1}", i, bps.Count);
                            if (bps.Count > 0)
                            {
                                added = seekTargetV3AddScore(bps, skill_id, score_height2);
                                if (added != null)
                                    dest_list.Add(added);
                            }
                        }
                        else
                        {
                            IEnumerable<BattleParticipant> bp_alive = position[i].Where(e => e.exist && !e.death);
                            if (bp_alive.Count() > 0)
                            {
                                List<BattleParticipant> bps = new List<BattleParticipant>();
                                //giai tru สลายสภาวะ, อัญเชิญ
                                if (side_both.Contains(effect))
                                {
                                    //ถ้าฝ่ายมอนติด disable
                                    bps = bp_alive.Where(e => e.row < 2 && e.disable != 0).ToList();
                                    if (bps.Count > 0)
                                    {
                                        added = seekTargetV3AddScore(bps, skill_id, score_height2);
                                        if (added != null)
                                            dest_list.Add(added);
                                    }

                                    //ถ้าฝ่ายมอนติด debuff
                                    bps = bp_alive.Where(e => e.row < 2 && e.debuff != 0).ToList();
                                    if (bps.Count > 0)
                                    {
                                        added = seekTargetV3AddScore(bps, skill_id, score_height);
                                        if (added != null)
                                            dest_list.Add(added);
                                    }


                                    //ถ้าฝ่ายคนติด buff
                                    bps = bp_alive.Where(e => e.row >= 2 && e.buff != 0).ToList();
                                    if (bps.Count > 0)
                                    {
                                        added = seekTargetV3AddScore(bps, skill_id, score_height2);
                                        if (added != null)
                                            dest_list.Add(added);
                                    }

                                    //ถ้าฝ่ายคนติด aura
                                    bps = bp_alive.Where(e => e.row >= 2 && e.aura != 0).ToList();
                                    if (bps.Count > 0)
                                    {
                                        added = seekTargetV3AddScore(bps, skill_id, score_height);
                                        if (added != null)
                                            dest_list.Add(added);
                                    }
                                }
                                else
                                {
                                    //List<BattleParticipant> player_hiding = bp_alive.Where(e => e.row >= 2 && e.getHiding()).ToList();
                                    switch (effect)
                                    {
                                        case 1: goto case 2;
                                        case 2: //Attact
                                            {
                                                bps = bp_alive.Where(e => !e.getHiding() && !buff_ugly.Contains(e.buff_type) && !buff_ugly.Contains(e.aura_type)).ToList();
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, skill_id == 10000 ? score_low : score_meduim + skill_grade);
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                        case 3: //disable skills ปิศาจต้นไม้ | น้ำแข็งปิดคลุม | ลมสลาตัน | หลับไหล | มังกรเขียว | ปิศาจต้นไม้3คน | น้ำแข็งปิดคลุม3คน | ลมสลาตัน3คน | ฝ่ามือยูไล
                                            {
                                                int boost = SkillData.skillSummonWind.Contains(skill_id) || skillInfo.nb_target > 1 ? 2 : 1; //เพิ่มความกวนตีน
                                                bps = bp_alive.Where(e => !e.getHiding() && e.disable == 0).ToList();
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, (score_height2 * boost));
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                        case 4://buff skills ม่านคุ้มกัน | กระจก | กำแพงน้ำแข็ง | หลบหลีก | วิชาซ่อนร่าง | วิชาขยายใหญ่ | พลังปราณ | ร่วมใจ | ปลุกใจ | กระจกศักดิ์สิทธิ์ | ตะวันคุ้มกาย | เคลื่อนดาวย้ายดารา | ไร้รูปไร้ลักษณ์ | พลังปราณ10คน | ระฆังทองคุ้มกาย
                                            {
                                                bps = bp_alive.Where(e => e.buff == 0).ToList();
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, score_height2);
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                        //case 5: //สลาย ไปทำที่ if ((skill_type == 5 || skill_type == 18)) ไว้แล้ว
                                        //case 6: //hoi SP คืนมาร ไม่ต้องทำ
                                        case 7: //hoi hp //รักษาบาดเจ็บ | สุดยอดการรักษา
                                            goto case 14;
                                        //case 8: //ressurrection วิชาฟื้นคืนชีพ
                                        //case 9: //phan than //วิชาแบ่งร่าง + ซ่อนกายสายลม
                                        //case 10: //ป้องกัน
                                        //case 11: // tha luoi -_- จับศัตตรู จับศัตรู | ตาข่ายจับศัตรู | จับศัตรูสำเร็จ | ก้อนข้าวจับตาข่าย | ก้อนข้าวจับสำเร็จ
                                        //case 12: // run
                                        //case 13: //item, later
                                        case 14: //ฮิว HP/SP วารีคืนพลัง | มือทิพย์คืนพลัง | น้ำค้างเซียน
                                            {
                                                bps = bp_alive.Where(e => e.getHp() < (e.getMaxHp() / 2)).ToList(); //เลือดน้อยกว่า 50% ถึงจะให้ฮิล
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, score_meduim);
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                        case 15: //debuff วิชาย่อเล็ก | ปล่อยพิษ | รังสับวุ่น | สับสนวุ่นวาย | จ้าวหินผา | กระสุนอุนจิ | ค่ายกลดินทะลาย | สี่คนสับสน | เต่าพลังเร็ว | ปล่อยพิษ4คน | ลมพิษภูตวิบัติ | ผีดูดพลัง | คุณธรรมไร้พ่าย
                                            {
                                                int boost = SkillData.skillSummonEarth.Contains(skill_id) || skillInfo.nb_target > 1 ? 3 : 1; //เพิ่มความกวนตีน
                                                bps = bp_alive.Where(e => !e.getHiding() && e.debuff == 0).ToList();
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, (score_meduim * boost));
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                        case 16: //สลายคุ้มกัน | สลายกระจก
                                            {
                                                bps = bp_alive.Where(e => !e.getHiding() && e.buff_type == skill || e.aura == skill).ToList();
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, score_meduim2);
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                        //case 17: //คุ้มครองนาย | สลายคุ้มครอง
                                        //case 18: //อัญเชิญวารี | สลายสภาวะสี่คน | สลายสภาวะ6คน
                                        case 19: //ออร่า คาถาแผ่นดินไหว | คาถาใจวารี | กระจกใสกลางน้ำ | เวทย์ตะวันเพลิง | วิญญาณกระหายรบ | เวทย์ลมทรนง | จิตรบแห่งลม | แผนการทลายศึก | เกราะเทพเบื้องบน | เกราะคุ้มลมปราณ | พลังเทพพิชิตศัตรู | สายธารแห่งชีวิต | คุณธรรมไร้พ่าย
                                            {
                                                bps = bp_alive.Where(e => e.aura == 0).ToList();
                                                if (bps.Count > 0)
                                                {
                                                    added = seekTargetV3AddScore(bps, skill_id, score_height);
                                                    if (added != null)
                                                        dest_list.Add(added);
                                                }
                                                break;
                                            }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    //จริงๆ ไม่มีโอกาสเข้านี่นะ แต่เขียนดักไว้ก่อน
                    dest_row = row;
                    dest_col = col;
                    skill = 17001; //ป้องกัน
                }
            }



            if (dest_list != null && dest_list.Count > 0)
            {
                int formular = 0;
                int max = dest_list.Sum(e => e.Item3);
                int rnd = randomize.getInt(0, max);
                bool chk = true;
                //Console.WriteLine("ai = {0}\trnd = {1}", ai_level, rnd);
                foreach (Tuple<BattleParticipant, ushort, int, byte> b in dest_list)
                {
                    //SkillInfo info = SkillData.skillList[b.Item2];
                    //string sk_name = Encoding.Default.GetString(info.name, 0, info.namelength);

                    formular += b.Item3;
                    if (formular >= rnd && chk)
                    {
                        dest_row = b.Item1.row;
                        dest_col = b.Item1.col;
                        skill = b.Item2;
                        type_sk = b.Item4;
                        chk = false;
                        break;
                        //Console.Write("> ");
                    }

                    //Console.WriteLine("[{0}][{1}] {2} {3}\t[{4}][{5}] = {6}", init.row, init.col, b.Item2, sk_name, b.Item1.row, b.Item1.col, b.Item3);

                }
            }

            //ถ้าเกิน 4 แถว หรือ 5 คอลัมน์ ให้กดกันซะ
            if (dest_row > 3 || dest_col > 4)
            {
                BattleParticipant bp_test = position[3][2];
                string bp_id = "";
                if (bp_test.type == TSConstants.BT_POS_TYPE_CHR && bp_test.chr != null)
                    bp_id = bp_test.chr.client.accID + " ";
                Logger.Error(DateTime.Now + " " + bp_id + "BT flood row:" + dest_row + ", col:" + dest_col);

                dest_row = row;
                dest_col = col;
                skill = 17001; //ป้องกัน 
            }

            if (skill == 10016 || skill == 11016 || skill == 12016 || skill == 13015) //NPC trieu goi lvl 10 อัญเชิญ 4 ธาตุ
                skill += 3;

            //สหรับทดสอบฟิกให้มอนยิงสกิล
            {
                //dest_row = row;
                //dest_col = col;
                //type_sk = 0;
                //skill = 13005;
            }


            pushCommand(row, col, dest_row, dest_col, type_sk, (ushort)skill);
        }
        /// <summary>
        /// Adding Positon
        /// </summary>
        /// <param name="bp_list">BattleParticipant list can hit skill</param>
        /// <param name="skill">Skill ID</param>
        /// <param name="score">Score</param>
        /// <param name="type_use_item">0 = skill; 1 = use item;</param>
        /// <returns></returns>
        private Tuple<BattleParticipant, ushort, int, byte> seekTargetV3AddScore(List<BattleParticipant> bp_list, ushort skill, int score, byte type_use_item = 0)
        {
            if (bp_list != null && bp_list.Count > 0)
            {
                int rnd = randomize.getInt(0, bp_list.Count);
                return new Tuple<BattleParticipant, ushort, int, byte>(bp_list[rnd], skill, score, type_use_item);
            }
            return null;
        }
        private int[] skillBattleFilter(ushort skill_id, NpcInfo npcInfo)
        {
            int npc_line = npcInfo.atk > npcInfo.mag ? 1 : 2;
            ushort[] npc_summon = new ushort[] { 10016 };
            if (SkillData.skillList.ContainsKey(skill_id))
            {
                SkillInfo info = SkillData.skillList[skill_id];
                double mul = npc_line == info.unk17 ? 1.2 : 1; //ให้ออกสกิลตรงสายได้ง่ายกว่า
                int grade = info.grade;
                if (info.unk12 == 245) //อัญเชิญของมอน
                {
                    grade = 15; //สกิลทั่วไปเกรดสูงๆมันก็ประมาณ 15 16 แหละ
                    mul = 1.2;
                }
                int final_grade = (int)Math.Floor(grade * mul);
                //Console.WriteLine("{0} {1} * {2} = {3}", Encoding.Default.GetString(info.name, 0, info.namelength), grade, mul, final_grade);
                return new int[] { skill_id, info.skill_type, final_grade };
            }
            return new int[] { skill_id, 0, 0};
        }

        //private int seekSkill(List<int[]> skill_list)
        //{
        //    int rndmax = 0;
        //    for (int i = 0; i < skill_list.Count; i++)
        //    {
        //        skill_list[i][1] = (i + 1) * 100 * (i + 1);
        //        rndmax = skill_list[i][1];
        //    }
        //    int rnd = randomize.getInt(0, rndmax);
        //    for (int i = 0; i < skill_list.Count; i++)
        //    {
        //        if (rnd < skill_list[i][1])
        //        {
        //            //return skill_list[i][0];
        //            ushort result_sk = (ushort)skill_list[i][0];
        //            if (!SkillData.skillList.ContainsKey(result_sk))
        //                result_sk = 10000;
        //            return result_sk;
        //        }
        //    }
        //    return 10000;
        //}
        private int seekSkill(List<int[]> skill_list) //[id, skill_type, grade with added]
        {
            int total_grade = skill_list.Sum(x => x[2]);
            int rnd = randomize.getInt(0, (total_grade + 1));

            //Console.WriteLine("{0}\t{1}", total_grade, rnd);
            int sum_grade = 0;
            for(int i = 0; i < skill_list.Count; i++)
            {
                int skill_id = skill_list[i][0];
                int skill_type = skill_list[i][1];
                int skill_grade = skill_list[i][2];
                sum_grade += skill_grade;

                if (sum_grade >= rnd)
                {
                    //Console.WriteLine("sum_grade {0}\t{1}", sum_grade, skill_id);
                    return skill_id;
                }
            }
            return 10000;
        }
        //public override void checkDeath(byte row, byte col, BattleCommand c)
        //{
        //    if (position[row][col].getHp() <= 0)
        //    {
        //        BattleParticipant bp = position[row][col];
        //        if (!bp.death)
        //        {
        //            if (row < 2)
        //            {
        //                countEnemy--;
        //                bp.npc.drop = bp.npc.generateDrop();
        //                if (c.init_row >= 2) bp.npc.killer.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
        //                if (countEnemy == 0) finish = position[3][2].death ? 2 : 1;
        //            }
        //            else
        //            {
        //                countAlly--;
        //                if (countAlly == 0) finish = 2;
        //            }
        //            bp.death = true;
        //            bp.purge_type = 3;
        //            //bp.purge_status();
        //        }
        //        else if ((row < 2) && (c.init_row >= 2)) bp.npc.killer.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
        //    }
        //    else if ((row < 2) && (c.init_row >= 2)) position[row][col].npc.attacker.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
        //}
        //public override void checkDeath(byte row, byte col, BattleCommand c)
        //{
        //    if (position[row][col].getHp() <= 0)
        //    {
        //        BattleParticipant bp = position[row][col];
        //        if (!bp.death)//ตัวนี้สถานะยังไม่ตาย แต่เลือดหมดแล้ว แสดงว่าตอนนี้โดนตีตาย
        //        {
        //            if (row < 2 && bp.npc != null)
        //            {
        //                countEnemy--;
        //                bp.npc.drop = bp.npc == null ? (ushort)0 : bp.npc.generateDrop();
        //                //if (c.init_row >= 2) bp.npc.killer.Add(new Tuple<byte, byte>(c.init_row, c.init_col)); //ของเดิม
        //                if (c.init_row >= 2)
        //                {
        //                    //Logger.Error("Combo count " + string.Join(", ", comboList));
        //                    if (comboList.Count > 0)
        //                        foreach (Tuple<byte, byte> comb in comboList.ToList())
        //                        {
        //                            if (!bp.npc.killer.Contains(comb))
        //                                bp.npc.killer.Add(comb);
        //                        }
        //                    else
        //                        bp.npc.killer.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
        //                }
        //                if (countEnemy == 0) finish = position[3][2].death ? 2 : 1;
        //            }
        //            else
        //            {
        //                countAlly--;
        //                if (countAlly == 0) finish = 2;
        //            }
        //            bp.death = true;
        //            bp.purge_type = 3;
        //            bp.purge_status();
        //        }
        //        //else if ((row < 2) && (c.init_row >= 2)) bp.npc.killer.Add(new Tuple<byte, byte>(c.init_row, c.init_col)); //ของเดิม
        //        else if (row < 2 && bp.npc != null && c.init_row >= 2)
        //        {
        //            if (comboList.Count > 0)
        //            {
        //                //Logger.Debug("Combo count " + comboList.Count);
        //                foreach (Tuple<byte, byte> comb in comboList.ToList())
        //                {
        //                    if (!bp.npc.killer.Contains(comb))
        //                        bp.npc.killer.Add(comb);
        //                }
        //            }
        //            else
        //                bp.npc.killer.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
        //        }
        //    }
        //    //else if ((row < 2) && (c.init_row >= 2)) position[row][col].npc.attacker.Add(new Tuple<byte, byte>(c.init_row, c.init_col)); //ของเดิม
        //    else if (row < 2 && position[row][col].npc != null && c.init_row >= 2)
        //    {
        //        BattleParticipant bp = position[row][col];
        //        if (comboList.Count > 0)
        //        {
        //            //Logger.Debug("Combo count " + comboList.Count);
        //            foreach (Tuple<byte, byte> comb in comboList.ToList())
        //            {
        //                //Logger.SystemWarning("[" + row + "][" + col + "] attack by [" + comb.Item1 + "][" + comb.Item2 + "]");
        //                if (!bp.npc.attacker.Contains(comb))
        //                    bp.npc.attacker.Add(comb);
        //            }
        //        }
        //        else
        //            bp.npc.attacker.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
        //    }
        //}
        public override void checkDeath(byte row, byte col)
        {
            //Console.WriteLine("checkDeath[{0}][{1}]", row, col);
            BattleParticipant bp = position[row][col];
            //Console.WriteLine("attacker {0}", comboList.Count);

            bool is_npc = row >= 0 && row < 2 && bp !=null && bp.type == TSConstants.BT_POS_TYPE_NPC && bp.npc != null;

            //Console.WriteLine("bp.getHp() {0}\tbp.death {1}", bp.getHp(), bp.death);
            //ตัวนี้สถานะยังไม่ตาย แต่เลือดหมดแล้ว แสดงว่าตอนนี้โดนตีตาย
            if (bp.getHp() <= 0 && !bp.death)
            {

                bp.death = true;

                int countAlly5 = 0;
                int countEnemy5 = 0;

                try
                {
                    for (byte i = 0; i < 4; i++)
                        for (byte j = 0; j < 5; j++)
                        {
                            if (position[i][j].exist && !position[i][j].death)
                            {
                                if (i < 2)
                                    countEnemy5++;
                                else countAlly5++;
                            }
                        }
                }
                catch (Exception e)
                {

                }

                //is npc
                if (is_npc)
                {
                    //countEnemy--;
                    bp.npc.drop = bp.npc == null ? (ushort)0 : bp.npc.generateDrop();
                    foreach (Tuple<byte, byte> comb in comboList.ToList())
                    {
                        if (comb != null && comb.Item1 >= 2 && comb.Item2 <= 4 && !bp.npc.killer.Contains(comb))
                        {
                            //Console.WriteLine("checkDeath [{0}][{1}] killer is [{2}][{3}]\t{4}", row, col, comb.Item1, comb.Item2, position[comb.Item1][comb.Item2].getAgi());
                            bp.npc.killer.Add(comb);
                        }
                    }
                    //Console.WriteLine("countEnemy {0}", countEnemy);
                    if (countEnemy5 <= 0)
                        finish = 1;
                }
                else
                {
                    //countAlly--;
                    if (countAlly5 == 0)
                    {
                        finish = 2;
                    }
                    //if (position[3][2].death) finish = 2;
                }
                //bp.death = true;
                bp.purge_type = 3;
                bp.purge_status(); //checkDeath
            }
            else
            {
                //is npc
                if (is_npc)
                {
                    foreach (Tuple<byte, byte> comb in comboList.ToList())
                    {
                        //Logger.SystemWarning("[" + row + "][" + col + "] attack by [" + comb.Item1 + "][" + comb.Item2 + "]");
                        if (comb != null && comb.Item1 >= 2 && comb.Item2 <= 3 && !bp.npc.attacker.Contains(comb))
                            bp.npc.attacker.Add(comb);
                    }
                }
            }
        }

        public void giveDrop(ushort itemid, byte init_row, byte init_col, byte dest_row, byte dest_col)
        {
            BattleParticipant init = position[init_row][init_col];
            BattleParticipant dest = position[dest_row][dest_col];
            ushort map_id = 0;
            if (dest.type == TSConstants.BT_POS_TYPE_CHR)
                map_id = dest.chr.mapID;
            if(dest.type == TSConstants.BT_POS_TYPE_PET)
                map_id = dest.pet.owner.mapID;
            if (TSServer.config.map_no_drop.Contains(map_id))
                return;

            PacketCreator p = new PacketCreator(0x35, 0x04);
            p.add16(itemid);
            p.add8(init_row); p.add8(init_col);
            p.add8(dest_row); p.add8(dest_col);

            NpcInfo npcInfo = init.npc.getNpcInfo();
            bool init_type_ore = init.npc != null && npcInfo.type == 16; //npc แร่
            ushort sk_mining_id = 14010;
            if (dest.type == TSConstants.BT_POS_TYPE_CHR)
            {
                byte sk_mining_lv = dest.chr.skill.ContainsKey(sk_mining_id) ? dest.chr.skill[sk_mining_id] : (byte)0; //Level skill วิชาขุดแร่
                bool allow_drop = true;
                if (init_type_ore && !checkDropOre(npcInfo, sk_mining_lv, itemid))
                {
                    allow_drop = false;
                }

                if (allow_drop)
                {
                    ushort amount = 1;
                    bool has_autobox_drop = dest.autoBoxCanCrystal(TSAutoBox.CRYSTAL_DROP);
                    if (has_autobox_drop)
                    {
                        amount = 2;
                        dest.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_DROP);
                    }
                    dest.chr.inventory.addItem(itemid, amount, true, null, "BattleNPC Drop");
                    battleBroadcast(p.send());
                }
            }
            else if (dest.type == TSConstants.BT_POS_TYPE_PET)
            {
                byte sk_mining_lv = dest.pet.owner.skill.ContainsKey(sk_mining_id) ? dest.pet.owner.skill[sk_mining_id] : (byte)0; //Level skill วิชาขุดแร่
                bool allow_drop = true;
                if (init_type_ore && !checkDropOre(npcInfo, sk_mining_lv, itemid))
                {
                    allow_drop = false;
                }

                if (allow_drop)
                {
                    ushort amount = 1;
                    bool has_autobox_drop = dest.autoBoxCanCrystal(TSAutoBox.CRYSTAL_DROP);
                    if (has_autobox_drop)
                    {
                        amount = 2;
                        dest.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_DROP);
                    }
                    dest.pet.owner.inventory.addItem(itemid, amount, true, null, "BattleNPC Drop");
                    battleBroadcast(p.send());
                }
            }
        }
        private bool checkDropOre(NpcInfo npcInfo, byte sk_mining_lv, ushort itemid)
        {
            if (itemid == npcInfo.drop5) return sk_mining_lv > 8;
            else if (itemid == npcInfo.drop4) return sk_mining_lv > 6;
            else if (itemid == npcInfo.drop3) return sk_mining_lv > 4;
            else if (itemid == npcInfo.drop2) return sk_mining_lv > 2;
            else if (itemid == npcInfo.drop1) return sk_mining_lv >= 0;
            else return itemid > 0;
        }

        public void giveMoral(BattleParticipant bp_ally, BattleParticipant bp_enemy)
        {
            TSCharacter chr = null;
            if (bp_ally.type == TSConstants.BT_POS_TYPE_CHR)
                chr = bp_ally.chr;
            else if (bp_ally.type == TSConstants.BT_POS_TYPE_PET)
                chr = bp_ally.pet.owner;

            if (chr != null && bp_enemy.type == TSConstants.BT_POS_TYPE_NPC)
            {
                ushort npc_id = bp_enemy.npc.npcid;
                if (NpcData.npcList.ContainsKey(npc_id))
                {
                    NpcInfo npcInfo = NpcData.npcList[npc_id];
                    if (npcInfo.doanhtrai >= 1 && npcInfo.doanhtrai <= 5)
                    {
                        ushort value = chr.moral[npcInfo.doanhtrai - 1];
                        value = (ushort)Math.Max((value - 1), 0);
                        chr.moral[npcInfo.doanhtrai - 1] = value;
                        chr.refreshMoral();
                    }
                }
            }
        }

        //public override void endBattle(bool win)
        //{
        //    //Logger.Warning("endBattle " + win);
        //    roundCount -= 1;
        //    //Logger.SystemWarning("endBattle win: "+win);
        //    int[,] exp_gain = new int[2, 5];
        //    int[,] exp_soul_gain = new int[2, 5];
        //    //int exp_rate = 10000;
        //    int exp_rate = TSServer.config.per_exp;
        //    int exp_soul_rate = TSServer.config.per_exp_soul;
        //    //Logger.Error("TSBattleNPC endBattle :" + win);
        //    System.Threading.Thread.Sleep(100);

        //    for (int i = 0; i < 4; i++)
        //    {
        //        for (int j = 0; j < 5; j++)
        //        {
        //            BattleParticipant bp = position[i][j];
        //            if (bp.exist && bp.type == TSConstants.BT_POS_TYPE_NPC)
        //            {
        //                if (battle_type == 3 && win)
        //                {
        //                    foreach (Tuple<byte, byte> k in bp.npc.attacker)
        //                    {
        //                        if (
        //                            k.Item1 > 1
        //                            && k.Item1 < 4
        //                            && k.Item2 >= 0
        //                            && k.Item2 < 5
        //                            && position[k.Item1][k.Item2].exist
        //                            && position[i][j].npc.getNpcInfo().type != 16 //แร่
        //                            && (position[k.Item1][k.Item2].getLvl() - position[i][j].npc.level) < 30
        //                            && (position[i][j].npc.level - position[k.Item1][k.Item2].getLvl()) < 30
        //                        )
        //                            exp_gain[k.Item1 - 2, k.Item2] += position[i][j].npc.level * exp_rate / 4;
        //                    }
        //                    foreach (Tuple<byte, byte> k in bp.npc.killer)
        //                    {
        //                        //Console.WriteLine("Killer: [{0}][{1}]", k.Item1, k.Item2);
        //                        if (
        //                            k.Item1 > 1
        //                            && k.Item1 < 4
        //                            && k.Item2 >= 0
        //                            && k.Item2 < 5
        //                            && position[k.Item1][k.Item2].exist
        //                            && position[i][j].npc.getNpcInfo().type != 16 //แร่
        //                        )
        //                        {
        //                            if (
        //                                (position[k.Item1][k.Item2].getLvl() - position[i][j].npc.level) < 30
        //                                && (position[i][j].npc.level - position[k.Item1][k.Item2].getLvl()) < 30
        //                            )
        //                                exp_gain[k.Item1 - 2, k.Item2] += position[i][j].npc.level * exp_rate;

        //                            BattleParticipant bp_killer = position[k.Item1][k.Item2];
        //                            for (int e = 0; e < 6; e++)
        //                            {
        //                                switch (bp_killer.type)
        //                                {
        //                                    case TSConstants.BT_POS_TYPE_CHR:
        //                                        {
        //                                            if (
        //                                                bp_killer.chr != null &&
        //                                                bp_killer.chr.equipment[e] != null &&
        //                                                bp_killer.chr.equipment[e].canSoul &&
        //                                                bp.npc.level >= bp_killer.chr.equipment[e].info.level
        //                                            )
        //                                            {
        //                                                bp_killer.chr.equipment[e].battleExp += exp_soul_rate;
        //                                            }
        //                                            break;
        //                                        }
        //                                    case TSConstants.BT_POS_TYPE_PET:
        //                                        {
        //                                            if (
        //                                                bp_killer.pet != null &&
        //                                                bp_killer.pet.equipment[e] != null &&
        //                                                bp_killer.pet.equipment[e].canSoul &&
        //                                                bp.npc.level >= bp_killer.pet.equipment[e].info.level
        //                                            )
        //                                            {
        //                                                bp_killer.pet.equipment[e].battleExp += exp_soul_rate;
        //                                            }
        //                                            break;
        //                                        }
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }

        //            //if (bp.exist && bp.type == TSConstants.BT_POS_TYPE_CHR)
        //            //{
        //            //    TSCharacter c = position[i][j].chr;
        //            //    if (!c.isJoinedTeam() || c.isTeamLeader())
        //            //    {
        //            //        // Clear battle smoke
        //            //        var p = new PacketCreator(0x0B);
        //            //        p.add8(0); p.add32(c.client.accID); p.add16(0);
        //            //        c.replyToMap(p.send(), false);
        //            //    }

        //            //    ///
        //            //    //checkPetDeath(c);
        //            //}

        //            if (i > 1 && bp.exist)
        //            {
        //                battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, (byte)i, (byte)j, 0 }).send());
        //            }
        //        }
        //    }

        //    for (int i = 0; i < 5; i++)
        //    {
        //        int addMultiply = 0;
        //        bool isChangeShowGod = false;
        //        if (position[3][i].exist)
        //        {
        //            TSCharacter chr = position[3][i].chr;

        //            //ลูกชิ้นไม่ลด
        //            if (chr.autoFigth > 0 && chr.autoFigth <= 2)
        //            {
        //                PacketCreator pAutoFigth = new PacketCreator(0x0b, 0x07);
        //                pAutoFigth.addByte(chr.autoFigth);
        //                pAutoFigth.add16(1);
        //                chr.reply(pAutoFigth.send());
        //            }

        //            if (chr.hp == 0)
        //            {
        //                chr.hp = 1;
        //                // lost xp
        //                if (chr.party != null)
        //                    chr.refresh(1, TSConstants.CHAR_HP, true);

        //                chr.refresh(1, TSConstants.CHAR_HP);
        //            }
        //            else
        //            {
        //                //gain xp;
        //                bool hasMultiplyTag = (chr.equipment[5] != null && ItemData.itemMultiplyExpList.ContainsKey(chr.equipment[5].Itemid));
        //                if (chr.god > 0)
        //                {
        //                    addMultiply = 20;
        //                    byte beforeGod = chr.god;
        //                    chr.god--;
        //                    if ((beforeGod == 0 && chr.god > 0) || (beforeGod > 0 && chr.god == 0))
        //                    {
        //                        isChangeShowGod = true;
        //                        //chr.sendLook(isRb);
        //                        //chr.replyToMap(chr.sendLookForOther(), false);
        //                    }
        //                }
        //                else if (hasMultiplyTag)
        //                {
        //                    addMultiply = ItemData.itemMultiplyExpList[chr.equipment[5].Itemid];
        //                    if (chr.equipment[5].duration < 250)
        //                    {
        //                        chr.equipment[5].duration += 1;
        //                        chr.equipment[5].refreshDuration();
        //                    }
        //                    else
        //                    {
        //                        chr.equipment[5].setBroken();
        //                    }
        //                }
        //                if (addMultiply > 0)
        //                {
        //                    double tmp = exp_gain[1, i];
        //                    exp_gain[1, i] = (int)Math.Round((tmp * addMultiply) / 10);
        //                }
        //                //Logger.SystemWarning("Exp char: " + chr.client.accID.ToString());
        //                chr.setExp(exp_gain[1, i]);

        //                for (int e = 0; e < 6; e++)
        //                {
        //                    if (chr.equipment[e] != null)
        //                        chr.equipment[e].commitSoul();
        //                }
        //            }
        //            checkAutoRefill(chr);
        //            for (int j = 0; j < 4; j++)
        //                if (chr.pet[j] != null)
        //                {
        //                    if (chr.pet[j].hp <= 0)
        //                    {
        //                        chr.pet[j].hp = 1;
        //                        chr.pet[j].fai--;
        //                        chr.pet[j].refresh(chr.pet[j].fai, 0x40);
        //                        if (chr.pet[j].fai < 20)
        //                        {
        //                            if (chr.unsetBattlePet())
        //                            {
        //                                chr.announce(chr.pet[j].name + " ซื่อสัตย์ต่ำกว่า 20 ไม่สามารถออกรบได้");
        //                                chr.reply(new PacketCreator(new byte[] { 0x13, 0x02 }).send());
        //                            }
        //                        }
        //                        //lost xp
        //                        chr.pet[j].refresh(1, 0x19);
        //                    }
        //                    else if (j == chr.pet_battle)
        //                    {
        //                        //gain xp
        //                        if (addMultiply > 0)
        //                        {
        //                            double tmp = exp_gain[0, i];
        //                            exp_gain[0, i] = (int)Math.Round((tmp * addMultiply) / 10);
        //                        }
        //                        //Logger.SystemWarning("Exp pet: " + chr.client.accID.ToString());
        //                        chr.pet[j].setExp(exp_gain[0, i]);

        //                        for (int e = 0; e < 6; e++)
        //                            if (chr.pet[j].equipment[e] != null)
        //                                chr.pet[j].equipment[e].commitSoul();
        //                    }
        //                }

        //            chr.client.battle = null;

        //            //if (position[2][i].exist)
        //            //    battleBroadcast(new PacketCreator(new byte[] { 0xb, 1, 2, (byte)i, 0 }).send()); //pet walk out
        //            //battleBroadcast(new PacketCreator(new byte[] { 0xb, 1, 3, (byte)i, 0 }).send()); //char walk out

        //            // Clear battle smoke
        //            PacketCreator p = new PacketCreator(0x0b, 0x00);
        //            p.add32(chr.client.accID);
        //            p.add16(0); //win / lose ?
        //            chr.replyToMap(p.send(), true);

        //            //chr.client.map.BroadCast(chr.client, chr.sendBattleSmoke(true), true);

        //            //chr.inventory.sendItems(0x17, 5);
        //            #region #Auto Pack
        //            //if (battle_type == 3)
        //            //{
        //            //    if (chr.autoPack)
        //            //        chr.inventory.packTag();
        //            //    if (chr.autoSell != null && chr.autoSell.Count > 0)
        //            //    {
        //            //        for (int s = 0; s < chr.autoSell.Count; s++)
        //            //        {
        //            //            ushort item_id = chr.autoSell[s];
        //            //            int r = 0;
        //            //            for (int f = 0; f < chr.inventory.capacity; f++)
        //            //            {
        //            //                TSItem item = chr.inventory.items[f];
        //            //                if (item != null && item.Itemid == item_id && item.quantity == 50)
        //            //                {
        //            //                    if (r > 0)
        //            //                    {
        //            //                        //1B-02-05-04
        //            //                        byte[] data_bytes = new byte[] { 0x1b, 0x02, (byte)(f + 1), 50 };
        //            //                        new PacketHandlers.SellItemHandler(chr.client, data_bytes);
        //            //                    }
        //            //                    r++;
        //            //                }
        //            //            }
        //            //        }
        //            //    }
        //            //}
        //            #endregion

        //            //if (chr.client.talk == null) chr.client.continueMoving(); //แก้ตรงนี้ว่าถ้ายังอยู่ในเควสอย่าเพิ่งให้เดิน
        //            //if (chr.myEvent == null) chr.client.continueMoving(); //ถ้าไม่อยู่ในเควสให้เดินต่อได้

        //            if (isChangeShowGod)
        //            {
        //                chr.sendLookGod(2, chr.god); //1=ผี 2=เทพ
        //            }
        //        }
        //    }

        //    //sendExitStreamers(); //////เพิ่มตรงนี้ ใครที่ส่องอยู่ออกไปได้แล้ว สู้จบแล้วนะ
        //    sendCharExitView();
        //    //Logger.Error("ใครที่ส่องอยู่ออกไปได้แล้ว สู้จบแล้วนะ");

        //    //Console.WriteLine("Battle has ended "+win);

        //    TSCharacter chrLeader = position[3][2].chr;

        //    if (chrLeader != null && chrLeader.myEvent != null && chrLeader.myEvent.clickId > 0)
        //    {
        //        chrLeader.myEvent.onBattleFinish(this); //จะทดลองเปลี่ยนมาใช้อันนี้เพื่อรองรับเงื่อนไขการชนะการต่อสู้ เช่นหนีถึงจะผ่านไรงี้
        //    }

        //    if (chrLeader != null && chrLeader.bt_countdown == 0)
        //    {
        //        switch (chrLeader.bt_type)
        //        {
        //            case TSMyEvent.CLICK_TYPE_NPC: chrLeader.bt_countdown = 5; break;
        //            case TSMyEvent.CLICK_TYPE_ENC: chrLeader.bt_countdown = 15; break;
        //            default: Logger.Error("แปลกจังไม่รู้จัก type ของคำสั่ง bt_***"); chrLeader.btOff(); break;
        //        }
        //    }

        //    //อันนี้ไว้หลังสุด
        //    //Console.WriteLine("endBattle หยุเวลา " + win);
        //    aTimer.Stop();
        //    //aTimer.Dispose();
        //}
        public override void endBattle(bool win)
        {
            List<uint> fly_to_save = new List<uint>();
            roundCount -= 1;
            TSCharacter chrLeader = position[3][2].chr;

            int[,] exp_gain = new int[2, 5];
            int[,] exp_soul_gain = new int[2, 5];
            try {
                for (int i = 0; i < 2; i++) {
                    for (int j = 0; j < 5; j++)
                    {
                        BattleParticipant bp_npc = position[i][j];
                        if (win && bp_npc != null && bp_npc.npc != null && bp_npc.type == TSConstants.BT_POS_TYPE_NPC && bp_npc.exist && battle_type == 3)
                        {
                            calcExp(ref exp_gain, ref exp_soul_gain, bp_npc, 0); //calc exp for attacker
                            calcExp(ref exp_gain, ref exp_soul_gain, bp_npc, 1); //calc exp for killer
                        }
                    }
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.ToString());
            }

            sendCharExitView();

            //set exp for killer or attacker,
            //pet fai,
            //god,
            //lucky badge,
            //clear smoke, 
            //auto eat Hp/SP
            for (int i = 0; i < 5; i++)
            {
                //set char exp
                BattleParticipant bp_chr = position[3][i];
                if (bp_chr != null && bp_chr.exist && bp_chr.type == TSConstants.BT_POS_TYPE_CHR && bp_chr.chr != null)
                {
                    TSCharacter chr = bp_chr.chr;

                    //ลูกชิ้นไม่ลด
                    if (chr.autoFigth > 0 && chr.autoFigth <= 2)
                    {
                        PacketCreator pAutoFigth = new PacketCreator(0x0b, 0x07);
                        pAutoFigth.addByte(chr.autoFigth);
                        pAutoFigth.add16(1);
                        chr.reply(pAutoFigth.send());
                    }

                    ItemInfo item_badge = chr.equipment[5] != null ? chr.equipment[5].getItemInfo() : new ItemInfo();
                    byte[] lucky_badge_unk3 = new byte[] { 40, 50, 60 };
                    bool has_lucky_badge = item_badge.id > 0 && lucky_badge_unk3.Contains(item_badge.unk3);
                    bool has_autobox_exp = bp_chr.autoBoxCanCrystal(TSAutoBox.CRYSTAL_MUL_EXP);//46163 ผลึกชมพูเข้ม : ความสามารถ คือ เพิ่ม EXP 20%
                    //set exp whet alive
                    if (!bp_chr.death)
                    {
                        //double mul = TSServer.config.exp_multiply;
                        double mul = 0;
                        int base_exp = exp_gain[1, i];
                        if (chr.god > 0) //เทพโชคลาภ
                        {
                            mul += 2;
                        }

                        ushort lucky_badge_val = 0;
                        if (has_lucky_badge)
                        {
                            switch (item_badge.unk3)
                            {
                                case 40: lucky_badge_val = 15; break;
                                case 50: lucky_badge_val = 20; break;
                                case 60: lucky_badge_val = item_badge.unk9; break;
                            }
                        }
                        if (lucky_badge_val > 0)
                        {
                            mul += lucky_badge_val * 0.1;
                        }

                        if (has_autobox_exp && win) 
                        {
                            bp_chr.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_MUL_EXP);
                            mul += 0.2;
                        }
                        if (mul < 1) mul += 1;

                        int final_exp = (int)Math.Floor((base_exp * TSServer.config.exp_multiply) * mul);
                        //Console.WriteLine($"Summary multiply exp: {base_exp}*{mul}\t{final_exp}");
                        //Console.Write($"final_exp: {final_exp}");
                        chr.setExp(final_exp);
                        //Console.WriteLine("Chr final_exp: {0}", final_exp);

                        int soul_exp = exp_soul_gain[1, i];
                        for(int e = 0; e < 6; e++)
                        {
                            TSEquipment eq = chr.equipment[e];
                            if (eq != null)
                            {
                                eq.setSoulExp(soul_exp);
                            }
                        }
                        //////////////////////////////

                        if (chr.pet_battle > -1)
                        {
                            BattleParticipant bp_pet = position[2][i];
                            if (bp_pet != null && bp_pet.exist && !bp_pet.death && bp_pet.type == TSConstants.BT_POS_TYPE_PET && bp_pet.pet != null)
                            {
                                base_exp = exp_gain[0, i];
                                //final_exp = (int)Math.Floor(base_exp * mul);
                                final_exp = (int)Math.Floor((base_exp * TSServer.config.exp_multiply) * mul);
                                //Console.WriteLine($"/{final_exp}");
                                bp_pet.pet.setExp(final_exp);
                                //Console.WriteLine("Pet final_exp: {0}", final_exp);

                                int soul_pet_exp = exp_soul_gain[0, i];
                                for (int e = 0; e < 6; e++)
                                {
                                    try
                                    {
                                        if (bp_pet.pet != null)
                                        {
                                            TSEquipment eq = bp_pet.pet.equipment[e];//มีปัญหาเด้ง
                                            if (eq != null)
                                            {
                                                eq.setSoulExp(soul_pet_exp);
                                            }
                                        }
                                    }
                                    catch (NullReferenceException err)
                                    {
                                        Console.WriteLine(err.ToString());
                                    }
                                }
                            }

                            if (bp_pet != null && bp_pet.pet != null)
                                chr.counter.useTankHp(bp_pet.pet.slot);
                        }
                    }

                    if(chr.hp <= 0)
                    {
                        chr.setHp(1);
                        chr.refresh(chr.hp, TSConstants.CHAR_HP);
                        if (chr.party != null)
                            chr.refresh(chr.hp, TSConstants.CHAR_HP, true);

                        //if (chr.autobox.Length == 8 && chr.autobox[7] == 1) fly_to_save.Add(chr.client.accID);
                    }

                    // Clear battle smoke หลัง setExp
                    PacketCreator p = new PacketCreator(0x0b, 0x00);
                    p.add32(chr.client.accID);
                    p.add16(0); //win / lose ?
                    chr.replyToMap(p.send(), true);

                    //เดินออกฉากสู้ ต้องหยู่หลัง Clear battle smoke
                    if (position[3][i].exist)
                        battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, 3, (byte)i, 0 }).send());
                    if (position[2][i].exist)
                        battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, 2, (byte)i, 0 }).send());


                    //decrease pet fai when death
                    for (int j = 0; j < 4; j++)
                    {
                        if (chr.pet[j] != null)
                        {
                            TSPet pet = chr.pet[j];
                            if(pet.hp <= 0)
                            {
                                //46161 ผลึกน้ำเงิน : ความสามารถ คือ ตายไม่เสียซื่อสัตว์และ EXP
                                if (!bp_chr.autoBoxCanCrystal(TSAutoBox.CRYSTAL_LOST_EXP))
                                {
                                    pet.fai -= 1;
                                    pet.refresh(pet.fai, TSConstants.CHAR_FAI);
                                }
                                else
                                {
                                    bp_chr.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_LOST_EXP);
                                }
                                if (pet.fai < 20)
                                {
                                    if (chr.unsetBattlePet())
                                    {
                                        chr.announce(pet.name + " ซื่อสัตย์ต่ำกว่า 20 ไม่สามารถออกรบได้");
                                        chr.reply(new PacketCreator(new byte[] { 0x13, 0x02 }).send());
                                    }
                                }
                                //lost xp
                                pet.hp = 1;
                                pet.refresh(1, TSConstants.CHAR_HP);

                                //if (chr.autobox.Length == 8 && chr.autobox[8] == 1) fly_to_save.Add(chr.client.accID);
                            }
                        }
                    }

                    //decrease god
                    if(chr.god > 0)
                    {
                        chr.god -= 1;
                        chr.sendLookGod(2, chr.god); //1=ผี 2=เทพ
                    }
                    //increase lucky badge duration or set broken
                    if (has_lucky_badge)
                    {
                        if (chr.equipment[5].duration < 250)
                        {
                            chr.equipment[5].duration += 1;
                            chr.equipment[5].refreshDuration();
                        }
                        else
                        {
                            chr.equipment[5].setBroken();
                        }
                    }


                    //Console.WriteLine("[{0}][{1}]endBattle count {2}", bp_chr.row, bp_chr.col, bp_chr.autoBoxUsedCrystal.Count);
                    for (int a = 0; a < bp_chr.autoBoxUsedCrystal.Count; a++)
                    {
                        //Console.WriteLine("endBattle {0}", bp_chr.autoBoxUsedCrystal[a]);
                        bp_chr.autoBoxDropCrystal(bp_chr.autoBoxUsedCrystal[a], 1);
                    }

                    if (chr != null)
                    {
                        chr.counter.useTankHp();
                        chr.counter.useTankSp();
                        if (chr.pet_battle > -1)
                        {
                            TSPet pet = chr.pet[chr.pet_battle];
                            if(pet != null)
                            {
                                chr.counter.useTankHp(pet.slot);
                                chr.counter.useTankSp(pet.slot);
                            }
                        }

                        if (chr.autoBox.isAvailable())
                        {
                            if((chr.hp * 100) / chr.hp_max < chr.autoBox.charHp)
                            {
                                chr.refillByAutoBox(1, 0);
                            }
                            if ((chr.sp * 100) / chr.sp_max < chr.autoBox.charSp)
                            {
                                chr.refillByAutoBox(2, 0);
                            }

                            if(chr.pet_battle > -1 && chr.pet[chr.pet_battle] != null)
                            {
                                TSPet pet = chr.pet[chr.pet_battle];
                                if ((pet.hp * 100) / pet.hp_max < chr.autoBox.petHp)
                                {
                                    chr.refillByAutoBox(1, pet.slot);
                                }
                                if ((pet.sp * 100) / pet.sp_max < chr.autoBox.petSp)
                                {
                                    chr.refillByAutoBox(2, pet.slot);
                                }
                            }
                        }
                    }


                    //set battle to null
                    chr.client.battle = null;

                    //decrease auto use item HP/SP
                    if(chr.autoFill > 0)
                    {
                        if((chr.hp < chr.hp_max * 0.5) || (chr.sp < chr.sp_max * 0.5))
                        {
                            chr.charRefillUseItemHpSp(100); //กินของจนเลือดถึง 100%
                            ////ถ้าต้องการให้ลดยาบำรุงอัตโนมัติ ก็เปิดคอมเมนต์ตรงนี้
                            //chr.autoFill--;
                            //p = new PacketCreator(0x0b, 0x09);
                            //p.addByte(chr.autoFill);
                            //p.addByte(2);
                            //chr.reply(p.send());
                        }
                    }

                    if (chr.setting.autoSell)
                    {
                        if (chr.setting.autoSellList != null && chr.setting.autoSellList.Count > 0)
                        {
                            for (int s = 0; s < chr.setting.autoSellList.Count; s++)
                            {
                                ushort item_id = chr.setting.autoSellList[s];
                                TSItem[] items = chr.inventory.items.Where(x => x != null && x.Itemid == item_id).ToArray();
                                for (int t = 0; t < items.Length; t++)
                                {
                                    if (t > 0)
                                    {
                                        byte[] data_bytes = new byte[] { 0x1b, 0x02, items[t].slot, items[t].quantity };
                                        new PacketHandlers.SellItemHandler(chr.client, data_bytes);
                                    }
                                }
                            }
                        }


                        //for (int s = 0; s < chr.autoSellList.Count; s++)
                        //{
                        //    ushort item_id = chr.autoSellList[s];
                        //    int r = 0;
                        //    for (int f = 0; f < chr.inventory.capacity; f++)
                        //    {
                        //        TSItem item = chr.inventory.items[f];
                        //        if (item != null && item.Itemid == item_id && item.quantity == 50)
                        //        {
                        //            if (r > 0)
                        //            {
                        //                //1B-02-05-04
                        //                byte[] data_bytes = new byte[] { 0x1b, 0x02, (byte)(f + 1), 50 };
                        //                new PacketHandlers.SellItemHandler(chr.client, data_bytes);
                        //            }
                        //            r++;
                        //        }
                        //    }
                        //}
                    }

                    if (chr.setting.autoPack && chr.client.isVipAvailable())
                    {
                        chr.inventory.packTag();
                    }
                }
            }

            if (win)
            {
                if (position[3][2].death)
                {
                    finish = 2;
                }
                else
                {
                    finish = 1;
                }
            }
            else
            {
                if (position[3][2].death)
                {
                    finish = 2;
                }
                else
                {
                    finish = 3;
                }
            }

            //send battle result to event
            if (chrLeader != null && chrLeader.myEvent != null && chrLeader.myEvent.clickId > 0)
            {
                chrLeader.myEvent.onBattleFinish(this); //จะทดลองเปลี่ยนมาใช้อันนี้เพื่อรองรับเงื่อนไขการชนะการต่อสู้ เช่นหนีถึงจะผ่านไรงี้
            }

            //add delay next fight npc
            if (chrLeader != null && chrLeader.bt_countdown == 0)
            {
                switch (chrLeader.bt_type)
                {
                    case TSMyEvent.CLICK_TYPE_NPC: chrLeader.bt_countdown = 3; break;
                    case TSMyEvent.CLICK_TYPE_ENC: chrLeader.bt_countdown = 10; break;
                    default: Logger.Error("แปลกจังไม่รู้จัก type ของคำสั่ง bt_***"); chrLeader.btOff(); break;
                }
            }


            //อันนี้ไว้หลังสุด
            //Console.WriteLine("endBattle หยุเวลา " + win);
            aTimer.Stop();
            //aTimer.Dispose();

            // if (fly_to_save.Count() > 0) //บินกลับเซฟ
            // {
            //     foreach(uint acc_id in fly_to_save)
            //     {
            //         TSClient _cl = TSServer.getInstance().getPlayerById((int)acc_id);
            //         if(_cl != null)
            //         {
            //             TSCharacter _ch = _cl.getChar();
            //             if (_ch != null)
            //             {
            //                 if(_ch.party != null)
            //                 {
            //                     if(_ch.party.leader_id == acc_id)
            //                     {
            //                         _ch.party.Disband(_ch);
            //                     }
            //                     else
            //                     {
            //                         _ch.party.LeaveTeam(_ch);
            //                     }
            //                 }
            //                 _cl.sendWarpToXY(_ch.saved_map_id, _ch.saved_map_x, _ch.saved_map_y);
            //             }
            //         }
            //     }
            // }
        }
        /// <summary>
        /// type 0=Attacker, 1=Killer
        /// </summary>
        /// <param name="exp_gain">exp_gain</param>
        /// <param name="npc">npc</param>
        /// <param name="_type">0=Attacker, 1=Killer</param>
        private void calcExp(ref int[,] exp_gain, ref int[,] exp_soul_gain, BattleParticipant bp_npc, int _type)
        {
            BattleNpcAI npc = bp_npc.npc;
            List<Tuple<byte, byte>> npc_response = _type == 0 ? npc.attacker : npc.killer;
            NpcInfo npcInfo = npc.getNpcInfo();
            int exp_range_lv = 30;
            double exp_rate = 1;
            int exp_soul_rate = TSServer.config.per_exp_soul;
            foreach (Tuple<byte, byte> k in npc_response)
            {
                if (k != null)
                {
                    byte row = k.Item1; //char or pet
                    byte col = k.Item2; //char or pet
                                        //Console.WriteLine("calcExp [{0}][{1}]", row, col);
                    bool is_real_ally_pos = row > 1 && row < 4 && col >= 0 && col < 5;
                    if (is_real_ally_pos && !npc.isNpcOre())
                    {
                        BattleParticipant bp_ally = position[row][col];
                        bool ready_chr = bp_ally.type == TSConstants.BT_POS_TYPE_CHR && bp_ally.exist && bp_ally.chr != null;
                        bool ready_pet = bp_ally.type == TSConstants.BT_POS_TYPE_PET && bp_ally.exist && bp_ally.pet != null;
                        if (bp_ally != null && (ready_chr || ready_pet))
                        {
                            int rb = Math.Min(2, bp_ally.getRb());
                            exp_rate = TSServer.config.exp_gain[rb];
                            if (rb > 1 && npcInfo.reborn > 1) exp_range_lv = 40; //เพิ่มระยะห่างถ้าคนจุ 2 ตีมอนศักดิ์ศิทธิ
                            int npc_increase_lv = 0;
                            if ((ready_chr && bp_ally.chr.autoBox.isAvailable()) || (ready_pet && bp_ally.pet.owner.autoBox.isAvailable())) 
                                npc_increase_lv = 10; //เพิ่ม npc level สูงสุดที่สามารถเวลได้

                            //if(ready_chr && bp_ally.chr.autoBox.isAvailable()) exp_range_lv = 40;
                            //if(ready_pet && bp_ally.pet.owner.autoBox.isAvailable()) exp_range_lv = 40;

                            //สมการคำนวณ exp แบบลดลงเรื่อยๆ
                            double r = Math.Max(0, exp_range_lv + (npcInfo.level - bp_ally.getLvl()));
                            double ra = r < (exp_range_lv + npc_increase_lv) * 2 ? (double)npcInfo.level * (r * 0.05) : 0;
                            //Console.WriteLine($"[{row}][{col}]={ra}");
                            if (ra > 0)
                            {
                                double div = _type == 0 ? 4 : 1; //divide attacker = 4, killer = 1;
                                //if (rb > 1 && npcInfo.reborn < 2) div = 10; //ถ้าจุ 2 แล้วไปตีมอนธรรมดา exp / 10
                                int result = 0;
                                if (rb > 1 && npcInfo.reborn < 2) //ถ้าจุติ 2 ไปตีมอนธรรมดา
                                {
                                    result = (int)Math.Floor(ra / (div * 10));
                                }
                                else
                                {
                                    result = (int)Math.Floor((ra * exp_rate) / div);
                                }
                                result = Math.Max(1, result);

                                //string t = _type == 0 ? "Att" : "Kil";
                                //Console.WriteLine($"{exp_range_lv}\t[{row}][{col}]={result}");

                                //exp_gain[row - 2, col] += result;
                                exp_gain[row - 2, col] += result;
                            }

                            //calc soul exp
                            if (_type == 1)//kill
                            {
                                exp_soul_gain[row - 2, col] += exp_soul_rate; //give soul when kill only
                            }
                        }
                    }
                }
            }
        }

        //private void checkAutoRefill(TSCharacter c)
        //{
        //    if (c.autoFill > 0)
        //    {
        //        /*
        //        //ถ้าต้องการให้ลดยาบำรุงอัตโนมัติ ก็เปิดคอมเมนต์ตรงนี้
        //        c.autoFill--;
        //        PacketCreator p = new PacketCreator(0x0b, 9);
        //        p.addByte(c.autoFill);
        //        p.addByte(2);
        //        c.reply(p.send());
        //        */
                            //        double cHp = c.hp;
                            //        double cHpMax = c.hp_max;
                            //        double cSp = c.sp;
                            //        double cSpMax = c.sp_max;
                            //        double cHp_per = (cHp / cHpMax) * 100;
                            //        double cSp_per = (cSp / cSpMax) * 100;
                            //        if (cHp_per < 50 || cSp_per < 50) //ถ้าเลือดเหลือน้อยกว่า 50%
                            //            c.charRefillUseItemHpSp(100); //กินของจนเลือดถึง 100%

                            //        if (c.pet_battle >= 0)
                            //        {
                            //            TSPet pet = c.pet[c.pet_battle];
                            //            double pHp = pet.hp;
                            //            double pHpMax = pet.hp_max;
                            //            double pSp = pet.sp;
                            //            double pSpMax = pet.sp_max;
                            //            double pHp_per = (pHp / pHpMax) * 100;
                            //            double pSp_per = (pSp / pSpMax) * 100;
                            //            if (pHp_per < 50 || pSp_per < 50)
                            //                c.charRefillUseItemHpSp(100, true);
                            //        }
                            //    }
                            //}

                            //public override void view(TSCharacter chr)
                            //{
                            //    chr.streamBattleId = position[3][2].chr.client.accID;
                            //    PacketCreator p = new PacketCreator(0x0b, 0xfa);
                            //    p.addByte(ffield); p.addByte(0);
                            //    BattleParticipant bp = new BattleParticipant(this, 255, 255);
                            //    bp.chr = chr;
                            //    p.addBytes(bp.announce(4, 0).getData());

                            //    PacketCreator pos_statuses = new PacketCreator(0x35, 1);
                            //    PacketCreator prefix = new PacketCreator();
                            //    for (int i = 0; i < 4; i++)
                            //    {
                            //        for (int j = 0; j < 5; j++)
                            //        {
                            //            if (position[i][j].exist)
                            //            {
                            //                prefix.addBytes(position[i][j].announce(position[i][j].type, countAlly).getData());
                            //                pos_statuses.addBytes(position[i][j].getPacketPurgeStatus().getData());
                            //            }
                            //        }
                            //    }
                            //    p.addBytes(prefix.getData());

                            //    byte[] b = p.send();
                            //    chr.reply(b);
                            //    chr.reply(pos_statuses.send());
                            //}

        public override void jam(TSClient clientInit, TSClient clientDest)
        {
            BattleParticipant bp = getBpByClient(clientDest);
            //Console.WriteLine("getPosEmptyCount client " + client + " " + bp.getMag());
            int r = bp != null ? (bp.row < 2 ? 0 : 3) : -1;
            if (r < 0) return;

            int col_jam = -1;
            int[] cols = new int[] { 1, 3, 0, 4 };
            foreach (byte col_chk in cols)
            {
                if (!position[r][col_chk].exist)
                {
                    col_jam = col_chk;
                    break;
                }
            }
            if (col_jam < 0) return;

            PacketCreator pos_statuses = new PacketCreator(0x35, 0x01);
            PacketCreator prefix = new PacketCreator();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    if (position[i][j].exist)
                    {
                        prefix.addBytes(position[i][j].announce(position[i][j].type, countAlly).getData());
                        pos_statuses.addBytes(position[i][j].getPacketPurgeStatus().getData());
                    }
                }
            }

            announceStart(clientInit.getChar(), 3, (byte)col_jam, 5, prefix);

            clientInit.reply(pos_statuses.send());
        }

        public void npcReinforcement(BattleParticipant bp) //มอนแจม
        {
            if (bp.exist && bp.death && bp.npc != null)
            {
                if (bp.npc.reinforcement != null && bp.npc.reinforcement.Count > 0 && bp.npc.reinIndex < bp.npc.reinforcement.Count)
                {
                    ///////
                    foreach (Tuple<byte, byte> k in bp.npc.killer)
                        if (k != null)
                        {
                            giveMoral(position[k.Item1][k.Item2], bp);
                            if (bp.npc.drop != 0)
                            {
                                //Console.WriteLine("ดรอบ {0} ก่อนตัวซ้อนจะมา", bp.npc.drop);
                                giveDrop(bp.npc.drop, bp.row, bp.col, k.Item1, k.Item2);
                            }
                        }
                    //////
                    List<ushort> reinforcement = bp.npc.reinforcement;
                    int rein_index = bp.npc.reinIndex + 1;
                    //Logger.Error(string.Join(", ", position[i][j].npc.reinforcement.ToArray()));
                    if (bp.npc.reinIndex < bp.npc.reinforcement.Count)
                    {
                        countEnemy++;
                        battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, bp.row, bp.col }).send());
                        ushort npc_id = bp.npc.reinforcement[bp.npc.reinIndex];
                        BattleNpcAI ai = new BattleNpcAI(this, countEnemy, npc_id);
                        bp.npcIn(ai);
                        bp.npc.reinforcement = reinforcement;
                        bp.npc.reinIndex = rein_index;
                        bp.death = false;
                        var p2 = new PacketCreator(0x0B, 0x05);
                        p2.addBytes(bp.announce(5, countEnemy).getData());
                        //Logger.SystemWarning(BitConverter.ToString(p2.getData()));
                        battleBroadcast(p2.send());
                        finish = 0;

                        PacketCreator p3 = new PacketCreator(0x35, 0x06);
                        p3.addByte(1);
                        p3.add32(npc_id);
                        p3.add32(npc_id);
                        battleBroadcast(p3.send());
                    }
                }
            }
        }

    }
}
