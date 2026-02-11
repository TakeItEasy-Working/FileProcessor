using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using FileProcessor.Infrastructure.Runtime;
using FileProcessor.Infrastructure.Services;
using System.Text;

namespace FileProcessor.ToDelTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Log("=== 引擎深度调试启动 (Target: .NET 10.0) ===", ConsoleColor.Cyan);

            // 1. 初始化核心
            var loader = new PluginLoader();
            var (templates, registry) = loader.LoadFromPluginsFolder();
            var snapshot = new SnapshotManager();
            var coordinator = new VersionCoordinator();
            var orchestrator = new FileOrchestrator(templates, snapshot, registry, coordinator);

            // 订阅封版事件
            coordinator.VersionCommitted += (versionId) =>
            {
                Log($"[Event] 收到封版信号 {versionId}，驱动编排器执行待处理队列...", ConsoleColor.Magenta);
                orchestrator.FlushBatchTasks();
            };

            // 2. 环境准备
            var monitor = new ProjectMonitorService(orchestrator, coordinator);
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DebugWorkspace");
            string designDir = Path.Combine(root, "设计结果");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(designDir);

            // 3. 启动模拟
            monitor.StartScanning(root);
            RunSimulation(root, designDir, coordinator, snapshot);

            Log("\n=== 调试结束，按回车退出 ===", ConsoleColor.Cyan);
            Console.ReadLine();
        }

        static void RunSimulation(string root, string designDir, IVersionCoordinator coord, ISnapshotManager cache)
        {
            // --- A. 开启批次 ---
            coord.StartNewBatch("check.out");
            Thread.Sleep(300);

            // --- B. 写入数据 ---
            Log("\n[Action] 正在写入 wmass.out 数据并等待解析...", ConsoleColor.Yellow);
            string wmassPath = Path.Combine(designDir, "wmass.out");
            File.WriteAllText(wmassPath, GetWMassSampleContent());

            Thread.Sleep(500); // 等待 Watcher 捕获

            // --- C. 提交信号 ---
            coord.CommitCurrentBatch("mainjss.out");
            Thread.Sleep(1000); // 等待解析任务完成

            // --- D. 结果审计与表格打印 ---
            Log("\n" + new string('=', 50), ConsoleColor.White);
            Log("【 最终识别数据预览 】", ConsoleColor.White);
            Log(new string('=', 50), ConsoleColor.White);

            // 获取标准 ID 的历史数据
            var results = cache.GetHistory("WMass_StiffnessAndCentroid").ToList();

            if (!results.Any())
            {
                Log("[-] 结果库为空，请检查正则匹配或 Processor 注册。", ConsoleColor.Red);
            }
            else
            {
                foreach (var res in results)
                {
                    Log($"\n数据源: {res.SourceFileName} | 版本: {res.VersionId} | 块名: {res.RawBlockName}", ConsoleColor.Green);
                    PrintTable(res.Rows);
                }
            }
        }

        /// <summary>
        /// 核心：表格化打印逻辑
        /// </summary>
        static void PrintTable(List<Dictionary<string, string>> rows)
        {
            if (rows == null || rows.Count == 0) return;

            // 1. 确定所有的列名
            var columns = rows.SelectMany(r => r.Keys).Distinct().ToList();

            // 2. 计算每列的最大宽度
            var columnWidths = new Dictionary<string, int>();
            foreach (var col in columns)
            {
                int max = Math.Max(col.Length, rows.Max(r => r.ContainsKey(col) ? r[col]?.Length ?? 0 : 0));
                columnWidths[col] = max + 2; // 留一点间距
            }

            // 3. 打印表头
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.White;
            foreach (var col in columns)
            {
                Console.Write(col.PadRight(columnWidths[col]));
            }
            Console.WriteLine();
            Console.ResetColor();

            // 4. 打印分割线
            Console.WriteLine(new string('-', columnWidths.Values.Sum()));

            // 5. 打印行数据
            foreach (var row in rows)
            {
                foreach (var col in columns)
                {
                    string val = row.ContainsKey(col) ? row[col] : "-";
                    Console.Write(val.PadRight(columnWidths[col]));
                }
                Console.WriteLine();
            }
        }

        static void Log(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:mm:ss.fff}] {msg}");
            Console.ResetColor();
        }

        static string GetWMassSampleContent()
        {
            return @"
     **********************************************************
             各层刚心、偏心率、相邻层侧移刚度比等计算信息
     **********************************************************
  Floor No. 1     Tower No. 1
  Xstif=   230.3179(m)     Ystif=    69.2699(m)     Alf  =    45.0000(Degree)
  Xmass=   228.3240(m)     Ymass=    67.7515(m)     Gmass & G= 3700.3789 & 3462.8433(t)
  Eex  =     0.0467         Eey  =     0.0581
  Ratx =     1.0000         Raty =     1.0000
     ----------------------------------------------------------
  Floor No. 2     Tower No. 1
  Xstif=   240.1100(m)     Ystif=    70.1200(m)     Alf  =    45.0000(Degree)
  Xmass=   228.3240(m)     Ymass=    67.7515(m)     Gmass & G= 3750.1200 & 3500.0000(t)
  Eex  =     0.0512         Eey  =     0.0620
  Ratx =     0.9500         Raty =     0.9800
";
        }
    }
}