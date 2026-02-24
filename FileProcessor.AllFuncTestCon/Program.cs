using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using FileProcessor.Infrastructure.Runtime;
using FileProcessor.Infrastructure.Services;
using FileProcessor.Mediator;
using System.Data;
using System.Text;

namespace FileProcessor.ConsoleDebug
{
    class Program
    {
        [STAThread] // 对话框必须在 STA 线程运行
        static void Main(string[] args)
        {
            Console.Title = "YJK 数据流底层诊断工具";
            Header("第一阶段：环境初始化");

            // 1. 注册编码支持
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            LogToConsole("OK: 已注册 GB2312 编码支持", ConsoleColor.Green);

            // 2. 初始化核心组件
            var loader = new PluginLoader();
            var (templates, registry) = loader.LoadFromPluginsFolder();
            var snapshot = new SnapshotManager();
            var versionCoord = new VersionCoordinator();
            var orchestrator = new FileOrchestrator(templates, snapshot, registry, versionCoord);
            var monitor = new ProjectMonitorService(orchestrator, versionCoord);

            // 3. 初始化中介层
            var mediator = new DataCoordinator(snapshot, versionCoord);
            LogToConsole("OK: 核心装配完成 (Snapshot -> Orchestrator -> Mediator)", ConsoleColor.Green);

            // 4. 配置模拟插槽 (Slot 0) 监听
            mediator.SlotDataChanged += (idx, data) =>
            {
                Console.WriteLine("\n" + new string('>', 10) + $" [数据推送] 插槽 {idx} 接收到更新 " + new string('<', 10));
                if (data != null)
                {
                    PrintSimpleTable(data);
                }
                else
                {
                    LogToConsole("WARN: 接收到推送，但数据为空 (null)", ConsoleColor.Yellow);
                }
            };

            Header("第二阶段：选择项目目录");
            Console.WriteLine("请输入或粘贴 YJK 项目的完整路径 (例如 D:\\Project\\YJK_Test):");
            string selectedPath = Console.ReadLine()?.Trim('"'); // Trim('"') 是为了处理带引号的粘贴路径

            if (string.IsNullOrEmpty(selectedPath) || !Directory.Exists(selectedPath))
            {
                LogToConsole("ERROR: 路径不存在，程序退出", ConsoleColor.Red);
                return;
            }
            LogToConsole($"OK: 目标目录确认: {selectedPath}", ConsoleColor.Green);

            Header("第三阶段：执行初始扫描 (InitialScan)");
            LogToConsole("正在调用 StartScanning...", ConsoleColor.Cyan);

            // 此处会触发 InitialScan
            monitor.StartScanning(selectedPath);

            // 在 monitor.StartScanning(selectedPath); 之后
            System.Threading.Thread.Sleep(500); // 等待解析完成

            // 查找 Snapshot 中实际存在的最新版本
            var anySample = snapshot.GetHistory("WDisp_DisplacementData").LastOrDefault();
            if (anySample != null)
            {
                string realVersion = anySample.VersionId;
                LogToConsole($"[修正] 检测到实际数据版本为: '{realVersion}'，正在切换视图...", ConsoleColor.Cyan);
                mediator.SwitchViewVersion(realVersion); // 关键：让中介层切换到有数据的那个桶
            }

            // 给后台扫描留一点点时间
            System.Threading.Thread.Sleep(500);

            // --- 核心诊断点：版本检查 ---
            string currentVer = mediator.ActiveViewVersionId ?? "NULL";
            LogToConsole($"[诊断] 当前 Mediator 活动版本 ID: '{currentVer}'", ConsoleColor.Yellow);

            // 关键：对齐版本，如果 Coordinator 默认版本是空的，我们根据 UI 逻辑尝试切一下
            if (string.IsNullOrEmpty(currentVer) || currentVer == "NULL")
            {
                LogToConsole("[诊断] 检测到版本为空，尝试切换至 'LIVE 实时状态'", ConsoleColor.Yellow);
                mediator.SwitchViewVersion("LIVE 实时状态");
                currentVer = mediator.ActiveViewVersionId;
            }

            Header("第四阶段：文件发现测试 (ComboBox 1 模拟)");
            var availableFiles = mediator.GetAvailableFiles().ToList();
            if (availableFiles.Any())
            {
                LogToConsole($"OK: 成功发现 {availableFiles.Count} 个可用文件:", ConsoleColor.Green);
                foreach (var f in availableFiles) Console.WriteLine($"  - {f}");
            }
            else
            {
                LogToConsole("FAIL: GetAvailableFiles() 返回为空！", ConsoleColor.Red);
                LogToConsole("[诊断] 正在检查 SnapshotManager 内部所有 Key...", ConsoleColor.Yellow);
                // 这里利用反射或调试方法查看 snapshot 内部
                // 模拟查看：如果是版本不匹配，通过这个日志就能看出
                CheckSnapshotInventory(snapshot);

                LogToConsole("\n请检查 InitialScan 是否将数据存入了正确的 VersionId 桶中。", ConsoleColor.White);
            }

            Header("第五阶段：交互式插槽配置 (Slot Configuration)");
            Console.WriteLine("请输入你想监听的【文件名】(例如 wdisp.out):");
            string fileName = Console.ReadLine();

            if (!string.IsNullOrEmpty(fileName))
            {
                var blocks = mediator.GetBlocksForFile(fileName).ToList();
                if (blocks.Any())
                {
                    LogToConsole($"OK: 找到 {blocks.Count} 个数据块:", ConsoleColor.Green);
                    for (int i = 0; i < blocks.Count; i++) Console.WriteLine($"  [{i}] {blocks[i]}");

                    Console.WriteLine("\n请输入数据块索引以开启 Slot 0 监控:");
                    if (int.TryParse(Console.ReadLine(), out int blockIdx) && blockIdx < blocks.Count)
                    {
                        string selectedDisplayName = blocks[blockIdx];

                        // [关键] 从 Snapshot 中反查这个显示名对应的标准 ID
                        var resultInfo = snapshot.GetResultsByFile(mediator.ActiveViewVersionId, fileName)
                                                 .FirstOrDefault(r => r.DisplayName == selectedDisplayName || r.RawBlockName == selectedDisplayName);

                        if (resultInfo != null)
                        {
                            LogToConsole($"[Action] 正在配置 Slot 0 -> {fileName} : {resultInfo.StandardBlockName}", ConsoleColor.Cyan);
                            mediator.ConfigureSlot(0, fileName, resultInfo.StandardBlockName);
                        }
                    }
                }
                else
                {
                    LogToConsole($"FAIL: 文件 '{fileName}' 中未发现任何数据块。", ConsoleColor.Red);
                }
            }

            Header("第六阶段：实时监控模式已开启");
            LogToConsole(">>> 此时你可以手动修改文件夹中的 .out 文件，或者通过 YJK 重新计算。", ConsoleColor.Magenta);
            LogToConsole(">>> 插槽更新后会自动刷新上方表格。", ConsoleColor.Magenta);
            LogToConsole(">>> 输入 'exit' 退出程序。", ConsoleColor.Magenta);

            while (true)
            {
                string cmd = Console.ReadLine();
                if (cmd?.ToLower() == "exit") break;
            }
        }

