using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TS_Server.Server.BattleClasses;
using TS_Server.Client;
using TS_Server.DataTools;
using System.ComponentModel.Design;
using System.Runtime.ConstrainedExecution;
using System.Reflection;

namespace TS_Server.Server
{
    public abstract class BattleAbstract
    {
        public TSMap map;
        public BattleParticipant[][] position;
        public List<BattleCommand> cmdReg;
        public int cmdNeeded;
        public int nextcmd;
        public int countDisabled = 0;
        public ushort countEnemy = 0;
        public ushort countAlly = 0;
        public byte battle_type;
        public byte ffield = 134;
        public int finish = 0;
        public Timer aTimer = new Timer(21000);
        //public System.Threading.Thread btThread;
        //public List<uint> streamers = new List<uint>();
        public bool executing = false;
        public int roundCount;
        public string uniqId;
        public int team1AvgLevel = 0;
        public int team2AvgLevel = 0;
        public TSRandomize randomize = new TSRandomize();
        public List<uint> charViewList = new List<uint>();
        public int dmg_max = 60000;


        protected List<Tuple<byte, byte>> hitedList; //ลิสของตัวที่ถูกโจมตี
        protected List<Tuple<byte, byte>> comboList; //ลิสของผู้โจมตี (อาจจะ 1 ตัวหรือมากกว่า)

        protected BattleAbstract()
        {
        }

        protected BattleAbstract(TSClient c, byte type)
        {
            TSCharacter chr = c.getChar();
            ffield = GroundData.getFieldByPos(chr.mapID, chr.mapX, chr.mapY);
            //Console.WriteLine("ffield: {0}", ffield);
            //Logger.Info("BattleAbstract");
            if (c.battle != null)
            {
                //Logger.Error("construct destroy old battle");
                //Console.WriteLine(c.accID + " battle[" + c.battle.uniqId + "] ซ้ำซ้อน ทำการเคลียร์ battle เก่า");
                c.battle.destroyBattle();
                if (c.getChar().party != null)
                {
                    foreach (TSCharacter mem in chr.party.member.ToList())
                    {
                        mem.client.battle = null;
                    }
                }
                else
                {
                    c.battle = null;
                }
            }
            uniqId = Guid.NewGuid().ToString("N").Substring(0, 12);
            c.battle = this;
            map = c.map;
            battle_type = type;

            position = new BattleParticipant[4][];
            for (byte i = 0; i < 4; i++)
            {
                position[i] = new BattleParticipant[5];
                for (byte j = 0; j < 5; j++)
                    position[i][j] = new BattleParticipant(this, i, j);
            }

            aTimer.Elapsed += new ElapsedEventHandler(timeOut);

            cmdReg = new List<BattleCommand>();

            //map.announceBattle(c);
        }

        public abstract void start_round();

        public abstract void checkDeath(byte row, byte col);

        public abstract void endBattle(bool win);

        public abstract void jam(TSClient clientInit, TSClient clientDest);
        public int getPosEmptyCount(TSClient client)
        {
            BattleParticipant bp = getBpByClient(client);
            //Console.WriteLine("getPosEmptyCount client " + client + " " + bp.getMag());
            if (bp != null)
            {
                int r = bp.row < 2 ? 0 : 3;
                return position[r].Where(p => !p.exist).Count();
            }
            return 0;
        }
        public void view(TSClient client)
        {
            //client ในนี้เป็นของคนส่องนะ
            if (!charViewList.Contains(client.accID))
                charViewList.Add(client.accID);
            TSCharacter chr = client.getChar();
            client.battle = this;
            PacketCreator p = new PacketCreator(0x0b, 0xfa);
            p.addByte(ffield); p.addByte(0);
            BattleParticipant bp = new BattleParticipant(this, 255, 255);
            bp.chr = chr;
            p.addBytes(bp.announce(4, 0).getData());

            PacketCreator pos_statuses = new PacketCreator(0x35, 1);
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
            p.addBytes(prefix.getData());

            byte[] b = p.send();
            chr.reply(b);
            chr.reply(pos_statuses.send());
            chr.reply(new PacketCreator(new byte[] { 0x0b, 0x0a, 0x01 }).send());
        }

        public void beginBattle()
        {
            if (map == null)
                endBattle(false);
            else
            {
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 5; j++)
                        if (position[i][j].exist && position[i][j].type == TSConstants.BT_POS_TYPE_CHR && position[i][j].chr != null)
                        {
                            TSCharacter chr = position[i][j].chr;
                            map.BroadCast(chr.client, chr.sendBattleSmoke(true), true);
                        }
                start_round();
            }
        }

        public void timeOut(object sender, EventArgs e)
        {
            //Console.WriteLine("-----------time out " + uniqId);
            BattleParticipant bp = position[3][2];
            if (bp.exist && bp.chr != null && bp.chr.client != null)
            {
                string base_info = bp.chr.client.accID + " " + bp.chr.name + " battle[" + uniqId + "]";
                //Console.WriteLine(base_info + " สมาชิกในตี้ปล่อยให้หมดเวลาการต่อสู้");

                //Logger.Warning(bp.chr.client.accID + " " + bp.chr.name + " battle[" + uniqId + "] สมาชิกในตี้ปล่อยให้หมดเวลาการต่อสู้");
                //Logger.Info(uniqId + " - " + (bp.chr.client.battle != null ? bp.chr.client.battle.uniqId : "bt null"));

                string current_uniqid = bp.chr.client.battle != null ? bp.chr.client.battle.uniqId : string.Empty;
                //Console.WriteLine("char bt id " + current_uniqid + " || this bt id " + uniqId);
                if (uniqId != current_uniqid)
                {
                    Console.WriteLine(base_info + " uniqid ไม่ใช่ล่าสุด ทำลายการต่อสู้ (ป้องกัน Battle ซ้อน)");
                    destroyBattle();
                    return;
                }
            }
            else
            {
                //Logger.Error("timeOut ไม่มีเจ้าของ battle");
                Console.WriteLine("timeOut อ้าวงง battle ไม่มีเจ้าของ งั้นหยุดการต่อสู้นี้เลยละกัน");
                endBattle(false);
                //aTimer.Stop();
                //aTimer.Dispose();
                return;
            }

            if (countAlly == 0 || (bp.battle != null && bp.battle.uniqId != uniqId))
            {
                endBattle(false);
                //aTimer.Dispose(); // Fix
            }
            else
            {
                //Console.WriteLine("TIME OUT ปกติ --> executeThread()");
                //execute();
                executeThread(); //time out
            }
        }

        public void registerCommand(TSClient c, byte[] data, byte type)
        {
            byte init_row = data[2];
            byte init_col = data[3];
            byte dest_row = data[4];
            byte dest_col = data[5];
            //Logger.Error("registerCommand " + type.ToString());
            if (!aTimer.Enabled || position[init_row][init_col].alreadyCommand) return;

            battleBroadcast(new PacketCreator(new byte[] { 0x35, 5, init_row, init_col }).send());
            if (init_row > 3 || init_col > 4 || dest_row > 3 || dest_col > 4)
            {
                //32-01-03-03-FF-FF-FC-2E-AE-EA
                string date_string = DateTime.Now.ToString();
                c.getChar().announce(date_string + " แจ้งรายละเอียดการสู้ให้จีเอ็มที");
                Console.WriteLine(date_string + " " + c.accID + " registerCommand แปลกๆ " + BitConverter.ToString(data));
            }

            ushort sk_id = PacketReader.read16(data, 6);
            BattleParticipant init = position[init_row][init_col];
            BattleParticipant dest = position[dest_row][dest_col];

            ushort[] sk_allow = new ushort[]
            {
                18001, //หลบหนี
                18002, //หลบหนีล้มเหลว
                19001, //สิ่งของ
                15001, //จับศัตรู
                15002, //ตาข่ายจับศัตรู
                15003, //จับศัตรูสำเร็จ
                17001, //ป้องกัน
            };
            if (!sk_allow.Contains(sk_id) && type == 0) //type 0=attack, 1=useItem
            {
                if (init.type == TSConstants.BT_POS_TYPE_CHR)
                {
                    if (!init.chr.skill.ContainsKey(sk_id) && !init.chr.skill_rb2.Contains(sk_id))
                        sk_id = 10000;
                }
                else if (init.type == TSConstants.BT_POS_TYPE_PET)
                {
                    NpcInfo npcInfo = NpcData.npcList[init.pet.NPCid];
                    if (npcInfo.skill1 != sk_id && npcInfo.skill2 != sk_id && npcInfo.skill3 != sk_id && npcInfo.skill4 != sk_id)
                        sk_id = 10000;
                }
            }
            if (sk_id == 13008 || sk_id == 13032) //วิชาแบ่งร่าง ซ่อนกายสายลม
            {
                //ดักไว้เผื่อมีคนพิเรนใช้ร่างแยกออกสกิลแบ่งร่างอีก
                if (dest.type != TSConstants.BT_POS_TYPE_CHR)
                    sk_id = 17001; //ป้องกัน
            }
            //pushCommand(data[2], data[3], data[4], data[5], type, PacketReader.read16(data, 6));
            if (sk_id == 21026 && isTargetEnemy(init, dest)) //คุณธรรมไร้พ่าย ถ้าใช้กับศัตรูให้เปลี่ยนเป็นสกิลมึน
            {
                sk_id = 21028;
            }

            if (type == 0 && SkillData.skillList.ContainsKey(sk_id)) //บอทมันส่งแปลกๆมา ต้องฟิลเตอร์ให้มันถูกต้องซะก่อน
            {
                SkillInfo skillInfo = SkillData.skillList[sk_id];
                byte effect;
                if (sk_id >= 13016 && sk_id <= 13018) { effect = 3; }//เชิญมังกร;
                else if (sk_id >= 10017 && sk_id <= 10019) { effect = 15; } //เชิญหินผา;
                else if (sk_id >= 11017 && sk_id <= 11019) effect = 18; //เชิญวารี;
                else if (sk_id >= 20001 && sk_id <= 20003) effect = 20; //ต้นไม้ดูดเลือด พิษเสียเลือด สะท้อนบาดเจ็บ
                else effect = skillInfo.skill_type;
                byte[] side_init = new byte[] { 4, 6, 7, 8, 9, 10, 12, 14, 19 };//4 บัพ, 6 ฮิล SP, 7 ฮิล HP, 14 ฮิล HP/SP, 19 ออร่า แสดงว่าต้องฝั่งเมอนเท่านั้น
                byte[] side_dest = new byte[] { 1, 2, 3, 11, 15 };
                byte[] side_both = new byte[] { 5, 13, 16, 17, 18, 99 };//5 สลาย, 18 อัญเชิญ, แสดงว่าใส่ได้ทั้ง 2 ฝ่าย

                //ถ้าเป็นสกิลที่ใส่ฝั่งตัวเองเท่านั้นเช่นพวกบัพ แต่ไปใส่ให้ศัตรู
                if (side_init.Contains(effect) && !sameSide(init_row, dest_row))
                {
                    //sk_id = 17001; //ป้องกันซะ
                    dest_row = init_row;
                    dest_col = init_col;
                }
                //ถ้าเป็นสกิลที่ใส่ฝั่งตัวเองเท่านั้นเช่นพวกโจมตี ดีบัฟ แต่ไปใส่ให้ฝั่งตัวเอง
                else if (side_dest.Contains(effect) && sameSide(init_row, dest_row))
                {
                    sk_id = 17001; //ป้องกันซะ
                    dest_row = init_row;
                    dest_col = init_col;
                }
            }



            if (init.getHp() > 0 && init.disable == 0)
                pushCommand(init_row, init_col, dest_row, dest_col, type, sk_id);
        }

        public void pushCommand(byte init_row, byte init_col, byte dest_row, byte dest_col, byte type, ushort command_id)
        {
            //Logger.Error("pushCommand " + command_id.ToString());

            BattleParticipant bp_init = position[init_row][init_col];

            //เคล็ดลับชัยชนะ
            if (type == 1 && command_id == 46301)
            {
                if (this.battle_type == 3 && init_row == 3 && init_col == 2 && bp_init.type == TSConstants.BT_POS_TYPE_CHR)
                {
                    bp_init.chr.inventory.dropItemById(46301, 1);
                    finish = 1;
                    start_round();
                }
                else
                {
                    TSCharacter chr = null;
                    switch (bp_init.type)
                    {
                        case TSConstants.BT_POS_TYPE_CHR: chr = bp_init.chr; break;
                        case TSConstants.BT_POS_TYPE_PET: chr = bp_init.pet.owner; break;
                        case TSConstants.BT_POS_TYPE_CLONE: chr = bp_init.clone.owner; break;
                    }
                    if (chr != null)
                        chr.announce("ไม่สามารถใช้เคล็ดลับชัยชนะได้");
                }
                return;
            }

            BattleCommand cmd = new BattleCommand(init_row, init_col, dest_row, dest_col, type);

            bp_init.alreadyCommand = true;
            if (type == 0)
            {
                cmd.skill = command_id;
                cmd.skill_lvl = bp_init.getSkillLvl(command_id);
                if (command_id == 13015 || command_id == 10016 || command_id == 11016 || command_id == 12016) //Trieu goi อัญเชิญ
                {
                    cmd.skill += (ushort)((cmd.skill_lvl - 1) / 3);
                    cmd.isSummonSkill = true;
                    //Logger.SystemWarning("pushCommand");
                }
                //cmd.dmg = calcDmg(init_row, init_col, cmd.skill, cmd.skill_lvl); //calculate base dmg here
                if (dest_row < 4 && dest_col < 5)
                    cmd.dmg = CalcDmg(init_row, init_col, dest_row, dest_col, cmd.skill, cmd.skill_lvl); //calculate base dmg here

                if (SkillData.skillList[cmd.skill].skill_type == 14)//สกิลฮิลทั้ง HP และ SP
                {
                    cmd.sub_dmg = (ushort)(cmd.dmg / 5);
                }

                //if (command_id == 14054)//สายฟ้าลงทัณฑ์ สุ่มว่าจะโดนเป้าหมายหรือตัวเอง
                //{
                //    BattleParticipant dest = position[dest_row][dest_col];
                //    if (dest.isNpcBoss()) //ถ้าผ่าบอสก็ให้โดนตัวเองทันที
                //    {
                //        cmd.dest_row = init_row;
                //        cmd.dest_col = init_col;
                //    }
                //    else
                //    {
                //        if ((randomize.getInt(1, 101) - (cmd.skill_lvl * 2)) > SkillData.skillList[14054].unk19)
                //        {
                //            cmd.dest_row = init_row;
                //            cmd.dest_col = init_col;
                //        }
                //    }
                //}


            }
            if (type == 1) // use item
            {
                cmd.skill = command_id;
                if (ItemData.itemList[command_id].type != 16)
                    cmd.dmg = (ushort)ItemData.itemList[command_id].prop1_val;
                else
                {
                    cmd.type = 2; // bua` ngai? =))
                    cmd.dmg = CalcDmg(init_row, init_col, dest_row, dest_col, ItemData.itemList[command_id].unk9, 1);
                }
            }

            //int agi_increse = 0;
            if (cmd.skill == 14033) //วิชายิงธนู(ธนูทะลวงใจ) เพิ่มไวไปเลย 6 หมื่น ถ้าจะให้เหลือแค่นี้ก็เปิดคอมเม้นบรรทัดนี้ แล้วคอมม้น if บรรทัดล่างไว้
            //if (cmd.skill == 14037 || cmd.skill == 14033) //ธนูรำเก้าฟ้า || วิชายิงธนู(ธนูทะลวงใจ) เพิ่มไวไปเลย 6 หมื่น
            {
                //agi_increse = 60000;
                cmd.archery = 1;
            }
            //if (bp_init.debuff > 0 && (bp_init.debuff_type >= 10016 && bp_init.debuff_type <= 10019)) //ติดมึนเชิญดินอยู่
            //{
            //    agi_increse = (bp_init.getAgi() * -1);
            //}

            //ushort[] cri = bp_init.getCritical();
            //int cri_mul = 1;
            //ushort cri_agi_equip = cri[1];
            //if (cri_agi_equip > 0)
            //{
            //    int rnd_cri_agi = randomize.getInt(0, 101);
            //    if (cri_agi_equip >= rnd_cri_agi)
            //        cri_mul = 2;
            //}
            ////Console.WriteLine("[{0}][{1}]\tcri_agi_equip: {2}, cri_mul: {3}", bp_init.row, bp_init.col, cri_agi_equip, cri_mul);
            //cmd.priority = (bp_init.getAgi() * cri_mul) + agi_increse;
            ////Logger.Info("agi_increse: " + agi_increse + ", cmd.priority:" + cmd.priority);

            ////Logger.Error("rnd_cri_agi --> " + rnd_cri_agi + " init_cri[1] " + init_cri[1] + " mul " + mul_cri_agi + " cmd.priority " + cmd.priority);

            int final_agi = bp_init.getAgiSummary();
            cmd.priority = final_agi;

            if (cmd.skill == 17001) //def
            {
                //to do : handle def      
                position[cmd.init_row][cmd.init_col].def = true;
                cmdNeeded--;
            }
            else
            {
                //int pos = 0;
                //while (pos < cmdReg.Count) //find the proper order to place the BattleCommand
                //{
                //    if (cmd.priority <= cmdReg[pos].priority) pos++;
                //    else break;
                //}
                //lock (cmdReg)
                //    cmdReg.Insert(pos, cmd);
                //if (position[cmd.init_row][cmd.init_col].getHp() > 0)
                //    cmdNeeded--;

                //ของใหม่ตรงนี้ และต้องไปทำที่ cmdNeeded == 0 ด้วยนะ
                lock (cmdReg)
                {
                    if (position[cmd.init_row][cmd.init_col].getHp() > 0)
                    {
                        cmdReg.Add(cmd);
                        cmdNeeded--;
                        //Console.WriteLine("pushCommand [{0}][{1}], need: {2}", cmd.init_row, cmd.init_col, cmdNeeded);
                    }
                }
            }

            //Logger.Success("cmdNeeded: " + cmdNeeded + "/"+ cmdReg.Count);

            if (cmdNeeded == 0)
            {
                //cmdReg = cmdReg.OrderByDescending(n => n.priority).ToList(); //เรียงไว
                //Logger.Info("cmdNeeded == 0 --- executeThread");
                //execute();
                executeThread(); //pushCommand cmdNeeded == 0
            }
        }

