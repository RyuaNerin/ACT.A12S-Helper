using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ACT.A12Helper.Properties;
using Advanced_Combat_Tracker;

namespace ACT.A12Helper
{
    public sealed class Plugin : IActPluginV1
    {
        private static Plugin instance;
        public  static Plugin Instance => instance;

        public Plugin()
        {
            Plugin.instance = this;

            Settings.Default.SettingsSaving += (ls, le) => this.SavePlayerInfo();
            this.LoadPlayerInfo();

            Config.WriteLog(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
        }

        private Label m_pluginStatusText;

        public static readonly bool[,] Relationship =
        {
            //  MT      ST      MH      SH      D1      D2      D3      D4
            {   true,   false,  false,  false,  false,  false,  false,  false, },
            {   false,  true,   false,  false,  false,  false,  false,  false, },
            {   false,  false,  true,   false,  false,  false,  false,  false, },
            {   false,  false,  false,  true,   false,  false,  false,  false, },
            {   false,  false,  false,  false,  true,   false,  false,  false, },
            {   false,  false,  false,  false,  true,   true,   false,  false, },
            {   false,  false,  false,  false,  true,   true,   true,   true,  },
            {   false,  false,  false,  false,  true,   true,   true,   true,  },
        };

        private readonly PlayerInfo[] m_playerInfos =
        {
            new PlayerInfo(0), new PlayerInfo(1),
            new PlayerInfo(2), new PlayerInfo(3),
            new PlayerInfo(4), new PlayerInfo(5),
            new PlayerInfo(6), new PlayerInfo(7),
        };
        public PlayerInfo[] PlayerInfos => this.m_playerInfos;
        
        public static void DebugPlayerInfo()
        {
#if DEBUG
            for (int i = 0; i < 8; ++i)
                Config.WriteLog(
                    "{0} / {1} / {2} / {3}",
                    i,
                    Plugin.Instance.PlayerInfos[i].Name,
                    Plugin.Instance.PlayerInfos[i].FirstPos.ToString(),
                    Plugin.Instance.PlayerInfos[i].Debuff.ToString());
#endif
        }

        private int m_myIndex = Settings.Default.MyIndex;
        public int MyIndex
        {
            get => this.m_myIndex;
            set
            {
                this.m_myIndex = value;
                Settings.Default.MyIndex = value;
            }
        }

        private void LoadPlayerInfo()
        {
            int i;
            try
            {
                for (i = 0; i < 8; ++i)
                {
                    this.m_playerInfos[i].Name = Settings.Default.PlayerInfo[i * 2];

                    if (i >= 4)
                        this.m_playerInfos[i].FirstPos = (A12Position)Enum.Parse(typeof(A12Position), Settings.Default.PlayerInfo[i * 2 + 1], true);
                }
            }
            catch
            {
                for (i = 0; i < 8; ++i)
                    this.m_playerInfos[i].Clear();
            }
        }
        private void SavePlayerInfo()
        {
            try
            {
                Settings.Default.PlayerInfo = new System.Collections.Specialized.StringCollection();

                var arr = new string[16];
                for (int i = 0; i < 8; ++i)
                {
                    Settings.Default.PlayerInfo.Add(this.m_playerInfos[i].Name);
                    Settings.Default.PlayerInfo.Add(this.m_playerInfos[i].FirstPos.ToString());
                }
            }
            catch
            {
                Settings.Default.PlayerInfo = null;
            }
        }

        void IActPluginV1.DeInitPlugin()
        {
            this.Stop();

            Settings.Default.Save();

            this.m_pluginStatusText.Text = "Plugin exited.";

            Overlay.OverlayHide();

            ActGlobals.oFormActMain.OnCombatStart -= this.OFormActMain_OnCombatStart;
            ActGlobals.oFormActMain.OnCombatEnd -= this.OFormActMain_OnCombatEnd;
            ActGlobals.oFormActMain.OnLogLineRead -= this.OFormActMain_OnLogLineRead;
        }
        void IActPluginV1.InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            pluginScreenSpace.Text = "A12S Helper";
            pluginScreenSpace.Controls.Add(Config.Instance);

            this.m_pluginStatusText = pluginStatusText;
            this.m_pluginStatusText.Text = "Plugin inited.";

            ActGlobals.oFormActMain.OnCombatStart += this.OFormActMain_OnCombatStart;
            ActGlobals.oFormActMain.OnCombatEnd += this.OFormActMain_OnCombatEnd;
        }

