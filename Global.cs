using System;

namespace NetTemperatureMonitor
{
    public class Global
    {
        public static byte[] Readcommand = {0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00};
        public static byte[] Writecommand = {0x00, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00};
        //服务器IP地址
        //public static readonly string IpAddress = "192.168.240.172";
        public static readonly string IpAddress = "192.168.240.171";
        //服务器端口
        public static readonly Int32 Port = 10067;
        //ModbusRtu读功能
        public static readonly byte Read = 0x03;
        //ModbusTtu写功能
        public static readonly byte Write = 0x06;
        //给定Sv
        public static readonly byte TargetSv = 0x00;
        //自整定选择
        public static readonly byte At = 0x1D;
        //只读 PV
        public static readonly byte Pv = 0x4A;
        //只读 SV
        public static readonly byte Sv = 0x4B;
        //已运行时间
        public static readonly byte RunTime = 0x2F;
        //上限报警
        public static readonly byte Hial = 0x01;
        //下限报警
        public static readonly byte Loal = 0x02;
        //温控仪控制方式
        public static  readonly byte Ctrl = 0x06;
        //升温速率
        public static readonly byte Spr = 0x2A;
        //报警选择
        public static readonly byte Adis = 0x23;
        //运行状态
        public static  readonly byte Srun = 0x1B;

        #region 程序段数设置
        //程序段数
        public static readonly byte Pno = 0x2b;
        //第一段
        public static readonly byte Sp1 = 0x50;
        public static readonly byte T1 = 0x51;
        //第二段
        public static readonly byte Sp2 = 0x52;
        public static readonly byte T2 = 0x53;
        //第三段
        public static readonly byte Sp3 = 0x54;
        public static readonly byte T3 = 0x55;
        //第四段
        public static readonly byte Sp4 = 0x56;
        public static readonly byte T4 = 0x57;
        //第五段
        public static readonly byte Sp5 = 0x58;
        public static readonly byte T5 = 0x59;
        //第六段
        public static readonly byte Sp6 = 0x60;
        public static readonly byte T6 = 0x61;
        //第七段
        public static readonly byte Sp7 = 0x62;
        public static readonly byte T7 = 0x63;
        #endregion

    }
    public enum Ctrl
    {
        ONOFF,
        APID,
        NPID,
        POP,
        SOP
    }
    public enum At
    {
        OFF,
        ON,
        FOFF,
        AAT
    }
    public enum Adis
    {
        OFF,
        ON,
        FOFF
    }
    public enum Srun
    {
        RUN,
        STOP,
        HOLD
    }
}