        public ushort CalcDmg(byte initRow, byte initCol, byte destRow, byte destCol, ushort skill, byte skill_lvl)
        {
            BattleParticipant init = position[initRow][initCol];
            BattleParticipant dest = position[destRow][destCol];
            //int initLvl = init.getLvl();
            //int initEle = init.getElem();
            SkillInfo skillInfo = SkillData.skillList[skill];
            int initAtk = Math.Max(1, init.getAtk());
            int initMag = Math.Max(1, init.getMag());
            TSConfig.CFG_DAMAGE cfg_dmg = TSServer.config.damage;
            //double dmgbase = init.getLvl() * cfg_dmg.level;
            //double per_range = ((init.getLvl() - dest.getLvl()) * cfg_dmg.range_level) + cfg_dmg.range_rb;
            double test = Math.Pow(init.getLvl(), cfg_dmg.level) + 4;
            double dmgbase = (ushort)(Math.Floor(test));
            //dmgbase *= (1 + per_range);

            if (skill == 10000) //มือปล่าว
            {
                dmgbase += (initMag * cfg_dmg.hand_mag) + (initAtk * cfg_dmg.hand_atk);
            }
            else
            {
                ushort unk17 = SkillData.skillSummonFire.Contains(skill) ? SkillData.skillList[12016].unk17 : skillInfo.unk17;
                double c_mag = cfg_dmg.sk_unk17[unk17][0];
                double c_atk = cfg_dmg.sk_unk17[unk17][1];
                dmgbase += (initMag * c_mag) + (initAtk * c_atk);
            }

            dmgbase = Math.Min(50000, dmgbase);
            dmgbase = Math.Max(1, dmgbase);

            //Console.WriteLine("[{0}][{1}] dmgbase {2}", initRow, initCol, dmgbase);

            return (ushort)dmgbase;
        }

        public void executeThread()
        {
            new System.Threading.Thread(execute)
            {
                IsBackground = true
            }.Start();
        }

