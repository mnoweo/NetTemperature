using OfficeOpenXml;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetTemperatureMonitor.Tools
{
    public class DataProcess
    {
        //生成产品Id
        public static string GenerateProductId(string pn, string mn, DateTime time)
        {
            return $"{pn}{mn}{time:yyyyMMddHHmmss}";
        }
        //封装DataGridView更新逻辑
        public static void UpdateDataGridView<T>(DataGridView dgv, List<T> data, Func<T, object[]> dataGenerator)
        {
            dgv.Rows.Clear();
            if (data == null || data.Count == 0) return;
            foreach (var item in data)
            {
                dgv.Rows.Add(dataGenerator(item));
            }
        }
        //图表导出
        public  static async Task ChartExport(FormsPlot plot)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "保存图表";
                saveFileDialog.FileName = "温度曲线_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                saveFileDialog.Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg)" +
                    "|*.jpg|SVG 矢量图 (*.svg)|*.svg|所有文件 (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string savePath = saveFileDialog.FileName;
                        await Task.Run(() =>
                        {
                            plot.Plot.Save(savePath, 1920, 1080);
                            MessageBox.Show("导出成功！文件路径：" + saveFileDialog.FileName, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        //Excel表格导出
        public static async Task ExcelDataExport(DataGridView dgv)
        {
            try
            {
                ExcelPackage.License.SetNonCommercialOrganization("MaL");
                //非商业组织用途加入
                if (dgv.Rows.Count == 0)
                {
                    MessageBox.Show("没有数据可导出！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                //创建保存文件对话框
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "保存Excel文件",
                    FileName = "数据导出_" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Filter = "Excel文件 (*.xlsx)|*.xlsx|所有文件 (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var uiContext = SynchronizationContext.Current;
                    await Task.Run(() =>
                    {
                        try
                        {
                            using (var package = new ExcelPackage(new FileInfo(saveFileDialog.FileName)))
                            {
                                //创建工作表
                                var worksheet = package.Workbook.Worksheets.Add("数据导出");
                                //添加表头
                                for (int i = 0; i < dgv.Columns.Count; i++)
                                {
                                    if (dgv.Columns[i].Visible)
                                    {
                                        worksheet.Cells[1, i + 1].Value = dgv.Columns[i].HeaderText;
                                        worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                                    }
                                }
                                //添加数据
                                for (int i = 0; i < dgv.Rows.Count; i++)
                                {
                                    int colIndex = 0;
                                    for (int j = 0; j < dgv.Columns.Count; j++)
                                    {
                                        if (dgv.Columns[j].Visible)
                                        {
                                            if (dgv.Rows[i].Cells[j].Value != null)
                                            {
                                                worksheet.Cells[i + 2, colIndex + 1].Value = dgv.Rows[i].Cells[j].Value.ToString();
                                            }
                                            colIndex++;
                                        }
                                    }
                                }
                                //自动调整列宽
                                worksheet.Cells.AutoFitColumns();
                                //保存文件
                                package.Save();
                            }
                            uiContext.Post(_ =>
                            {
                                MessageBox.Show("导出成功！文件路径：" + saveFileDialog.FileName, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }, null);
                        }
                        catch (Exception ex)
                        {
                            uiContext.Post(_ =>
                            {
                                MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }, null);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
