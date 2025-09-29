using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetTemperatureMonitor.Service
{
    public class YudianClient : IDisposable
    {
        //监测线程
        private Thread monitorThread;
        private bool isRunning;
        private TcpClient tcpClient;
        //线程安全锁
        private readonly object lockObj = new object();
        //连接状态变化事件
        public event Action<bool> OnConnectionStateChanged;
        public event Action<string> OnErrorOccurred;

        private static readonly Lazy<YudianClient> instance = new Lazy<YudianClient>(() => new YudianClient());
        private YudianClient() 
        {
            tcpClient = new TcpClient
            {
                SendTimeout = 200,
                ReceiveTimeout = 200,
                ExclusiveAddressUse = false
            };
        }
        public static YudianClient Instance => instance.Value;
        //连接状态
        public bool IsConnected
        {
            get
            {
                lock (lockObj)
                {
                    if (tcpClient == null || !tcpClient.Connected)
                        return false;

                    var stream = tcpClient.GetStream();
                    //流不可读或Poll检测到断开
                    if (!stream.CanRead || (tcpClient.Client.Poll(100, SelectMode.SelectRead) && !stream.DataAvailable))
                    {
                        tcpClient.Close();
                        return false;
                    }
                    return true;
                }
            }
        }
        //设备连接
        public async Task DeviceConnectAsync(string ip, int port)
        {
            if (IsConnected)
            {
                OnErrorOccurred?.Invoke("已处于连接状态，无需重复连接");
                return;
            }
            isRunning = true;
            bool initialConnect = await TryConnectAsync(ip, port);
            //启动监测
            if (monitorThread ==  null || !monitorThread.IsAlive)
            {
                monitorThread = new Thread(MonitorConnect)
                {
                    IsBackground = true,
                    Name = "tcp连接监测"
                };
                monitorThread.Start((ip, port));
            }
        }
        //监测连接
        public void MonitorConnect(object state)
        {
            var (ip, port) = ((string, int))state;
            while (isRunning)
            {
                bool isConnected = IsConnected;
                //连接状态变化
                OnConnectionStateChanged?.Invoke(isConnected);
                //如果连接断开，尝试重连
                if (!isConnected)
                {
                    OnErrorOccurred?.Invoke("连接已断开，尝试重连...");
                    _ = TryConnectAsync(ip, port);
                }
                Thread.Sleep(3000);
            }
        }
        public async Task<bool> TryConnectAsync(string ip, int port)
        {
            try
            {
                lock(lockObj)
                {
                    if (tcpClient.Connected)
                    {
                        tcpClient.Close();
                    }
                    tcpClient = new TcpClient
                    {
                        SendTimeout = 300,
                        ReceiveTimeout = 300,
                        ExclusiveAddressUse = false
                    };
                }
                //使用异步连接并设置超时
                var connectTask = tcpClient.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(3000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    OnErrorOccurred?.Invoke($"连接 {ip}:{port} 超时");
                    return false;
                }
                //确保连接任务没有异常
                await connectTask;
                OnErrorOccurred?.Invoke($"成功连接到 {ip}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke($"连接失败: {ex.Message}");
                return false;
            }
        }
        //断开连接
        public void Disconnect()
        {
            isRunning = false;
            lock (lockObj)
            {
                if (tcpClient?.Connected ?? false)
                {
                    tcpClient.Close();
                }
            }
            OnConnectionStateChanged?.Invoke(false);
        }
        //CRC校验
        public byte[] CRC16(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new byte[2];
            //只处理前6位数据
            int lengthToProcess = Math.Min(data.Length, 6);
            ushort crc = 0xFFFF; //初始值
            ushort polynomial = 0xA001; //多项式

            for (int i = 0; i < lengthToProcess; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0) //检查最低位
                    {
                        crc = (ushort)((crc >> 1) ^ polynomial);
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            //转换为字节数组并交换高低位
            return new byte[]
            {
                (byte)(crc & 0xFF),       
                (byte)((crc >> 8) & 0xFF) 
            };
        }
        //构建读指令
        public byte[] PrepareReadCommandFrame(byte address, byte parameterCode)
        {
            byte[] command = new byte[8];
            Array.Copy(Global.Readcommand, 0, command, 0, Global.Readcommand.Length);
            command[0] = address;
            command[3] = parameterCode;
            byte[] crcBytes = CRC16(command);
            command[6] = crcBytes[0];
            command[7] = crcBytes[1];
            return command;
        }
        //构建写指令
        public byte[] PrepareWriteCommandFrame(byte address, byte parameterCode, int target)
        {
            byte[] command = new byte[8];
            Array.Copy(Global.Writecommand, 0, command, 0, Global.Readcommand.Length);
            command[0] = address;
            command[3] = parameterCode;
            command[4] = (byte)(target / 256);
            command[5] = (byte)(target % 256);
            byte[] crcBytes = CRC16(command);
            command[6] = crcBytes[0];
            command[7] = crcBytes[1];
            return command;
        }
        //发送写指令并解析返回
        public short GetReadValue(byte[] command, byte address)
        {
            short rawValue; // 初始化返回值
            lock (lockObj)
            {
                if (!tcpClient.Connected)
                    throw new InvalidOperationException("连接已断开");
                var stream = tcpClient.GetStream();
                if (stream.DataAvailable)
                {
                    stream.ReadByte();
                }
                
                // 设置读取超时
                tcpClient.ReceiveTimeout = 100;
                
                try
                {
                    stream.Write(command, 0, command.Length);
                    byte[] response = new byte[7];
                    int bytesRead = stream.Read(response, 0, response.Length);
                    
                    if (bytesRead <= 0)
                        return 0;
                    if (bytesRead < 7)
                        return 0;
                    
                    rawValue = (short)((response[3] << 8) | response[4]);
                }
                catch (IOException ex)
                {
                    return 0;
                }
            }
            return rawValue;
        }
        //判读是否有返回指令
        //发送读指令
        public void GetWriteValue(byte[] command)
        {
            lock (lockObj)
            {
                if (!tcpClient.Connected)
                    throw new InvalidOperationException("连接已断开");
                var stream = tcpClient.GetStream();

                //发送
                stream.Write(command, 0, command.Length);
                //接收
                byte[] response = new byte[7];
                int bytesRead = stream.Read(response, 0, response.Length);
                if (bytesRead <= 0)
                    throw new Exception("未收到设备响应");
                ////验证响应的基本有效性
                //if (bytesRead < 7)
                //    throw new FormatException("响应数据长度不足");

                ////解析16位数据（高字节在前，低字节在后）
                //rawValue = (short)((response[3] << 8) | response[4]);
            }
            //return rawValue;
        }
        //获取显示温度
        public float GetRealTimeTemp(byte address, byte parameterCode)
        {
            byte[] command = PrepareReadCommandFrame(address, parameterCode);
            return GetReadValue(command, address) / 10.0f;
        }
        //获取启动时间
        public short GetRunTime(byte address, byte paramterCode)
        {
            byte[] command = PrepareReadCommandFrame(address, paramterCode);
            return (short)(GetReadValue(command, address) / 10);
        }
        //获取上下限报警
        public float GetAlarmAsync(byte address, byte parameterCode)
        {
            byte[] command = PrepareReadCommandFrame(address, parameterCode);
            return GetReadValue(command, address) / 10.0f;
        }
        //设置目标温度
        public void SetTargetTemperature(byte address, byte parameterCode, short target)
        {
            byte[] command = PrepareWriteCommandFrame(address, parameterCode, target);
            GetWriteValue(command);
        }
        //设置程序段数
        public void SetStepNumber(byte address, byte parameterCode, short step)
        {
            byte[] command = PrepareWriteCommandFrame(address, parameterCode, step);
            GetWriteValue(command);
        }
        //设置阶段温度
        public void SetStepTemperature(byte address, byte parameterCode, int temperature)
        {
            byte[] command = PrepareWriteCommandFrame(address, parameterCode, temperature);
            GetWriteValue(command);
        }
        //设置阶段时间
        public void SetStepTime(byte address, byte parameterCode, short time)
        {
            byte[] command = PrepareWriteCommandFrame(address, parameterCode, time);
            GetWriteValue(command);
        }
        //设置烤箱启动
        public void SetStart(byte address, byte parameterCode, short status)
        {
            byte[] command = PrepareWriteCommandFrame(address, parameterCode, status);
            GetWriteValue(command);
        }
        //实现接口
        public void Dispose()
        {
            Disconnect();
            monitorThread?.Join();
            tcpClient?.Dispose();
        }
    }
}