        public void execute() // spartannnnnn!!!!!
        {
            executing = true;
            cmdReg = cmdReg.Where(n => n != null).OrderByDescending(n => n.archery).ThenByDescending(n => n.priority).ToList(); //เรียงไว
            BattleParticipant init, dest;
            nextcmd = 0;

            aTimer.Stop();
            System.Threading.Thread.Sleep(200); //<< ของเดิมถ่วงเวลาก่อนโดดตีครึ่งวินาที (1 วินาที = 1000 millisec)

            while (nextcmd < cmdReg.Count)
            {
                if (finish != 0) break;

                BattleCommand cmd = cmdReg[nextcmd];
                try
                {
                    if (cmd.dest_row > 3 || cmd.dest_col > 4)
                    {
                        cmd.dest_row = cmd.init_row;
                        cmd.dest_col = cmd.init_col;
                    }
                    init = position[cmd.init_row][cmd.init_col];
                    dest = position[cmd.dest_row][cmd.dest_col];
                    hitedList = new List<Tuple<byte, byte>>();
                    comboList = new List<Tuple<byte, byte>>();

                    if (init.exist && init.disable == 0 && !init.death)
                    {
                        //if (!dest.exist || dest.death || dest.buff_type == 13005 || dest.buff_type == 13025 || (dest.type == TSConstants.BT_POS_TYPE_CHR && dest.chr.client.disconnecting) || (dest.type == TSConstants.BT_POS_TYPE_PET && dest.pet.owner.client.disconnecting)) //auto choose another target if former one not available
                        if (!dest.exist || dest.death || dest.getHiding() || (dest.type == TSConstants.BT_POS_TYPE_CHR && dest.chr.client.disconnecting) || (dest.type == TSConstants.BT_POS_TYPE_PET && dest.pet.owner.client.disconnecting)) //auto choose another target if former one not available
                        {
                            bool change_target = changeTarget(cmd);
                            if (change_target) // target is changable
                                execCommand(cmd);
                            else nextcmd++;
                        }
                        else
                            execCommand(cmd); //มีปัญหา
                    }
                    else nextcmd++;

                    for (int i = 0; i < hitedList.Count; i++)
                    {
                        if (hitedList[i] != null && hitedList[i].Item1 != null && hitedList[i].Item2 != null)
                        {
                            byte row = hitedList[i].Item1;//มีปัญหาเด้ง
                            byte col = hitedList[i].Item2;
                            checkDeath(row, col);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    finish = 2;
                }
            }
            if (nextcmd < cmdReg.Count)
            {
                for (int i = nextcmd; i < cmdReg.Count; i++)
                {
                    BattleCommand c = cmdReg[i];
                    BattleParticipant bp = position[c.init_row][c.init_col];
                    if (bp.getHp() > 0 && !bp.death && (SkillData.skillHealPartyList.ContainsKey(c.skill) || c.skill == 11013)) //11013 วิชาฟื้นคืนชีพ
                    {
                        execCommand(c);
                        System.Threading.Thread.Sleep(SkillData.skillList[c.skill].delay * 100);
                    }

                    if (c.init_col == 2 && (c.skill == 18001 || c.skill == 14002)) //leader run หัวหนี
                    {
                        finish = 3;
                    }
                }
            }
            start_round();
        }

        public bool changeTarget(BattleCommand c)
        {
            //Console.WriteLine($"c.type {c.type}");
            //if(c.type == 1 && ItemData.itemList[c.skill].type == 50)//กินยาชุบ
            if (c.type == 1)//กินของ
            {
                return true;
            }
            int r = c.dest_row < 2 ? 0 : 2;
            BattleParticipant dest = position[c.dest_row][c.dest_col];

            ushort tmp_skill = c.skill;
            if (c.type == 2 && ItemData.itemList.ContainsKey(c.skill)) //ยัน
            {
                ItemInfo itif = ItemData.itemList[c.skill];
                if (SkillData.skillList.ContainsKey(itif.unk9))
                {
                    tmp_skill = SkillData.skillList[itif.unk9].id;
                }
            }
            SkillInfo skillInfo = SkillData.skillList[tmp_skill];
            if (dest.exist)
            {
                //จับมอนที่ตาย
                if (SkillData.skillList[tmp_skill].skill_type == 11 && dest.death)
                {
                    bool found = false;
                    for (int i = r; i < (r + 1); i++)
                    {
                        for (int j = 0; j < 5; j++)
                            if (position[i][j].exist && !position[i][j].death)
                            {
                                c.dest_row = position[i][j].row;
                                c.dest_col = position[i][j].col;
                                //Console.WriteLine("จับ เปลี่ยนตัว");
                                found = true;
                                break;
                            }
                        if (found) break;
                    }
                    return found;
                }
                if (sameSide(c.init_row, c.dest_row)) return true;
                if (c.type == 1) return true;
                //Logger.Error("SkillData.skillList[c.skill].skill_type " + SkillData.skillList[c.skill].skill_type);
                //if (SkillData.skillList[c.skill].skill_type > 2) return true;
                //if (SkillData.skillSummonEarth.Contains(c.skill) || SkillData.skillSummonWater.Contains(c.skill) || SkillData.skillSummonFire.Contains(c.skill) || SkillData.skillSummonWind.Contains(c.skill)) return true;
                //if (SkillData.skillList[c.skill].skill_type > 3) return true;
                if (SkillData.skillSummonWater.Contains(c.skill)) return true;
                if (skillInfo.skill_type == 5 || skillInfo.skill_type == 18) return true; //สลาย / เชินน้ำ
                //if (c.type == 2) //ยัน
                //    if (SkillData.skillList[ItemData.itemList[tmp_skill].unk9].skill_type > 2) return true;
            }

            for (int i = 0; i < 10; i++)
                if (position[i % 2 + r][i / 2].exist && !position[i % 2 + r][i / 2].death)
                    //if (position[i % 2 + r][i / 2].buff_type != 13005 && position[i % 2 + r][i / 2].buff_type != 13025)
                    if (!position[i % 2 + r][i / 2].getHiding())
                    {
                        //Console.WriteLine("change target to " + (i % 2 + r) + " " + (i / 2));
                        c.dest_row = (byte)(i % 2 + r);
                        c.dest_col = (byte)(i / 2);
                        return true;
                    }
            return false;
        }

        public void execCommand(BattleCommand c)
        {

            executeSummonCounter(c);

            //bool isSkillHeal = SkillData.skillHealPartyList.ContainsKey(c.skill);
            //if (isSkillHeal) c.dmg = (ushort)(c.dmg / 3); //ปรับความแรงของสกิลฮิล

            //if (c.skill == 18001 || c.skill == 14002) //run หนี
            //{
            //    //insert here some RNG
            //    if (c.init_col == 2) // leader run
            //        finish = 0;
            //    else if (position[c.init_row][c.init_col].type != TSConstants.BT_POS_TYPE_CHR)
            //        c.skill = 18002; //หลบหนีล้มเหลว
            //    else
            //    {
            //        position[c.init_row][c.init_col].outBattle = true;
            //        if (position[c.init_row][c.init_col].type == TSConstants.BT_POS_TYPE_CHR && position[c.init_row][c.init_col].chr != null)
            //        {
            //            TSCharacter chr = position[c.init_row][c.init_col].chr;
            //            PacketCreator p_run = new PacketCreator(0x0b, 0x04);
            //            p_run.addByte(0x02);
            //            p_run.add32(chr.client.accID);
            //            p_run.addZero(2);//00-00
            //            p_run.addByte(0);
            //            chr.replyToMap(p_run.send(), false);

            //            battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, c.init_row, c.init_col, 0 }).send()); //char walk out
            //        }
            //    }
            //    //nextcmd++;
            //    //return;
            //}


            byte skill_delay = c.type == 0 ? SkillData.skillList[c.skill].delay : (byte)0;

            PacketCreator p = new PacketCreator(0x32, 0x01);

            if (c.skill >= 20001 && c.skill <= 20003) //ต้นไม้ดูดเลือด พิษเสียเลือด สะท้อนบาดเจ็บ
            {
                p.addBytes(makeExecutionPacket(c, false));
            }
            #region check combo original
            //else if (cmdReg[nextcmd].type == 0 && checkCombo(nextcmd + 1)) //ของเดิม
            //{
            //    c.dmg = (ushort)(c.dmg * 1.2);
            //    p.addBytes(makeExecutionPacket(c, false));
            //    nextcmd++;
            //    while (checkCombo(nextcmd))
            //    {
            //        c.dmg = (ushort)(c.dmg * 1.2);
            //        p.addBytes(makeExecutionPacket(cmdReg[nextcmd], true));
            //        nextcmd++;
            //    }
            //}
            #endregion
            #region check combo v.1
            //else if (c.type == 0 && SkillData.skillList[c.skill].skill_type == 1)
            //{
            //    List<int> combo_index_list = new List<int>();
            //    List<byte> combo_delay_list = new List<byte>();
            //    combo_index_list.Add(nextcmd);
            //    combo_delay_list.Add(skill_delay);
            //    comboList.Add(new Tuple<byte, byte>(c.init_row, c.init_col));
            //    int tmp_next_cmd = nextcmd + 1;

            //    while (checkCombo(tmp_next_cmd))
            //    {
            //        combo_index_list.Add(tmp_next_cmd);
            //        combo_delay_list.Add(SkillData.skillList[cmdReg[tmp_next_cmd].skill].delay);
            //        comboList.Add(new Tuple<byte, byte>(cmdReg[tmp_next_cmd].init_row, cmdReg[tmp_next_cmd].init_col));
            //        tmp_next_cmd++;
            //    }
            //    skill_delay = combo_delay_list.Max();

            //    double mul_dmg = comboList.Count > 1 ? 1.2 : 1;
            //    for (int i = 0; i < comboList.Count; i++)
            //    {
            //        int index = combo_index_list[i];//มีปัญหา
            //        BattleCommand bc = cmdReg[index];
            //        bc.dmg = (ushort)(bc.dmg * mul_dmg);
            //        //Console.WriteLine("[{0}][{1}]: {2}", bc.init_row, bc.init_col, bc.dmg);
            //        p.addBytes(makeExecutionPacket(bc, i > 0));
            //    }
            //    //Console.WriteLine("index: {0}", string.Join(", ", combo_index_list));
            //    nextcmd += comboList.Count;
            //    //Console.WriteLine("comboList.Count " + comboList.Count);
            //    //comboList.Clear();
            //}
            #endregion
            else if (c.type == 0 && SkillData.skillList[c.skill].skill_type == 1)
            {
                int index = nextcmd;
                BattleCommand bc = cmdReg[index];
                skill_delay = SkillData.skillList[bc.skill].delay;
                List<int> cmd_combo_list = new List<int>();
                cmd_combo_list.Add(index);

                index += 1;
                while (checkCombo(index))
                {
                    cmd_combo_list.Add(index);
                    index += 1;
                }

                double mul_dmg = cmd_combo_list.Count > 1 ? 1.2 : 1;
                for (int i = 0; i < cmd_combo_list.Count; i++)
                {
                    index = cmd_combo_list[i];
                    bc = cmdReg[index];
                    bc.dmg = (ushort)Math.Floor(bc.dmg * mul_dmg);
                    SkillInfo skillInfo = SkillData.skillList[bc.skill];
                    skill_delay = Math.Max(skill_delay, skillInfo.delay);
                    p.addBytes(makeExecutionPacket(bc, i > 0));
                }
                nextcmd += cmd_combo_list.Count;
            }
            else
            {
                //Logger.Error("execCommand " + skill_delay);
                //Logger.Info(position[c.init_row][c.init_col].type + " dmg [" + c.init_row + "][" + c.init_col + "]: " + c.dmg);
                p.addBytes(makeExecutionPacket(c, false));
                nextcmd++;
            }

            // Console.WriteLine("send : " + BitConverter.ToString(p.getData()));
            //battleBroadcast(p.send()); <<--ของเดิม
            //ของใหม่/////////////////////////////////////
            //Logger.Error(BitConverter.ToString(p.getData()));
            byte[] pCommand = p.send();
            battleBroadcast(pCommand);

            BattleParticipant init = position[c.init_row][c.init_col];
            ///พวกนี้จะเป็นเอฟเฟคต่างๆหลังใช้สกิล หรือพิษเสียเลือด
            if (init.outBattle && init.type == TSConstants.BT_POS_TYPE_CHR && init.chr != null)
            {
                TSCharacter chr = init.chr;

                //out battle
                PacketCreator p_out;
                if (c.skill != 14002) //ถ้าไม่ใช่ วิชาหลบหนี
                {
                    p_out = new PacketCreator(new byte[] { 0x0b, 0x01, c.init_row, c.init_col, 0 });
                    battleBroadcast(p_out.send());
                }
                if (position[c.init_row][c.init_col].chr.pet_battle > -1)
                {
                    int pet_row = c.init_row < 2 ? 1 : 2;
                    if (position[pet_row][c.init_col] != null && position[pet_row][c.init_col].exist)
                    {
                        position[pet_row][c.init_col].outBattle = true;
                        p_out = new PacketCreator(new byte[] { 0x0b, 0x01, position[pet_row][c.init_col].row, c.init_col, 0 });
                        battleBroadcast(p_out.send());
                    }
                }
                p_out = new PacketCreator(0x0b, 0x04);
                p_out.addByte(0x02);
                p_out.add32(chr.client.accID);
                p_out.addZero(2);//00-00
                p_out.addByte(0);
                chr.replyToMap(p_out.send(), false);
                //end out battle
            }
            //Logger.Text("position[" + c.init_row + "][" + c.init_col + "]" + position[c.init_row][c.init_col].exist);

            //ไม่ใช่ ต้นไม้ดูดเลือด พิษเสียเลือด สะท้อนบาดเจ็บ
            if (c.skill < 20001 || c.skill > 20003)
            {
                //if (c.type == 0) System.Threading.Thread.Sleep(SkillData.skillList[c.skill].delay * 80);
                int mul_delay = TSServer.config.battle_speed;
                int final_delay = 1000; //1s
                if (finish == 3) //หนี
                {
                    final_delay = 500;
                }
                else if (c.type == 0 && skill_delay > 0) //ปกติ
                {
                    final_delay = skill_delay * mul_delay;
                }
                else if (c.type == 1) //กินของ
                {
                    final_delay = 2400;
                }
                else if (c.type == 2) //ยันต์
                {
                    //Console.WriteLine($"ยัน {c.skill}");
                    final_delay = SkillData.skillList.ContainsKey(c.skill) ? SkillData.skillList[c.skill].delay * mul_delay : 2000;
                }
                //Console.WriteLine("final_delay {0}", final_delay);
                System.Threading.Thread.Sleep(final_delay);

                if (finish > 0) return;

                //after-BattleCommand effect
                for (byte i = 0; i < 4; i++)
                    for (byte j = 0; j < 5; j++)
                    {
                        if (position[i][j].outBattle)
                            checkOutBattle(position[i][j]);

                        position[i][j].checkCommandEffect();
                        //position[i][j].purge_status();
                    }

                //System.Threading.Thread.Sleep(300);
            }

            //วิชาแบ่งร่าง, ซ่อนกายสายลม ย้ายไปอยู่ใน BattleParticipant.checkCommandEffect
            //if (c.skill == 13008 || c.skill == 13032) 
            //{
            //    BattleParticipant bp_dest = position[c.dest_row][c.dest_col];
            //    byte row_pet = bp_dest.getTeamSameRow();
            //    if (bp_dest.type == TSConstants.BT_POS_TYPE_CHR && row_pet < 4 && !bp_dest.haveClone())//ถ้ายังไม่มีร่างโคลน
            //    {
            //        int[] turn_count = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl);
            //        battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, row_pet, c.dest_col }).send());
            //        BattleCharClone charClone = new BattleCharClone(this, countEnemy, bp_dest);
            //        charClone.turnCount = c.skill == 13008 ? turn_count[0] : turn_count[1];

            //        if (!position[row_pet][c.dest_col].exist || (position[row_pet][c.dest_col].exist && position[row_pet][c.dest_col].death))
            //        {
            //            if (c.dest_row > 1)
            //                countAlly += 1;
            //            else
            //                countEnemy += 1;
            //        }
            //        position[row_pet][c.dest_col].purge_type = 3; //clear all purge status
            //        position[row_pet][c.dest_col].purge_status(); //แยกร่าง
            //        position[row_pet][c.dest_col].cloneIn(charClone);
            //        position[row_pet][c.dest_col].exist = false; //ให้ฝั่งตรงข้ามไม่ตีตัวโคลนก่อน
            //        var p2 = new PacketCreator(0x0B, 0x05);
            //        p2.addBytes(position[row_pet][c.dest_col].announce(5, countAlly).getData());
            //        //Logger.Info(BitConverter.ToString(p2.getData()));
            //        battleBroadcast(p2.send());

            //        if (c.skill == 13032 && bp_dest.buff_type == 0) //ถ้ายังไม่มีบัพ และใช้สกิลซ่อนกายสายลม ก็ให้ยัดบัพลงไป
            //        {
            //            bp_dest.buff_type = c.skill;
            //            bp_dest.buff = turn_count[0];
            //        }
            //    }
            //}
        }

        public void executeSummonCounter(BattleCommand c)
        {
            if (c.isSummonSkill)
            {
                BattleParticipant pos = position[c.init_row][c.init_col];
                if (pos != null)
                {
                    if (pos.type == TSConstants.BT_POS_TYPE_CHR || pos.type == TSConstants.BT_POS_TYPE_CLONE)
                    {
                        if (pos.type == TSConstants.BT_POS_TYPE_CLONE)
                        {
                            byte ownerRow = c.init_row == 1 ? (byte)0 : (byte)3;
                            pos = position[ownerRow][c.init_col];
                        }
                        if (pos.chr.equipment[5] != null && ItemData.itemList.ContainsKey(pos.chr.equipment[5].Itemid) && ItemData.itemList[pos.chr.equipment[5].Itemid].unk3 == 59) //unk3 = 59 คือตราเชิญ
                        {
                            pos.chr.counter.summon++;
                            pos.chr.calcSummonSkill();
                            TSEquipment eqm = pos.chr.equipment[5];
                            byte dur;
                            if (eqm.duration < 150)
                                dur = 150;
                            else if (eqm.duration < 200)
                                dur = 200;
                            else
                                dur = 250;
                            eqm.setDuration(dur);

                            if (eqm.duration >= 250)
                            {
                                eqm.setBroken();
                                pos.chr.removeSummonSkill();
                            }
                        }
                        else
                        {
                            c.skill = 10000;
                            c.skill_lvl = 1;
                        }
                    }
                    else if (pos.type == TSConstants.BT_POS_TYPE_PET)
                    {
                        if (pos.pet.equipment[5] != null && ItemData.itemList.ContainsKey(pos.pet.equipment[5].Itemid) && ItemData.itemList[pos.pet.equipment[5].Itemid].unk3 == 59) //unk3 = 59 คือตราเชิญ
                        {
                            pos.pet.counter.summon++;
                            pos.pet.refreshSummonLevel();
                            TSEquipment eqm = pos.pet.equipment[5];
                            byte dur;
                            if (eqm.duration < 150)
                                dur = 150;
                            else if (eqm.duration < 200)
                                dur = 200;
                            else
                                dur = 250;
                            eqm.setDuration(dur);

                            if (eqm.duration >= 250)
                            {
                                eqm.setBroken();
                            }
                            //pos.pet.sendInfo();
                            //PacketCreator p1 = new PacketCreator(0x0f, 8);
                            //p1.addBytes(pos.pet.sendInfo());
                            //pos.pet.owner.reply(p1.send());
                        }
                        else
                        {
                            c.skill = 10000;
                            c.skill_lvl = 1;
                        }
                    }
                }
            }
        }

        public byte[] makeExecutionPacket(BattleCommand c, bool combo)
        {
            //Logger.Error("makeExecutionPacket " + c.dmg);
            //BattleParticipant init = position[c.init_row][c.init_col];
            //if (init.chr != null)
            //    Console.WriteLine("{0}\t{1}", init.chr.name, init.getAgi());
            //else if (init.pet != null)
            //    Console.WriteLine("{0}\t{1}", init.pet.name, init.getAgi());
            byte count = 0;
            bool isSkillHeal = SkillData.skillHealPartyList.ContainsKey(c.skill);
            PacketCreator temp = new PacketCreator();
            int nb_target = 1;

            if (c.type != 0)
            {
                position[c.init_row][c.init_col].useItem(c.skill);
                if (c.type == 1) nb_target = 1;
                else if (c.type == 2)
                {
                    c.skill = ItemData.itemList[c.skill].unk9;
                    nb_target = SkillData.skillList[c.skill].nb_target;

                    //แสดงเอฟเฟคยันต์
                    //F4 44 13 00 32 01 0F 00 00 02 28 4E 01 01 03 02 01 03 01 E0 00 00
                    PacketCreator p_yun = new PacketCreator(0x32, 0x01);
                    p_yun.addByte(0x0f);
                    p_yun.addByte(0x00);
                    p_yun.addByte(c.init_row);
                    p_yun.addByte(c.init_col);
                    p_yun.add16(20008); //4E28 ยันต์
                    p_yun.addByte(1);
                    p_yun.addByte(1);
                    p_yun.addByte(c.dest_row);
                    p_yun.addByte(c.dest_col);
                    p_yun.addByte(1);//hit
                    p_yun.addByte(3);//dest_anim
                    p_yun.addByte(1);//nb_effect
                    p_yun.addByte(0xe0);//effect_code
                    p_yun.add16(0); //dmg
                    p_yun.addByte(1);//effect_type
                    //Console.WriteLine(BitConverter.ToString(p_yun.getData()));
                    battleBroadcast(p_yun.send());
                }
            }
            else
            {
                if (c.skill > 20003 || c.skill < 20001) nb_target = SkillData.skillList[c.skill].nb_target; //ต้นไม้ดูดเลือด พิษเสียเลือด สะท้อนบาดเจ็บ

                //if (c.skill == 11009 || c.skill == 11010)
                //    nb_target = c.skill_lvl < 4 ? 1 : c.skill_lvl < 7 ? 3 : c.skill_lvl < 10 ? 6 : 8;


                ushort[] skill_buff_paty = new ushort[]
                {
                    14012, //ร่วมใจ
                    14013, //ปลุกใจ
                };
                if (isSkillHeal || skill_buff_paty.Contains(c.skill))
                    nb_target = c.skill_lvl < 4 ? 1 : c.skill_lvl < 7 ? 3 : c.skill_lvl < 10 ? 6 : 8;

                //Logger.SystemWarning(c.dest_row + " " + c.dest_col);

                // sp_cost
                int sp_cost = 0;
                if (SkillData.skillList.ContainsKey(c.skill))
                {
                    sp_cost = SkillData.skillList[c.skill].sp_cost;
                }
                position[c.init_row][c.init_col].setSp(-sp_cost);
                position[c.init_row][c.init_col].refreshSp();
            }
            //Logger.Error("makeExecutionPacket " + c.dmg);
            //ushort tmpDmg = c.dmg;
            //ushort tmpSubDmg = c.sub_dmg;
            //Logger.Debug("makeExecutionPacket nb_target " + nb_target);
            double[] divides = new double[]
            {
                0,
                TSServer.config.damage_divide.target_1,
                TSServer.config.damage_divide.target_2,
                TSServer.config.damage_divide.target_3,
                TSServer.config.damage_divide.target_4,
                TSServer.config.damage_divide.target_5,
                TSServer.config.damage_divide.target_6,
                TSServer.config.damage_divide.target_7,
                TSServer.config.damage_divide.target_8,
            };
            comboList.Add(new Tuple<byte, byte>(c.init_row, c.init_col));//มีปัญหาเด้ง
            switch (nb_target)
            {
                case 8: //กวาด 10
                    {
                        //c.dmg = (ushort)(c.dmg / 4); <<< ของเดิม
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        //Logger.Error("makeExecutionPacket " + c.dmg);
                        byte r = (byte)(c.dest_row >= 2 ? 2 : 0);
                        for (byte i = r; i < r + 2; i++)
                            for (byte j = 0; j < 5; j++)
                                if (position[i][j].exist && !position[i][j].death)
                                {
                                    //Logger.SystemWarning("makeExecutionPacket position[" + i + "][" + j + "] " + c.dmg);
                                    count++;
                                    temp.addBytes(getSkillEffect(i, j, c));
                                }
                        //Logger.Success("Count " + count);

                        break;
                    }
                case 7: // hong thuy, ngu loi กวาด 5 ตำแหน่งเช่นน้ำป่า
                    {
                        //c.dmg = (ushort)(c.dmg / 3);
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        for (byte j = 0; j < 5; j++)
                            if (position[c.dest_row][j].exist && !position[c.dest_row][j].death)
                            {
                                count++;
                                temp.addBytes(getSkillEffect(c.dest_row, j, c));
                            }
                        //Logger.Success("Count " + count);
                        break;
                    }
                case 6: //6 ตำแหน่ง หน้า 3, หลัง 3
                    {
                        //c.dmg = (ushort)(c.dmg / 3);
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        count++;
                        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                        byte r1 = (byte)(c.dest_row == 0 || c.dest_row == 1 ? 0 : 2);
                        for (byte i = r1; i < r1 + 2; i++)
                            for (int j = c.dest_col - 1; j <= c.dest_col + 1; j++)
                                if (j >= 0 && j < 5)
                                    if (position[i][j].exist && !position[i][j].death)
                                    {
                                        if (i != c.dest_row || j != c.dest_col)
                                        {
                                            //Console.WriteLine("{0} {1}", i, j);
                                            count++;
                                            temp.addBytes(getSkillEffect(i, (byte)j, c));
                                        }
                                    }
                        //Console.WriteLine("------{0}------\n",count);
                        break;
                    }
                case 5: // bang da', phi sa tau thach 4 ตำแหน่งแบบตัว T (ทรายบิน ลูกเห็บ เป็นต้น)
                    {
                        //c.dmg = (ushort)(c.dmg / 3);
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        count++;
                        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                        for (int j = c.dest_col - 1; j <= c.dest_col + 1; j += 2)
                            if (j >= 0 && j < 5)
                                if (position[c.dest_row][j].exist && (combo || !position[c.dest_row][j].death))
                                {
                                    count++;
                                    temp.addBytes(getSkillEffect(c.dest_row, (byte)j, c));
                                }
                        sbyte r2 = (sbyte)(c.dest_row == 0 || c.dest_row == 2 ? 1 : -1);
                        if (position[c.dest_row + r2][c.dest_col].exist && (combo || !position[c.dest_row + r2][c.dest_col].death))
                        {
                            count++;
                            temp.addBytes(getSkillEffect((byte)(c.dest_row + r2), c.dest_col, c));
                        }
                        break;
                    }
                case 4: // loan kich 3 ตำแหน่ง (โจมตีกระจาย, พลังสายฟ้าสะเทือน)
                    {
                        //c.dmg = (ushort)(c.dmg / 2);
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        count++;
                        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                        //Console.WriteLine("main_col {0}", c.dest_col);
                        for (int j = c.dest_col - 1; j <= c.dest_col + 1; j += 2)
                        {
                            #region โค๊ดเดิม
                            //if (j >= 0 && j < 5)
                            //{
                            //    //if (position[c.dest_row][j].exist && (combo || !position[c.dest_row][j].death))
                            //    if (position[c.dest_row][j].exist && !position[c.dest_row][j].death)
                            //    {
                            //        //Console.WriteLine("col: {0}", j);
                            //        count++;
                            //        temp.addBytes(getSkillEffect(c.dest_row, (byte)j, c));
                            //    }
                            //    else
                            //    {
                            //        count++;
                            //        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                            //    }
                            //}
                            //else
                            //{
                            //    count++;
                            //    temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                            //}
                            #endregion
                            byte near_col = c.dest_col;
                            if (j >= 0 && j < 5)
                            {
                                BattleParticipant bp_near = position[c.dest_row][j];
                                if (bp_near.exist && !bp_near.death)
                                    near_col = (byte)j;
                            }
                            //Console.WriteLine("near_pos {0}", near_col);
                            count++;
                            temp.addBytes(getSkillEffect(c.dest_row, near_col, c));
                        }
                        break;
                    }
                case 3: //3 ตำแหน่ง (นูไฟ ...)
                    {
                        //c.dmg = (ushort)(c.dmg / 2);
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        count++;
                        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                        for (int j = c.dest_col - 1; j <= c.dest_col + 1; j += 2)
                            if (j >= 0 && j < 5)
                                if (position[c.dest_row][j].exist && (combo || !position[c.dest_row][j].death))
                                {
                                    count++;
                                    temp.addBytes(getSkillEffect(c.dest_row, (byte)j, c));
                                }
                        break;
                    }
                case 2: // 2 ตำแหน่ง (หินถล่ม, คำราม ...)
                    {
                        //c.dmg = (ushort)(c.dmg / 2);
                        c.dmg = isSkillHeal ? c.dmg : (ushort)(c.dmg / divides[nb_target]);
                        c.sub_dmg = isSkillHeal ? c.sub_dmg : (ushort)(c.sub_dmg / divides[nb_target]);
                        count++;
                        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));
                        sbyte r3 = (sbyte)(c.dest_row == 0 || c.dest_row == 2 ? 1 : -1);
                        if (position[c.dest_row + r3][c.dest_col].exist && (combo || !position[c.dest_row + r3][c.dest_col].death))
                        {
                            count++;
                            temp.addBytes(getSkillEffect((byte)(c.dest_row + r3), c.dest_col, c));
                        }
                        break;
                    }
                default: //1 ตำแหน่งแล้วละอันนี้
                    {
                        //Console.WriteLine("skill {0}\tnb_target {1}", c.skill, nb_target);
                        count++;
                        if (divides.Contains(nb_target))
                        {
                            c.dmg = (ushort)(c.dmg / divides[nb_target]);
                        }
                        temp.addBytes(getSkillEffect(c.dest_row, c.dest_col, c));//มีปัญหา
                        break;
                    }
            }
            //Logger.Error(c.dmg.ToString());
            //Logger.Success("Count " + count);

            //Console.WriteLine($"nb_target: {nb_target}\tcount:{count}");

            byte[] command_data = temp.getData();
            //Logger.Debug(BitConverter.ToString(command_data));
            PacketCreator ret = new PacketCreator();
            ret.add16((ushort)(6 + command_data.Length)); //total length
            ret.add8(c.init_row); ret.add8(c.init_col);
            if (c.type != 1) ret.add16(c.skill);
            else ret.add16(19001); //สิ่งของ
            ret.add8((byte)nb_target);
            ret.add8(count); //nb of target affected
            ret.addBytes(command_data);
            //Console.WriteLine(BitConverter.ToString(ret.getData()));

            return ret.getData();
        }

