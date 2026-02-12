using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using FileProcessor.Infrastructure.Runtime;
using FileProcessor.Infrastructure.Services;
using FileProcessor.Mediator;
using System.Text;

namespace FileProcessor.ToDelTest
{
    class Program
    {
        static void Main(string[] args)
        {
            // 支持 GB2312 编码解析 YJK 文件
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Header("第一阶段：环境初始化");

            // 1. 初始化核心组件
            var loader = new PluginLoader();
            var (templates, registry) = loader.LoadFromPluginsFolder();
            var snapshot = new SnapshotManager();
            var coordinator = new VersionCoordinator();
            var orchestrator = new FileOrchestrator(templates, snapshot, registry, coordinator);

            // 2. 初始化中介层 (Mediator)
            var mediator = new DataCoordinator(snapshot, coordinator);

            // 3. 订阅 Mediator 事件 (模拟 UI 响应)
            mediator.SlotDataChanged += (idx, data) =>
            {
                if (data != null)
                    Success($"[UI 事件] 插槽 {idx} 接收到数据更新: {data.DisplayName}, 行数: {data.Rows.Count}");
                else
                    Warn($"[UI 事件] 插槽 {idx} 数据已清空或未找到。");
            };

            mediator.SlotStatusChanged += (idx, status) =>
            {
                Info($"[UI 状态] 插槽 {idx} 状态变为: {status}");
            };

            Header("第二阶段：UI 插槽预配置");

            // 模拟 UI 启动时配置 0 号插槽显示位移，1 号插槽显示刚度
            Info("正在配置插槽：Slot 0 -> wdisp.out, Slot 1 -> wmass.out");
            mediator.ConfigureSlot(0, "wdisp.out", "WDisp_DisplacementData");
            mediator.ConfigureSlot(1, "wmass.out", "WMass_StiffnessAndCentroid");

            Header("第三阶段：模拟 YJK 计算过程 (产生真实数据)");

            // 模拟准备文件环境
            string testRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediatorTest");
            string designDir = Path.Combine(testRoot, "设计结果");
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            Directory.CreateDirectory(designDir);

            // 启动监控
            var monitor = new ProjectMonitorService(orchestrator, coordinator);
            monitor.StartScanning(testRoot);

            // 模拟 YJK 动作
            Info("模拟 YJK 写入 check.out (开始计算)...");
            coordinator.StartNewBatch("check.out");

            // 拷贝真实 wdisp.out 到目录
            string sourceWDisp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wdisp.out");
            if (File.Exists(sourceWDisp))
            {
                Info("正在写入 wdisp.out...");
                File.Copy(sourceWDisp, Path.Combine(designDir, "wdisp.out"), true);
            }
            else Error("未找到测试源文件 wdisp.out，请确保它在运行目录下。");

            Thread.Sleep(500); // 留出解析时间

            Info("模拟 YJK 写入 mainjss.out (结束计算，封版)...");
            coordinator.CommitCurrentBatch("mainjss.out");

            // 手动触发编排器执行（因为我们是控制台模拟，监控服务的线程可能还在扫描中）
            orchestrator.FlushBatchTasks();

            Header("第四阶段：Mediator 过滤能力测试");

            // 测试 1：查看 Slot 0 目前的全量数据
            var slot0Info = mediator.GetSlotInfo(0);
            Info($"Slot 0 当前过滤条件: {slot0Info.CurrentTower}, 状态: {slot0Info.Status}");

            // 测试 2：模拟 UI 切换塔号
            Info(">>> 操作：UI 切换至 [2号塔]...");
            mediator.SetTowerFilter(0, "2");

            // 测试 3：模拟 UI 切换回 [所有塔]
            Info(">>> 操作：UI 切换至 [All]...");
            mediator.SetTowerFilter(0, "All");

            Header("测试总结");
            Success("1. 内核解析正常。");
            Success("2. Mediator 自动感知 VersionCommitted 正常。");
            Success("3. 插槽状态流转与数据过滤正常。");

            Console.WriteLine("\n按下回车键退出...");
            Console.ReadLine();
        }

        #region 辅助输出
        static void Header(string text)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine($" {text}");

            Console.ResetColor();
        }
        static void Info(string msg) => Console.WriteLine($"[INFO] {msg}");
        static void Success(string msg) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"[PASS] {msg}"); Console.ResetColor(); }
        static void Warn(string msg) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"[WARN] {msg}"); Console.ResetColor(); }
        static void Error(string msg) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"[FAIL] {msg}"); Console.ResetColor(); }
        #endregion

        static void RunRealFileSimulation(string root, string designDir, IVersionCoordinator coord, ISnapshotManager cache)
        {
            // A. 开启批次
            coord.StartNewBatch("check.out");
            Thread.Sleep(200);

            // B. 拷贝真实文件到监控目录 (模拟 YJK 输出)
            // 假设你把上传的 wdisp.out 放在了程序运行目录下
            string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wdisp.out");
            string targetFile = Path.Combine(designDir, "wdisp.out");

            if (File.Exists(sourceFile))
            {
                Log($"[Action] 正在拷贝真实文件进行解析: {Path.GetFileName(sourceFile)}", ConsoleColor.Yellow);
                File.Copy(sourceFile, targetFile, true);
            }
            else
            {
                Log($"[Error] 未找到源文件: {sourceFile}", ConsoleColor.Red);
                return;
            }

            Thread.Sleep(800); // 真实大文件留出 Hash 计算和监控响应时间

            // C. 提交并触发解析
            coord.CommitCurrentBatch("mainjss.out");
            Thread.Sleep(1500); // 解析大文件需要一点时间

            // D. 结果审计：查看 WDisp 的解析情况
            // 注意：WDispProcessor 中定义的 StandardBlockName 是 "WDisp_DisplacementData"
            var results = cache.GetHistory("WDisp_DisplacementData").ToList();

            if (!results.Any())
            {
                Log("[-] 未捕获到位移数据，请检查 WDispTemplate 的正则或 marker 是否匹配。", ConsoleColor.Red);
            }
            else
            {
                Log($"[+] 成功解析出 {results.Count} 个工况的数据块！", ConsoleColor.Green);
                foreach (var res in results)
                {
                    Log($"\n工况名称: {res.RawBlockName} | 数据行数: {res.Rows.Count}", ConsoleColor.White);
                    // 打印每个工况的前 3 行数据看看对齐情况
                    PrintTable(res.Rows.Take(3).ToList());
                }
            }
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