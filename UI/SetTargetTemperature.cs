using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTemperatureMonitor.Model;
using NetTemperatureMonitor.Service;
using NetTemperatureMonitor.Tools;
using Sunny.UI;

namespace NetTemperatureMonitor.UI
{
    public partial class SetTargetTemperature : UIForm
    {
        private readonly IFreeSql fsql;
        private readonly YudianClient tcpClient;
        public SetTargetTemperature(IFreeSql freeSql, YudianClient yudianClient)
        {
            fsql = freeSql;
            tcpClient = yudianClient;
            InitializeComponent();
        }
        private async Task<Tuple<int, List<float>, List<int>>> CountStep()
        {
            //在后台线程处理数据计算
            return await Task.Run(() =>
            {
                int count = 0;
                List<float> putInTemperture = new List<float>();
                List<int> roastTime = new List<int>();
                for (int i = 0; i < 7; i++)
                {
                    //检查是否有值
                    if (!string.IsNullOrEmpty(Controls.Find($"TxtStepTemperature{i + 1}", true).FirstOrDefault()?.Text)
                    && !string.IsNullOrEmpty(Controls.Find($"TxtStepTime{i + 1}", true).FirstOrDefault()?.Text))
                    {
                        count++;
                        //安全转换温度值
                        if (float.TryParse(Controls.Find($"TxtStepTemperature{i + 1}", true).FirstOrDefault()?.Text, out float temp))
                        {
                            putInTemperture.Add(temp); //只保留最后一个有效的温度值
                        }
                        //安全转换时间值并累加
                        if (int.TryParse(Controls.Find($"TxtStepTime{i + 1}", true).FirstOrDefault()?.Text, out int time))
                        {
                            roastTime.Add(time);
                        }
                    }
                }
                return Tuple.Create(count, putInTemperture, roastTime);
            });
        }
        //确认信息，设定温控仪，写入数据库
        private async void BtnConfirm_Click(object sender, EventArgs e)
        {
            //获取结果
            var (count, putInTemperature, roastTime) = await CountStep();
            //确认对话框并获取结果
            DialogResult result = MessageBox.Show("确认信息", "提示", MessageBoxButtons.YesNoCancel);
            if (result != DialogResult.Yes)
            {
                return;
            }
            //验证输入字段不为空
            if (string.IsNullOrEmpty(TxtPn.Text) ||
                string.IsNullOrEmpty(TxtMn.Text) ||
                string.IsNullOrEmpty(TxtPnCount.Text) ||
                string.IsNullOrEmpty(TxtPutInWorker.Text) ||
                string.IsNullOrEmpty(TxtStepCount.Text))
            {
                MessageBox.Show("输入信息不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            //安全转换StepCount
            if (!short.TryParse(TxtStepCount.Text, out short stepCount))
            {
                MessageBox.Show("段数格式不正确", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //验证段数匹配
            if (count != stepCount)
            {
                MessageBox.Show("程序段数不匹配", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //安全转换产品数量
            if (!short.TryParse(TxtPnCount.Text, out short productCount))
            {
                MessageBox.Show("产品数量格式不正确", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //准备要插入的数据
            string pn = TxtPn.Text;
            string mn = TxtMn.Text;
            string putInWorker = TxtPutInWorker.Text;
            float putInTemp = tcpClient.GetRealTimeTemp(Convert.ToByte(mn), Global.Pv);
            //在后台线程执行数据库操作
            try
            {
                await Task.Run(() =>
                {
                    tcpClient.SetStepNumber(Convert.ToByte(mn), Global.Pno, stepCount);
                    for (int i = 0; i < stepCount; i++)
                    {
                            UITextBox tempText = this.Controls.Find($"TxtStepTemperature{i + 1}", true).FirstOrDefault() as UITextBox;
                            int temp = Convert.ToInt16(tempText.Text) * 10;
                            UITextBox timeText = this.Controls.Find($"TxtStepTime{i + 1}", true).FirstOrDefault() as UITextBox;
                            short t = Convert.ToInt16(timeText.Text);
                            tcpClient.SetStepTemperature(Convert.ToByte(mn), Convert.ToByte(Global.Sp1 + i * 2), temp);
                            tcpClient.SetStepTime(Convert.ToByte(mn), Convert.ToByte(Global.T1 + i * 2), t);
                    }
                    DateTime time = DateTime.Now;
                    Product product = new Product
                    {
                        Id =  DataProcess.GenerateProductId(pn, mn, time),
                        Pn = pn,
                        Mn = mn,
                        Count = productCount,
                        PutInWorker = putInWorker,
                        PutInTime = time,
                        PutInTemperture = putInTemp,
                        RoastTime = roastTime.Sum()
                    };
                    Temperature temperature = new Temperature
                    {
                        Mn = mn,
                        TempTime = time,
                        TempValue = putInTemp
                    };
                    fsql.Insert<Product>().AppendData(product).ExecuteAffrows();
                    fsql.Insert<Temperature>().AppendData(temperature).ExecuteAffrows();
                });
                this.Close();
                //MessageBox.Show("数据保存成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}