        public byte[] getSkillEffect(byte row, byte col, BattleCommand c)
        {
            BattleParticipant init = position[c.init_row][c.init_col];
            BattleParticipant dest = position[row][col];
            byte dest_anim = 0;
            byte nb_effect = 1;
            byte effect_code = 0;
            byte effect_type = 1;
            int final_dmg = 0;
            //Console.WriteLine("getSkillEffect init.type {0}", init.type);
            SkillInfo skillInfo = new SkillInfo();
            if (c.type != 1)
                skillInfo = SkillData.skillList[c.skill];

            if (skillInfo.nb_target > 0)
            {
                Tuple<byte, byte> tmp = hitedList.FirstOrDefault(e => e != null && e.Item1 == row && e.Item2 == col);//มีปัญหาเด้ง
                if (tmp == null)
                {
                    hitedList.Add(new Tuple<byte, byte>(row, col));
                    //Console.WriteLine("added [{0}][{1}]", row, col);
                }
            }

            ushort tmp_dmg = c.dmg;
            ushort tmp_sub_dmg = c.sub_dmg;
            //Logger.SystemWarning("getSkillEffect " + c.dmg);
            //int init_elem = init.getElem();
            int dest_elem = dest.getElem();
            int elem_coef = getElementCoef(c, init, dest);
            //if (!isSkillHeal)
            //    switch (init_elem)
            //    {
            //        case 1: //earth
            //            if (dest_elem == 2) elem_coef = 1.2;
            //            else if (dest_elem == 4) elem_coef = 0.8;
            //            break;
            //        case 2: //water
            //            if (dest_elem == 3) elem_coef = 1.2;
            //            else if (dest_elem == 1) elem_coef = 0.8;
            //            break;
            //        case 3: //fire
            //            if (dest_elem == 4) elem_coef = 1.2;
            //            else if (dest_elem == 2) elem_coef = 0.8;
            //            break;
            //        case 4: //wind
            //            if (dest_elem == 1) elem_coef = 1.2;
            //            else if (dest_elem == 3) elem_coef = 0.8;
            //            break;
            //        default:
            //            break;
            //    }

            byte effect = 1;
            if (c.skill >= 13016 && c.skill <= 13018) { effect = 3; }//เชิญมังกร;
            else if (c.skill >= 10017 && c.skill <= 10019) { effect = 15; } //เชิญหินผา;
            else if (c.skill >= 11017 && c.skill <= 11019) effect = 18; //เชิญวารี;
            //else if (c.skill >= 12017 && c.skill <= 12019) c.dmg = Math.Min((ushort)(c.dmg * (c.skill_lvl / 3)), (ushort)50000); //boost dmg phoenix
            else if (c.skill >= 20001 && c.skill <= 20003) effect = 20;
            else if (c.type == 1) effect = 13; //item
            else effect = skillInfo.skill_type;

            byte hit = 1;
            if (effect != 20)
            {
                ///[จอมยุทธ]พลังจิตอ่านใจศัตรู 14045
                ///ขณะที่ใกล้ตายมีโอกาสหลบการโจมตีได้ระดับหนึ่ง มีผลเมื่อเลือดเหลือน้อยกว่า30%
                if (
                    (effect == 1 || effect == 2)
                    && c.skill != 14054 //ไม่ใช่ผ่า
                    && dest.getSkillLvl(14045) > 0
                    && dest.getHp() <= (dest.getMaxHp() * 0.3)
                    && RandomGen.getInt(0, 101) < SkillData.skillList[14045].unk19)
                {
                    hit = 0;
                    battleBroadcast(new PacketCreator(new byte[] { 0x35, 0x0C, dest.row, dest.col }).send()); //พริ้วลอยติ้วววววว
                    //Console.WriteLine("Miss because skill 14045");
                }
                else if (c.skill == 14054)//สายฟ้าลงทัณฑ์
                {
                    hit = 1;
                }
                else
                {
                    hit = calculateHit(row, col, ref c, effect, elem_coef);
                }
            }

            //	1	11027	กงจักรน้ำแข็งพันปี
            //	1	11028	หมัดเกล็ดหิมะ
            //	5	13007	โจมตีสลบ
            //	5	13029	หมอกภูผา
            //	5	20016	หัวมังกร
            //	5	20020	3เต่าพิฆาต
            //	7	14033	วิชายิงธนู
            //	7	14036	ร้อยก้าวผ่าตะวัน
            //	7	14037	ธนูรำเก้าฟ้า
            //	7	14042	พลังโกรธาพิชิตศัตรู
            //	7	16002	ประตูเมือง
            //	7	20033	กระบี่ไร้ปราณี
            byte[] hit_disable_state = new byte[] { 1, 5, 7 };

            //	175	21001	พลองสะท้านดิน
            //	179	12030	อัญเชิญสายฟ้าอัคคี
            //	179	12031	หมัดหงส์อัคคี
            //	179	12032	เทพอัคคีทำลายล้าง
            //	179	21014	เคียวอัคคีฟ้า
            //	181	10034	มังกรกลืนพสุธา
            byte[] hit_debuff_state = new byte[] { 175, 179, 181 };

            switch (effect)
            {
                case 1:
                case 2:
                    {
                        effect_code = 0x19;
                        if (hit == 1)
                        {
                            int fixed_dmg = 0;
                            //0=โดน, 1=กัน, 2=miss
                            dest_anim = 0;
                            //สายฟ้าลงทัณฑ์
                            if (c.skill == 14054)
                            {
                                //final_dmg = dest.getHp() - 1;
                                //fixed_dmg = dest.getHp() - 1;
                                if (dest.isNpcBoss()) //ถ้าผ่าบอสก็ให้โดนตัวเองทันที
                                {
                                    row = init.row;
                                    col = init.col;
                                    dest = position[init.row][init.col];
                                }
                                else
                                {
                                    int rnd = randomize.getInt(1, 101);
                                    if ((rnd - (c.skill_lvl * 2)) > SkillData.skillList[14054].unk19)
                                    {
                                        row = init.row;
                                        col = init.col;
                                        dest = position[init.row][init.col];
                                    }
                                }
                                fixed_dmg = dest.getHp() - 1;
                            }
                            //ม่านคุ้มกัน
                            else if (dest.buff_type == 10010 || dest.buff == 10031)
                            {
                                final_dmg = 0;
                                dest_anim = 1;
                            }
                            //กำแพงน้ำแข็ง
                            //else if (dest.buff_type == 11002 && dest.buff_lv == 5 && elem_coef == -1) 
                            //{
                            //    fixed_dmg = 1;
                            //}
                            else
                            {
                                final_dmg = calcFinalDmg(c, init, dest, elem_coef);
                                //final_dmg = 100; //ชั่วคราว
                                //Console.WriteLine("final_dmg: {0}\t{1}", final_dmg, dest.buff_lv);
                                if (SkillData.skillSummonFire.Contains(c.skill)) //boost or reduce phoenix
                                    final_dmg = (int)Math.Floor(final_dmg * 0.8);
                                //final_dmg = 1007;
                                bool decrease_buff = false;
                                switch (dest.buff_type)
                                {
                                    case 10015: //กระจก
                                        {
                                            init.reflect_dmg += (ushort)final_dmg;
                                            init.reflect = dest.buff_type;
                                            final_dmg = 0;
                                            dest_anim = 1;
                                            decrease_buff = true;
                                            break;
                                        }
                                    case 10031: //ระฆังทองคุ้มกาย
                                        {
                                            init.reflect_dmg += (ushort)(final_dmg * 0.1);
                                            init.reflect = dest.buff_type;
                                            final_dmg = 0;
                                            dest_anim = 1;
                                            break;
                                        }
                                    case 12024: //ตะวันคุ้มกาย
                                        {
                                            init.reflect_dmg += (ushort)Math.Floor(final_dmg * 0.5);
                                            init.reflect = dest.buff_type;
                                            final_dmg = final_dmg - init.reflect_dmg;
                                            int init_hp = init.getHp();
                                            if (init_hp - init.reflect_dmg < 1)
                                                init.reflect_dmg = (ushort)(init_hp - 1);
                                            dest_anim = 1;
                                            decrease_buff = true;
                                            break;
                                        }
                                    case 13021: //เคลื่อนดาวย้ายดารา
                                        {
                                            ushort reflect_dmg = (ushort)Math.Floor(final_dmg * 0.1);

                                            for (int i = 0; i < 5; i++)
                                            {
                                                BattleParticipant bp_reflect1 = position[init.row][i];
                                                BattleParticipant bp_reflect2 = position[getSecondRow(init.row)][i];
                                                if (bp_reflect1.exist && !bp_reflect1.death)
                                                {
                                                    bp_reflect1.reflect_dmg = (ushort)(bp_reflect1.reflect_dmg + reflect_dmg);
                                                    bp_reflect1.reflect = dest.buff_type;
                                                }
                                                if (bp_reflect2.exist && !bp_reflect2.death)
                                                {
                                                    bp_reflect2.reflect_dmg = (ushort)(bp_reflect2.reflect_dmg + reflect_dmg);
                                                    bp_reflect2.reflect = dest.buff_type;
                                                }
                                            }
                                            final_dmg = (int)Math.Floor(final_dmg * 0.9);
                                            dest_anim = 1;
                                            decrease_buff = true;
                                            break;
                                        }
                                }
                                switch (dest.aura_type)
                                {
                                    case 14044: //เกราะเทพเบื้องบน
                                        {
                                            final_dmg = Math.Max(final_dmg - 1500, 1);
                                            break;
                                        }
                                    case 14046: //เกราะคุ้มลมปราณ
                                        {
                                            int sp_dmg = (int)Math.Floor(final_dmg * 0.1);
                                            int hp_dmg = final_dmg % 10;
                                            //Console.WriteLine("SP {0}", Math.Floor(final_dmg * 0.1));
                                            //Console.WriteLine("HP {0}", final_dmg % 10);
                                            final_dmg = hp_dmg;
                                            c.sub_dmg = (ushort)sp_dmg;
                                            nb_effect = 2;
                                            break;
                                        }
                                }

                                //Console.WriteLine("init.buff_type {0}", init.buff_type);
                                //if(init.buff_type == 13014 || init.buff_type == 20024) //พลังปราณ | พลังปราณ10คน
                                if (SkillData.skillList.ContainsKey(init.buff_type) && SkillData.skillList[init.buff_type].state == 106) //พลังปราณ | พลังปราณ10คน
                                {
                                    //init.reflect_heal = 1;
                                    //init.reflect_dmg = (ushort)Math.Min(Math.Floor(final_dmg * 0.5), this.dmg_max);
                                    init.reflect_heal_hp = (ushort)Math.Min(Math.Floor(final_dmg * 0.5), this.dmg_max);
                                }

                                //เอาบัพพกระจกออกเมื่อโดนตีครบจำนวน
                                if (decrease_buff)
                                {
                                    dest.buff--;
                                    //Console.WriteLine("ลดพัพจากการโดนโจมตี {0}", dest.buff);
                                    if (dest.buff <= 0)
                                    {
                                        //Console.WriteLine("โดนตีบัพแตกกกกกกก");
                                        dest.buff = 0;
                                        dest.buff_type = 0;
                                        dest.buff_lv = 0;
                                        battleBroadcast(new PacketCreator(new byte[] { 0x35, 0x01, row, col, 2, 0, 0 }).send());
                                    }
                                }
                            }

                            if (init.reflect > 0 && init.reflect_dmg > 0)
                            {
                                int rnd_dmg = randomize.getInt(-10, 10);
                                int reflect_dmg = init.reflect_dmg + rnd_dmg;
                                reflect_dmg = Math.Min(reflect_dmg, this.dmg_max);
                                reflect_dmg = Math.Max(reflect_dmg, 1);
                                init.reflect_dmg = (ushort)reflect_dmg;
                            }

                            if (fixed_dmg > 0)
                            {
                                final_dmg = fixed_dmg;
                            }
                            else if (final_dmg > 0)
                            {
                                //คริชุด
                                ushort[] cri = init.getCritical();
                                ushort cri_dmg_equip = cri[0];
                                if (cri_dmg_equip > 0)
                                {
                                    int rnd_cri_dmg = randomize.getInt(0, 101);
                                    if (cri_dmg_equip >= rnd_cri_dmg)
                                    {
                                        //Console.WriteLine("ติดครินะ {0}/{1}", rnd_cri_dmg, cri_dmg_equip);
                                        final_dmg *= 2;
                                    }
                                }

                                int rnd_dmg = randomize.getInt(-10, 10);
                                final_dmg += rnd_dmg;
                                final_dmg = Math.Min(final_dmg, this.dmg_max);
                                final_dmg = Math.Max(final_dmg, 1);
                            }

                            //if(init.row == 2)
                            //    final_dmg = 1;
                            //if(init.type == TSConstants.BT_POS_TYPE_CHR)
                            //    final_dmg = 26400;
                            //Console.WriteLine("Final dmg {0}", final_dmg);
                            //if (init.type == TSConstants.BT_POS_TYPE_CHR)
                            //{
                            //    Console.WriteLine("Init buff: {0}\tdmg: {1}", init.buff_type, final_dmg);
                            //}

                            dest.setHp(-final_dmg);
                            dest.refreshHp();//มีปัญหา
                            //checkDeath(row, col, c); //ต้องย้ายไปเช็คืั้หลังจากออกตี execute

                            //ถ้าเป้าหมายยังไม่ตาย ก็มาสุ่มพวกสกิลดาเมจ+ติดสถานะ
                            if (!dest.death)
                            {
                                bool hit_disable = false;
                                if (dest.disable <= 0 && hit_disable_state.Contains(skillInfo.state) && final_dmg > 0 && !dest.isNpcBoss())
                                {
                                    int per_hit = SkillData.skillList.ContainsKey(c.skill) ? SkillData.skillList[c.skill].unk19 : 0;
                                    int rnd = randomize.getInt(0, 101);
                                    hit_disable = rnd <= per_hit;
                                    //Console.WriteLine("hit_disable {0}/{1} = {2}", rnd, per_hit, hit_disable);
                                }

                                bool hit_debuff = false;
                                if (dest.debuff <= 0 && hit_debuff_state.Contains(skillInfo.state) && final_dmg > 0 && !dest.isNpcBoss())
                                {
                                    int per_hit = SkillData.skillList.ContainsKey(c.skill) ? SkillData.skillList[c.skill].unk19 : 0;
                                    int rnd = randomize.getInt(0, 101);
                                    hit_debuff = rnd <= per_hit;
                                    //Console.WriteLine("hit_debuff {0}/{1} = {2}", rnd, per_hit, hit_disable);
                                }

                                if (hit_disable || hit_debuff)
                                    nb_effect = (byte)((init.isNpcBoss() && dest.debuff_type == 10026) ? 1 : 2); //ถึงแม้จะติดแต่ถ้าบอสโจมตีใส่กระจกทองก็ให้คืนค่ากลับเป็น 1
                            }

                            //เซียน พลังแปรเปลี่ยน
                            //เมื่อถูกศัตรูโจมตี ค่าเลือดที่สูญเสียไปจะกลายเป็น SP ในสัดส่วนหนึ่ง แต่ค่าเลือดยังคงจะสูญเสีย
                            //อัตราการเพิ่มของ SP จะอยู่ที่ประมาณ 15% คือถ้าถูกโจมตี 100 SP จะเพิ่มเท่ากับ 15 เป็น Passive Skill
                            if (dest != null && dest.exist && !dest.death && dest.getSkillLvl(14052) > 0 && dest.type != TSConstants.BT_POS_TYPE_NPC)
                            {
                                ushort reflect_sp = (ushort)Math.Floor(Math.Min(dest.getMaxSp() - dest.getSp(), final_dmg * 0.15));
                                if (reflect_sp < 0) reflect_sp = 1;
                                //Console.WriteLine("dest.getSkillLvl(14052) {0}", dest.getSkillLvl(14052));
                                int amount = Math.Min(dest.getMaxSp() - dest.getSp(), reflect_sp);
                                dest.reflect_heal_sp += (ushort)amount;
                                //if (amount > 0)
                                //{
                                //    dest.reflectHeal(row, col, row, col, 0x1a, (ushort)amount);
                                //    dest.setSp(amount);
                                //    dest.refreshSp();
                                //}
                            }

                            if (dest.def) dest_anim = 1;
                        }
                        else
                        {
                            dest_anim = 2;
                            final_dmg = 0;
                        }
                        break;
                    }
                case 3: //disable skills ปิศาจต้นไม้ | น้ำแข็งปิดคลุม | ลมสลาตัน | หลับไหล | มังกรเขียว | ปิศาจต้นไม้3คน | น้ำแข็งปิดคลุม3คน | ลมสลาตัน3คน | ฝ่ามือยูไล
                    {
                        final_dmg = 0;
                        effect_code = 0xdd;
                        if (hit == 1)
                        {
                            //if (dest.buff_type == 10026 && init.disable <= 0 && !isSkillWindSummon)
                            if (dest.buff_type == 10026) //กระจกศักดิ์สิทธิ์
                            {
                                //Logger.Error("ล๊อคโดนจกทอง สะท้อนไปดิ");
                                dest_anim = 0;
                                effect_code = 0;
                                if (init.disable <= 0 && !init.isNpcBoss())
                                {
                                    //Logger.Error("ล๊อคกลับ");
                                    //init.disable = (byte)(Math.Ceiling((double)c.skill_lvl / 2) + 1);
                                    init.disable = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                                    init.disable_type = c.skill;
                                    init.reflect_disable = c.skill;
                                    countDisabled++;
                                }
                            }
                            else
                            {
                                if (dest.disable <= 0) //ใส่ if ไว้อีกทีกันเหนียว
                                {
                                    dest_anim = 1;
                                    //dest.disable += (byte)(Math.Ceiling((double)c.skill_lvl / 2) + 1); // Sao lai la += ??
                                    //dest.disable = (byte)(Math.Ceiling((double)c.skill_lvl / 2) + 1);
                                    dest.disable = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                                    dest.disable_type = c.skill;
                                    countDisabled++;

                                    if (SkillData.skillSummonWind.Contains(c.skill))
                                    {
                                        ushort amount = (ushort)Math.Floor(dest.getSp() * 0.7);
                                        if (amount > 0)
                                        {
                                            //dest.setSp(-amount);
                                            //dest.refreshSp();

                                            //dest.reflect_heal_sp = -amount;

                                            dest.reflect = c.skill;
                                            dest.reflect_dmg = amount;
                                            init.reflect_heal_hp += amount;
                                        }
                                    }
                                    //Logger.Text("disable skill " + dest.disable_type + " turn " + SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0]);
                                }
                            }

                            //Console.WriteLine(row + " " + col + " get disable " + c.skill);
                        }
                        else
                        {
                            dest_anim = 0;
                            effect_code = 0;
                        }
                        break;
                    }
                case 4: //buff skills ม่านคุ้มกัน | กระจก | กำแพงน้ำแข็ง | หลบหลีก | วิชาซ่อนร่าง | วิชาขยายใหญ่ | พลังปราณ | ร่วมใจ | ปลุกใจ | กระจกศักดิ์สิทธิ์ | ตะวันคุ้มกาย | เคลื่อนดาวย้ายดารา | ไร้รูปไร้ลักษณ์ | พลังปราณ10คน | ระฆังทองคุ้มกาย
                    {
                        final_dmg = 0;
                        effect_code = 0xde;
                        if (hit == 1)
                        {
                            dest_anim = 1;
                            dest.buff = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                            dest.buff_type = c.skill;
                            dest.buff_lv = c.skill_lvl;
                        }
                        else
                            dest_anim = 0;
                        break;
                    }
                case 5: //giai tru สลายสภาวะ | ละลายน้ำแข็ง | สลายหลับไหล | สลายพิษ | สลายสับสน | สลายสภาวะเลื่อนขั้น | พลังชะล้างสรรพสิ่ง | รังสีเซียนคุ้มกาย
                    {
                        ushort[] clear_all_skills = new ushort[]
                        {
                            11012, //สลายสภาวะ
                            11025, //สลายสภาวะเลื่อนขั้น
                            11031, //พลังชะล้างสรรพสิ่ง
                        };
                        final_dmg = 0;
                        dest_anim = 0;
                        effect_code = 0;
                        if (clear_all_skills.Contains(c.skill))
                            nb_effect = 5;
                        break;
                    }
                case 6: //hoi SP วิชาคืนมาร | คืนมารสุดยอด | พลังเทพวารีฟื้นกลับ
                    {
                        final_dmg = init == dest ? (ushort)0 : c.dmg;
                        dest_anim = 0;
                        effect_code = 0x1a;
                        effect_type = 0;
                        break;
                    }
                case 7: //hoi hp //รักษาบาดเจ็บ | สุดยอดการรักษา
                    {
                        final_dmg = c.dmg;
                        dest_anim = 0;
                        effect_code = 0x19;
                        effect_type = 0;
                        break;
                    }
                case 8: //ressurrection วิชาฟื้นคืนชีพ
                    {
                        if (hit == 1)
                        {
                            dest.death = false;
                            if (row < 2) countEnemy++; else countAlly++;
                            goto case 7;
                        }
                        final_dmg = 0;
                        dest_anim = 1;
                        effect_code = 0x19;
                        break;
                    }
                case 9: //phan than //วิชาแบ่งร่าง + ซ่อนกายสายลม
                    {
                        final_dmg = 0;
                        if (hit == 1)
                        {
                            //Console.WriteLine("วิชาแบ่งร่าง + ซ่อนกายสายลม hit");
                            dest.reflect_clone = c.skill;
                            dest_anim = 1;
                            effect_code = 0x19;
                            if (c.skill == 13032 && dest.buff_type == 0) //ถ้ายังไม่มีบัพก็ใส่บัพซ่อนกายสายลมให้
                            {
                                effect_code = 0xde;
                                //nb_effect = 2; //มี 2 effect
                            }
                        }
                        break;
                    }
                //case 10: //ป้องกัน
                //    {
                //        dest_anim = 1;
                //        break;
                //    }
                case 11: // tha luoi -_- จับศัตตรู จับศัตรู | ตาข่ายจับศัตรู | จับศัตรูสำเร็จ | ก้อนข้าวจับตาข่าย | ก้อนข้าวจับสำเร็จ
                    {
                        if (CatchPet(init, dest))
                            if (init.chr != null)
                               if (dest.npc != null)
                                {
                                    c.skill = 15003;
                                    init.chr.addPet((ushort)dest.npc.npcid, 0, 1, "BT Catch");
                                    dest.outBattle = true;
                                    dest_anim = 0;
                                    effect_code = 0x19;
                                    NpcInfo npcInfo = NpcData.npcList[dest.npc.npcid];
                                    if (npcInfo.doanhtrai >= 1 && npcInfo.doanhtrai <= 5)
                                    {                            
                                            int value = init.chr.moral[npcInfo.doanhtrai - 1];
                                            value += npcInfo.level;
                                            value = Math.Min(value, 5000);
                                            init.chr.moral[npcInfo.doanhtrai - 1] = (ushort)value;
                                            init.chr.refreshMoral();
                                        
                                    }
                                }
                        break;
                    }
                case 12: // run
                    {
                        dest = init;
                        if (hit == 1)
                        {
                            bool isChar = init.type == TSConstants.BT_POS_TYPE_CHR && init.chr != null;
                            if (col == 2 && row == 0 && isChar)
                            {
                                finish = 1;
                            }
                            if (col == 2 && row == 3 && isChar)
                            {
                                finish = 3;
                            }
                            else
                            {
                                init.outBattle = true;
                            }
                        }
                        else
                        {
                            c.skill = 18002;
                        }
                        //Console.WriteLine($"[{c.init_row}][{c.init_col}] หนี {c.skill} [{c.dest_col}][{c.dest_row}]");

                        c.dmg = 0;
                        final_dmg = 0;
                        //effect_code = 0x19;
                        //effect_code = 0;
                        //dest_anim = 0;
                        break;
                    }
                case 13: //item, later
                    {
                        if (hit == 1)
                        {
                            ushort prop1 = ItemData.itemList[c.skill].prop1;
                            final_dmg = ItemData.itemList[c.skill].prop1_val;
                            if (ItemData.itemFullHpSp.Contains(c.skill) || ItemData.itemFullHp.Contains(c.skill))
                            {
                                prop1 = 25;
                                c.dmg = (ushort)(dest.getMaxHp() - dest.getHp());
                                final_dmg = c.dmg;
                            }
                            else if (ItemData.itemFullSp.Contains(c.skill))
                            {
                                prop1 = 26;
                                c.dmg = (ushort)(dest.getMaxSp() - dest.getSp());
                                final_dmg = c.dmg;
                            }

                            dest_anim = 0;
                            effect_type = 0;
                            if (prop1 == 25)
                            {
                                if ((final_dmg + dest.getHp()) > dest.getMaxHp())
                                    final_dmg = (ushort)(dest.getMaxHp() - dest.getHp());
                                //Logger.Error("hp: " + dest.getHp()+"/"+ dest.getMaxHp()+" ==> "+final_dmg);
                                effect_code = 0x19;
                                dest.setHp(c.dmg);
                                dest.refreshHp();
                            }
                            else if (prop1 == 26)
                            {
                                if ((final_dmg + dest.getSp()) > dest.getMaxSp())
                                    final_dmg = (ushort)(dest.getMaxSp() - dest.getSp());
                                effect_code = 0x1a;
                                dest.setSp(c.dmg);
                                dest.refreshSp();
                            }
                            if (ItemData.itemList[c.skill].prop2 != 0 || ItemData.itemFullHpSp.Contains(c.skill)) nb_effect = 2;
                            if (ItemData.itemList[c.skill].type == 50) //HHD
                            {
                                //if (prop1 == 25 && final_dmg > dest.getMaxHp()) final_dmg = dest.getMaxHp();
                                //if (prop1 == 26 && final_dmg > dest.getMaxSp()) final_dmg = dest.getMaxSp();
                                dest.death = false;
                                if (c.dest_row < 2) countEnemy++; else countAlly++;
                            }
                        }
                        else
                        {
                            final_dmg = 0;
                            dest_anim = 1;
                            effect_code = 0x19;
                            effect_type = 0;
                        }
                        break;
                    }
                case 14: //ฮิว HP/SP วารีคืนพลัง | มือทิพย์คืนพลัง | น้ำค้างเซียน
                    {
                        nb_effect = 2;
                        final_dmg = c.dmg;
                        dest_anim = 1;
                        effect_code = 0x19;
                        effect_type = 0;
                        break;
                    }
                case 15: //debuff วิชาย่อเล็ก | ปล่อยพิษ | รังสับวุ่น | สับสนวุ่นวาย | จ้าวหินผา | กระสุนอุนจิ | ค่ายกลดินทะลาย | สี่คนสับสน | เต่าพลังเร็ว | ปล่อยพิษ4คน | ลมพิษภูตวิบัติ | ผีดูดพลัง | คุณธรรมไร้พ่าย
                    {
                        final_dmg = 0;
                        effect_code = 0xdf;
                        if (hit == 1)
                        {
                            //จกทอง
                            if (dest.buff_type == 10026)
                            {
                                //Logger.SystemWarning("debuff โดนจกทอง สะท้อนไปดิ");
                                dest_anim = 0;
                                effect_code = 0;
                                if (!init.isNpcBoss() || init.debuff <= 0)
                                {
                                    //Logger.SystemWarning("debuff สะท้อนกลับ");
                                    init.debuff = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                                    init.debuff_type = c.skill;
                                    init.debuff_lv = c.skill_lvl;
                                    init.debuff_init = init;
                                    init.reflect_debuff = c.skill;
                                }
                            }
                            else
                            {
                                dest_anim = 0;
                                dest.debuff = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                                dest.debuff_type = c.skill;
                                dest.debuff_lv = c.skill_lvl;
                                dest.debuff_init = init;

                                //สับสนวุ่นวาย สี่คนสับสน
                                if (c.skill == 14021 || c.skill == 20014)
                                    for (int i = nextcmd; i < cmdReg.Count; i++)
                                        if (cmdReg[i].init_row == row && cmdReg[i].init_col == col) //ถูกแล้ว
                                        {
                                            //Console.WriteLine("{0}=={1} && {2}=={3}", cmdReg[i].init_row, row, cmdReg[i].init_col, col);
                                            cmdReg.RemoveAt(i);
                                        }
                            }
                            //Console.WriteLine(row + " " + col + " get debuff " + c.skill);
                        }
                        else
                            dest_anim = 2;
                        break;
                    }
                case 16: //สลายคุ้มกัน | สลายกระจก
                    {
                        //goto case 5;
                        final_dmg = 0;
                        dest_anim = 0;
                        effect_code = 0;
                        nb_effect = 1;
                        break;
                    }
                //case 17: //คุ้มครองนาย | สลายคุ้มครอง
                //    break;
                case 18: //อัญเชิญวารี | สลายสภาวะสี่คน | สลายสภาวะ6คน
                    {
                        final_dmg = 0;
                        dest_anim = 0;
                        effect_code = 0;
                        nb_effect = 3;
                        if (!dest.death && !isTargetEnemy(init, dest) && dest.debuff == 0 && dest.disable == 0)
                        {
                            final_dmg = c.dmg;
                            effect_code = 0x19;
                            dest_anim = 1;
                            effect_type = 0;
                        }
                        break;
                    }
                case 19: //aura คาถาแผ่นดินไหว | คาถาใจวารี | กระจกใสกลางน้ำ | เวทย์ตะวันเพลิง | วิญญาณกระหายรบ | เวทย์ลมทรนง | จิตรบแห่งลม | แผนการทลายศึก | เกราะเทพเบื้องบน | เกราะคุ้มลมปราณ | พลังเทพพิชิตศัตรู | สายธารแห่งชีวิต | คุณธรรมไร้พ่าย
                    {
                        //Console.WriteLine("aura hit {0}", hit);
                        final_dmg = 0;
                        effect_code = 0xe1;
                        if (hit == 1)
                        {
                            dest_anim = 1;
                            //dest.aura = (byte)(Math.Ceiling((double)c.skill_lvl / 2) + 1); //not really true, fix later
                            dest.aura = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                            dest.aura_type = c.skill;
                            //Console.WriteLine(row + " " + col + " get aura " + c.skill);
                        }
                        else
                            dest_anim = 0;
                        break;
                    }
                case 20: //special case for no anim dmg (reflect, poison, etc.)
                    {
                        final_dmg = c.dmg;
                        effect_code = 0x19;
                        //Console.WriteLine("special dmg " + final_dmg);
                        dest_anim = 0;
                        dest.setHp(-c.dmg);
                        dest.refreshHp();
                        //checkDeath(row, col, c);
                        break;
                    }
                default: goto case 2;
            }

            //Console.WriteLine($"dest_anim {dest_anim}\teffect: {effect}");
            byte[] p = new byte[5 + nb_effect * 4];
            p[0] = row;
            p[1] = col;
            p[2] = hit; //Fly text 0 = Miss, 1 = Dmg
            p[3] = dest_anim; //0 = attcak, 1 = def, 2 = miss
            p[4] = nb_effect;
            p[5] = effect_code;
            p[6] = (byte)final_dmg;
            p[7] = (byte)(final_dmg >> 8);
            p[8] = effect_type;      //effect : 1 : dmg, 0: heal, ...

            //subeffects
            if (nb_effect > 1)
            {
                byte[] subeffect_code = null;
                ushort[] dmg_subeffect = null;
                byte[] subeffect_type = null;
                if (nb_effect == 2)
                {
                    subeffect_code = new byte[1];
                    dmg_subeffect = new ushort[1];
                    subeffect_type = new byte[1];
                    //Logger.Error("c.type " + c.type); 
                    if (c.type == 1) //กินของที่มีทั้ง HP/SP
                    {
                        ushort prop2 = ItemData.itemList[c.skill].prop2;
                        dmg_subeffect[0] = (ushort)ItemData.itemList[c.skill].prop2_val;
                        if (ItemData.itemFullHpSp.Contains(c.skill))
                        {
                            prop2 = 26;
                            dmg_subeffect[0] = (ushort)(dest.getMaxSp() - dest.getSp());
                        }
                        if (prop2 == 25)
                        {
                            //Logger.Error("prop2 = 25, getHp: " + dest.getHp());
                            if ((dmg_subeffect[0] + dest.getHp()) > dest.getMaxHp())
                                dmg_subeffect[0] = (ushort)(dest.getMaxHp() - dest.getHp());
                            subeffect_code[0] = 0x19;
                            dest.setHp(dmg_subeffect[0]);
                            dest.refreshHp();
                        }
                        else if (prop2 == 26)
                        {
                            //Logger.Error("prop2: " + dest.getSp());
                            if ((dmg_subeffect[0] + dest.getSp()) > dest.getMaxSp())
                                dmg_subeffect[0] = (ushort)(dest.getMaxSp() - dest.getSp());
                            subeffect_code[0] = 0x1a;
                            dest.setSp(dmg_subeffect[0]);
                            dest.refreshSp();
                        }
                        subeffect_type[0] = 0;
                    }
                    else if (effect == 14) //ฮิล HP/SP
                    {
                        //Logger.Error("aaaaa "+ c.sub_dmg);
                        subeffect_code[0] = 0x1a;
                        //dmg_subeffect[0] = (ushort)(c.dmg / 5);
                        dmg_subeffect[0] = init == dest ? (ushort)0 : c.sub_dmg;
                        subeffect_type[0] = 0;
                    }
                    if (dest.aura_type == 14046)//เกราะคุ้มลมปราณ
                    {
                        subeffect_code[0] = 0x1a;
                        dmg_subeffect[0] = c.sub_dmg;
                        subeffect_type[0] = 1;
                    }

                    if (hit_disable_state.Contains(skillInfo.state)) //พวกโจมตีสลบ
                    {
                        int turn_count = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                        if (dest.buff_type == 10026) // กระจกศักดิ์สิทธิ์
                        {
                            //ถ้าสะท้อนใส่ npc ที่ไม่ใช่ boss ก็ให้ติดสถานะไป
                            if (!init.isNpcBoss())
                            {
                                init.reflect_disable = c.skill;
                                init.disable_type = c.skill;
                                init.disable = turn_count;
                                init.disable_lv = c.skill_lvl;
                            }
                        }
                        else
                        {
                            subeffect_code[0] = 0xdd;
                            dmg_subeffect[0] = 0;
                            subeffect_type[0] = 1;
                            dest.disable = turn_count;
                            dest.disable_type = c.skill;
                            dest.disable_lv = c.skill_lvl;
                        }
                        countDisabled++;
                    }
                    if (hit_debuff_state.Contains(skillInfo.state))
                    {
                        int turn_count = SkillData.getSkillPurgeCount(c.skill, c.skill_lvl)[0];
                        subeffect_code[0] = 0xdf;
                        dmg_subeffect[0] = 0;
                        subeffect_type[0] = 1;
                        dest.debuff = turn_count;
                        dest.debuff_type = c.skill;
                        dest.debuff_lv = c.skill_lvl;
                    }

                }
                if (nb_effect == 3)
                {
                    if (isTargetEnemy(init, dest))
                    {
                        //Logger.Error("เชิญน้ำใส่ศัตรู");
                        subeffect_code = new byte[] { 0xde, 0xe1 };
                        dmg_subeffect = new ushort[] { 0, 0 };
                        subeffect_type = new byte[] { 1, 1 };
                        dest.purge_type = 1;
                        //dest.clearStateGood();
                    }
                    else
                    {
                        //Logger.Debug("เชิญน้ำใส่พวกเรา");
                        subeffect_code = new byte[] { 0xdd, 0xdf };
                        dmg_subeffect = new ushort[] { 0, 0 };
                        subeffect_type = new byte[] { 0, 0 };
                        dest.purge_type = 2;
                        //dest.clearStateBad();

                        //if (!isTargetEnemy(init, dest) && dest.debuff == 0 && dest.disable == 0)
                        //{
                        //    dest.setHp(c.dmg);
                        //    dest.refreshHp();
                        //}
                    }
                }
                if (nb_effect == 5)//สลาย
                {
                    subeffect_code = new byte[] { 0xdd, 0xde, 0xdf, 0xe1 };
                    dmg_subeffect = new ushort[] { 0, 0, 0, 0 };
                    subeffect_type = isTargetEnemy(init, dest) ? new byte[] { 1, 1, 1, 1 } : new byte[] { 0, 0, 0, 0 };
                    dest.purge_type = 3;
                    //dest.clearStateGood();
                    //dest.clearStateBad();
                    //Console.WriteLine("dest buff = {0}", dest.buff);
                }

                for (int i = 1; i < nb_effect; i++)
                {
                    p[5 + i * 4] = subeffect_code[i - 1];
                    p[6 + i * 4] = (byte)dmg_subeffect[i - 1];
                    p[7 + i * 4] = (byte)(dmg_subeffect[i - 1] >> 8);
                    p[8 + i * 4] = subeffect_type[i - 1];      //effect : 1 : dmg, 0: heal, ...
                }
            }

            //Logger.Debug("p len: " + p.Length);
            //if (p.Length > 9 && (position[c.dest_row][c.dest_col].disable_type == 13007 || position[c.dest_row][c.dest_col].disable_type == 13029) && position[c.dest_row][c.dest_col].disable == 3)
            //{
            //    p[9] = 0xdd;
            //    p[12] = (byte)position[c.dest_row][c.dest_col].disable;
            //    //Logger.Error("aAAAA: " + BitConverter.ToString(p));
            //}
            //Logger.Error("aAAAA: " + BitConverter.ToString(p));
            //Logger.Info("final_dmg [" + c.dest_row + "][" + c.dest_col + "]: " + final_dmg);
            //Logger.Info(position[c.init_row][c.init_col].type + " final_dmg [" + c.init_row + "][" + c.init_col + "]: " + final_dmg);

            c.dmg = tmp_dmg; //<<< เพิ่มตรงนี้เพื่อคืนค่าดาเมจให้กลับเป็นเท่าเดิมเพื่อพวกสกิลหมู่
            c.sub_dmg = tmp_sub_dmg; //<<< เพิ่มตรงนี้เพื่อคืนค่าดาเมจให้กลับเป็นเท่าเดิมพวกสกิลหมู่

            //Logger.Error("ว่าพรือ " + init.getHp() + " " + init.getSp());

            return p;
        }

