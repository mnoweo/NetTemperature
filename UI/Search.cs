using NetTemperatureMonitor.Model;
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
    public partial class Search : UIForm
    {
        public readonly IFreeSql fsql;
        public Search(IFreeSql freeSql)
        {
            fsql = freeSql;
            InitializeComponent();
            //加入时间选择框
            this.toolStrip2.Items.Insert(4, new ToolStripControlHost(new DateTimePicker()));
            this.Load += Search_Load;
        }
        //窗体加载
        private void Search_Load(object sender, EventArgs e)
        {
            DgvSearch.RowHeadersVisible = false;
            DgvSearch.AllowUserToAddRows = false;
            DgvRecord.RowHeadersVisible = false;
            DgvRecord.AllowUserToAddRows = false;
            #region 初始图配置
            formsPlot.Plot.Font.Set("微软雅黑");
            formsPlot.Plot.Title("温度曲线");
            formsPlot.Plot.XLabel("时间");
            formsPlot.Plot.Axes.DateTimeTicksBottom();
            formsPlot.Plot.YLabel("温度(℃)");
            //formsPlot.Refresh();
            #endregion
            var uiContext = SynchronizationContext.Current;
            //默认查询
            Task.Run(() =>
            {
                var mnList = fsql.Select<Temperature>()
                        .ToList(a => a.Mn)
                        .Distinct();
                var list = fsql.Select<Product>()
                        .ToList();
                uiContext.Post(_ =>
                {
                    tstMnList.Items.Clear();
                    if (mnList != null && mnList.Count() > 0)
                    {
                        tstMnList.Items.AddRange(mnList.ToArray());
                    }
                    //tstMnList.SelectedIndex = 0;
                    DgvSearch.Rows.Clear();
                    foreach (var item in list)
                    {
                        DgvSearch.Rows.Add(item.Id, item.Mn, item.Count, item.PutInTime, item.TakeOutTime);
                    }
                }, null);
            });
        }
        //根据条件查询
        private void TbtnSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tstOrder.Text) && 
                string.IsNullOrEmpty(tstPutIn.Text) && 
                string.IsNullOrEmpty(tstTakeOut.Text))
            {
                UIMessageBox.Show("请输入查询条件");
                return;
            }
            ExecuteSearchQuery();
        }
        //执行查询并更新UI
        private void ExecuteSearchQuery()
        {
            var uiContext = SynchronizationContext.Current;
            Task.Run(() =>
            {
                List<Product> list;
                try
                {
                    var query = fsql.Select<Product>();
                    if (!string.IsNullOrEmpty(tstOrder.Text))
                    {
                        query = query.Where(a => a.Id == tstOrder.Text.Trim());
                    }
                    if (!string.IsNullOrEmpty(tstPutIn.Text))
                    {
                        query = query.Where(a => a.PutInWorker == tstPutIn.Text.Trim());
                    }
                    if (!string.IsNullOrEmpty(tstTakeOut.Text))
                    {
                        query = query.Where(a => a.TakeOutWorker == tstTakeOut.Text.Trim());
                    }
                    list = query.ToList();
                }
                catch (Exception ex)
                {
                    uiContext.Post(_ => {
                        UIMessageBox.Show("查询失败：" + ex.Message);
                    }, null);
                    return;
                }
                uiContext.Post(_ =>
                {
                    DgvSearch.Rows.Clear();
                    foreach (var item in list)
                    {
                        DgvSearch.Rows.Add(item.Id, item.Mn, item.Count, item.PutInTime, item.TakeOutTime);
                    }
                }, null);
            });
        }
        //根据机器编号获取曲线图和记录
        private void TbtnSearchRecord_Click(object sender, EventArgs e)
        {
            string mnValue = tstMnList.Text;
            DateTime datetime = Convert.ToDateTime(this.toolStrip2.Items[4].Text);
            //dataGridView数据更新
            Task.Run(() =>
            {
                try
                {
                    var list = fsql.Select<Temperature>()
                                    .Where(a => a.Mn == mnValue)
                                    .ToList();
                    //根据烤箱编号和时间联合查询
                    var plotList = fsql.Select<Temperature>()
                            .Where(a => a.Mn == mnValue && 
                                    a.TempTime >= datetime)
                            .ToList();
                    var dateList = plotList.Select(item => item.TempTime).ToList();
                    var valueList = plotList.Select(item => item.TempValue).ToList();
                    this.Invoke(new Action(() =>
                    {
                        //数据容器填充
                        DataProcess.UpdateDataGridView(DgvRecord, list, item =>
                            new object[] { item.TempValue, item.TempTime });
                        //图形显示
                        formsPlot.Plot.Clear();
                        formsPlot.Plot.Add.Scatter(dateList, valueList);
                        formsPlot.Plot.Axes.AutoScale();
                        formsPlot.Refresh();
                    }));
                }
                catch (Exception ex)
                {
                    //捕获所有异常，避免线程崩溃
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"数据更新失败：{ex.Message}", "错误",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }
        //导出图表
        private async void BtnExport_Click(object sender, EventArgs e)
        {
            await DataProcess.ChartExport(formsPlot);
        }
        //导出Excel
        private async void TbtnExportExcel_Click(object sender, EventArgs e)
        {
            await DataProcess.ExcelDataExport(DgvSearch);
        }
    }
}