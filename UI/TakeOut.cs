using NetTemperatureMonitor.Model;
using NetTemperatureMonitor.Service;
using Sunny.UI;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetTemperatureMonitor.UI
{
    public partial class TakeOut : UIForm
    {
        private readonly IFreeSql fsql;
        private readonly string id;
        private readonly string mn;
        private readonly YudianClient tcpClient = YudianClient.Instance;
        public TakeOut(IFreeSql freeSql, string Id, string Mn)
        {
            fsql = freeSql;
            id = Id;
            mn = Mn;
            InitializeComponent();
        }
        //数据库更新
        private void uiButton1_Click(object sender, System.EventArgs e)
        {
            DialogResult result = MessageBox.Show("确认信息", "提示", MessageBoxButtons.YesNoCancel);
            if (result != DialogResult.Yes)
            {
                return;
            }
            if (string.IsNullOrEmpty(TxtTakeWorker.Text))
            {
                MessageBox.Show("输入信息不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Task.Run(() =>
                fsql.Update<Product>()
                    .Set( a => new Product
                    {
                        TakeOutWorker = TxtTakeWorker.Text,
                        TakeOutTime = DateTime.Now,
                        TakeOutTemperture = tcpClient.GetRealTimeTemp(Convert.ToByte(mn), Global.Pv)
                    })
                    .Where(a => a.Id == id)
                    .ExecuteAffrows()
            );
            Task.Delay(200).Wait();
            this.Dispose();
        }
    }
}