        public byte calculateHit(byte row, byte col, ref BattleCommand c, byte effect, int elem_coef) //calculate miss or hit here
        {
            BattleParticipant init = position[c.init_row][c.init_col];
            BattleParticipant dest = position[row][col];
            //Console.WriteLine("calculateHit Effect " + effect);
            //Logger.Error("calculateHit c.dmg " + c.dmg);
            //Logger.SystemWarning("[" + row + "][" + col + "] buff_type "+dest.buff_type);
            //if (init.type == TSConstants.BT_POS_TYPE_NPC) return 0; //for test
            ushort sk_int_atk;
            if (effect == 13) //กินของ
                sk_int_atk = 2;
            else
                sk_int_atk = SkillData.skillList[c.skill].unk17; //สกิลสาย int(2), atk(1), มือปล่าว(0), ประตูเมือง(3)


            switch (effect)
            {
                case 1:
                    {
                        if (dest.buff_type == 13003) return 0; //lan tranh หลบหลีก
                        //if (c.skill != 10000 && dest.buff_type == 10026) return 0; //กระจกศักดิ์สิทธิ์
                        if (init.buff_type == 13005 && (sk_int_atk == 1 || c.skill == 10000)) //ถ้ามีซ่อนร่างอยู่ แล้วโดดตี หรือใช้สกิลสายโจมตี
                            position[c.init_row][c.init_col].buff = 1; //het an minh trong luot sau; วิชาซ่อนร่าง
                        goto case 2;
                    }
                case 2:
                    {
                        if (dest.type == TSConstants.BT_POS_TYPE_NPC && dest.npc != null)
                        {
                            NpcInfo dest_npc = dest.npc.getNpcInfo();
                            if (dest_npc.id > 0)
                            {
                                //แร่
                                if (dest_npc.type == 16)
                                {
                                    int weapon_pos = 2;
                                    TSEquipment weapon;
                                    switch (init.type)
                                    {
                                        case TSConstants.BT_POS_TYPE_CHR: weapon = init.chr.equipment[weapon_pos]; break;
                                        case TSConstants.BT_POS_TYPE_CLONE: weapon = init.clone.owner.equipment[weapon_pos]; break;
                                        case TSConstants.BT_POS_TYPE_PET: weapon = init.pet.equipment[weapon_pos]; break;
                                        default: weapon = null; break;
                                    }
                                    //Console.WriteLine(weapon.info.id);
                                    bool is_hoe = weapon != null && weapon.info.type == 39 && weapon.info.unk5 == 12; //จอบ
                                    if (!is_hoe)
                                        return 0;
                                }
                            }
                        }
                        if (c.skill == 14054) return 1;//สายฟ้าลงทัณฑ์

                        //พลังจิตอ่านใจศัตรู
                        if (dest.getSkillLvl(14045) > 0)
                        {
                            if (
                                 //c.skill != 14054 //skill ไม่ใช่ผ่า
                                 dest.getHp() <= (dest.getMaxHp() * 0.3) //เลือดเหลือน้อยกว่า 30%
                                                                         //&& randomize.getInt(0, 101) < SkillData.skillList[14045].unk19
                                 && randomize.getInt(0, 101) < 30
                            )
                            {
                                battleBroadcast(new PacketCreator(new byte[] { 0x35, 0x0C, dest.row, dest.col }).send()); //พริ้วลอยติ้วววววว
                                return 0;
                            }
                        }

                        if (c.skill == 20004 || c.skill == 20005) return 1; //เทพอัปโชค, เทพโชคลาภ
                        //if (dest.buff > 0 && dest.buff_type == 10010) return 0; //ม่านคุ้มกัน

                        //ติดมึน
                        if (init.getStunID() > 0)
                        {
                            if (init.debuff == SkillData.getSkillPurgeCount(init.debuff_type, 10)[0]) return 0; //ติดมึนเทินแรก miss 100%

                            int rnd_stun = randomize.getInt(0, 1001);
                            //Console.WriteLine("calculateHit init ติดมึน rnd: {0}", rnd_stun);
                            return (byte)(rnd_stun > 950 ? 1 : 0); //Miss 90%
                        }

                        //ระฆังทองคุ้มกาย
                        if (dest.buff > 0 && dest.buff_type == 10031)
                        {
                            init.reflect = dest.buff_type;
                            init.reflect_dmg = c.dmg;
                            //return 0;
                        }

                        //V.1
                        //int gain_min = 80; //ปรับน้อยยิ่ง miss
                        //int gain_max = 99;
                        //int range_lv = (init.getLvl() + 5) - dest.getLvl();
                        //int hit_per = range_lv + 95;
                        //if (hit_per < gain_min) hit_per = gain_min;
                        //if (hit_per >= 100) hit_per = gain_max;

                        //v.2
                        int range_lv = init.getLvl() - dest.getLvl();
                        int perhit = 198 + range_lv; //เวลเท่ากัน hit 95%
                        perhit = Math.Min(perhit, 200); //โอกาสติดสูงสุดไม่เกิน 100%
                        perhit = Math.Max(perhit, 185); //โอกาสติดต่ำสุดไม่เกิน 80%
                        int rnd = randomize.getInt(0, 201);
                        if (perhit >= rnd) return 1;
                        return (byte)(perhit >= rnd ? 1 : 0);

                        //int rnd_hit = randomize.getInt(0, 100);
                        //return (byte)(rnd_hit < hit_per ? 1 : 0);

                        //increse_miss = Math.Min(90, increse_miss);
                        ////Logger.Log("increse_miss " + increse_miss);

                        ////miss 10% if equal lvl, 2% more each lvl
                        ////return (byte)(RandomGen.getByte(0, 100) >= (dest.getLvl() - init.getLvl() + 5) * 0.2 + increse_miss ? 1 : 0);
                        //int rnd_miss = randomize.getInt(0, 100) + increse_miss;
                        //int lv = (init.getLvl() + 5) - dest.getLvl();
                        //int per = 90 + lv;
                        //per = Math.Max(per, 80);
                        //per = Math.Min(per, 99);
                        ////Console.WriteLine("{0}\t{1}\t{2}\t{3}", lv, per, rnd_hit, (per >= rnd_hit ? "Hit" : "Miss"));
                        //return (byte)(per >= rnd_miss ? 1 : 0);
                    }
                case 3: //ปิศาจต้นไม้ ฝ่ามือยูไล น้ำแข็งปิดคลุม ลมสลาตัน เชิญลม หลับไหล ปิศาจต้นไม้3คน น้ำแข็งปิดคลุม3คน ลมสลาตัน3คน
                    {
                        if (dest.death) return 0;
                        if (init.getStunID() > 0) return 0; //ติดมึน
                        if (dest.isNpcBoss()) return 0; //npc พิเศษ จับ disable ไม่ได้จ้า
                        if (dest.disable_type != 0) return 0;
                        //disable skills always miss 20% plus 10% if equal lvl, 2% more each lvl                    
                        //else return (byte)(RandomGen.getByte(0, 100) >= Math.Max((dest.getLvl() - init.getLvl() + 5) * 0.2, 0) + 20 ? 1 : 0);
                        else //ถ้าจะเปิดการคิดเปอร์เซ็นการติดแบบใม่ให้คอมเม้น else บรรทัดบน แล้วเปิดคอมเม้นท่อนนี้
                        {
                            //Console.WriteLine("SkillData.skillList[{0}].unk19 {1}", c.skill, SkillData.skillList[c.skill].unk19);
                            //Console.WriteLine("tmp_skill {0}", tmp_skill);
                            int range_lv = init.getLvl() - dest.getLvl();
                            int perhit = (SkillData.skillList[c.skill].unk19 * 2) + range_lv;

                            //ถ้าต้องการให้ DEF มีผลก็เปิดตรงนี้
                            //int dest_def = Math.Min((int)(dest.getDef() * 0.2), 160);
                            //perhit -= dest_def;

                            perhit = Math.Min(perhit, 200); //โอกาสติดสูงสุดไม่เกิน 100%
                            int rnd = randomize.getInt(0, 201);
                            //Console.WriteLine("{0}>={1}\t{2}", perhit, rnd, perhit >= rnd);
                            if (perhit >= rnd) return 1;
                            return 0;
                        }
                    }
                case 4://buff skills
                    {
                        if (dest.death) return 0;
                        if (dest.buff_type != 0) return 0;
                        if (!sameSide(c.init_row, c.dest_row)) return 0;
                        else return 1; //buff skill always hit
                    }
                case 5: //สลาย ไปทำที่
                    {
                        if (c.skill == 11012 || c.skill == 11025 || c.skill == 11031) //11012=สลายสภาวะ //11025=สลายสภาวะเลื่อนขั้น //11031=พลังชะล้างสรรพสิ่ง
                        {
                            position[row][col].purge_type = 3;
                            return 1;
                        }
                        if (c.skill == 11015 && (dest.disable_type == 11014 || dest.disable_type == 20026)) //giai bang phong //11015=ละลายน้ำแข็ง //11014=น้ำแข็งปิดคลุม //20026=น้ำแข็งปิดคลุม3คน
                        {
                            //Console.WriteLine("ละลายน้ำแข็ง disable: {0}", dest.disable);
                            dest.purge_type = 4;
                            return 1;
                        }
                        if (c.skill == 14007 && dest.disable_type == 14008) //giai hon me //14007=สลายหลับไหล //14008=หลับไหล
                        {
                            position[row][col].purge_type = 4;
                            return 1;
                        }
                        if (c.skill == 14014 && dest.debuff_type == 14015) //giai doc //14014=สลายพิษ //14015=ปล่อยพิษ
                        {
                            position[row][col].purge_type = 6;
                            return 1;
                        }
                        if (c.skill == 14022 && dest.debuff_type == 14021) //giai hon loan //14021=สับสนวุ่นวาย //14022=สลายสับสน
                        {
                            position[row][col].purge_type = 6;
                            return 1;
                        }
                        if (c.skill == 10009 && dest.buff_type == 10010) //giai ket gioi //10009=สลายคุ้มกัน //10010=ม่านคุ้มกัน
                        {
                            position[row][col].purge_type = 5;
                            return 1;
                        }
                        return 0;
                    }
                case 6: //คืนมาร
                    {
                        if (dest.death) return 0;
                        c.dmg = (ushort)Math.Min(dest.getMaxSp() - dest.getSp(), c.dmg);
                        if (dest != init)
                        {
                            dest.setSp(c.dmg);
                            dest.refreshSp();
                        }
                        return 1;
                    }
                case 7: //รักษาบาดเจ็บ | สุดยอดการรักษา
                    {
                        if (dest.death) return 0;
                        c.dmg = (ushort)Math.Min(dest.getMaxHp() - dest.getHp(), c.dmg);
                        dest.setHp(c.dmg);
                        dest.refreshHp();
                        return 1;
                    }
                case 8: //ressurrection วิชาฟื้นคืนชีพ
                    {
                        if (!dest.death) return 0;
                        c.dmg = (ushort)(dest.getMaxHp() * init.getSkillLvl(11013) * 0.1); //วิชาฟื้นคืนชีพ
                        dest.setHp(c.dmg);
                        dest.refreshHp();
                        return 1;
                    }
                case 9: //วิชาแบ่งร่าง, ซ่อนกายสายลม
                    {
                        //c.dmg = 0;
                        if (init.row != dest.row || init.col != dest.col) return 0;
                        if (init.type != TSConstants.BT_POS_TYPE_CHR || dest.haveClone()) return 0; //ถ้าผู้ใช้สกิลไม่ใช่คน หรือถ้าคนนั้นมีร่างโคลนอยู่แล้วก็ให้มิส
                        return 1;
                    }
                case 11: //tha luoi -_- จับศัตตรู จับศัตรู | ตาข่ายจับศัตรู | จับศัตรูสำเร็จ | ก้อนข้าวจับตาข่าย | ก้อนข้าวจับสำเร็จ
                    {
                        return 1;
                    }
                case 12: // run
                    {
                        c.dmg = 0;
                        if (init.type != TSConstants.BT_POS_TYPE_CHR) return 0;
                        if (SkillData.skillList[c.skill].sp_cost > 0)
                            return 1;

                        int range_lvl = init.row < 2 ? init.getLvl() - team1AvgLevel : init.getLvl() - team2AvgLevel;
                        int rnd = randomize.getInt(0, 200);
                        int final_run = rnd + range_lvl + 100;
                        //Console.WriteLine($"calculateHit [{c.init_row}][{c.init_col}] หนี {rnd} + ({range_lvl}) = {final_run}");
                        return (byte)(final_run > 100 ? 1 : 0);

                    }
                case 13: //item, later
                    {
                        if (dest.death && ItemData.itemList[c.skill].type != 50)
                            return 0;
                        if (!dest.death && ItemData.itemList[c.skill].type == 50)
                            return 0;
                        if (ItemData.itemList[c.skill].unk3 == 206) //กินค้อนงัดหินมินิในสู้ให้มิสไปก่อน เด่วค่อยตามแก้ type
                            return 0;
                        else return 1;
                    }
                case 14: //ฮิวทั้ง HP และ SP
                    {
                        if (dest.death) return 0;
                        //ushort tmpSkDmg = c.dmg;
                        c.dmg = (ushort)Math.Min(dest.getMaxHp() - dest.getHp(), c.dmg);
                        dest.setHp(c.dmg);
                        dest.refreshHp();
                        if (dest != init)
                        {
                            //int spDmg = tmpSkDmg / 5;
                            c.sub_dmg = (ushort)Math.Min(dest.getMaxSp() - dest.getSp(), c.sub_dmg);
                            dest.setSp(c.sub_dmg);
                            dest.refreshSp();
                        }
                        return 1;
                    }
                case 15: //debuff
                    {
                        //if (init.getStunID() > 0) return 0; //ติดมึน
                        //if (dest.death) return 0;
                        //if (dest.debuff_type != 0) return 0;
                        //if (dest.isNpcBoss()) return 0; //npc พิเศษ จับ debuff ไม่ได้จ้า
                        if (
                            dest.death
                            || dest.debuff_type != 0
                            || init.getStunID() > 0 //ติดมึน
                            || dest.isNpcBoss() //npc พิเศษ จับ debuff ไม่ได้จ้า
                        )
                        {
                            return 0;
                        }
                        else
                        {
                            //ใส่ตราสงบจิต
                            if (c.skill == 14021 || c.skill == 20014) //14021=สับสนวุ่นวาย, 20014=สี่คนสับสน
                            {
                                if (dest.type == TSConstants.BT_POS_TYPE_CHR && dest.chr.equipment[5] != null && dest.chr.equipment[5].Itemid == 23101) //23101=ตราสงบจิต
                                {
                                    return 0;
                                }
                                if (dest.type == TSConstants.BT_POS_TYPE_PET && dest.pet.equipment[5] != null && dest.pet.equipment[5].Itemid == 23101) //23101=ตราสงบจิต
                                {
                                    return 0;
                                }
                            }

                            //Console.WriteLine("tmp_skill {0}", tmp_skill);
                            int range_lv = init.getLvl() - dest.getLvl();
                            int perhit = (SkillData.skillList[c.skill].unk19 * 2) + range_lv;
                            //Console.WriteLine("perhit {0}", perhit);

                            //ถ้าต้องการให้ DEF มีผลก็เปิดตรงนี้
                            //int dest_def = Math.Min((int)(dest.getDef() * 0.2), 160);
                            //perhit -= dest_def;

                            perhit = Math.Min(perhit, 200); //โอกาสติดสูงสุดไม่เกิน 100%
                            int rnd = randomize.getInt(0, 201);
                            //Console.WriteLine("{0}>={1}\t{2}", perhit, rnd, perhit >= rnd);
                            if (perhit >= rnd) return 1;

                            return 0;
                        }
                    }
                case 16: //สลายคุ้มกัน | สลายกระจก
                    {
                        if (c.skill == 10009 && dest.buff_type == 10010) //giai ket gioi //10009=สลายคุ้มกัน //10010=ม่านคุ้มกัน
                        {
                            position[row][col].purge_type = 5;
                            return 1;
                        }
                        if (c.skill == 10014 && (dest.buff_type == 10015 || dest.buff_type == 10026)) //giai kinh //10014=สลายกระจก //10015=กระจก //10026=กระจกศักดิ์สิทธิ์
                        {
                            position[row][col].purge_type = 5;
                            return 1;
                        }
                        return 0;
                    }
                case 18: //เชิญน้ำ
                    {
                        //if (sameSide(c.init_row, row)) dest.purge_type = 2; //<<ของเดิม
                        //else dest.purge_type = 1; //<<ของเดิม

                        //Logger.Error("row " + dest.row + " col " + dest.col+ "dmg "+c.dmg);
                        if (isTargetEnemy(init, dest)) //<<ของใหม่
                        {
                            dest.purge_type = 1;
                        }
                        else
                        {
                            dest.purge_type = 2;
                            if (!isTargetEnemy(init, dest) && dest.debuff == 0 && dest.disable == 0) //ถ้าไม่ติดสถานะก็ให้เพิ่มเลือด
                            {
                                c.dmg = (ushort)Math.Min(dest.getMaxHp() - dest.getHp(), c.dmg);
                                dest.setHp(c.dmg);
                                dest.refreshHp();
                            }
                        }

                        return 1;
                    }
                case 19: //aura
                    {
                        SkillInfo skillInfo = SkillData.skillList[c.skill];
                        if (dest.death || dest.aura != 0) return 0;
                        else if (skillInfo.elem <= 5 && skillInfo.sp_cost > 0) return (byte)(sameSide(c.init_row, c.dest_row) ? 1 : 0);
                        else return 1; //aura skill always hit
                    }
                default:
                    return 0;
            }

        }

