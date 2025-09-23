using System;

namespace NetTemperatureMonitor.Model
{
    public class Temperature
    {
        //温控仪编号
        public string Mn{ get; set; }
        //温度
        public float TempValue { get; set; }
        //时间
        public DateTime TempTime { get; set; }
        //放入时间，方便进行关联
    }
}