        #region 辅助工具

        static void Header(string text)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.WriteLine($"\n === {text} === ");
            Console.ResetColor();
        }

        static void LogToConsole(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            Console.ResetColor();
        }

        static void PrintSimpleTable(ProcessedResult result)
        {
            if (result == null || result.Rows.Count == 0) return;

            Console.WriteLine($"\n显示数据块: {result.DisplayName} (共 {result.Rows.Count} 行)");

            // 取前 5 行展示
            var columns = result.Columns.Select(c => c.Header).ToList();
            string headerRow = string.Join(" | ", columns.Take(6));
            Console.WriteLine(new string('-', headerRow.Length));
            Console.WriteLine(headerRow);
            Console.WriteLine(new string('-', headerRow.Length));

            foreach (var row in result.Rows.Take(5))
            {
                var vals = result.Columns.Take(6).Select(c => row.ContainsKey(c.Key) ? row[c.Key] : "");
                Console.WriteLine(string.Join(" | ", vals));
            }
            if (result.Rows.Count > 5) Console.WriteLine("... (仅显示前 5 行)");
            Console.WriteLine(new string('-', headerRow.Length));
        }

        static void CheckSnapshotInventory(ISnapshotManager snapshot)
        {
            LogToConsole(">>> 正在扫描 SnapshotManager 内部指纹...", ConsoleColor.DarkYellow);

            // 我们尝试暴力反查：不管版本，看看 SnapshotManager 里有没有任何数据
            // 假设你的 ISnapshotManager 提供了获取所有版本的方法，如果没有，我们用常见 Key 试探
            var testKeys = new[] { "WDisp_DisplacementData", "WMass_StiffnessAndCentroid" };

            foreach (var key in testKeys)
            {
                var history = snapshot.GetHistory(key).ToList();
                if (history.Any())
                {
                    LogToConsole($"[FOUND] 发现数据块 '{key}'，但它存在于以下版本中:", ConsoleColor.Green);
                    foreach (var h in history)
                        Console.WriteLine($"   - 版本: '{h.VersionId}' | 文件: {h.SourceFileName}");
                }
                else
                {
                    LogToConsole($"[EMPTY] 块 '{key}' 在 Snapshot 中完全不存在。", ConsoleColor.Red);
                }
            }
        }

        #endregion
    }
}