using System;

namespace NetTemperatureMonitor.Model
{
    public class Product
    {
        //单号 Pn + 机台编号 + 放入时间
        [FreeSql.DataAnnotations.Column(IsPrimary = true, IsIdentity = false)]
        public string Id { get; set; }
        //产品编号
        public string Pn { get; set;  }
        //机台编号
        public string Mn { get; set; }
        //数量
        public int Count { get; set; } 
        //放入人员
        public string PutInWorker { get; set; }
        //放入时间
        public DateTime PutInTime { get; set; }
        //放入温度
        public float PutInTemperture { get; set; }
        //烘烤时间
        public int RoastTime { get; set; }
        //取出人员
        public string TakeOutWorker { get; set; }
        //取出时间
        public DateTime TakeOutTime { get; set; }
        //取出温度
        public float TakeOutTemperture { get; set; }
    }
}