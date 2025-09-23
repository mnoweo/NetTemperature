using NetTemperatureMonitor.Model;
using NetTemperatureMonitor.Service;
using NetTemperatureMonitor.Tools;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetTemperatureMonitor.UI
{
    public partial class MainForm : UIForm
    {
        #region 成员变量
        private readonly IFreeSql fsql = DataHelper.Instance;
        private readonly YudianClient tcpClient = YudianClient.Instance;
        public Product product = new Product();
        public Temperature temperature = new Temperature();
        //容器数据更新线程
        private Thread updateData = null;
        private CancellationTokenSource cts;
        
        //所有烤箱字典，键为烤箱标号，值表示是否在线
        //private Dictionary<string, bool> allMnList = new Dictionary<string, bool>();
        //private static readonly object communicationLock = new object();
      
        #endregion

        public MainForm()
        {
            _ = tcpClient.DeviceConnectAsync(Global.IpAddress, Global.Port);
            InitializeComponent();
            //事件订阅
            tcpClient.OnConnectionStateChanged += TcpClient_OnConnectionStateChanged;
            tcpClient.OnErrorOccurred += TcpClient_OnErrorOccurred;
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            CbxMnList.SelectedIndexChanged += CbxMnList_SelectedIndexChanged;
        }
        //窗体加载
        private void MainForm_Load(object sender, EventArgs e)
        {
            InitDataGridViews();
            Temperaturetimer.Start();
            cts = new CancellationTokenSource();
            if (updateData == null || !updateData.IsAlive)
            {
                updateData = new Thread(DateGridViewDisplay)
                {
                    IsBackground = true,
                    Name = "容器数据更新"
                };
                updateData.Start(cts.Token);
            }
        }
        //烤箱编号变化是显示温度
        private void CbxMnList_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }
        //初始化DataGridView公共属性
        private void InitDataGridViews()
        {
            var dataGridViews = new List<DataGridView> { DgvLeft, DgvMain, DgvBottom };
            foreach (var dgv in dataGridViews)
            {
                dgv.RowHeadersVisible = false;
                dgv.AllowUserToAddRows = false;
                dgv.DoubleBuffered(true);
            }
        }
        //容器数据显示
        private void DateGridViewDisplay(object tokenObj)
        {
            var token = (CancellationToken)tokenObj;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var list = fsql.Select<Product>().ToList();
                    this.Invoke(new Action(() =>
                    {
                        DataProcess.UpdateDataGridView(DgvMain, list, item =>
                            new object[] { item.Id, item.Mn, item.Count, item.PutInTime, item.TakeOutTime });
                        DataProcess.UpdateDataGridView(DgvLeft, list, item =>
                            new object[] { item.Id, item.Mn, item.Count, item.RoastTime });
                        DataProcess.UpdateDataGridView(DgvBottom, list, item =>
                            new object[] { item.Id, item.Mn, item.PutInTime, item.TakeOutTime, item.PutInWorker, item.TakeOutWorker });
                    }));
                    Thread.Sleep(1000);
                    
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    //捕获所有异常，避免线程崩溃
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"数据更新失败：{ex.Message}", "错误",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                    //出错后延迟重试
                    Thread.Sleep(3000);
                }
            }
        }
        private void TcpClient_OnErrorOccurred(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(TcpClient_OnErrorOccurred), message);
                return;
            }
            Console.WriteLine(message);
            // 如果需要也可以更新UI状态
            //if (message.Contains("断开"))
            //{
            //    BtnConnectStatus.Text = "连接状态：已断开";
            //}
        }
        //网口连接状态变化
        private void TcpClient_OnConnectionStateChanged(bool isConnected)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool>(TcpClient_OnConnectionStateChanged), isConnected);
                return;
            }
            if (isConnected)
            {
                BtnConnectStatus.Text = "已连接";
                LedStatus.Color = System.Drawing.Color.LightGreen;
            }
            else
            {
                BtnConnectStatus.Text = "已断开";
                LedStatus.Color = System.Drawing.Color.OrangeRed;
            }
        }
        
        //放入物料
        private void BtnPutIn_Click(object sender, EventArgs e)
        {
            SetTargetTemperature setTargetTemperature = new SetTargetTemperature(fsql, tcpClient);
            setTargetTemperature.Show();
        }
        //查询
        private void BtnSearch_Click(object sender, EventArgs e)
        {
            Search search = new Search(fsql);
            search.Show();
        }
        //退出系统
        private void BtnExit_Click(object sender, EventArgs e)
        {
            Temperaturetimer.Stop();
            tcpClient.Dispose();
            this.Dispose();
            Application.Exit();
        }
        //关闭时取消订阅
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            cts?.Cancel();
            if (updateData != null && updateData.IsAlive)
            {
                updateData.Join(1000);
            }
            cts?.Dispose();
            tcpClient.OnConnectionStateChanged -= TcpClient_OnConnectionStateChanged;
            tcpClient.OnErrorOccurred -= TcpClient_OnErrorOccurred;
            //断开连接并释放资源
            tcpClient.Disconnect();
            tcpClient.Dispose();
        }
        #region
        public DateTime CurTime { get; set; }
        public float CurTemperature { get; set; }
        public string CurMn { get; set; }
        #endregion
        //循环获取温控仪数据
        private void Temperaturetimer_Tick(object sender, EventArgs e)
        {
            // 禁用计时器防止重入
            Temperaturetimer.Enabled = false;
            string SelectMn = CbxMnList.Text;
            
            Task.Run(() =>
            {
                try
                {
                    // 执行同步操作
                    var mnlist = fsql.Select<Product>()
                                    .Where(a => a.Mn != null)
                                    .ToList(a => a.Mn)
                                    .Distinct()
                                    .ToList();
                    var dataList = new List<Temperature>();
                    foreach (var item in mnlist)
                    {
                        CurMn = item;
                        CurTime = DateTime.Now;
                        CurTemperature = tcpClient.GetRealTimeTemp(Convert.ToByte(item), Global.Pv);
                        if (SelectMn == CurMn)
                        {
                            this.Invoke(new Action(() =>
                        {
                            TxtTemperature.Text = CurTemperature.ToString();
                        }));
                        }
                        if (CurTemperature != 0)
                        {
                            dataList.Add(new Temperature
                            {
                                Mn = item,
                                TempTime = CurTime,
                                TempValue = CurTemperature
                            });
                        }
                    }
                    fsql.Insert<Temperature>(dataList).ExecuteAffrows();
                    // 更新UI时使用Invoke
                    this.Invoke(new Action(() =>
                    {
                        CbxMnList.Items.Clear();
                        foreach (var mn in mnlist)
                        {
                            CbxMnList.Items.Add(mn);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    
                }
                finally
                {
                    this.Invoke(new Action(() =>
                    {
                        Temperaturetimer.Enabled = true;
                    }));
                }
            });
        }
        //数据容器中取出事件
        private void DgvBottom_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                if (e.ColumnIndex == DgvBottom.Columns["BtakeOut"].Index)
                {
                    //取出当前行的 takeOutWorker 单元格值
                    string takeOutWorker = DgvBottom.Rows[e.RowIndex].Cells["BtakeOutWorker"].Value?.ToString();
                    string id = DgvBottom.Rows[e.RowIndex].Cells["BorderList"].Value.ToString();
                    string mn = DgvBottom.Rows[e.RowIndex].Cells["Bmn"].Value.ToString();
                    
                    //检查是否为空
                    if (!string.IsNullOrEmpty(takeOutWorker))
                    {
                        MessageBox.Show("该物料已经取出，请选择其它物料操作");
                        return;
                    }
                    else
                    {
                        TakeOut takeOut = new TakeOut(fsql, id, mn);
                        takeOut.Show();
                    }
                }
            }
        }
    }
}