        public bool checkCombo(int index)
        {
            if (index == cmdReg.Count) return false;
            if ((cmdReg[index].type != 0) || (cmdReg[index - 1].type != 0)) return false;
            if (SkillData.skillList[cmdReg[index - 1].skill].skill_type != 1 || SkillData.skillList[cmdReg[index].skill].skill_type != 1)
                return false;
            if (cmdReg[index - 1].dest_col != cmdReg[index].dest_col || cmdReg[index - 1].dest_row != cmdReg[index].dest_row)
                return false;
            if (position[cmdReg[index].init_row][cmdReg[index].init_col].disable > 0 || position[cmdReg[index].init_row][cmdReg[index].init_col].death)
                return false;

            //return true; //combo 100% regardless of agi and level //เปิดบรรทัดนี้เพื่อคอมโบ 100%

            if (TSServer.config.battle_combo.rate >= 100) return true; //ถ้าปรับเรท 100 ก็คอมโบไปเลย ไม่ไต้องไปคำนวนข้างล่างให้เสียเวลา

            /////หินคอมโบ AutoBox 46158	หินผลึกจิตเสือ คอมโบ 100%///
            BattleParticipant init = position[cmdReg[index].init_row][cmdReg[index].init_col];
            bool canAutoboxCrystalCombo = init.autoBoxCanCrystal(TSAutoBox.CRYSTAL_COMBO);
            if (canAutoboxCrystalCombo)
            {
                init.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_COMBO);
                return canAutoboxCrystalCombo;
            }
            //จบหินคอมโบ