        private object m_sync = new object();
        private volatile bool m_worker;
        private void OFormActMain_OnCombatStart(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            this.Start();
        }

        private void OFormActMain_OnCombatEnd(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            this.Stop();
        }

        public void Start()
        {
            lock (this.m_sync)
            {
                if (this.m_worker) return;
                this.m_worker = true;
            }

            ActGlobals.oFormActMain.OnLogLineRead += this.OFormActMain_OnLogLineRead;
            this.m_workerTask = Task.Run(new Action(this.Worker));
        }
        public void Stop()
        {
            lock (this.m_sync)
            {
                if (!this.m_worker) return;
                this.m_worker = false;
            }

            ActGlobals.oFormActMain.OnLogLineRead -= this.OFormActMain_OnLogLineRead;

            lock (this.m_log)
                this.m_log.Clear();
        }

        private readonly Queue<string> m_log = new Queue<string>();
        private void OFormActMain_OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (!this.m_worker || logInfo.logLine.Length < 16) return;

            var line = logInfo.logLine.Substring(15);
            lock (this.m_log)
                this.m_log.Enqueue(line);
        }

        public void InputLog(string str)
        {
            lock (this.m_log)
                this.m_log.Enqueue(str);
        }

        private Task m_workerTask;
        private void Worker()
        {
            int i;
            string log;
            Debuffs debuff = Debuffs.None;
            A12Position pos = A12Position.None;
            A12Position pos2 = A12Position.None;
            string pos2cond = null;

            string skillDebuffId;
            string userName;

            int phase = 0;
            bool shownOverlay = false;

            Config.WriteLog("Start Worker");

            try
            {
                while (this.m_worker)
                {
                    lock (this.m_log)
                    {
                        if (this.m_log.Count == 0)
                            log = null;
                        else
                            log = this.m_log.Dequeue();
                    }

                    if (log == null)
                    {
                        Thread.Sleep(0);
                    }
                    else
                    {
                        if (log.StartsWith("1A:"))
                        {
                            /*
                            [23:13:02.884] 1A:클자 gains the effect of 확정 판결: 단체형 from  for 9999.00 Seconds.
                            [23:13:02.884] 1A:진주멋쟁이 gains the effect of 확정 판결: 명예형 from  for 9999.00 Seconds.
                            [23:13:02.884] 1A:바스뷔름 gains the effect of 확정 판결: 강제접근 명령 from  for 9999.00 Seconds.
                            [23:13:02.884] 1A:쿨쿨자 gains the effect of 확정 판결: 명예형 from  for 9999.00 Seconds.
                            [23:13:02.884] 1A:재도리 gains the effect of 확정 판결: 강제접근 명령 from  for 9999.00 Seconds.
                            [23:13:02.884] 1A:륜아린 gains the effect of 확정 판결: 명예형 from  for 9999.00 Seconds.
                            [23:13:02.884] 1A:루비블러드 gains the effect of 확정 판결: 단체형 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:클자 gains the effect of 확정 판결: 강제접근 명령 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:진주멋쟁이 gains the effect of 확정 판결: 단체형 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:바스뷔름 gains the effect of 확정 판결: 강제접근 명령 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:쿨쿨자 gains the effect of 확정 판결: 단체형 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:재도리 gains the effect of 확정 판결: 명예형 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:륜아린 gains the effect of 확정 판결: 접근금지 명령 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:큐니 gains the effect of 확정 판결: 명예형 from  for 9999.00 Seconds.
                            [23:13:54.986] 1A:루비블러드 gains the effect of 확정 판결: 접근금지 명령 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:클자 gains the effect of 확정 판결: 단체형 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:진주멋쟁이 gains the effect of 확정 판결: 접근금지 명령 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:바스뷔름 gains the effect of 확정 판결: 명예형 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:쿨쿨자 gains the effect of 확정 판결: 강제접근 명령 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:재도리 gains the effect of 확정 판결: 강제접근 명령 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:륜아린 gains the effect of 확정 판결: 접근금지 명령 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:큐니 gains the effect of 확정 판결: 접근금지 명령 from  for 9999.00 Seconds.
                            [23:17:21.157] 1A:루비블러드 gains the effect of 확정 판결: 접근금지 명령 from  for 9999.00 Seconds.
                            */

                            if (shownOverlay)
                                continue;

                            //               123456789012345678901
                            i = log.IndexOf(" gains the effect of ");
                            userName = log.Substring(3, i - 3);

                            i += 21;
                            skillDebuffId = log.Substring(i, log.IndexOf(" from", i) - i);
                            
                            switch (skillDebuffId)
                            {
                                case "확정 판결: 명예형":        debuff = Debuffs.Purple_Circle; break; // 명예형
                                case "확정 판결: 단체형":        debuff = Debuffs.Green_Share;   break; // 단체형
                                case "확정 판결: 강제접근 명령": debuff = Debuffs.Red_Near;      break; // 강제 접근
                                case "확정 판결: 접근금지 명령": debuff = Debuffs.Blue_Far;      break; // 접근 금지
                                default: continue;
                            }

                            Config.WriteLog("{0} {1}", userName, debuff.ToString());
                            try
                            {
                                this.m_playerInfos.First(e => e.Name == userName).Debuff = debuff;
                            }
                            catch
                            { }
                        }
                        else if (log.StartsWith("14:"))
                        {
                            /*
                                           0  1    2
                            [15:32:57.812] 14:19FB:알렉산더 프라임 starts using 시간 정지 on 알렉산더 프라임.
                            [15:33:51.535] 14:19FB:알렉산더 프라임 starts using 시간 정지 on 알렉산더 프라임.
                            [15:36:25.462] 14:1A08:알렉산더 프라임 starts using 시공 잠행 on 알렉산더 프라임.
                            [15:37:22.056] 14:1A08:알렉산더 프라임 starts using 시공 잠행 on 알렉산더 프라임.
                            */
                            skillDebuffId = log.GetSeparatedPart(':', 1);

                                 if (phase == 0 && skillDebuffId == "19FB") { phase = 1; shownOverlay = false; Config.WriteLog("1 시정"); }
                            else if (phase == 1 && skillDebuffId == "19FB") { phase = 2; shownOverlay = false; Config.WriteLog("2 시정"); }
                            else if (phase == 2 && skillDebuffId == "1A08") { phase = 3; shownOverlay = false; Config.WriteLog("1 잠행"); }
                            else if (phase == 3 && skillDebuffId == "1A08") { phase = 4; shownOverlay = false; Config.WriteLog("2 잠행"); }
                            else continue;
                        }
                        else if (shownOverlay && log.StartsWith("15:"))
                        {
                            /*
                                           0 :1       :2              :3   :4        :5       :6
                            [15:48:21.268] 15:40002749:알렉산더 프라임:19FB:시간 정지:40002749:알렉산더 프라임: (...)
                            [15:54:23.098] 15:400028AE:알렉산더 프라임:19FB:시간 정지:400028AE:알렉산더 프라임: (...)
                            [15:57:44.726] 15:400028AE:알렉산더 프라임:1A08:시공 잠행:400028AE:알렉산더 프라임: (...)
                            [15:58:41.434] 15:400028AE:알렉산더 프라임:1A08:시공 잠행:400028AE:알렉산더 프라임: (...)
                            */
                            skillDebuffId = log.GetSeparatedPart(':', 3);

                            if (skillDebuffId == "19FB" || skillDebuffId == "19FB")
                                Overlay.OverlayHide();
                        }
                        else
                            continue;

                        
                        pos = A12Position.None;
                        pos2 = A12Position.None;
                        pos2cond = string.Empty;
                        switch (phase)
                        {
                            case 1:
                                switch (this.MyIndex)
                                {
                                    case 0:
                                        if (this.m_playerInfos[0].Debuff == Debuffs.Blue_Far)
                                            pos = A12Position.Top;
                                        else if (this.m_playerInfos[0].Debuff == Debuffs.Red_Near)
                                            pos = A12Position.BottomLeft;
                                        break;
                                    case 1:
                                        pos = A12Position.BottomLeft; break;

                                    case 2: pos = A12Position.BottomRight; break;
                                    case 3: pos = A12Position.BottomRight; break;

                                    case 4:
                                    case 5:
                                    case 6:
                                        switch (this.m_playerInfos[this.MyIndex].Debuff)
                                        {
                                            case Debuffs.Purple_Circle: pos = this.m_playerInfos[this.MyIndex].FirstPos; break;
                                            case Debuffs.Green_Share:   pos = A12Position.BottomLeft;                    break;
                                        }
                                        break;
                                    case 7:
                                        switch (this.m_playerInfos[7].Debuff)
                                        {
                                            case Debuffs.Green_Share: pos = A12Position.BottomLeft; break;
                                            case Debuffs.Purple_Circle:
                                                     if (this.m_playerInfos[4].Debuff == Debuffs.Green_Share) pos = this.m_playerInfos[4].FirstPos;
                                                else if (this.m_playerInfos[5].Debuff == Debuffs.Green_Share) pos = this.m_playerInfos[5].FirstPos;
                                                else if (this.m_playerInfos[6].Debuff == Debuffs.Green_Share) pos = this.m_playerInfos[6].FirstPos;
                                                break;
                                        }
                                        break;
                                }
                                break;

                            case 2:
                                switch (this.MyIndex)
                                {
                                    case 0:
                                    case 1:
                                        switch (this.m_playerInfos[this.MyIndex].Debuff)
                                        {
                                            case Debuffs.Purple_Circle: pos = A12Position.Left;        break;
                                            case Debuffs.Blue_Far:      pos = A12Position.Top;         break;
                                            case Debuffs.Red_Near:      pos = A12Position.BottomRight; break;
                                        }
                                        break;

                                    case 2:
                                    case 3:
                                        switch (this.m_playerInfos[this.MyIndex].Debuff)
                                        {
                                            case Debuffs.Purple_Circle: pos = A12Position.Right;       break;
                                            case Debuffs.Blue_Far:      pos = A12Position.BottomRight; break;
                                            case Debuffs.Red_Near:      pos = A12Position.BottomRight; break;
                                        }
                                        break;

                                    case 4:
                                        switch (this.m_playerInfos[4].Debuff)
                                        {
                                            case Debuffs.Green_Share: pos = A12Position.BottomRight; break;
                                            case Debuffs.Red_Near:    pos = A12Position.BottomLeft;  break;
                                            case Debuffs.Blue_Far:    pos = A12Position.Top;         break;
                                        }
                                        break;

                                    case 5:
                                        switch (this.m_playerInfos[5].Debuff)
                                        {
                                            case Debuffs.Red_Near: pos = A12Position.BottomLeft; break;
                                            case Debuffs.Green_Share:
                                                     if (this.m_playerInfos[4].Debuff == Debuffs.Green_Share) pos = A12Position.BottomLeft;
                                                else if (this.m_playerInfos[4].Debuff != Debuffs.None)        pos = A12Position.BottomRight;
                                                break;
                                            case Debuffs.Blue_Far:
                                                /*
                                                if (this.m_playerInfos[4].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.Top;

                                                    if (this.m_playerInfos[4].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.BottomLeft;
                                                        pos2cond = "1";
                                                    }
                                                }
                                                */
                                                if (this.m_playerInfos[4].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.Top;

                                                    if (this.m_playerInfos[4].Debuff == Debuffs.Blue_Far)
                                                        pos = A12Position.BottomLeft;
                                                }
                                                break;
                                        }
                                        break;
                                    case 6:
                                        switch (this.m_playerInfos[6].Debuff)
                                        {
                                            case Debuffs.Red_Near: pos = A12Position.BottomLeft; break;
                                            case Debuffs.Green_Share:
                                                     if (this.m_playerInfos[7].Debuff == Debuffs.Green_Share) pos = A12Position.BottomRight;
                                                else if (this.m_playerInfos[7].Debuff != Debuffs.None)        pos = A12Position.BottomLeft;
                                                break;
                                            case Debuffs.Blue_Far:
                                                /*
                                                if (this.m_playerInfos[7].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.BottomLeft;

                                                    if (this.m_playerInfos[7].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.Top;
                                                        pos2cond = "4";
                                                    }
                                                }
                                                */
                                                if (this.m_playerInfos[7].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.BottomLeft;

                                                    if (this.m_playerInfos[7].Debuff == Debuffs.Blue_Far)
                                                        pos = A12Position.Top;
                                                }
                                                break;
                                        }
                                        break;
                                    case 7:
                                        pos = A12Position.BottomLeft;
                                        break;
                                }
                                break;

                            case 4:
                                switch (this.MyIndex)
                                {
                                    case 0:
                                    case 1:
                                        switch (this.m_playerInfos[this.MyIndex].Debuff)
                                        {
                                            case Debuffs.Purple_Circle: pos = A12Position.Left;  break;
                                            case Debuffs.Blue_Far:      pos = A12Position.Right; break;
                                            case Debuffs.Red_Near:      pos = A12Position.Right; break;
                                        }

                                        break;

                                    case 2:
                                    case 3:
                                        pos = A12Position.Right;
                                        break;

                                    case 4:
                                        switch (this.m_playerInfos[4].Debuff)
                                        {
                                            case Debuffs.Red_Near: pos = A12Position.Right; break;
                                            case Debuffs.Blue_Far: pos = A12Position.Left;  break;
                                        }
                                        break;
                                    case 5:
                                        switch (this.m_playerInfos[5].Debuff)
                                        {
                                            case Debuffs.Red_Near: pos = A12Position.Right; break;
                                            case Debuffs.Blue_Far:
                                                if (this.m_playerInfos[4].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.Left;

                                                    if (this.m_playerInfos[1].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.Right;
                                                        pos2cond = "1";
                                                    }
                                                }
                                                break;
                                        }
                                        break;
                                    case 6:
                                        switch (this.m_playerInfos[5].Debuff)
                                        {
                                            case Debuffs.Red_Near: pos = A12Position.Right; break;                                                    
                                            case Debuffs.Blue_Far:
                                                if (this.m_playerInfos[4].Debuff != Debuffs.None &&
                                                    this.m_playerInfos[5].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.Left;

                                                    if (this.m_playerInfos[4].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.Right;
                                                        pos2cond += " 1";
                                                    }
                                                    if (this.m_playerInfos[5].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos = A12Position.Left;
                                                        pos2 = A12Position.Right;
                                                        pos2cond += " 2";
                                                    }

                                                    pos2cond = pos2cond.Trim();
                                                }
                                                break;
                                        }

                                        break;
                                    case 7:
                                        switch (this.m_playerInfos[5].Debuff)
                                        {
                                            case Debuffs.Red_Near: pos = A12Position.Right; break;
                                            case Debuffs.Blue_Far:
                                                if (this.m_playerInfos[4].Debuff != Debuffs.None &&
                                                    this.m_playerInfos[5].Debuff != Debuffs.None &&
                                                    this.m_playerInfos[6].Debuff != Debuffs.None)
                                                {
                                                    pos = A12Position.Left;

                                                    if (this.m_playerInfos[4].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.Right;
                                                        pos2cond += " 1";
                                                    }
                                                    if (this.m_playerInfos[5].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.Right;
                                                        pos2cond += " 2";
                                                    }
                                                    if (this.m_playerInfos[6].Debuff == Debuffs.Blue_Far)
                                                    {
                                                        pos2 = A12Position.Right;
                                                        pos2cond += " 3";
                                                    }

                                                    pos2cond = pos2cond.Trim();
                                                }
                                                break;
                                        }
                                        break;
                                }
                                break;
                        }
                            
                        Config.WriteLog("My Location : " + pos.ToString());

                        if (pos != A12Position.None)
                        {
                            Overlay.OverlayShow(pos, pos2, pos2cond);
                            for (i = 0; i < 8; ++i)
                                this.m_playerInfos[i].Debuff = Debuffs.None;
                            shownOverlay = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Config.WriteLog(ex.ToString());
            }
            Config.WriteLog("End Worker");
        }
    }
    
    public enum A12Position { None, Top, Left, Right, BottomLeft, BottomRight }
    public enum Debuffs { None,  Blue_Far, Purple_Circle, Red_Near, Green_Share }
    public class PlayerInfo
    {
        public PlayerInfo(int index)
        {
            this.m_index = index;
        }
        private readonly int m_index;
        public int Index => this.m_index;

        public string Name { get; set; }
        public Debuffs Debuff { get; set; }
        public A12Position FirstPos { get; set; }

        public void Clear()
        {
            this.Name = string.Empty;
            this.Debuff = Debuffs.None;
            this.FirstPos = A12Position.None;
        }
    }
}
