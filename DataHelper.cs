using FreeSql;
using System;

namespace NetTemperatureMonitor
{
    public class DataHelper
    {
        private static Lazy<IFreeSql> sqlserver = new Lazy<IFreeSql>(() =>
        {
            var fsql = new FreeSql.FreeSqlBuilder()
                .UseAdoConnectionPool(true)
                .UseConnectionString(DataType.SqlServer, "Data Source=DESKTOP-US7E3PM;" +
                "Initial Catalog=NetTm;" +
                "User ID=sa;" +
                "Password=123456")
                //.UseAutoSyncStructure(true)
                .Build();

            //fsql.CodeFirst.SyncStructure(typeof(Model.Product));
            //fsql.CodeFirst.SyncStructure(typeof(Model.Temperature));

            return fsql;
        });
        public static IFreeSql Instance => sqlserver.Value;
    }
}