            ////////////////ระบบคำนวณคอมโบ///////////////
            int range_lvl = cmdReg[index].init_row < 2 ? team2AvgLevel - team1AvgLevel : team1AvgLevel - team2AvgLevel;
            if (range_lvl > TSServer.config.battle_combo.level) //ถ้าเวลเฉลี่ยสูงกว่าฝั่งตรงข้าม 25
            {
                return true;
            }
            else
            {
                BattleParticipant bp_prev = position[cmdReg[index - 1].init_row][cmdReg[index - 1].init_col];
                int range_agi = Math.Abs(init.getAgiSummary() - bp_prev.getAgiSummary());
                int _base = 200;
                int max = Math.Max(100, _base);
                int rnd = randomize.getInt(0, max);
                int result = rnd + Math.Max(0, range_lvl);
                result += TSServer.config.battle_combo.agi - range_agi;
                result = TSServer.config.battle_combo.rate + Math.Max(0, result);
                //Console.WriteLine("max:{0}\trnd:{1}\tresult{2}", max, rnd, result);
                return result >= (_base / 2);
            }
            ////////////////จบระบบคำนวณคอมโบ///////////////
        }

        public bool sameSide(byte row1, byte row2)
        {
            if (row1 > 3 || row2 > 3) return false; //เผื่อหลุดมา
            if (row1 < 2 && row2 < 2) return true;
            if (row1 >= 2 && row2 >= 2) return true;
            return false;

            //return (Math.Abs(row1 - row2) == 1 && row1 + row2 != 3);
        }

        public void battleBroadcast(byte[] msg)
        {
            for (int i = 0; i < 4; i += 3)
                for (int j = 0; j < 5; j++)
                    if (position[i][j].exist && position[i][j].type == TSConstants.BT_POS_TYPE_CHR && !position[i][j].chr.client.disconnecting)
                    {
                        //Logger.Warning(position[i][j].chr.name);
                        position[i][j].chr.reply(msg);
                    }

            if (position[3][2].chr != null && position[3][2].chr.client.battle != null)
            {
                //foreach (KeyValuePair<uint, TSClient> i in position[3][2].chr.client.map.listPlayers.ToList())
                //{
                //    TSClient c = i.Value;
                //    if (position[3][2].chr != null && c != null && c.getChar().streamBattleId == position[3][2].chr.client.accID)
                //    {
                //        c.reply(msg);
                //    }
                //}
                for (int i = 0; i < this.charViewList.Count; i++)
                {
                    uint vId = this.charViewList[i];
                    TSClient vClient = TSServer.getInstance().getPlayerById((int)vId);
                    if (vClient != null)
                        vClient.reply(msg);
                }
            }
        }

        public BattleParticipant getBpByClient(TSClient client)
        {
            for (int i = 0; i < 4; i += 3)
                for (int j = 0; j < 5; j++)
                {
                    if (position[i][j].exist && position[i][j].chr == client.getChar())
                        return position[i][j];
                }
            return null;
        }

        public bool CanUseItem(TSClient client, byte pos_type)
        {
            BattleParticipant bp = getBpByClient(client);
            if (bp != null)
            {
                switch (pos_type)
                {
                    case TSConstants.BT_POS_TYPE_CHR:
                        return bp != null && bp.exist && bp.getHp() > 0 && !bp.alreadyCommand;
                    case TSConstants.BT_POS_TYPE_PET:
                        {
                            int r = bp.row == 0 ? 1 : 2;
                            BattleParticipant bp_pet = position[r][bp.col];
                            return bp_pet != null && bp_pet.exist && bp_pet.getHp() > 0 && !bp_pet.alreadyCommand;
                        }
                    default: return false;
                }
            }
            return false;
        }

        public void DoEquip(TSClient client)
        {
            BattleParticipant bp = getBpByClient(client);
            if (bp == null) return;
            // Send Battle Command
            bp.alreadyCommand = true;
            battleBroadcast(new PacketCreator(new byte[] { 0x35, 5, bp.row, bp.col }).send());
            cmdNeeded--;
            if (cmdNeeded == 0)
            {
                //execute();
                executeThread(); //DoEquip cmdNeeded == 0
            }
        }

        public void DoEquipPet(TSClient client)
        {
            BattleParticipant bp = getBpByClient(client);
            if (bp == null) return;

            // Pet Row
            byte col = 0; byte row = 0;
            if (bp.row == 0) row = 1; else row = 2;
            col = bp.col;
            // Send Battle Command
            position[row][col].alreadyCommand = true;
            battleBroadcast(new PacketCreator(new byte[] { 0x35, 5, row, col }).send());
            cmdNeeded--;
            if (cmdNeeded == 0)
            {
                //execute();
                executeThread(); //DoEquipPet cmdNeeded == 0
            }
        }

        public void SetBattlePet(TSClient client, byte[] data)
        {
            ushort pet_npcid = PacketReader.read16(data, 2);
            TSCharacter player = client.getChar();

            byte col = 0; byte row = 0;
            if (client.getChar().setBattlePet(PacketReader.read16(data, 2)) && pet_npcid != player.pet_battle)
            {
                BattleParticipant bp = getBpByClient(client);
                if (bp == null) return;
                // Send Battle Command
                battleBroadcast(new PacketCreator(new byte[] { 0x35, 5, bp.row, bp.col }).send());
                bp.alreadyCommand = true;
                cmdNeeded--;

                // Pet Row
                if (bp.row == 0) row = 1; else row = 2;
                col = bp.col;

                if (position[row][col].exist)
                    checkOutBattle(position[row][col]);

                position[row][col].purge_type = 3; //clear all purge status
                position[row][col].purge_status(); //pet battle
                position[row][col] = new BattleParticipant(this, row, col);
                position[row][col].petIn(player.pet[player.pet_battle]);
                countAlly++;

                // Refresh Pet here
                //TSPet pet = player.pet[player.pet_battle];
                var p = new PacketCreator(0x0B, 0x05);
                p.addBytes(position[row][col].announce(5, countAlly).getData());
                battleBroadcast(p.send());

                client.reply(new PacketCreator(data).send());
            }

            if (cmdNeeded == 0)
            {
                //execute();
                executeThread(); //SetBattlePet cmdNeeded == 0
            }
        }

        public void UnBattlePet(TSClient client, byte[] data)
        {
            BattleParticipant bp = getBpByClient(client);
            if (bp == null) return;

            battleBroadcast(new PacketCreator(new byte[] { 0x35, 5, bp.row, bp.col }).send());
            bp.alreadyCommand = true;
            cmdNeeded--;

            // Pet Position
            int row = bp.row == 0 ? 1 : 2;
            checkOutBattle(position[row][bp.col]);

            // Send unbattle pet
            if (client.getChar().unsetBattlePet())
                client.reply(new PacketCreator(data).send());

            if (cmdNeeded == 0)
            {
                //execute();
                executeThread(); //UnBattlePet cmdNeeded == 0
            }
        }

        public void outBattle(TSClient c) //อันนี้เราทดลองทำขึ้นมาเอง
        {
            //Logger.Text("outBattle "+c.accID);
            for (byte i = 0; i < 4; i++)
            {
                for (byte j = 0; j < 5; j++)
                {
                    BattleParticipant bp = position[i][j];
                    if (bp.exist && bp.type == TSConstants.BT_POS_TYPE_CHR && bp.chr != null && bp.chr.client.accID == c.accID)
                    {

                        if (bp.chr.party == null || bp.chr.isTeamLeader())
                        {
                            //sendExitStreamers();
                            sendCharExitView();
                            finish = 3; //ในเมื่อเป็นหัวตี้แล้วก็ต้องใส่ตรงนี้เพื่อให้หยุดลูปการโดดหวด
                            endBattle(false);
                            return;
                        }

                        if (bp.chr != null && bp.chr.pet_battle > -1)
                        {
                            byte second_row = getSecondRow(i);
                            BattleParticipant pos2 = position[second_row][j];
                            if (pos2.exist && pos2.type == TSConstants.BT_POS_TYPE_PET && pos2.pet.owner.client.accID == c.accID)
                            {
                                checkOutBattle(pos2);
                            }
                        }

                        checkOutBattle(bp);
                    }
                }
            }

            if (aTimer.Enabled && cmdNeeded == 0)
            {
                //Logger.Info("outBattle aTimer.Enabled && cmdNeeded == 0");
                //execute();
                executeThread(); //outBattle aTimer.Enabled && cmdNeeded == 0
            }
        }
        //public void outBattle(TSClient c)
        //{
        //    //Logger.Error("BattleAbstract outBattle " + c.accID.ToString());
        //    if (position == null) return; //position เป็น null ได้ไง
        //    for (int i = 0; i < 5; i++)
        //    {
        //        if (position[3][i].exist)
        //        {
        //            if (position[3][i].chr.client == c) //search for position of client
        //            {
        //                //Console.WriteLine("3 " + i + " out of battle");
        //                BattleParticipant charOut = position[3][i];
        //                charOut.outBattle = true;
        //                if (charOut.chr.party == null || charOut.chr.party.leader_id == charOut.chr.client.accID)
        //                {
        //                    sendExitStreamers();
        //                    c.battle.aTimer.Stop();
        //                    //c.battle.aTimer.Dispose();
        //                }
        //                if (aTimer.Enabled)  //disconnect during 20s timer
        //                    checkOutBattle(charOut);

        //                //same with pet
        //                if (position[2][i].exist)
        //                {
        //                    BattleParticipant petOut = position[2][i];
        //                    petOut.outBattle = true;
        //                    if (aTimer.Enabled)
        //                        checkOutBattle(petOut);
        //                }
        //            }
        //        }
        //    }
        //    if (aTimer.Enabled && cmdNeeded == 0)
        //    {
        //        //execute();
        //        executeThread();
        //    }
        //    //Logger.Error("outBattle "+c.accID.ToString());
        //}

        public void checkOutBattle(BattleParticipant bp)
        {
            //Console.WriteLine("checkOutBattle");
            byte row = bp.row;
            byte col = bp.col;
            bp.exist = false;
            bp.outBattle = false; //reset the value so that this won't get called again

            bool cmdRegistered = false;
            for (int i = nextcmd; i < cmdReg.Count; i++)
                if (cmdReg[i] == null || (cmdReg[i] != null && cmdReg[i].init_row == row && cmdReg[i].init_col == col))
                {
                    cmdReg.RemoveAt(i);
                    cmdRegistered = true;
                    //Logger.Warning("init pos[" + cmdReg[i].init_row + "][" + cmdReg[i].init_col + "] command registered");
                }
            if (!cmdRegistered)
            {

            }

            if (!bp.death) //if char still alive
            {
                if (bp.row >= 2) countAlly--;
                else countEnemy--;
                if (countEnemy == 0) finish = 1;
                else if (countAlly == 0) finish = 2;
                if (bp.disable > 0) //char alive but disabled
                    countDisabled--;
            }
            battleBroadcast(new PacketCreator(new byte[] { 0x0b, 0x01, bp.row, bp.col }).send());

            if (aTimer.Enabled && !bp.alreadyCommand && !bp.death && bp.disable == 0) cmdNeeded--;  //not given BattleCommand yet

            if (bp.type == TSConstants.BT_POS_TYPE_CHR && bp.chr != null)
                bp.chr.client.battle = null;

            bp.reset();
            bp.exist = false;

            //Logger.Info("countAlly " + countAlly + ", cmdNeeded " + cmdNeeded);
            //if (!executing && cmdNeeded == 0)
            //{
            //    executeThread();
            //}
        }

        public bool CatchPet(BattleParticipant init, BattleParticipant dest)
        {
            if (dest.npc != null)
            {
                if (init.chr != null)
                {
                    if (init.chr.next_pet < 4)
                    {
                        if (init.chr.level + 5 >= dest.npc.level && NpcData.npcList[(ushort)dest.npc.npcid].notPet == 0)
                        {
                            double hpPer = (dest.getHp() * 100) / dest.getMaxHp();
                            if (hpPer <= 30)
                            {
                                int rnd = randomize.getInt(0, 100);
                                //Console.WriteLine(rnd);
                                if (rnd < 50)
                                    return true;
                            }
                            else
                            {
                                //Console.WriteLine("เลือดมากกว่า 30% ยังจับไม่ได้");
                            }
                            //double rate = (1 - (double)dest.getHp() / dest.getMaxHp()) * 100;
                            //if (RandomGen.getInt(0, 100) < rate)
                            //{
                            //    return true;
                            //}
                        }
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// ให้คนที่ส่องอยู่ออกจากการดูการต่อสู้
        /// </summary>
        public void sendCharExitView()
        {
            if (position[3][2].chr != null)
            {
                for (int i = 0; i < this.charViewList.Count; i++)
                {
                    uint vId = this.charViewList[i];
                    TSClient vClient = TSServer.getInstance().getPlayerById((int)vId);
                    if (vClient != null)
                    {
                        vClient.sendExitViewBattle();
                    }
                }
            }
        }

        public bool isTargetEnemy(BattleParticipant init, BattleParticipant dest)
        {
            bool ret = true;
            if (init.row < 2 && dest.row < 2)
                ret = false;
            else if (init.row >= 2 && dest.row >= 2)
                ret = false;
            return ret;
        }

        public byte getSecondRow(byte currentRow)
        {
            byte secondRow = 2;
            switch (currentRow)
            {
                case 0: secondRow = 1; break;
                case 1: secondRow = 0; break;
                case 2: secondRow = 3; break;
                case 3: secondRow = 2; break;
                default: secondRow = 2; break;
            }
            return secondRow;
        }

        public int calcFinalDmg(BattleCommand c, BattleParticipant init, BattleParticipant dest, int elem_coef)
        {
            //init แพ้ธาตุ dest
            //dest กดป้องกัน หรือมี buff กำแพงน้ำแข็วเต็มเวล
            if (elem_coef < 0 && dest.def)
                return 1;
            //c.dmg = 100; //test
            TSConfig.CFG_DAMAGE cfg_dmg = TSServer.config.damage;
            SkillInfo skillInfo = SkillData.skillList[c.skill];
            double ret_dmg;
            double dmg = c.dmg;
            //Console.WriteLine("base dmg: {0}", dmg);
            List<double> percent = new List<double>();

            double skill_base_passive_rb1 = 0;
            double skill_base_passive_rb2 = 0;

            double range_lv = (init.getLvl() - dest.getLvl()) * cfg_dmg.percent.range_level;
            double range_rb = (init.getRb() - dest.getRb()) * cfg_dmg.percent.range_rb;
            double sk_rb = (skillInfo.unk20 - 1) * cfg_dmg.percent.sk_rb;

            double sk_grade = SkillData.skillSummonFire.Contains(c.skill) ? SkillData.skillList[12016].grade : skillInfo.grade;
            sk_grade = sk_grade * cfg_dmg.percent.sk_grade;

            double sk_lv = c.skill_lvl * cfg_dmg.percent.sk_level;

            percent.Add(range_lv);
            percent.Add(range_rb);

            switch (elem_coef)
            {
                case -1: percent.Add(cfg_dmg.percent.coef_lose); break;
                case 1: percent.Add(cfg_dmg.percent.coef_win); break;
            }
            percent.Add(sk_rb);
            percent.Add(sk_grade);
            percent.Add(sk_lv);

            if (init.type == TSConstants.BT_POS_TYPE_CHR || init.type == TSConstants.BT_POS_TYPE_PET || init.type == TSConstants.BT_POS_TYPE_CLONE)
                percent.Add(cfg_dmg.percent.mul_player);
            else if (init.type == TSConstants.BT_POS_TYPE_NPC)
                percent.Add(cfg_dmg.percent.mul_monster);

            ///skill_type
            ///3 = disable
            ///4 = buff
            ///15 = debuf
            ///19 = aura

            Dictionary<int, double> per_init_buff = new Dictionary<int, double>()
            {
                { 13012, getBuffPer(13012) }, //วิชาขยายใหญ่
                { 14013, getBuffPer(14013) }, //ปลุกใจ
            };
            if (init.buff_type > 0 && init.buff > 0 && per_init_buff.ContainsKey(init.buff_type)) percent.Add(per_init_buff[init.buff_type]);
            Dictionary<int, double> per_init_debuff = new Dictionary<int, double>()
            {
                { 13011, -getBuffPer(13012) } //วิชาย่อเล็ก เอาจากขยายใหญ่มาคิด
            };
            if (init.debuff_type > 0 && init.debuff > 0 && per_init_debuff.ContainsKey(init.debuff_type)) percent.Add(per_init_debuff[init.debuff_type]);

            Dictionary<int, double> per_init_aura = new Dictionary<int, double>()
            {
                { 12025, getBuffPer(12025) }, //วิญญาณกระหายรบ
                { 21026, getBuffPer(21026) }, //คุณธรรมไร้พ่าย (ใช้กับทีมจะฟื้น HP ให้ทีม 300 จุด และเพิ่มโจมตี 1.6เท่า ต่อเนื่อง 3 เทิร์น)
                { 14053, elem_coef > 0 ? getBuffPer(14053) : 0 }, //พลังเทพพิชิตศัตรู ถ้าข่มธาตุให้บวกบัพ
                { 14040, randomize.getInt(0, 101) >= 50 ? getBuffPer(14040) : 0 }, //แผนการทลายศึก
            };
            if (init.aura_type > 0 && init.aura > 0 && per_init_aura.ContainsKey(init.aura_type)) percent.Add(per_init_aura[init.aura_type]);



            Dictionary<int, double> per_dest_buff = new Dictionary<int, double>()
            {
                { 13012, -getBuffPer(13012) }, //วิชาขยายใหญ่
                { 11002, -0.1 * dest.buff_lv }, //กำแพงน้ำแข็ง
                { 14012, -0.06 * dest.buff_lv }, //ร่วมใจ
            };
            if (dest.buff_type > 0 && dest.buff > 0 && per_dest_buff.ContainsKey(dest.buff_type)) percent.Add(per_dest_buff[dest.buff_type]);

            Dictionary<int, double> per_dest_debuff = new Dictionary<int, double>()
            {
                { 13011, getBuffPer(13012) } //วิชาย่อเล็ก เอาจากขยายใหญ่มาคิด
            };
            if (dest.debuff_type > 0 && dest.debuff > 0 && per_dest_debuff.ContainsKey(dest.debuff_type)) percent.Add(per_dest_debuff[dest.debuff_type]);

            Dictionary<int, double> per_dest_aura = new Dictionary<int, double>()
            {
                { 12025, 0.05 }, //วิญญาณกระหายรบ (เพิ่มดาเมจให้คนโจมตี)
                { 14053, elem_coef > 0 ? 0.7 : 0 }, //พลังเทพพิชิตศัตรู ถ้าคนโจมตีข่มธาตุให้บวกดาเมจ
            };
            if (dest.aura_type > 0 && dest.aura > 0 && per_dest_aura.ContainsKey(dest.aura_type)) percent.Add(per_dest_aura[dest.aura_type]);

            if (dest.def) percent.Add(-0.5);

            double sum = percent.Sum();

            ret_dmg = (dmg * (sum + 1));
            //Console.WriteLine("dmg: {0}, sum: {1}, ret: {2}", dmg, sum, getBuffPer(14053));
            //////////////// end 1 //////////

            ////////// start passive ///////////
            percent.Clear();
            //Passive charecter reborn 1 skill
            if (skillInfo.unk20 == 2)
            {
                byte skill_base_passive_rb1_lv;
                switch (skillInfo.elem)
                {
                    case 1: skill_base_passive_rb1_lv = init.getSkillLvl(10020); break; //คาถาแผ่นดินไหว
                    case 2: skill_base_passive_rb1_lv = init.getSkillLvl(11020); break; //คาถาใจวารี
                    case 3: skill_base_passive_rb1_lv = init.getSkillLvl(12020); break; //เวทย์ตะวันเพลิง
                    case 4: skill_base_passive_rb1_lv = init.getSkillLvl(13019); break; //เวทย์ลมทรนง
                    default: skill_base_passive_rb1_lv = 0; break;
                }
                skill_base_passive_rb1 = skill_base_passive_rb1_lv * cfg_dmg.sk_base_passive_rb1;
            }
            percent.Add(skill_base_passive_rb1);

            //Passive charecter reborn 2 skill
            if (skillInfo.unk20 == 3 && init.getSkillLvl(14038) > 0) //กำเนิดวีรบุรุษ
            {
                skill_base_passive_rb2 = init.getSkillLvl(14038) * cfg_dmg.sk_base_passive_rb2;
            }
            percent.Add(skill_base_passive_rb2);


            //passive mind rb2
            bool can_combo = skillInfo.skill_type == 1;
            byte nb_target = skillInfo.nb_target;
            BattleParticipant bp_init_leader = position[init.row][2];
            BattleParticipant bp_dest_leader = position[dest.row][2];

            //จอมทัพ จอมทัพเกรียงไกร
            //เพิ่มพลังการใช้สกิลโจมตีเดี่ยว/เวทย์เดี่ยว
            //ที่สามารถคอมโบได้ ของผู้เล่นจอมทัพต่อศัครูที่
            //แพ้ธาตุตัวเอง รวมถึงการใช้สกิลข้ามธาตุกับศัตรูที่แพ้ทางสกิลธาตุนั้น ๆ ด้วย
            //เป็น Passive Skill เฉพาะตัวเองเท่านั้น ในระดับ 10 จะเพิ่มอัตราการโจมตีประมาณ 15-20%
            if (init.getSkillLvl(14039) > 0 && can_combo && nb_target == 1 && elem_coef > 0)
            {
                //Console.WriteLine("จอมทัพ จอมทัพเกรียงไกร");
                percent.Add(init.getSkillLvl(14039) * 0.02);
            }

            //จอมทัพ พลังภูผาต้านศัตรู
            //เพิ่มพลังการใช้สกิลโจมตีเดี่ยว/เวทย์เดี่ยว
            //ที่สามารถคอมโบได้ของสมาชิกในทีมต่อศัครูที่
            //แพ้ธาตุตัวเอง รวมถึงการใช้สกิลข้ามธาตุกับศัตรูที่แพ้ทางสกิลธาตุนั้น ๆ ด้วย
            //เป็น Passive Skill
            //ต้องเป็นหัวหน้าทีมเท่านั้น ในระดับ 10 จะเพิ่มอัตราการโจมตีประมาณ 15%
            if (bp_init_leader.getSkillLvl(14041) > 0 && can_combo && nb_target == 1 && elem_coef > 0)
            {
                percent.Add(bp_init_leader.getSkillLvl(14041) * 0.015);
            }

            //จอมยุทธ วิญญาณคุ้มครอง
            //ลดความสูญเสียจากการทำร้ายของศัตรูที่ข่มธาตุตัวเอง รวมถึงการใช้สกิลธาตุที่ข่มตัวเองในระดับสูงสุดจะลดการโจมตีได้ 20-25% ลักษณะเป็น Passive Skill
            if (dest.getSkillLvl(14043) > 0 && elem_coef > 0)
            {
                percent.Add(-dest.getSkillLvl(14043) * 0.025);
            }

            //จอมยุทธ ****************** พลังจิตอ่านใจศัตรู ******************
            //ขณะผู้เล่นจอมยุทธอยู่ในสภาวะใกล้ตาย จะหลบหลีกการโจมตีหรือการทำร้ายด้วยสกิลได้ระดับหนึ่ง สกิลจะแสดงผลเมื่อผู้เล่นเหลือ HP น้อยกว่า 30% ลงไป ลักษณะเป็น Passive Skill
            //ไปทำที่ calculateHit


            //กุนซือ กลยุทธ์พิชิตศัตรู
            //เพิ่มพลังการใช้สกิลเวทย์เดี่ยว/โจมตีเดี่ยวที่ไม่สามารถคอมโบได้ของกุนซือ
            //ต่อศัครูที่แพ้ธาตุตัวเอง รวมถึงการใช้สกิลข้ามธาตุกับศัตรูที่แพ้ทางสกิลธาตุนั้น ๆ ด้วย
            //เป็น Passive Skill เฉพาะตัวเองเท่านั้น ในระดับ 10 จะเพิ่มอัตราการโจมตีประมาณ 15%
            if (init.getSkillLvl(14047) > 0 && !can_combo && nb_target == 1 && elem_coef > 0)
            {
                percent.Add(init.getSkillLvl(14047) * 0.015);
            }

            //กุนซือ ยอดกลยุทธ์
            //เพิ่มพลังการใช้สกิลเวทย์เดี่ยว/โจมตีเดี่ยว
            //ที่ไม่สามารถคอมโบได้ของสามชิกในทีมต่อ
            //ศัครูที่แพ้ธาตุตัวเอง รวมถึงการใช้สกิลข้ามธาตุกับศัตรูที่แพ้ทางสกิลธาตุนั้น ๆ ด้วย
            //ต้องเป็นหัวหน้าทีมเท่านั้น ในระดับ 10 จะเพิ่มอัตราการโจมตีประมาณ 15%
            //เป็น Passive Skill 
            if (bp_init_leader.getSkillLvl(14048) > 0 && !can_combo && nb_target == 1 && elem_coef > 0)
            {
                double val = bp_init_leader.getSkillLvl(14048) * 0.015;
                //Console.WriteLine("bp_init_leader.getSkillLvl(14048): {0}", val);
                percent.Add(val);
            }

            //กุนซือ สืบสารต้านศัตรู
            //ลดการถูกทำร้ายจากสกิลของศัตรูที่
            //ข่มธาตุ
            //ของสมาชิกในทีมให้น้อยลง รวมถึงการใช้สกิลธาตุที่ข่มตัวเองด้วย
            //เป็น Passive Skill ต้องเป็นหัวหน้าทีมเท่านั้น
            //ในระดับ 10 จะลดอัตราความเสียหายได้ประมาณ 15%
            if (bp_dest_leader.getSkillLvl(14049) > 0 && elem_coef > 0)
            {
                percent.Add(-bp_dest_leader.getSkillLvl(14049) * 0.015);
            }

            //กุนซือ สืบสารต้านศัตรู
            //เพิ่มพลังการใช้สกิลโจมตีหมู่/เวทย์หมู่ของสมาชิกในทีมต่อ
            //ศัครูที่แพ้ธาตุตัวเอง รวมถึงการใช้สกิลธาตุที่ข่มตัวเองด้วย
            //เป็น Passive Skill ต้องเป็นหัวหน้าทีมเท่านั้น
            //ในระดับ 10 จะเพิ่มอัตราการโจมตีประมาณ 15%
            if (bp_init_leader.getSkillLvl(14050) > 0 && nb_target > 1 && elem_coef > 0)
            {
                percent.Add(bp_init_leader.getSkillLvl(14050) * 0.015);
            }


            //เซียน เคล็ดเซียน
            //เพิ่มพลังการใช้
            //สกิลเวทย์โจมตีหมู่ที่
            //ไม่สามารถคอมโบได้ของผู้เล่นเซียนต่อ
            //ศัตรูที่แพ้ธาตุตัวเอง รวมถึงการใช้สกิลข้ามธาตุกับศัตรูที่แพ้ทางสกิลธาตุนั้น ๆ ด้วย
            //เป็น Passive Skill เฉพาะตัวเองเท่านั้น
            //ในระดับ 10 จะเพิ่มอัตราการโจมตีประมาณ 15-20%
            if (init.getSkillLvl(14051) > 0 && !can_combo && nb_target > 1 && elem_coef > 0)
            {
                percent.Add(init.getSkillLvl(14051) * 0.02);
            }

            //เซียน ****************** พลังแปรเปลี่ยน ******************
            //เมื่อถูกศัตรูโจมตี ค่าเลือดที่สูญเสียไปจะกลายเป็น SP ในสัดส่วนหนึ่ง แต่ค่าเลือดยังคงจะสูญเสีย อัตราการเพิ่มของ SP จะอยู่ที่ประมาณ 15% คือถ้าถูกโจมตี 100 SP จะเพิ่มเท่ากับ 15 เป็น Passive Skill
            //ไปทำที่ calcFinalDmg

            //Autobox บัพหิน 
            byte[] bt_type_fore_buff = new byte[] { 2, 3 }; //pk npc & battle npc
            if (bt_type_fore_buff.Contains(this.battle_type))
            {
                if (init.autoBoxCanCrystal(TSAutoBox.CRYSTAL_DMG)) //46157 หินผลึกไฟจ้า (ผลึกแดง : เพิ่มค่าความดาเมท 10% ทั้งคนและขุน มีผลเฉพาะ NPC เก็บเวลและด่าน ไม่มีผลใน PVP)
                {
                    init.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_DMG);
                    percent.Add(0.1);
                }
                if (dest.autoBoxCanCrystal(TSAutoBox.CRYSTAL_DEF)) //46160 ผลึกเขียว : ความสามารถ คือ เพิ่มค่าอัตตราการหลบหลีกและป้องกัน
                {
                    dest.autoBoxAddEffectCrystal(TSAutoBox.CRYSTAL_DEF);
                    percent.Add(-0.1);
                }
            }

            //double sum = Math.Max(0, percent.Sum());
            //sum += 1;
            sum = percent.Sum();

            //Console.WriteLine("sum: {0}", sum);
            double old_dmg = ret_dmg;
            double dest_def = dest.getDef() * cfg_dmg.dest_def;
            ret_dmg = (old_dmg * (sum + 1)) - dest_def;
            //Console.WriteLine("dmg: {0}, sum: {1}, def: {2}, ret: {3}", old_dmg, sum, dest_def, ret_dmg);

            //string t;
            //switch (init.type)
            //{
            //    case TSConstants.BT_POS_TYPE_CHR: t = "CHAR"; break;
            //    case TSConstants.BT_POS_TYPE_PET: t = "PET"; break;
            //    case TSConstants.BT_POS_TYPE_CLONE: t = "CLONE"; break;
            //    case TSConstants.BT_POS_TYPE_NPC: t = "NPC"; break;
            //    default: t = "UNK"; break;
            //}
            //Console.WriteLine("{0} => ({1} + ({1} * {2})) - ({3} * {4}) = {5}", t, dmg, sum, dest.getDef(), cfg_dmg.dest_def, ret_dmg);

            ret_dmg = ret_dmg * (cfg_dmg.multiply + 1);

            //int rnd_dmg = randomize.getInt(-10, 10);
            //ret_dmg += rnd_dmg;


            if (ret_dmg > this.dmg_max) ret_dmg = this.dmg_max;
            if (ret_dmg < 0) ret_dmg = 1;

            //Console.WriteLine("sum: {0}\tret_dmg = {1}", sum, ret_dmg);

            //return (int)Math.Max(1, ret_dmg);
            return (int)Math.Floor(ret_dmg);
        }

        private double getBuffPer(ushort sk_id)
        {
            if (SkillData.skillList.ContainsKey(sk_id))
            {
                return SkillData.skillList[sk_id].unk19 / (double)100;
            }
            return 0;
        }

        /// <summary>
        /// 0 = เสมอ;
        /// 1 = ชนะธาตุ
        /// -1 = แพ้ธาตุ
        /// </summary>
        /// <param name="sk_id"></param>
        /// <param name="dest_elem"></param>
        /// <returns></returns>
        public int getElementCoef(BattleCommand c, BattleParticipant init, BattleParticipant dest)
        {
            if (c.type == 1) return 0;
            //0=normal;
            //1=ชนะธาตุ
            //-1=แพ้ธาตุ
            int result = 0;
            SkillInfo skillInfo = SkillData.skillList[c.skill];
            int init_elem = c.skill == 10000 ? init.getElem() : skillInfo.elem;
            int dest_elem = dest.getElem();
            switch (init_elem)
            {
                case 1: //earth
                    if (dest_elem == 2) result = 1;
                    else if (dest_elem == 4) result = -1;
                    break;
                case 2: //water
                    if (dest_elem == 3) result = 1;
                    else if (dest_elem == 1) result = -1;
                    break;
                case 3: //fire
                    if (dest_elem == 4) result = 1;
                    else if (dest_elem == 2) result = -1;
                    break;
                case 4: //wind
                    if (dest_elem == 1) result = 1;
                    else if (dest_elem == 3) result = -1;
                    break;
                default:
                    break;
            }

            return result;
        }
        public void reflectSpSubLeader(BattleParticipant bp)
        {
            if (
                (bp.type == TSConstants.BT_POS_TYPE_CHR && bp.chr.party != null && bp.chr.party.subleader_id > 0) ||
                (bp.type == TSConstants.BT_POS_TYPE_PET && bp.pet.owner.party != null && bp.pet.owner.party.subleader_id > 0)
            )
            {
                int subLeaderId = bp.type == 1 ? bp.chr.party.subleader_id : bp.pet.owner.party.subleader_id;
                TSClient clientSubLeader = TSServer.getInstance().getPlayerById(subLeaderId);
                if (clientSubLeader != null)
                {
                    TSCharacter chrSubleader = clientSubLeader.getChar();
                    if (chrSubleader != null)
                    {
                        int subLeaderMag = chrSubleader.mag + chrSubleader.mag2;
                        //int subLeaderMag = bp.getMag(); //แบบนี้ได้อิ้นตัวเองสิไอ้เวร
                        int dmgSp = (int)Math.Round((double)subLeaderMag / 15.0);
                        bp.setSp(dmgSp);
                        bp.refreshSp();
                    }
                }
                //bp.pet.owner.party.sub
                //Logger.Error("Has sena " + sbuLeaderId + ", int "+ subLeaderMag + ", dmgSp "+ dmgSp + ", current sp: "+bp.getSp());
            }
        }





        public void destroyBattle()
        {
            aTimer.Stop();
            //aTimer.Dispose();

            Console.WriteLine("Battle " + uniqId + " destroyed.");
        }
    }
}
