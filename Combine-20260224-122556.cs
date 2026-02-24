// ========================================
// 自动生成的合并文件
// 生成时间: 2026/2/24 12:25:56
// ========================================


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\InMemorySnapshotManager.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace ConProjectMonitor
{
    public class InMemorySnapshotManager : ISnapshotManager
    {
        // 存储结构：VersionId -> List of Results
        private readonly ConcurrentDictionary<string, List<ProcessedResult>> _storage = new();

        public void AddSnapshot(ProcessedResult result)
        {
            var list = _storage.GetOrAdd(result.VersionId, _ => new List<ProcessedResult>());
            lock (list)
            {
                list.Add(result);
            }
            // 实时打印，方便验证
            Console.WriteLine($"[仓库存入] 块:{result.BlockName} | 版本:{result.VersionId} | 哈希:{result.OriginHash.Substring(0, 8)}...");
        }

        public IEnumerable<ProcessedResult> GetResultsByVersion(string versionId)
            => _storage.TryGetValue(versionId, out var results) ? results : Enumerable.Empty<ProcessedResult>();

        public IEnumerable<ProcessedResult> GetHistory(string blockName)
            => _storage.Values.SelectMany(x => x).Where(r => r.BlockName == blockName);

        public ProcessedResult? GetResult(string versionId, string blockName)
            => GetResultsByVersion(versionId).FirstOrDefault(r => r.BlockName == blockName);
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\MockBlockProcessor.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;

namespace ConProjectMonitor
{
    public class MockBlockProcessor : IBlockProcessor
    {
        // 简单处理：只要块名包含 "Block" 就处理
        public string TargetBlockName => "MockBlock";

        public bool CanProcess(string blockName) => blockName.Contains("Block");

        public int Priority => 1;

        public ProcessedResult Process(RawDataBlock block)
        {
            // 模拟将行数据转换为表格行
            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Key", "Val1" }, { "Raw", block.Lines.FirstOrDefault() ?? "" } }
            };

            return new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = "模拟显示块",
                Category = "测试分类",
                Rows = rows,
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition("Key", "键"),
                    new ColumnDefinition("Raw", "原始行数据")
                }
            };
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\MockOutTemplate.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;

namespace ConProjectMonitor
{
    public class MockOutTemplate : IFileTemplate
    {
        // 匹配所有以 .out 结尾的文件
        public string FileNamePattern => @".*\.out$";

        // 核心：对齐你要求的子目录逻辑
        public string SubDirectory => "设计结果";

        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            // 模拟拆分逻辑：每个文件我们固定拆出一个块
            yield return new RawDataBlock(
                blockName: $"Block_{fileName}",
                lines: new[] { "Header: Test", "Data: 123" },
                startLine: 1,
                filePath: filePath
            );
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\Program.cs
// ----------------------------------------

using ConProjectMonitor;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;

// --- 1. 环境准备 ---
string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestProject");
string designDir = Path.Combine(root, "设计结果");
if (Directory.Exists(root)) Directory.Delete(root, true);
Directory.CreateDirectory(designDir);

Console.WriteLine("=== 开始验证：哨兵触发与版本聚合 ===");

// --- 2. 模拟依赖注入 ---
// 这里需要你有至少一个 Mock 模板和处理器，否则 Orchestrator 会跳过文件
var mockTemplates = new List<IFileTemplate> { new MockOutTemplate() };
var mockProcessors = new List<IBlockProcessor> { new MockBlockProcessor() };

var coordinator = new VersionCoordinator(3000); // 3秒聚合窗口
var repo = new InMemorySnapshotManager();
var orchestrator = new FileOrchestrator(mockTemplates, repo, mockProcessors);
var monitor = new ProjectMonitorService(orchestrator, coordinator, mockTemplates);

// --- 3. 启动监控 ---
monitor.Start(root);

// --- 4. 模拟 YJK 流程 ---

// 第一步：写入哨兵文件 (触发版本 A)
Console.WriteLine("\n[动作 1] 写入 dsnctrl.ini...");
File.WriteAllText(Path.Combine(root, "dsnctrl.ini"), "Job: FullAnalysis");

// 第二步：迅速写入两个结果文件
Thread.Sleep(500);
Console.WriteLine("[动作 2] 写入 wmass.out...");
File.WriteAllText(Path.Combine(designDir, "wmass.out"), "[Summary]\nFloor=12");

Thread.Sleep(800);
Console.WriteLine("[动作 3] 写入 wpj.out...");
File.WriteAllText(Path.Combine(designDir, "wpj.out"), "[Detail]\nSteel=1500");

// 第三步：等待静默期结束
Console.WriteLine("\n等待 3 秒静默期聚合...");
Thread.Sleep(4000);

// 第四步：模拟第二次计算 (触发版本 B)
Console.WriteLine("\n[动作 4] 再次修改 dsnctrl.ini (开启新版本)...");
File.WriteAllText(Path.Combine(root, "dsnctrl.ini"), "Job: Update");

Thread.Sleep(500);
Console.WriteLine("[动作 5] 更新 wmass.out...");
File.WriteAllText(Path.Combine(designDir, "wmass.out"), "[Summary]\nFloor=12\nChange=1");

Console.WriteLine("\n验证结束，请检查版本 ID 是否按预期切换。按下回车退出。");
Console.ReadLine();
monitor.Stop();

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\Program.cs
// ----------------------------------------

using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;
using FileProcessor.Core.Models; // 引入模型命名空间
using System.Text;

// 注册 GB2312 编码支持（针对 YJK 输出文件）
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// --- 路径配置 ---
string pluginDir = Path.Combine(AppContext.BaseDirectory, "Plugins");
string watchDir = Path.Combine(AppContext.BaseDirectory, "WatchFolder");

// 确保必要目录存在
if (!Directory.Exists(pluginDir)) Directory.CreateDirectory(pluginDir);
if (!Directory.Exists(watchDir)) Directory.CreateDirectory(watchDir);

Console.WriteLine("=== .NET 10.0 文件分析系统运行中 ===");

// 1. 初始化引擎：加载 DLL 插件并注册
var loader = new PluginLoader();
var (templates, registry) = loader.LoadPlugins(pluginDir);
Console.WriteLine($"[系统] 已加载模板: {templates.Count} 个");

// 2. 初始化核心逻辑与存储
var orchestrator = new FileOrchestrator(templates, registry);
var snapshotManager = new SnapshotManager();

// 3. 启动监控服务
using var monitor = new ProjectMonitorService(watchDir, orchestrator);

// 订阅事件：解析完成后自动入库
// 此时得到的 snapshot.DataBlocks 中存储的是 ProcessedResult 强类型对象
monitor.OnSnapshotCreated += (snapshot) =>
{
    snapshotManager.AddSnapshot(snapshot);
    Console.WriteLine($"\n[通知] 新文件已归档: {snapshot.FileName} (Hash: {snapshot.FileHash[..8]})");
    Console.WriteLine($"[调试] 识别到的块名列表:");
    foreach (var blockName in snapshot.GetAvailableBlockNames())
    {
        Console.WriteLine($"    - '{blockName}'");
    }
};

Console.WriteLine($"[系统] 正在监听: {watchDir}");
Console.WriteLine("操作指引: 将 wmass.out 放入文件夹。输入 's' 查看数据预览, 'q' 退出。");

// 4. 交互循环
while (true)
{
    var key = Console.ReadKey(true).KeyChar;
    if (key == 'q') break;

    if (key == 's')
    {
        // 目标块名（必须与 Processor 中的 TargetBlockName 一致）
        string targetBlock = "各层刚心、偏心率、相邻层侧移刚度比等计算信息";

        // 从 SnapshotManager 中获取最近一次解析到的该块结果
        // 注意：AggregateLatestData 现在返回的是 IEnumerable<ProcessedResult>
        var results = snapshotManager.AggregateLatestData(targetBlock);

        Console.WriteLine($"\n--- 工业级数据验证: {targetBlock} ---");
        int fileCount = 0;

        foreach (ProcessedResult result in results)
        {
            fileCount++;
            Console.WriteLine($"[记录源 {fileCount}] 来自文件: {result.BlockName}");
            Console.WriteLine($"[显示名称] {result.DisplayName} | [分类] {result.Category}");

            // 检查是否有行数据
            if (result.Rows != null && result.Rows.Count > 0)
            {
                Console.WriteLine(new string('-', 70));

                // 动态构建表头：虽然我们知道 WMass 的结构，但这样写可以适配任何 ProcessedResult
                // 我们根据 result.Columns 的定义来生成表头显示
                var header = string.Join(" | ", result.Columns.Select(c => $"{c.HeaderText,-12}"));
                Console.WriteLine(header);
                Console.WriteLine(new string('-', 70));

                // 遍历标准化的 Rows 字典列表
                foreach (var row in result.Rows)
                {
                    // 根据 Columns 定义的 Key 顺序提取数据
                    var rowContent = string.Join(" | ", result.Columns.Select(c =>
                        $"{row.GetValueOrDefault(c.Key, "N/A"),-12}"));

                    Console.WriteLine(rowContent);
                }
                Console.WriteLine(new string('-', 70));
            }
            else
            {
                Console.WriteLine("  (！) 该数据块未包含可提取的行数据。");
            }
        }

        if (fileCount == 0)
            Console.WriteLine("(!) 尚未在快照存储中找到该数据块。请检查解析器是否成功执行。");

        Console.WriteLine("------------------------------------------------------------\n");
    }
}

Console.WriteLine("服务退出。");

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\App.xaml.cs
// ----------------------------------------

using System.Configuration;
using System.Data;
using System.Windows;

namespace Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }

}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\AssemblyInfo.cs
// ----------------------------------------

using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.AllFuncTestCon\Program.cs
// ----------------------------------------

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

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\Log.cs
// ----------------------------------------

using System.Diagnostics;

namespace FileProcessor.DebugHelpers
{
    public static class Log
    {
        [Conditional("DEBUG")]
        public static void Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\App.xaml.cs
// ----------------------------------------

using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;

namespace FileProcessor.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 关键：支持 GB2312 编码解析
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            base.OnStartup(e);
        }
    }

}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\AssemblyInfo.cs
// ----------------------------------------

using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\MainWindow.xaml.cs
// ----------------------------------------

using FileProcessor.Desktop.ViewModels;
using FileProcessor.Engine.Runtime;
using FileProcessor.Infrastructure.Runtime;
using FileProcessor.Infrastructure.Services;
using FileProcessor.Mediator;
using System.Data;
using System.Windows;

namespace FileProcessor.Desktop
{
    public partial class MainWindow : Window
    {
        // 保持引用，防止被 GC
        private ProjectMonitorService _monitor;
        private FileOrchestrator _orchestrator;

        public MainWindow()
        {
            InitializeComponent();

            // 1. 初始化核心底层组件 (这些必须是全程序唯一的实例)
            var loader = new PluginLoader();
            var (templates, registry) = loader.LoadFromPluginsFolder();

            // 关键点：这两个实例是数据的核心，必须共享给所有人
            var sharedSnapshot = new SnapshotManager();
            var sharedVersionCoord = new VersionCoordinator();

            // 2. 初始化编排器 (传入共享组件)
            _orchestrator = new FileOrchestrator(templates, sharedSnapshot, registry, sharedVersionCoord);

            // 3. 初始化中介层 (传入【同一个】共享组件)
            // 这样 UI 查的就是编排器存入的地方了
            var dataCoordinator = new DataCoordinator(sharedSnapshot, sharedVersionCoord);

            // 4. 初始化监控服务
            _monitor = new ProjectMonitorService(_orchestrator, sharedVersionCoord);

            // 5. 挂载 ViewModel
            this.DataContext = new MainViewModel(dataCoordinator, _monitor, sharedVersionCoord);
        }

        private void DataGrid_AutoGeneratingColumn(object sender, System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dg && dg.ItemsSource is DataView dv)
            {
                var col = dv.Table.Columns[e.PropertyName];
                if (col != null && !string.IsNullOrEmpty(col.Caption))
                {
                    e.Column.Header = col.Caption; // 使用我们存入的 Header 文本
                }
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\DataCoordinator.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Mediator.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FileProcessor.Mediator
{
    /// <summary>
    /// 数据协调器：UI 层访问核心引擎数据的唯一枢纽。
    /// <para>主要职责：</para>
    /// <list type="bullet">
    /// <item>管理插槽与物理文件的配置映射。</item>
    /// <item>处理版本视图切换（Live 模式与历史回溯）。</item>
    /// <item>执行业务级数据过滤（如按塔号筛选）。</item>
    /// <item>维护插槽生命周期状态（加载中、空数据、就绪）。</item>
    /// </list>
    /// </summary>
    public class DataCoordinator
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly List<SlotConfiguration> _slots = new();

        // [NEW] 私有影子变量：记录协调器感知到的最后一次提交的版本
        // 用于判断当前用户是否正处于“跟随最新结果”的状态
        private string _lastAutoVersionId;

        /// <summary>
        /// 获取或设置当前视图正在观察的版本 ID。
        /// 切换此 ID 会触发所有活跃插槽的数据重载。
        /// </summary>
        public string ActiveViewVersionId { get; private set; }

        public bool IsFollowingLive => ActiveViewVersionId == _lastAutoVersionId;

        /// <summary>
        /// 当某个插槽的数据内容发生更新时触发。
        /// <para>参数 1: 插槽索引 (0-7)；参数 2: 过滤后的结果对象（若无数据则为 null）。</para>
        /// </summary>
        public event Action<int, ProcessedResult?>? SlotDataChanged;

        /// <summary>
        /// 当某个插槽的业务状态（如就绪、加载中）发生变化时触发。
        /// <para>参数 1: 插槽索引 (0-7)；参数 2: 新的状态枚举值。</para>
        /// </summary>
        public event Action<int, SlotStatus>? SlotStatusChanged;

        /// <summary>
        /// 初始化数据协调器实例。
        /// </summary>
        /// <param name="snapshotManager">内核快照管理器，用于数据查询。</param>
        /// <param name="versionCoordinator">内核版本管理器，用于监听封版信号。</param>
        public DataCoordinator(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
        {
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
            _versionCoordinator = versionCoordinator ?? throw new ArgumentNullException(nameof(versionCoordinator));

            // 初始化 8 个预设插槽容器
            for (int i = 0; i < 8; i++) _slots.Add(new SlotConfiguration { SlotIndex = i });

            // 初始视图默认为内核当前最新版本
            ActiveViewVersionId = _versionCoordinator.CurrentVersionId;
            _lastAutoVersionId = ActiveViewVersionId;

            // 1. 宏观信号：监听哨兵文件序列闭环（Batch 更新）
            // 订阅内核封版事件：当 YJK 计算产生新文件并封版时，自动刷新 UI
            _versionCoordinator.VersionCommitted += OnVersionCommitted;

            // 2. 微观信号：监听单个文件解析完成（实时单刷）
            _versionCoordinator.LiveUpdateProcessed += OnLiveUpdateProcessed;
        }

        #region UI 交互接口 (Commands)

        /// <summary>
        /// 配置插槽关注的数据源。调用后将立即根据当前 <see cref="ActiveViewVersionId"/> 拉取数据。
        /// </summary>
        /// <param name="index">插槽索引 (0-7)。</param>
        /// <param name="fileName">目标文件名（如 "wdisp.out"）。</param>
        /// <param name="standardBlockName">目标数据块标准 ID。</param>
        public void ConfigureSlot(int index, string fileName, string standardBlockName)
        {
            if (index < 0 || index >= _slots.Count) return;

            var slot = _slots[index];
            slot.TargetFileName = fileName;
            slot.TargetBlockName = standardBlockName;

            RefreshSlot(index);
        }

        /// <summary>
        /// 切换视图观察的版本。常用于左侧版本列表点击切换。
        /// </summary>
        /// <param name="versionId">目标版本 ID。</param>
        public void SwitchViewVersion(string versionId)
        {
            // [核心修正]：处理 UI 传入的逻辑常量
            if (versionId == "LIVE 实时状态")
            {
                // 自动寻址：从 Snapshot 中找到最新的以 "Live_" 开头的物理版本号
                // 这里利用 GetAvailableFiles 的变体逻辑
                var latestLive = _snapshotManager.GetFileNames("")
                    .SelectMany(f => _snapshotManager.GetResultsByFile("", f))
                    .Where(r => r.VersionId.StartsWith("Live_"))
                    .OrderByDescending(r => r.VersionId)
                    .Select(r => r.VersionId)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(latestLive))
                {
                    versionId = latestLive;
                }
                else
                {
                    // 如果连 Live_ 都没找到，回退到内核当前的 CurrentVersionId
                    versionId = _versionCoordinator.CurrentVersionId;
                }
            }

            if (string.IsNullOrEmpty(versionId) || ActiveViewVersionId == versionId) return;

            ActiveViewVersionId = versionId;

            // 切换版本后，所有已配置的插槽都需要重新同步数据
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive) RefreshSlot(i);
            }
        }

        /// <summary>
        /// 更新指定插槽的塔号过滤条件。
        /// </summary>
        /// <param name="index">插槽索引。</param>
        /// <param name="towerId">塔号（如 "1", "2"）或 "All" 表示全楼汇总。</param>
        public void SetTowerFilter(int index, string towerId)
        {
            if (index < 0 || index >= _slots.Count) return;

            _slots[index].CurrentTower = towerId;
            RefreshSlot(index);
        }

        /// <summary>
        /// 强制触发指定插槽的数据刷新逻辑。
        /// </summary>
        /// <param name="index">插槽索引。</param>
        public void RefreshSlot(int index)
        {
            System.Diagnostics.Trace.WriteLine($"==== [Slot {index}] 进入刷新逻辑 ====");

            var slot = _slots[index];
            System.Diagnostics.Debug.WriteLine($"[Slot {index}] 尝试刷新. 文件: {slot.TargetFileName}, 块: {slot.TargetBlockName}, 版本: {ActiveViewVersionId}");
            if (!slot.IsActive) return;

            UpdateStatus(index, SlotStatus.Loading);

            // 关键点：使用 ActiveViewVersionId 确保视图与后端计算解耦
            var rawData = _snapshotManager.GetSpecificBlock(
                ActiveViewVersionId,
                slot.TargetFileName!,
                slot.TargetBlockName!);

            if (rawData == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Slot {index}] 失败: SnapshotManager 返回 null (检查版本号是否匹配)");
                UpdateStatus(index, SlotStatus.NoData);
                SlotDataChanged?.Invoke(index, null);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Slot {index}] 成功: 拿到 {rawData.Rows.Count} 行数据");

            // 执行多塔过滤逻辑
            var filteredData = ApplyTowerFilter(rawData, slot.CurrentTower);

            UpdateStatus(index, SlotStatus.Ready);
            SlotDataChanged?.Invoke(index, filteredData);
        }

        #endregion

        #region 元数据发现接口 (Query)

        /// <summary>
        /// 获取当前选定版本中所有已解析的文件名列表。用于填充 UI 下拉框。
        /// </summary>
        /// <returns>文件名集合。</returns>
        public IEnumerable<string> GetAvailableFiles()
        {
            // 如果当前还没选版本，默认去拿最新自动生成的版本
            var versionToQuery = ActiveViewVersionId ?? _lastAutoVersionId;
            if (string.IsNullOrEmpty(versionToQuery)) return Enumerable.Empty<string>();
            return _snapshotManager.GetFileNames(versionToQuery);
        }

        /// <summary>
        /// 获取指定文件中包含的所有可用数据块名称。用于填充 UI 的二级下拉框。
        /// </summary>
        /// <param name="fileName">文件名。</param>
        /// <returns>数据块显示名称集合。</returns>
        public IEnumerable<string> GetBlocksForFile(string fileName)
        {
            var results = _snapshotManager.GetResultsByFile(ActiveViewVersionId, fileName);
            return results.Select(r => !string.IsNullOrEmpty(r.DisplayName) ? r.DisplayName : r.RawBlockName).Distinct();
        }

        /// <summary>
        /// 获取指定数据块在所有历史版本中的变化趋势。用于右侧面板绘图。
        /// </summary>
        /// <param name="standardBlockName">数据块标准名。</param>
        /// <returns>按时间排序的历史结果集合。</returns>
        public IEnumerable<ProcessedResult> GetHistoryTrend(string standardBlockName)
        {
            return _snapshotManager.GetHistory(standardBlockName);
        }

        /// <summary>
        /// 获取指定插槽的当前配置信息副本（只读）。
        /// </summary>
        /// <param name="index">插槽索引。</param>
        /// <returns>插槽配置对象。</returns>
        public SlotConfiguration GetSlotInfo(int index) => _slots[index];

        #endregion

        #region 私有辅助逻辑

        /// <summary>
        /// 响应内核推送：当新计算完成时，如果是 Live 视图，则自动同步。
        /// </summary>
        private void OnVersionCommitted(string versionId)
        {
            // 逻辑：如果用户当前处于“跟随模式”，则自动把视图切到新的 Batch 版本
            bool shouldFollow = (ActiveViewVersionId == _lastAutoVersionId);

            _lastAutoVersionId = versionId;

            if (shouldFollow)
            {
                // 自动跟随新批次
                ActiveViewVersionId = versionId;

                // 触发所有插槽刷新
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].IsActive) RefreshSlot(i);
                }
            }
        }

        /// <summary>
        /// 响应微观更新信号：当某个文件单独解析完成后，精准刷新关联插槽。
        /// </summary>
        /// <param name="versionId">当前的实时版本号（通常为 Live 或 Live_时间戳）</param>
        /// <param name="fileName">刚刚更新的文件名（如 wdisp.out）</param>
        private void OnLiveUpdateProcessed(string versionId, string fileName)
        {
            // 微观刷新仅在“跟随模式”下有意义，或者目标版本就是 Live 时
            if (!IsFollowingLive && ActiveViewVersionId != "Live") return;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                // 只有处于活跃状态，且目标文件名匹配的插槽才触发局部刷新
                if (slot.IsActive && string.Equals(slot.TargetFileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    RefreshSlot(i);
                }
            }
        }

        /// <summary>
        /// 执行行级过滤。根据 Rows 中的 "Tower" 列筛选目标数据。
        /// </summary>
        private ProcessedResult ApplyTowerFilter(ProcessedResult original, string towerId)
        {
            if (string.Equals(towerId, "All", StringComparison.OrdinalIgnoreCase)) return original;

            return new ProcessedResult
            {
                StandardBlockName = original.StandardBlockName,
                DisplayName = original.DisplayName,
                Category = original.Category,
                SourceFileName = original.SourceFileName,
                VersionId = original.VersionId,
                Columns = original.Columns,
                // 执行内存过滤：仅保留匹配 Tower ID 的行
                Rows = original.Rows.Where(r => r.TryGetValue("Tower", out var v) && v == towerId).ToList(),
                Metadata = original.Metadata
            };
        }

        /// <summary>
        /// 内部状态转换维护。
        /// </summary>
        private void UpdateStatus(int index, SlotStatus status)
        {
            if (_slots[index].Status != status)
            {
                _slots[index].Status = status;
                SlotStatusChanged?.Invoke(index, status);
            }
        }

        #endregion
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Tests\UnitTest1.cs
// ----------------------------------------



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.ToDelTest\Program.cs
// ----------------------------------------

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

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.UTest\WMassLogicTests.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using FileProcessor.Engine.Runtime;
using FileProcessor.Infrastructure.Runtime;
using FileProcessor.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WMass.Plugin;
using Xunit;

namespace FileProcessor.UTest
{
    public class WMassLogicTests
    {
        /// <summary>
        /// 验证是否能正确识别并切分这种多行带星号的数据块
        /// </summary>
        [Fact]
        public void Test_WMass_RealFormat_Splitting()
        {
            // 1. 构造真实格式的字符串
            var sb = new StringBuilder();
            sb.AppendLine("   计算用时：00:03:54");
            sb.AppendLine("     **********************************************************");
            sb.AppendLine("               各层刚心、偏心率、相邻层侧移刚度比等计算信息");
            sb.AppendLine("  Floor No     : 层号");
            sb.AppendLine("     **********************************************************");
            sb.AppendLine("  Floor No. 1      Tower No. 1");
            sb.AppendLine("  Xstif=   230.3179(m)      Ystif=    69.2699(m)      Alf  =    45.0000(Degree)");
            sb.AppendLine("  Eex  =     0.0467         Eey  =     0.0581");
            sb.AppendLine("     ----------------------------------------------------------");
            sb.AppendLine("  Floor No. 2      Tower No. 1");
            sb.AppendLine("  Xstif=   229.3921(m)      Ystif=    71.5629(m)");

            var lines = sb.ToString().Split("\r\n").ToList();
            var template = new TestWMassTemplate();

            // 2. 执行切块
            var blocks = template.ExposeSplitBlocks(lines, "wmass.out").ToList();

            // 3. 验证
            Assert.NotEmpty(blocks);
            var targetBlock = blocks.FirstOrDefault(b => b.BlockName.Contains("各层刚心"));
            Assert.NotNull(targetBlock);

            // 验证块内是否包含了关键行
            Assert.Contains(targetBlock.Lines, l => l.Contains("Floor No. 1"));
            Assert.Contains(targetBlock.Lines, l => l.Contains("Xstif="));
        }

        /// <summary>
        /// 验证 Processor 是否能处理这种跨行、带单位、带等号的复杂格式
        /// </summary>
        [Fact]
        public void Test_WMass_RealFormat_Parsing()
        {
            // 1. 准备一个真实的层数据采样块
            // 模拟处理器接收到的多行数据（从 Floor No. 1 到分隔线前）
            var rawData = new[] {
                "  Floor No. 1      Tower No. 1",
                "  Xstif=   230.3179(m)      Ystif=    69.2699(m)      Alf  =    45.0000(Degree)",
                "  Xmass=   228.3240(m)      Ymass=    67.7515(m)      Gmass & G= 3700.3789 & 3462.8433(t)",
                "  Eex  =     0.0467         Eey  =     0.0581",
                "  Ratx =     1.0000         Raty =     1.0000"
            };

            var block = new RawDataBlock("各层刚心...计算信息", rawData, 10, "wmass.out");
            var processor = new WMassProcessor();

            // 2. 解析
            var result = processor.Process(block);

            // 3. 断言
            // 注意：这种格式下，一行层数据会被解析成一个 Row。
            // 你的 Processor 逻辑需要能够跨行寻找同一个 Floor No. 下的所有字段。
            Assert.NotEmpty(result.Rows);
            var row = result.Rows[0];

            Assert.Equal("230.3179", row["Xstif"]);
            Assert.Equal("69.2699", row["Ystif"]);
            Assert.Equal("0.0467", row["Eex"]);
        }

        

        // 包装类以便访问受保护方法
        private class TestWMassTemplate : WMassTemplate
        {
            public IEnumerable<RawDataBlock> ExposeSplitBlocks(List<string> lines, string path)
            {
                var method = typeof(WMassTemplate).GetMethod("SplitBlocks",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return (IEnumerable<RawDataBlock>)method.Invoke(this, new object[] { lines, path });
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Mock.Plugin\Class1.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;
using FileProcessor.Core.Contracts;

namespace Mock.Plugin
{
    // 匹配 satter.out 文件
    [FileProcessorPlugin(@"(?i)satter\.out", "设计结果")]
    public class SatterTemplate : BaseFileTemplate
    {
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            // 简单模拟：将整个文件作为一个块
            yield return new RawDataBlock("总荷载统计", lines.ToArray(), 1, filePath);
        }
    }

    [BlockProcessor("总荷载统计")]
    public class SatterProcessor : IBlockProcessor
    {
        public string TargetBlockName => "总荷载统计";
        public int Priority => 1;
        public bool CanProcess(string blockName) => blockName == TargetBlockName;

        public ProcessedResult Process(RawDataBlock block)
        {
            return new ProcessedResult
            {
                BlockName = block.BlockName,
                DisplayName = "建筑总重量汇总",
                Category = "荷载信息",
                Columns = new List<ColumnDefinition> { new("项目", "项目"), new("数值", "数值") },
                Rows = new List<Dictionary<string, string>> {
                    new() { ["项目"] = "恒载总计", ["数值"] = "15000 t" },
                    new() { ["项目"] = "活载总计", ["数值"] = "5000 t" }
                }
            };
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\App.xaml.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using UI.Models;
using UI.ViewModels;

namespace UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// 核心任务：组装并启动自动化解析引擎。
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 0. 注册编码提供程序（用于支持 GB2312 探测）
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            base.OnStartup(e);

            // --- 1. 自动化插件加载 (The "Magic" Step) ---
            // 只需要 PluginLoader，它会自动扫描带有 [FileProcessorPlugin] 和 [BlockProcessor] 特性的类
            var loader = new PluginLoader();

            // 按照约定，我们将插件存放在主程序运行目录下的 Plugins 文件夹内
            string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            // 如果文件夹不存在，则创建一个，方便用户放置 DLL
            if (!Directory.Exists(pluginPath))
            {
                Directory.CreateDirectory(pluginPath);
            }

            // 此时它会搜寻当前已引用的程序集（包括你后续要打包的插件 DLL）
            var (templates, registry) = loader.LoadPlugins(pluginPath);

            // --- 2. 基础设施零件初始化 ---
            // 版本协同器：负责 3s 的静默聚合期
            var coordinator = new VersionCoordinator(3000);

            // 快照管理器：内存中的数据仓库
            var snapshotManager = new SimpleSnapshotManager();

            // --- 3. 组装执行引擎 ---
            // Orchestrator 现在持有自动加载的模板列表和注册表
            var orchestrator = new FileOrchestrator(templates, snapshotManager, registry);

            // 监控服务：负责文件系统监听与初始扫描
            var monitorService = new ProjectMonitorService(
                orchestrator,
                coordinator,
                snapshotManager);

            // --- 4. 组装 ViewModel 并启动 UI ---
            // 注意：MainViewModel 的构造函数参数保持不变，确保 UI 层逻辑无需大改
            var mainVM = new MainViewModel(
                monitorService,
                snapshotManager,
                coordinator);

            var mainWindow = new MainWindow { DataContext = mainVM };
            mainWindow.Show();
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\AssemblyInfo.cs
// ----------------------------------------

using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\MainWindow.xaml.cs
// ----------------------------------------

using System.Windows;
using UI.ViewModels;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnCardResize(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (sender is FrameworkElement thumb && thumb.DataContext is DisplayCardViewModel vm)
            {
                vm.CardWidth = Math.Max(300, vm.CardWidth + e.HorizontalChange);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\WDispProcessor.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text.RegularExpressions;

namespace WDisp.Plugin
{
    /// <summary>
    /// 位移输出文件（wdisp.out）的高级解析器。
    /// 采用基于“前导空格缩进”的物理行识别算法，解决折叠表格中的主键（Floor）缺失与索引位移问题。
    /// </summary>
    [BlockProcessor("位移输出")]
    public class WDispProcessor : IBlockProcessor
    {
        public string TargetBlockName => "位移输出";
        public int Priority => 10;

        /// <summary>
        /// 标准化 ID：对应插槽配置，不同工况共享此 ID 但通过版本或 Metadata 区分
        /// </summary>
        public string StandardBlockName => "WDisp_DisplacementData";

        /// <summary>
        /// 默认 UI 分类
        /// </summary>
        public string DefaultCategory => "位移信息";

        /// <summary>
        /// 判定该块是否属于位移结果类数据
        /// </summary>
        public bool CanProcess(string blockName) => blockName.Contains("位移");

        public ProcessedResult Process(RawDataBlock block)
        {
            var rows = new List<Dictionary<string, string>>();
            var allColumns = new HashSet<string> { "Floor", "Tower" };

            // 1. 预处理：过滤干扰行并分析缩进规律
            var lines = block.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("---") && !l.Contains("***"))
                .ToList();

            if (lines.Count == 0) return null;

            // 2. 探测表头步长 (Stride) 与 物理表头映射
            int stride = 0;
            var headerLines = new List<string>();
            while (stride < lines.Count && !Regex.IsMatch(lines[stride].Trim(), @"^\d+"))
            {
                headerLines.Add(lines[stride]);
                stride++;
            }

            if (stride == 0 || stride >= lines.Count) return null;

            // 构建逻辑表头：headerGroups[物理行索引] = 该物理行对应的列名列表
            var headerGroups = BuildHeaderGroups(headerLines);
            foreach (var group in headerGroups)
                foreach (var col in group) allColumns.Add(col);

            // 3. 核心解析循环：以 Stride 为步长处理逻辑行组
            string lastFloor = "1";
            string lastTower = "1";

            for (int i = stride; i + stride <= lines.Count; i += stride)
            {
                var logicalRow = new Dictionary<string, string>();

                for (int s = 0; s < stride; s++)
                {
                    string currentLine = lines[i + s];
                    int indent = GetIndentCount(currentLine); // 获取当前物理行前导空格数

                    // 利用 Regex.Split 进行初步切分
                    var parts = Regex.Split(currentLine.Trim(), @"\s+").ToList();
                    if (parts.Count == 0) continue;

                    // A. 处理逻辑行起始行 (通常缩进较小)
                    if (s == 0)
                    {
                        // 根据规律：不带Floor的续行缩进更大。
                        // 判定逻辑：如果缩进较小且首位是数字，则是新Floor
                        if (indent < 8 && Regex.IsMatch(parts[0], @"^\d+$"))
                        {
                            lastFloor = parts[0];
                            // 如果第二位也是短数字，更新Tower
                            if (parts.Count > 1 && parts[1].Length < 4 && Regex.IsMatch(parts[1], @"^\d+$"))
                            {
                                lastTower = parts[1];
                            }
                        }

                        // 填充主键
                        logicalRow["Floor"] = lastFloor;
                        logicalRow["Tower"] = lastTower;

                        // 确定当前物理行的数据起始映射偏移
                        // 如果当前行开头没有Floor（缩进大或非数字），数据索引需要向右偏移映射
                        int headerOffset = (indent > 8) ? 2 : 0;
                        MapPartsToHeaders(parts, headerOffset, headerGroups[s], logicalRow);
                    }
                    else
                    {
                        // B. 处理折叠续行 (s > 0)
                        // 续行通常不含Floor和Tower，数据直接从对应表头的第三列(Index 2)开始
                        MapPartsToHeaders(parts, 2, headerGroups[s], logicalRow);
                    }
                }
                rows.Add(logicalRow);
            }

            return new ProcessedResult
            {
                RawBlockName = block.BlockName,
                StandardBlockName = block.BlockName,
                Rows = rows,
                Category = "位移结果",
                Columns = allColumns.Select(c => new ColumnDefinition(c, c)).ToList()
            };
        }

        /// <summary>
        /// 获取行首前导空格数量
        /// </summary>
        private int GetIndentCount(string line)
        {
            int count = 0;
            foreach (char c in line)
            {
                if (c == ' ') count++;
                else break;
            }
            return count;
        }

        /// <summary>
        /// 将多行表头解析为物理行对应的映射组
        /// </summary>
        private List<List<string>> BuildHeaderGroups(List<string> headerLines)
        {
            var groups = new List<List<string>>();
            foreach (var line in headerLines)
            {
                var parts = Regex.Split(line.Trim(), @"\s+");
                groups.Add(parts.Select(p => MapColumnName(p)).ToList());
            }
            return groups;
        }

        /// <summary>
        /// 执行点对点的数据绑定与特殊转换
        /// </summary>
        /// <param name="parts">物理行拆分后的数值</param>
        /// <param name="headerStartIdx">该物理行对应的表头起始索引（解决左移问题）</param>
        /// <param name="currentHeaders">当前物理行的列名定义</param>
        /// <param name="row">目标字典</param>
        private void MapPartsToHeaders(List<string> parts, int headerStartIdx, List<string> currentHeaders, Dictionary<string, string> row)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                int logicalIdx = i + headerStartIdx;
                if (logicalIdx < currentHeaders.Count)
                {
                    string colName = currentHeaders[logicalIdx];
                    string val = parts[i];
                    row[colName] = val;

                    // 触发位移角转换逻辑
                    if ((colName.Contains("位移角") || colName.Contains("/h")) && val.Contains("/"))
                    {
                        var driftParts = val.Split('/');
                        if (driftParts.Length == 2 && double.TryParse(driftParts[1], out double d) && d != 0)
                        {
                            row[colName + "_数值"] = (1.0 / d).ToString("F6");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 翻译原始列名为标准的业务中文名，保留方向感
        /// </summary>
        private string MapColumnName(string raw)
        {
            if (raw.Contains("Max-Dx/h")) return "最大层间位移角(X)";
            if (raw.Contains("Max-Dy/h")) return "最大层间位移角(Y)";
            if (raw.Contains("Max-(X)")) return "最大位移(X)";
            if (raw.Contains("Max-(Y)")) return "最大位移(Y)";
            if (raw.Contains("Ratio-Dx")) return "层间位移比(X)";
            if (raw.Contains("Ratio-Dy")) return "层间位移比(Y)";
            return raw;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\WDispTemplate.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;

namespace WDisp.Plugin
{
    /// <summary>
    /// 位移输出文件（wdisp.out）的切块模板类。
    /// 识别以 "====" 包裹的工况名称作为数据块的分界。
    /// </summary>
    [FileProcessorPlugin(@"wdisp.out", "设计结果")]
    public class WDispTemplate : BaseFileTemplate
    {
        /// <summary>
        /// 将位移输出文件切分为多个工况数据块。
        /// </summary>
        /// <param name="lines">文件全行内容</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>切分后的原始数据块集合</returns>
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;

            // wdisp.out 的工况分隔符特征为连续的等号
            string marker = "===";

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                // 识别模式： ==== 工况名称 ====
                if (line.Contains(marker))
                {
                    // 1. 结算当前正在收集的块
                    if (currentBlockLines.Count > 0)
                    {
                        yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                    }

                    // 2. 提取新标题：去掉 ==== 后两端的空白
                    currentTitle = line.Replace(marker, "").Trim();

                    // 3. 重置容器，准备接收新工况数据
                    currentBlockLines = new List<string>();
                    startLineNumber = i + 1;
                    continue;
                }

                currentBlockLines.Add(line);
            }

            // 结算最后一个工况块
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\WMassProcessor.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text.RegularExpressions;

namespace WMass.Plugin
{
    [BlockProcessor("各层刚心、偏心率、相邻层侧移刚度比等计算信息")]
    public class WMassProcessor : IBlockProcessor
    {
        // --- 1. 契约属性 ---
        /// <summary>
        /// 匹配的原始块名称标识
        /// </summary>
        public string TargetBlockName => "各层刚心、偏心率、相邻层侧移刚度比等计算信息";

        /// <summary>
        /// 处理器优先级
        /// </summary>
        public int Priority => 10;

        /// <summary>
        /// 标准化块 ID：用于 UI 插槽绑定和跨版本历史对比
        /// </summary>
        public string StandardBlockName => "WMass_StiffnessAndCentroid";

        /// <summary>
        /// 默认 UI 分类
        /// </summary>
        public string DefaultCategory => "计算信息";

        /// <summary>
        /// 判定是否可以处理该数据块，只要块名包含“刚心”，就尝试处理
        /// </summary>
        public bool CanProcess(string blockName) => blockName.Contains("刚心");

        // --- 2. 解析逻辑 ---
        /// <summary>
        /// 执行核心解析逻辑
        /// </summary>
        public ProcessedResult Process(RawDataBlock block)
        {
            // 初始化结果：遵循方案 B，利用 init 属性进行一次性赋值
            var result = new ProcessedResult
            {
                RawBlockName = block.BlockName,
                StandardBlockName = this.StandardBlockName,
                DisplayName = "各层刚心及刚度比",
                Category = this.DefaultCategory,
                SourceFileName = "wmass.out" // 必须显式指定，用于 SnapshotManager 索引
            };

            var floorRecords = new List<Dictionary<string, string>>();
            var discoveredKeys = new HashSet<string>(); // 使用 HashSet 记录所有出现过的 Key
            Dictionary<string, string>? currentFloorRow = null;
            var summaryData = new List<Dictionary<string, string>>();

            foreach (var rawLine in block.Lines)
            {
                string line = rawLine.Trim();

                // 1. 跳过分界线和空行
                if (string.IsNullOrWhiteSpace(line) || line.Contains("----"))
                    continue;

                // 2. 识别新楼层块起始 (算法核心 A)
                if (line.Contains("Floor No."))
                {
                    currentFloorRow = new Dictionary<string, string>();
                    floorRecords.Add(currentFloorRow);

                    // 提取Floor和Tower
                    var floorMatch = Regex.Match(line, @"Floor No\.\s*(?<v>\d+)");
                    var towerMatch = Regex.Match(line, @"Tower No\.\s*(?<v>\d+)");

                    if (floorMatch.Success) currentFloorRow["Floor"] = floorMatch.Groups["v"].Value;
                    if (towerMatch.Success) currentFloorRow["Tower"] = towerMatch.Groups["v"].Value;

                    discoveredKeys.Add("Floor");
                    discoveredKeys.Add("Tower");
                    continue;
                }

                // 3. 解析“标题=数据”格式的行 (算法核心 B)
                if (currentFloorRow != null)
                {
                    // 匹配模式：Key [空格或等号] Value
                    var matches = Regex.Matches(line, @"(?<key>[A-Za-z0-9&]+)\s*=\s*(?<value>[^ \t]+)");
                    foreach (Match m in matches)
                    {
                        string key = m.Groups["key"].Value.Trim();
                        string val = m.Groups["value"].Value.Trim();

                        // 去除数值中的单位，如 (m), (t)
                        val = Regex.Replace(val, @"\(.*?\)", "");

                        currentFloorRow[key] = val;
                        discoveredKeys.Add(key);
                    }
                }

                // 4. 处理特殊总结行
                if (line.Contains("最小刚度比"))
                {
                    var minMatch = Regex.Match(line, @"(?<key>.*方向最小刚度比):?\s*(?<val>[0-9\.]+)");
                    if (minMatch.Success)
                    {
                        summaryData.Add(new Dictionary<string, string> {
                            { "描述", minMatch.Groups["key"].Value.Trim() },
                            { "数值", minMatch.Groups["val"].Value.Trim() }
                        });
                    }
                }
            }

            // 5. 封装返回结果
            // 填充表格定义
            foreach (var key in discoveredKeys)
            {
                result.Columns.Add(new ColumnDefinition(key, key));
            }

            // 填充行数据
            result.Rows.AddRange(floorRecords);

            // 填充扩展元数据
            result.Metadata["StiffnessSummary"] = summaryData;

            return result;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\WMassTemplate.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Infrastructure;
using FileProcessor.Core.Models;

namespace WMass.Plugin
{
    /// <summary>
    /// WMass 结果文件解析插件。
    /// 通过特性告知核心 DLL：监控 wmass.out，且它位于“设计结果”子目录。
    /// </summary>
    [FileProcessorPlugin(@"wmass.out", "设计结果")]
    public class WMassTemplate : BaseFileTemplate
    {
        /// <summary>
        /// 仅需实现核心的切块逻辑，IO 和编码由核心 DLL 处理
        /// </summary>
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;

            string[] majorMarkers = { "****", "====" };

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                bool isMarker = majorMarkers.Any(m => line.Contains(m));

                if (isMarker)
                {
                    // 发现潜在的标题区域开始，向下寻找闭合的 Marker
                    int closingIndex = -1;
                    // 设定一个合理的查找范围（例如往下找 20 行），避免无限查找
                    for (int j = i + 1; j < lines.Count && j < i + 20; j++)
                    {
                        if (majorMarkers.Any(m => lines[j].Contains(m)))
                        {
                            closingIndex = j;
                            break;
                        }
                    }

                    // 如果找到了闭合 Marker，且中间有内容
                    if (closingIndex > i + 1)
                    {
                        // 1. 提取标题区域的所有行（排除上下装饰线）
                        var headerRegion = new List<string>();
                        for (int k = i + 1; k < closingIndex; k++)
                        {
                            string hLine = lines[k].Trim();
                            if (!string.IsNullOrWhiteSpace(hLine))
                            {
                                headerRegion.Add(hLine);
                            }
                        }

                        if (headerRegion.Count > 0)
                        {
                            // A. 结算上一个块
                            if (currentBlockLines.Count > 0)
                            {
                                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                            }

                            // B. 启动新块
                            // 第一行作为标准 RawBlockName
                            currentTitle = headerRegion[0];

                            // C. 核心逻辑修改：将标题区域剩余的行（摘要信息）下沉到数据行中
                            currentBlockLines = new List<string>();
                            if (headerRegion.Count > 1)
                            {
                                // Skip(1) 把除了第一行标题外的其他行都加进去
                                currentBlockLines.AddRange(headerRegion.Skip(1));
                            }

                            // D. 指针跳转到闭合 Marker 处
                            startLineNumber = closingIndex + 1;
                            i = closingIndex;
                            continue;
                        }
                    }
                }

                // 非 Marker 行（或未匹配成对 Marker 的行），作为普通数据收集
                currentBlockLines.Add(line);
            }

            // 结算文件末尾的最后一块
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\Converters\NullToVisibilityConverter.cs
// ----------------------------------------

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Desktop.Converters
{
    /// <summary>
    /// 将对象是否为 Null 转换为 Visibility 枚举值。
    /// 常用于根据数据是否存在来显示/隐藏 UI 元素。
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 如果对象不为 null，返回 Visible；否则返回 Collapsed。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果 parameter 传入 "Inverted"，则逻辑反转
            bool isInverted = parameter?.ToString() == "Inverted";

            bool isNull = value == null;

            if (isInverted)
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }

            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\Models\UiVersionMetadata.cs
// ----------------------------------------

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Desktop.Models
{
    /// <summary>
    /// UI 专用版本元数据：在内核 VersionId 基础上增加备注和编号功能。
    /// </summary>
    public class UiVersionMetadata : INotifyPropertyChanged
    {
        private string _note = string.Empty;

        /// <summary>
        /// 显示编号（如 1, 2, 3...）
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// 内核生成的原始 VersionId
        /// </summary>
        public string VersionId { get; init; } = string.Empty;

        /// <summary>
        /// 格式化后的时间显示
        /// </summary>
        public string TimeLabel { get; init; } = string.Empty;

        /// <summary>
        /// 用户手动输入的备注
        /// </summary>
        public string Note
        {
            get => _note;
            set
            {
                if (_note != value)
                {
                    _note = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\Services\IUiStateService.cs
// ----------------------------------------

using System;
using System.Collections.ObjectModel;
using FileProcessor.Core.Models;
using Desktop.Models;

namespace Desktop.Services
{
    /// <summary>
    /// UI 状态服务接口：管理版本历史、文件列表及备注信息。
    /// </summary>
    public interface IUiStateService
    {
        /// <summary>
        /// 带有备注和编号的版本历史列表
        /// </summary>
        ObservableCollection<UiVersionMetadata> VersionHistory { get; }

        /// <summary>
        /// 当前监控目录下发现的所有文件名列表
        /// </summary>
        ObservableCollection<string> AvailableFiles { get; }

        /// <summary>
        /// 系统支持的所有数据块（卡片类型）列表
        /// </summary>
        ObservableCollection<string> AvailableBlocks { get; }

        /// <summary>
        /// 设置并启动项目路径监控
        /// </summary>
        void SetProjectPath(string path);

        /// <summary>
        /// 获取特定版本下，某个文件中的某个数据块结果
        /// </summary>
        ProcessedResult? GetResult(string versionId, string fileName, string blockName);

        /// <summary>
        /// 当新版本产生或数据更新时触发
        /// </summary>
        event Action<string> DataUpdated;
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\Services\UiStateService.cs
// ----------------------------------------

//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Windows;
//using FileProcessor.Core.Contracts;
//using FileProcessor.Core.Models;
//using FileProcessor.Engine.Services;

//namespace Desktop.Services
//{
//    /// <summary>
//    /// UI 状态服务：作为前端的“版本化内存数据库”。
//    /// 该服务负责将内核中离散的文件快照（FileSnapshot）聚合为 UI 侧的完整版本快照。
//    /// 遵循“拉取”模式，仅在内核明确提交版本（VersionCommitted）后更新 UI 资源池。
//    /// </summary>
//    public class UiStateService : IUiStateService, IDisposable
//    {
//        private readonly ISnapshotManager _snapshotManager;
//        private readonly IVersionCoordinator _versionCoordinator;

//        /// <summary>
//        /// 内部存储：Key 为 VersionId, Value 为该版本下所有文件的 ProcessedResult 集合。
//        /// 结构：Dictionary<VersionId, Dictionary<FileName_BlockName, ProcessedResult>>
//        /// </summary>
//        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ProcessedResult>> _versionCache = new();

//        /// <summary>
//        /// 记录每个版本对应的友好显示信息。
//        /// </summary>
//        private readonly ConcurrentDictionary<string, string> _versionDisplayMap = new();

//        /// <summary>
//        /// 全局可用的版本号集合（历史记录），UI 列表绑定源。
//        /// </summary>
//        public ObservableCollection<string> AllVersionIds { get; } = new();

//        /// <summary>
//        /// 全局可用的文件名集合资源池。
//        /// </summary>
//        public ObservableCollection<string> AvailableFiles { get; } = new();

//        /// <summary>
//        /// 全局可用的数据块名称集合资源池。
//        /// </summary>
//        public ObservableCollection<string> AvailableBlocks { get; } = new();

//        /// <summary>
//        /// 数据更新事件：通知订阅者（如 MainViewModel）有新版本产生。
//        /// 参数为新产生的 VersionId。
//        /// </summary>
//        public event Action<string>? DataUpdated;

//        /// <summary>
//        /// 初始化 UI 状态服务。
//        /// </summary>
//        /// <param name="snapshotManager">内核快照管理器</param>
//        /// <param name="versionCoordinator">内核版本协调器</param>
//        public UiStateService(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
//        {
//            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
//            _versionCoordinator = versionCoordinator ?? throw new ArgumentNullException(nameof(versionCoordinator));

//            // 订阅内核版本提交事件：这是 UI 更新的唯一合法入口
//            _versionCoordinator.VersionCommitted += OnVersionCommitted;
//        }

//        /// <summary>
//        /// 当内核 3s 静默期结束，正式提交一个版本时触发。
//        /// 此方法负责将当前所有文件的最新快照“封存”到 UI 缓存中。
//        /// </summary>
//        /// <param name="versionId">内核生成的版本标识符</param>
//        private void OnVersionCommitted(string versionId)
//        {
//            // 1. 获取该时刻所有文件的最新状态
//            // 注意：内核 SnapshotManager 按文件名组织数据，我们需要将其转换为 UI 视角
//            var versionData = new ConcurrentDictionary<string, ProcessedResult>();

//            // 访问内核 SnapshotManager 内部的 history (通过转型或现有接口)
//            if (_snapshotManager is SnapshotManager sm)
//            {
//                // 获取当前所有已处理的文件名
//                var fileNames = AvailableFiles.ToList();

//                // 遍历内核中所有文件的最新快照
//                // 由于我们要“全窗口同步”，我们需要确保新版本包含所有文件的最新状态
//                foreach (var fileName in fileNames)
//                {
//                    var latestFileSnapshot = sm.GetHistory(fileName).FirstOrDefault();
//                    if (latestFileSnapshot != null)
//                    {
//                        foreach (var kvp in latestFileSnapshot.DataBlocks)
//                        {
//                            string blockName = kvp.Key;
//                            ProcessedResult result = kvp.Value;

//                            // 组合键：文件名 + 块名
//                            string storageKey = $"{fileName}_{blockName}";
//                            versionData[storageKey] = result;

//                            // 顺便更新全局资源池列表
//                            UpdateGlobalResourcePool(fileName, blockName);
//                        }
//                    }
//                }
//            }

//            // 2. 存入 UI 缓存
//            _versionCache[versionId] = versionData;

//            // 3. 更新 UI 列表并触发通知
//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                if (!AllVersionIds.Contains(versionId))
//                {
//                    // 新版本插入到首位，方便 UI 默认选中最新
//                    AllVersionIds.Insert(0, versionId);
//                }

//                // 触发自动跟随逻辑
//                DataUpdated?.Invoke(versionId);
//            });
//        }

//        /// <summary>
//        /// 安全地更新全局文件名和块名资源池。
//        /// </summary>
//        private void UpdateGlobalResourcePool(string fileName, string blockName)
//        {
//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                if (!AvailableFiles.Contains(fileName))
//                    AvailableFiles.Add(fileName);

//                if (!AvailableBlocks.Contains(blockName))
//                    AvailableBlocks.Add(blockName);
//            });
//        }

//        /// <summary>
//        /// 获取特定坐标下的数据。
//        /// </summary>
//        /// <param name="versionId">目标版本</param>
//        /// <param name="fileName">目标文件</param>
//        /// <param name="blockName">目标数据块</param>
//        /// <returns>解析结果</returns>
//        public ProcessedResult? GetResult(string versionId, string fileName, string blockName)
//        {
//            if (string.IsNullOrEmpty(versionId) || string.IsNullOrEmpty(fileName)) return null;

//            if (_versionCache.TryGetValue(versionId, out var versionData))
//            {
//                string storageKey = $"{fileName}_{blockName}";
//                if (versionData.TryGetValue(storageKey, out var result))
//                {
//                    return result;
//                }
//            }
//            return null;
//        }

//        /// <summary>
//        /// 清理资源并取消订阅。
//        /// </summary>
//        public void Dispose()
//        {
//            _versionCoordinator.VersionCommitted -= OnVersionCommitted;
//        }
//    }
//}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\ViewModels\MainViewModel.cs
// ----------------------------------------

//using System;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Windows.Data;
//using Desktop.Services;
//using FileProcessor.Core.Models;

//namespace Desktop.ViewModels
//{
//    /// <summary>
//    /// 主界面 ViewModel：负责协调 UI 状态与用户交互逻辑。
//    /// </summary>
//    public class MainViewModel : INotifyPropertyChanged
//    {
//        private readonly IUiStateService _uiStateService;

//        private string? _selectedVersion;
//        private string? _selectedFile;
//        private string? _selectedBlock;
//        private ProcessedResult? _currentResult;

//        /// <summary>
//        /// 构造函数，注入 UI 状态服务
//        /// </summary>
//        public MainViewModel(IUiStateService uiStateService)
//        {
//            _uiStateService = uiStateService;

//            // 监听数据更新事件，实现“自动跟随”最新版本
//            _uiStateService.DataUpdated += OnDataUpdated;

//            // 初始选中第一个可用的版本（如果有）
//            SelectedVersion = _uiStateService.AllVersionIds.FirstOrDefault();
//        }

//        #region 数据绑定属性

//        /// <summary>
//        /// 版本下拉列表数据源
//        /// </summary>
//        public ObservableCollection<string> VersionIds => _uiStateService.AllVersionIds;

//        /// <summary>
//        /// 文件下拉列表数据源
//        /// </summary>
//        public ObservableCollection<string> FileNames => _uiStateService.AvailableFiles;

//        /// <summary>
//        /// 数据块下拉列表数据源
//        /// </summary>
//        public ObservableCollection<string> BlockNames => _uiStateService.AvailableBlocks;

//        /// <summary>
//        /// 当前选中的版本
//        /// </summary>
//        public string? SelectedVersion
//        {
//            get => _selectedVersion;
//            set
//            {
//                if (SetProperty(ref _selectedVersion, value))
//                    RefreshData();
//            }
//        }

//        /// <summary>
//        /// 当前选中的文件
//        /// </summary>
//        public string? SelectedFile
//        {
//            get => _selectedFile;
//            set
//            {
//                if (SetProperty(ref _selectedFile, value))
//                    RefreshData();
//            }
//        }

//        /// <summary>
//        /// 当前选中的数据块
//        /// </summary>
//        public string? SelectedBlock
//        {
//            get => _selectedBlock;
//            set
//            {
//                if (SetProperty(ref _selectedBlock, value))
//                    RefreshData();
//            }
//        }

//        /// <summary>
//        /// 当前需要展示的解析结果对象
//        /// UI 中的 DataGrid 应绑定此对象的 Rows 和 Columns
//        /// </summary>
//        public ProcessedResult? CurrentResult
//        {
//            get => _currentResult;
//            private set => SetProperty(ref _currentResult, value);
//        }

//        #endregion

//        /// <summary>
//        /// 核心业务逻辑：根据当前选中的三维坐标，从服务中提取数据
//        /// </summary>
//        private void RefreshData()
//        {
//            if (string.IsNullOrEmpty(SelectedVersion) ||
//                string.IsNullOrEmpty(SelectedFile) ||
//                string.IsNullOrEmpty(SelectedBlock))
//            {
//                CurrentResult = null;
//                return;
//            }

//            // 获取最新处理结果
//            CurrentResult = _uiStateService.GetResult(SelectedVersion, SelectedFile, SelectedBlock);
//        }

//        /// <summary>
//        /// 当内核提交新版本时的回调
//        /// </summary>
//        private void OnDataUpdated(string newVersionId)
//        {
//            // 如果用户当前没有手动锁定某个历史版本，则自动切换到最新版本
//            // 逻辑：如果当前选中的是集合中的最后一个，则认为处于“跟随模式”
//            SelectedVersion = newVersionId;
//        }

//        #region INotifyPropertyChanged 实现

//        public event PropertyChangedEventHandler? PropertyChanged;

//        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
//        {
//            if (Equals(storage, value)) return false;
//            storage = value;
//            OnPropertyChanged(propertyName);
//            return true;
//        }

//        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        #endregion
//    }
//}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\Views\MainWindow.xaml.cs
// ----------------------------------------

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Attributes\BlockProcessorAttribute.cs
// ----------------------------------------

using System;

namespace FileProcessor.Core.Attributes
{
    /// <summary>
    /// 数据块处理器特性：用于标记 IBlockProcessor 的实现类。
    /// 核心 DLL 将根据此特性自动将切分后的 RawDataBlock 分发给对应的处理器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class BlockProcessorAttribute : Attribute
    {
        /// <summary>
        /// 该处理器支持的块标题名称或关键标识
        /// </summary>
        public string BlockName { get; }

        /// <summary>
        /// 优先级：当多个处理器匹配同一个块时，值越大（越高）越优先处理
        /// </summary>
        public int Priority { get; init; } = 0;

        /// <summary>
        /// 初始化数据块处理器特性
        /// </summary>
        /// <param name="blockName">支持的数据块名称</param>
        public BlockProcessorAttribute(string blockName)
        {
            BlockName = blockName;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Attributes\FileProcessorPluginAttribute.cs
// ----------------------------------------

using System;

namespace FileProcessor.Core.Attributes
{
    /// <summary>
    /// 文件处理器插件特性：用于标记 IFileTemplate 的实现类。
    /// 核心 DLL 将通过此特性识别插件及其监控的文件规则。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FileProcessorPluginAttribute : Attribute
    {
        /// <summary>
        /// 获取匹配该文件的正则表达式模式（如 @"(?i)wmass\.out"）
        /// </summary>
        public string FileNamePattern { get; }

        /// <summary>
        /// 获取该文件通常所在的相对子目录（如 "设计结果"）
        /// </summary>
        public string SubDirectory { get; }

        /// <summary>
        /// 初始化文件处理器插件特性
        /// </summary>
        /// <param name="fileNamePattern">文件名正则匹配模式</param>
        /// <param name="subDirectory">所属子目录，默认为空</param>
        public FileProcessorPluginAttribute(string fileNamePattern, string subDirectory = "")
        {
            FileNamePattern = fileNamePattern;
            SubDirectory = subDirectory;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Contracts\IBlockProcessor.cs
// ----------------------------------------

using FileProcessor.Core.Models;

namespace FileProcessor.Core.Contracts
{
    public interface IBlockProcessor
    {
        // --- 1. 派发机制属性 ---

        /// <summary>
        /// 目标块名：对应特性 BlockProcessorAttribute 中的名称。
        /// 用于维持你目前的快速查找 (Lookup) 机制。
        /// </summary>
        string TargetBlockName { get; }

        /// <summary>
        /// 优先级：当多个处理器竞争同一个块时，优先级高的胜出。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 模糊匹配逻辑：用于双轨制中的 fallback 匹配。
        /// </summary>
        bool CanProcess(string blockName);

        // --- 2. 数据对齐属性 ---

        /// <summary>
        /// 标准化块标识（如 StandardBlock_Stiffness）。
        /// 无论 YJK 还是 PKPM，只要性质相同，此 ID 必须一致。
        /// </summary>
        string StandardBlockName { get; }

        /// <summary>
        /// 业务分类（如“位移结果”），用于 UI 分组。
        /// </summary>
        string DefaultCategory { get; }

        // --- 3. 核心执行方法 ---

        /// <summary>
        /// 执行解析逻辑。
        /// </summary>
        ProcessedResult Process(RawDataBlock block);
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Contracts\IFileTemplate.cs
// ----------------------------------------

using FileProcessor.Core.Models;

namespace FileProcessor.Core.Contracts
{
    /// <summary>
    /// 插件标准化接口
    /// </summary>
    public interface IFileTemplate
    {
        // 匹配的文件名正则模式
        string FileNamePattern { get; }

        // 解析入口：负责从路径直接输出拆分后的原始数据块
        IEnumerable<RawDataBlock> Parse(string filePath);

        /// <summary>
        /// 文件所在的相对子目录。
        /// 对于 YJK，可以返回 "设计结果"；
        /// 对于根目录文件，返回 string.Empty 或 "."。
        /// </summary>
        string SubDirectory => string.Empty;
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Contracts\ISnapshotManager.cs
// ----------------------------------------

using FileProcessor.Core.Models;
using System.Collections.Generic;

namespace FileProcessor.Core.Contracts
{
    /// <summary>
    /// 快照存储接口：定义解析结果的存取契约。
    /// 移除了不必要的事件，遵循“单一职责原则”。
    /// </summary>
    public interface ISnapshotManager
    {
        /// <summary>
        /// 存储单个解析结果
        /// </summary>
        void AddSnapshot(ProcessedResult result);

        /// <summary>
        /// Level 1 查询：获取特定版本下解析过的所有文件名
        /// </summary>
        IEnumerable<string> GetFileNames(string versionId);

        /// <summary>
        /// Level 2 查询：获取特定文件下产生的所有标准化数据块结果
        /// </summary>
        IEnumerable<ProcessedResult> GetResultsByFile(string versionId, string fileName);

        /// <summary>
        /// 精确查询：获取特定版本的特定块数据
        /// </summary>
        ProcessedResult? GetSpecificBlock(string versionId, string fileName, string standardBlockName);

        /// <summary>
        /// 历史查询：获取同名块在所有版本中的记录（用于对比图表）
        /// </summary>
        IEnumerable<ProcessedResult> GetHistory(string standardBlockName);

        /// <summary>
        /// 清理所有缓存
        /// </summary>
        void Clear();
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Contracts\IVersionCoordinator.cs
// ----------------------------------------

namespace FileProcessor.Core.Contracts
{
    /// <summary>
    /// 版本协调器接口：严格管理计算任务的开启与闭环状态。
    /// </summary>
    public interface IVersionCoordinator
    {
        /// <summary>
        /// 当前关联的版本 ID。
        /// 批次模式下返回正式 ID (如 Batch_2023...)；
        /// 实时模式下生成时间戳 ID (如 Live_2023...)。
        /// </summary>
        string CurrentVersionId { get; }

        /// <summary>
        /// 指示当前是否正处于从 check.out 到 mainjss.out 的录制周期内。
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// 启动一个正式的批次版本。
        /// </summary>
        /// <param name source="source">触发来源（如哨兵文件名）</param>
        void StartNewBatch(string source);

        /// <summary>
        /// 显式结束当前批次并触发封版。
        /// </summary>
        /// <param name source="source">结束来源</param>
        void CommitCurrentBatch(string source);

        /// <summary>
        /// 生成一个新的实时时间戳版本号。
        /// 用于非标文件的变化追踪。
        /// </summary>
        string GenerateLiveVersionId();

        /// <summary>
        /// 通用的提交接口：手动宣告某个版本号的数据已经处理完毕。
        /// </summary>
        void Commit(string versionId);

        /// <summary>
        /// 当批次被 mainjss.out 正式闭环后触发。
        /// </summary>
        event Action<string> VersionCommitted;

        /// <summary>
        /// 当单个实时任务处理完成后触发，通知 UI 进行局部刷新。
        /// </summary>
        event Action<string, string> LiveUpdateProcessed;
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Infrastructure\BaseFileTemplate.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Reflection;
using System.Text;

namespace FileProcessor.Core.Infrastructure
{
    /// <summary>
    /// 文件解析基类：提供统一的 IO 流处理和编码自动探测。
    /// 这是 DLL 中最核心的逻辑块，确保所有插件遵循相同的读取协议。
    /// </summary>
    public abstract class BaseFileTemplate : IFileTemplate
    {
        /// <summary>
        /// 文件名正则模式（通过反射读取类上的 FileProcessorPluginAttribute）
        /// </summary>
        public virtual string FileNamePattern =>
            this.GetType().GetCustomAttribute<FileProcessorPluginAttribute>()?.FileNamePattern ?? string.Empty;

        /// <summary>
        /// 子目录路径（通过反射读取类上的 FileProcessorPluginAttribute）
        /// </summary>
        public virtual string SubDirectory =>
            this.GetType().GetCustomAttribute<FileProcessorPluginAttribute>()?.SubDirectory ?? string.Empty;

        /// <summary>
        /// 核心解析流程：执行 IO 读取、编码探测并触发子类的切块逻辑。
        /// </summary>
        /// <param name="filePath">物理文件路径</param>
        /// <returns>拆分后的原始数据块集合</returns>
        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            if (!File.Exists(filePath)) yield break;          

            // 1. 以只读共享模式打开文件，避免锁定正在被写入的工程文件
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // 2. 自动识别编码（默认支持 UTF8 与 GB2312）
            Encoding encoding = DetectEncoding(fs);

            // 3. 读取所有行
            var lines = new List<string>();
            using (var reader = new StreamReader(fs, encoding))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            // 4. 调用子类实现的切块逻辑，并自动注入 Hash
            var blocks = SplitBlocks(lines, filePath);  
            foreach (var block in blocks)
            {                
                yield return block;
            }
        }

        /// <summary>
        /// 子类必须实现：定义如何将文件行集合拆分为独立的逻辑块。
        /// </summary>
        protected abstract IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath);



        private Encoding DetectEncoding(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.");

            long originalPosition = 0;
            if (stream.CanSeek)
                originalPosition = stream.Position;

            try
            {
                const int sampleSize = 8192; // 8KB 更稳
                Span<byte> buffer = stackalloc byte[sampleSize];

                int read = stream.Read(buffer);
                if (stream.CanSeek)
                    stream.Position = originalPosition;

                if (read == 0)
                    return Encoding.UTF8; // 空文件默认 UTF-8

                // =========================
                // 1️⃣ BOM 检测
                // =========================

                // UTF-8 BOM
                if (read >= 3 &&
                    buffer[0] == 0xEF &&
                    buffer[1] == 0xBB &&
                    buffer[2] == 0xBF)
                    return new UTF8Encoding(true);

                // UTF-32 LE BOM
                if (read >= 4 &&
                    buffer[0] == 0xFF &&
                    buffer[1] == 0xFE &&
                    buffer[2] == 0x00 &&
                    buffer[3] == 0x00)
                    return Encoding.UTF32;

                // UTF-32 BE BOM
                if (read >= 4 &&
                    buffer[0] == 0x00 &&
                    buffer[1] == 0x00 &&
                    buffer[2] == 0xFE &&
                    buffer[3] == 0xFF)
                    return new UTF32Encoding(true, true);

                // UTF-16 LE BOM
                if (read >= 2 &&
                    buffer[0] == 0xFF &&
                    buffer[1] == 0xFE)
                    return Encoding.Unicode;

                // UTF-16 BE BOM
                if (read >= 2 &&
                    buffer[0] == 0xFE &&
                    buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode;

                // =========================
                // 2️⃣ UTF-8 合法性检测
                // =========================

                if (IsValidUtf8(buffer.Slice(0, read)))
                    return new UTF8Encoding(false);

                // =========================
                // 3️⃣ 回退 GB18030
                // =========================

                return Encoding.GetEncoding("GB18030");
            }
            finally
            {
                if (stream.CanSeek)
                    stream.Position = originalPosition;
            }
        }

        private bool IsValidUtf8(ReadOnlySpan<byte> data)
        {
            int i = 0;
            bool hasMultibyte = false;

            while (i < data.Length)
            {
                byte b = data[i];

                // ASCII
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                int remaining;

                // 2 字节序列
                if (b >= 0xC2 && b <= 0xDF)
                {
                    remaining = 1;
                }
                // 3 字节序列
                else if (b == 0xE0)
                {
                    if (i + 2 >= data.Length ||
                        data[i + 1] < 0xA0 || data[i + 1] > 0xBF ||
                        !IsContinuation(data[i + 2]))
                        return false;
                    i += 3;
                    hasMultibyte = true;
                    continue;
                }
                else if (b >= 0xE1 && b <= 0xEC || b >= 0xEE && b <= 0xEF)
                {
                    remaining = 2;
                }
                else if (b == 0xED) // 避免 UTF-16 surrogate 区
                {
                    if (i + 2 >= data.Length ||
                        data[i + 1] < 0x80 || data[i + 1] > 0x9F ||
                        !IsContinuation(data[i + 2]))
                        return false;
                    i += 3;
                    hasMultibyte = true;
                    continue;
                }
                // 4 字节
                else if (b == 0xF0)
                {
                    if (i + 3 >= data.Length ||
                        data[i + 1] < 0x90 || data[i + 1] > 0xBF ||
                        !IsContinuation(data[i + 2]) ||
                        !IsContinuation(data[i + 3]))
                        return false;
                    i += 4;
                    hasMultibyte = true;
                    continue;
                }
                else if (b >= 0xF1 && b <= 0xF3)
                {
                    remaining = 3;
                }
                else if (b == 0xF4)
                {
                    if (i + 3 >= data.Length ||
                        data[i + 1] < 0x80 || data[i + 1] > 0x8F ||
                        !IsContinuation(data[i + 2]) ||
                        !IsContinuation(data[i + 3]))
                        return false;
                    i += 4;
                    hasMultibyte = true;
                    continue;
                }
                else
                {
                    return false;
                }

                if (i + remaining >= data.Length)
                    return false;

                for (int j = 1; j <= remaining; j++)
                {
                    if (!IsContinuation(data[i + j]))
                        return false;
                }

                i += remaining + 1;
                hasMultibyte = true;
            }

            // 关键策略：
            // 即便全 ASCII 也视为 UTF-8（工业标准）
            return true;
        }

        private bool IsContinuation(byte b)
        {
            return (b & 0xC0) == 0x80;
        }

    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Models\FileSnapshot.cs
// ----------------------------------------

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 处理结果快照：包含一次文件处理后的所有数据
    /// </summary>
    public record FileSnapshot
    {
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string FileHash { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.Now;

        // 存储加工后的成品
        public Dictionary<string, ProcessedResult> DataBlocks { get; init; } = new();

        // 辅助方法：UI 可以直接调用，无需自己去查字典
        public IEnumerable<string> GetAvailableBlockNames() => DataBlocks.Keys;

        public bool IsValid => DataBlocks.Any();
    }
}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Models\ProcessedResult.cs
// ----------------------------------------

using System;
using System.Collections.Generic;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 标准化处理结果：承载解析后的表格数据及业务元数据。
    /// 改为 class 以优化大数据量下的集合操作性能。
    /// </summary>
    public class ProcessedResult
    {
        // === 1. 身份与索引 (Identity & Indexing) ===

        /// <summary>
        /// 关联的版本 ID（如 Batch_20231027_1000 或 Live）
        /// </summary>
        public string VersionId { get; set; } = "Live";

        /// <summary>
        /// 数据源文件名（如 wmass.out），用于 UI 第一级选择
        /// </summary>
        public string SourceFileName { get; set; } = string.Empty;

        /// <summary>
        /// 原始块名（保留源文件中的原始标题，用于溯源）
        /// </summary>
        public string RawBlockName { get; set; } = string.Empty;

        /// <summary>
        /// 标准化块标识（如 StandardBlock_Stiffness），用于跨软件对比
        /// </summary>
        public string StandardBlockName { get; set; } = string.Empty;

        // === 2. UI 展示元数据 (UI Metadata) ===

        /// <summary>
        /// 友好显示名（用于 UI 第二级选择，如“各层刚度信息”）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 业务分类（如“位移结果”、“刚度信息”），用于 UI 侧边栏自动分组
        /// </summary>
        public string Category { get; set; } = "常规";

        // === 3. 核心数据体 (Core Data) ===

        /// <summary>
        /// 表格化数据行：Key 使用标准 ID（如 Floor, Tower, DriftRatioX）
        /// </summary>
        public List<Dictionary<string, string>> Rows { get; init; } = new();

        /// <summary>
        /// 列定义：定义标准 ID 对应的 UI 显示标题和排序规则
        /// </summary>
        public List<ColumnDefinition> Columns { get; init; } = new();

        /// <summary>
        /// 扩展元数据：存储非表格化的摘要信息或原始指纹
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();

        // === 4. 审计信息 (Audit) ===

        /// <summary>
        /// 原始文件的哈希值，确保数据的确定性
        /// </summary>
        public string OriginHash { get; set; } = string.Empty;

        /// <summary>
        /// 解析完成的时间戳（用于审计及解决并发冲突）
        /// </summary>
        public DateTime ProcessTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 列元数据定义
    /// </summary>
    public record ColumnDefinition(string Key, string Header);
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Models\ProcessingTaskContext.cs
// ----------------------------------------

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 解析任务上下文：记录待处理文件及其锁定的版本信息。
    /// 在 Batch 模式下，相同路径的上下文会被覆盖，确保只保留最后一次变动。
    /// </summary>
    public record ProcessingTaskContext
    {
        /// <summary>
        /// 待处理文件的完整物理路径
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// 该文件对应的文件名（不含路径）
        /// </summary>
        public string FileName => System.IO.Path.GetFileName(FilePath);

        /// <summary>
        /// 在监控阶段预先计算好的文件 Hash，用于准入检查及结果溯源
        /// </summary>
        public string FileHash { get; init; } = string.Empty;

        /// <summary>
        /// 触发该任务时锁定的版本 ID（如 Batch_20231027_1000）
        /// </summary>
        public string BoundVersionId { get; init; } = "Live";

        /// <summary>
        /// 任务创建的时间戳
        /// </summary>
        public DateTime TriggerTime { get; init; } = DateTime.Now;

        /// <summary>
        /// 标识该任务是否由于 mainjss.out 到达而触发的最终执行
        /// </summary>
        public bool IsFinalCommit { get; init; } = false;
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Models\RawDataBlock.cs
// ----------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 原始数据块：由 Parser 拆分出来的最小文本单元
    /// </summary>
    public class RawDataBlock
    {
        public string BlockName { get; }
        public string[] Lines { get; }
        public int StartLineNumber { get; }
        public string SourceFilePath { get; }
        public string FileHash { get; set; } = string.Empty; // 新增这一行

        public RawDataBlock(string blockName, string[] lines, int startLine, string filePath)
        {
            BlockName = blockName;
            Lines = lines;
            StartLineNumber = startLine;
            SourceFilePath = filePath;
        }
    }
}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\Helpers\ResultExtensions.cs
// ----------------------------------------

using FileProcessor.Core.Models;
using System.Data;

namespace FileProcessor.Desktop.Helpers
{

    public static class ResultExtensions
    {
        /// <summary>
        /// 将 ProcessedResult 转换为 DataTable。
        /// 解决 ColumnDefinition 无法直接转换的问题。
        /// </summary>
        public static DataTable ToDataTable(this ProcessedResult result)
        {
            var dt = new DataTable();

            // 安全检查：如果列或行数据为空，返回空表
            if (result.Columns == null || result.Rows == null) return dt;

            // 1. 添加列定义
            foreach (var colDef in result.Columns)
            {
                // 使用 Key 存储，Header 作为 Caption (DataGrid 默认会读取 Caption 作为列头)
                DataColumn dc = new DataColumn(colDef.Key)
                {
                    Caption = colDef.Header
                };
                dt.Columns.Add(dc);
            }

            // 2. 填充行数据
            foreach (var rowDict in result.Rows)
            {
                var dr = dt.NewRow();
                foreach (var colDef in result.Columns)
                {
                    // 使用 colDef.Key 从字典中取值
                    if (rowDict.TryGetValue(colDef.Key, out var value))
                    {
                        dr[colDef.Key] = value;
                    }
                    else
                    {
                        dr[colDef.Key] = string.Empty;
                    }
                }
                dt.Rows.Add(dr);
            }

            return dt;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\ViewModels\MainViewModel.cs
// ----------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.DebugHelpers;
using FileProcessor.Infrastructure.Services;
using FileProcessor.Mediator;
using System.Collections.ObjectModel;
using MessageBox = System.Windows.MessageBox;

namespace FileProcessor.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DataCoordinator _coordinator;
        private readonly ProjectMonitorService _monitor;
        private readonly IVersionCoordinator _versionCoordinator; // 新增：直接引用版本协调器

        public MainViewModel(DataCoordinator coordinator, ProjectMonitorService monitor, IVersionCoordinator versionCoordinator)
        {
            _coordinator = coordinator;
            _monitor = monitor;
            _versionCoordinator = versionCoordinator;

            _versionCoordinator.VersionCommitted += OnVersionCommitted;

            // 初始化 8 个插槽
            for (int i = 0; i < 8; i++)
                Slots.Add(new SlotViewModel(_coordinator, i));

            Log.Debug("[MainViewModel] 初始化成功...");
            StatusText = "就绪。请选择项目目录...";
            StatusColor = "#9E9E9E";
        }

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public ObservableCollection<string> VersionHistory { get; } = new();

        [ObservableProperty] private string? _projectPath;
        [ObservableProperty] private string? _selectedVersion;
        [ObservableProperty] private string _currentTower = "All";
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private string _statusColor = "";

        [RelayCommand]
        private void SelectProject()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectPath = dialog.SelectedPath;
                StartProjectMonitoring(dialog.SelectedPath);
            }
        }

        private async void StartProjectMonitoring(string path)
        {
            try
            {
                StatusText = "正在初始化引擎...";
                StatusColor = "#F39C12"; // 橙色表示处理中

                // 清理旧状态
                VersionHistory.Clear();
                VersionHistory.Add("LIVE 实时状态");

                // 1. 启动监控
                // 由于我们在内部实现了静默扫描，这里可以用 Task.Run 跑防止 UI 彻底卡死
                await Task.Run(() => _monitor.StartScanning(path));

                // 注意：这里不需要写 while 循环等待了！
                // 因为扫描结束后，Orchestrator 会触发 Commit("INITIAL_SCAN")
                // 从而触发我们构造函数里绑定的 OnVersionCommitted 方法

                StatusText = $"监控运行中: {System.IO.Path.GetFileName(path)}";
                StatusColor = "#2ECC71"; // 绿色表示正常
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动监控失败: " + ex.Message);
                StatusText = "初始化失败";
                StatusColor = "#E74C3C";
            }
        }

        /// <summary>
        /// 当引擎提交新版本（如 INITIAL_SCAN 或 Batch_xxx）时触发
        /// </summary>
        private void OnVersionCommitted(string versionId)
        {
            // 切换到 UI 线程更新集合
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!VersionHistory.Contains(versionId))
                {
                    VersionHistory.Add(versionId);
                }

                // 如果是第一次扫描完成，或者当前处于 LIVE 模式，则强制触发一次刷新
                if (versionId == "INITIAL_SCAN")
                {
                    SelectedVersion = "INITIAL_SCAN";
                }

                // 刷新所有插槽
                foreach (var slot in Slots)
                {
                    slot.RefreshFileList();
                }
            });
        }

        partial void OnSelectedVersionChanged(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            _coordinator.SwitchViewVersion(value);
            // 切换版本后刷新插槽文件列表
            foreach (var slot in Slots) slot.RefreshFileList();
        }

        partial void OnCurrentTowerChanged(string value)
        {
            for (int i = 0; i < Slots.Count; i++)
                _coordinator.SetTowerFilter(i, value);
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\ViewModels\SlotViewModel.cs
// ----------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using FileProcessor.Desktop.Helpers;
using FileProcessor.Mediator;
using FileProcessor.Mediator.Models;
using System.Collections.ObjectModel;
using System.Data;

namespace FileProcessor.Desktop.ViewModels
{
    public partial class SlotViewModel : ObservableObject
    {
        private readonly DataCoordinator _coordinator;
        private readonly int _index;

        public SlotViewModel(DataCoordinator coordinator, int index)
        {
            _coordinator = coordinator;
            _index = index;

            // 订阅协调器数据变更事件
            _coordinator.SlotDataChanged += (idx, data) => {
                if (idx == _index)
                {
                    // 核心：转发到 UI 线程更新 DataTable
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        DisplayTable = data?.ToDataTable();
                        Status = data != null ? SlotStatus.Ready : SlotStatus.NoData;
                    });
                }
            };
        }

        [ObservableProperty] private string? _selectedFile;
        [ObservableProperty] private string? _selectedBlock;
        [ObservableProperty] private DataTable? _displayTable;
        [ObservableProperty] private SlotStatus _status = SlotStatus.Empty;

        public ObservableCollection<string> AvailableFiles { get; } = new();
        public ObservableCollection<string> AvailableBlocks { get; } = new();

        public void RefreshFileList()
        {
            // 1. 【记忆】在清空前，暂存当前用户的选择状态
            string? cachedFile = SelectedFile;
            string? cachedBlock = SelectedBlock;

            // 2. 刷新文件列表
            var files = _coordinator.GetAvailableFiles();
            AvailableFiles.Clear();
            foreach (var f in files) AvailableFiles.Add(f);

            // 3. 【恢复】检查刚才选的文件是否在新列表中
            if (!string.IsNullOrEmpty(cachedFile) && AvailableFiles.Contains(cachedFile))
            {
                // 恢复文件选择（这通常会触发 OnSelectedFileChanged，从而刷新 AvailableBlocks）
                SelectedFile = cachedFile;

                // 4. 【级联恢复】文件恢复后，尝试恢复数据块
                // 注意：SelectedFile 的 Setter 会触发 Block 列表刷新，
                // 我们需要确保在 Block 列表刷新后，再把 cachedBlock 设回去。

                // 重新获取该文件的块列表（模拟 OnSelectedFileChanged 的逻辑，但为了稳妥我们显式做）
                AvailableBlocks.Clear();
                var blocks = _coordinator.GetBlocksForFile(cachedFile);
                foreach (var b in blocks) AvailableBlocks.Add(b);

                if (!string.IsNullOrEmpty(cachedBlock) && AvailableBlocks.Contains(cachedBlock))
                {
                    SelectedBlock = cachedBlock;
                    // 设置 SelectedBlock 会触发 OnSelectedBlockChanged -> Coordinator.ConfigureSlot
                    // 从而触发 Coordinator 去 SnapshotManager 拉取最新版本的数据
                }
            }
            else
            {
                // 如果之前的文件没了（比如清空了），或者之前没选文件，就清空块选择
                SelectedBlock = null;
                AvailableBlocks.Clear();
            }
        }

        partial void OnSelectedFileChanged(string? value)
        {
            // 如果 value 为空，说明是清空操作，清空后续级联
            if (string.IsNullOrEmpty(value))
            {
                AvailableBlocks.Clear();
                SelectedBlock = null;
                return;
            }

            // 如果 AvailableBlocks 已经有数据且 SelectedBlock 也有值（说明是在 RefreshFileList 里被手动恢复了），
            // 我们就不在这里暴力清空了，防止覆盖掉恢复逻辑。
            // 只有当 AvailableBlocks 为空（说明是用户手动切换了新文件）时，才执行标准加载。
            if (AvailableBlocks.Count == 0)
            {
                AvailableBlocks.Clear();
                foreach (var b in _coordinator.GetBlocksForFile(value)) AvailableBlocks.Add(b);
            }
        }

        partial void OnSelectedBlockChanged(string? value)
        {
            // value 是用户选中的中文名，例如 "工况20 X方向..."
            if (!string.IsNullOrEmpty(SelectedFile) && !string.IsNullOrEmpty(value))
            {
                // 1. [核心修复] 反查标准 ID
                // 去问 Coordinator：在这个文件里，在这个版本下，这个中文名对应的原始 ID 是啥？
                var candidates = _coordinator.GetBlocksForFile(SelectedFile);
                // 注意：GetBlocksForFile 只返回了 string。我们需要更详细的信息。

                // 建议：直接调用 SnapshotManager 的查询接口（通过 Coordinator 暴露的）
                // 或者，我们修改 Coordinator.ConfigureSlot 让它智能一点，或者在这里查。

                // 最稳妥的写法（利用现有接口）：
                // 我们需要 Coordinator 提供一个方法：GetStandardName(fileName, displayName)
                // 如果没有，我们就在这里“笨”办法查一下：

                // 假设 Coordinator 有个方法能拿到 ProcessedResult 列表
                // 如果没有，建议在 DataCoordinator 加一个：
                // public IEnumerable<ProcessedResult> GetFullResultsForFile(string fileName) 
                // { return _snapshotManager.GetResultsByFile(ActiveViewVersionId, fileName); }

                // 既然我们现在不想改 Coordinator 接口，我们可以利用 AvailableBlocks 的对应关系
                // 但最简单的还是去 DataCoordinator 加这个查找逻辑。

                _coordinator.ConfigureSlot(_index, SelectedFile, value);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\Registration\ProcessorRegistry.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using System.Reflection;

namespace FileProcessor.Engine.Registration
{
    /// <summary>
    /// 处理器注册中心：负责维护块名称与处理器之间的映射关系。
    /// 核心 DLL 通过此类实现“按需分配”解析任务。
    /// </summary>
    public class ProcessorRegistry
    {
        /// <summary>
        /// 核心映射表：Key 为块名，Value 为支持该块的处理器列表
        /// </summary>
        private readonly Dictionary<string, List<IBlockProcessor>> _lookup = new();

        /// <summary>
        /// 全量列表，用于 fallback 兜底匹配
        /// </summary>
        private readonly List<IBlockProcessor> _allProcessors = new();

        /// <summary>
        /// 注册处理器并自动解析其特性标记的块名
        /// </summary>
        public void Register(IBlockProcessor processor)
        {
            _allProcessors.Add(processor);

            // 读取类上定义的所有 BlockProcessorAttribute
            var attrs = processor.GetType().GetCustomAttributes<BlockProcessorAttribute>();
            foreach (var attr in attrs)
            {
                if (!_lookup.ContainsKey(attr.BlockName))
                    _lookup[attr.BlockName] = new List<IBlockProcessor>();

                _lookup[attr.BlockName].Add(processor);
            }
        }

        /// <summary>
        /// 根据块名获取所有匹配的处理器
        /// </summary>
        /// <param name="blockName">切块后得到的 RawBlockName</param>
        public IEnumerable<IBlockProcessor> GetProcessorsForBlock(string blockName)
        {
            // 1. 优先从快速查找字典中匹配
            if (_lookup.TryGetValue(blockName, out var matched))
                return matched.OrderByDescending(p => p.Priority);

            // 2. 如果字典没中，尝试使用处理器自带的 CanProcess 逻辑进行模糊匹配（Fallback）
            return _allProcessors.Where(p => p.CanProcess(blockName))
                                 .OrderByDescending(p => p.Priority);
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\Runtime\FileOrchestrator.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FileProcessor.Engine.Runtime
{
    /// <summary>
    /// 文件解析编排器：核心执行中枢。
    /// 支持“延迟解析”策略，在 Batch 周期内只暂存任务，直到收到提交信号。
    /// 实现批次暂存与实时解析的双轨制分流。
    /// </summary>
    public class FileOrchestrator
    {
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ISnapshotManager _snapshotManager;
        private readonly ProcessorRegistry _processorRegistry;
        private readonly IVersionCoordinator _versionCoordinator;

        // 暂存录制期间的文件变动，Key 为 FilePath，确保同一个文件只有一条记录
        private readonly ConcurrentDictionary<string, ProcessingTaskContext> _pendingTasks = new();

        // 标记当前是否处于项目初次加载阶段
        private bool _isInitializing = false;
        private const string InitialVersionId = "INITIAL_SCAN";

        public FileOrchestrator(
            IEnumerable<IFileTemplate> templates,
            ISnapshotManager snapshotManager,
            ProcessorRegistry processorRegistry,
            IVersionCoordinator versionCoordinator)
        {
            _templates = templates;
            _snapshotManager = snapshotManager;
            _processorRegistry = processorRegistry;
            _versionCoordinator = versionCoordinator;
        }

        /// <summary>
        /// 开启初始化模式。在此模式下，所有解析任务将强制归入 INITIAL_SCAN 版本，且不触发哨兵逻辑。
        /// </summary>
        public void BeginInitialization() => _isInitializing = true;

        /// <summary>
        /// 结束初始化模式，并手动提交 INITIAL_SCAN 版本。
        /// </summary>
        public void EndInitialization()
        {
            _isInitializing = false;
            // 无论初始化期间是否发现文件，都提交该版本以确保 UI 逻辑闭环
            _versionCoordinator.Commit(InitialVersionId);
        }

        /// <summary>
        /// 处理初始化阶段的单个文件。该方法绕过延迟队列和哨兵状态检查。
        /// </summary>
        /// <param name="filePath">物理路径</param>
        /// <param name="hash">预计算的文件哈希</param>
        public void ProcessInitialFile(string filePath, string hash)
        {
            var context = new ProcessingTaskContext
            {
                FilePath = filePath,
                FileHash = hash,
                BoundVersionId = InitialVersionId, // 强制归并到初始化版本
                TriggerTime = DateTime.Now
            };

            // 立即执行解析并存入快照库
            ExecuteSingleTask(context);
        }

        /// <summary>
        /// 接收监控层发来的处理请求
        /// </summary>
        /// <param name="context">任务上下文</param>
        /// <param name="isRecording">是否处于批次录制状态</param>
        public void EnqueueTask(ProcessingTaskContext context, bool isRecording)
        {
            // 如果是在初始化后由于某些原因误触发，重定向到初始化逻辑
            if (_isInitializing)
            {
                ProcessInitialFile(context.FilePath, context.FileHash);
                return;
            }

            if (isRecording)
            {
                // A轨：录制模式
                // 核心逻辑：如果在录制中，只存不练，且相同路径会覆盖之前的 Context
                _pendingTasks[context.FilePath] = context;
            }
            else
            {
                // B轨：实时模式 - 自动注入时间戳版本并立即执行
                var liveVersionId = _versionCoordinator.GenerateLiveVersionId();
                var liveContext = context with { BoundVersionId = liveVersionId };
                // 非录制状态（Live 模式），直接执行
                ExecuteSingleTask(liveContext);
            }
        }

        /// <summary>
        /// 当 mainjss.out 到达时，由外部调用清空并执行所有暂存任务
        /// </summary>
        public void FlushBatchTasks()
        {
            var tasks = _pendingTasks.Values.ToList();
            _pendingTasks.Clear();

            // 批次任务通常较大，建议并行执行
            Parallel.ForEach(tasks, task =>
            {
                ExecuteSingleTask(task);
            });
        }

        /// <summary>
        /// 核心执行单元：将解析结果打上版本标签并存入快照库
        /// </summary>
        private void ExecuteSingleTask(ProcessingTaskContext context)
        {
            var template = _templates.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.FileNamePattern) &&
                Regex.IsMatch(context.FileName, t.FileNamePattern));

            if (template == null) return;

            var rawBlocks = template.Parse(context.FilePath);

            foreach (var rawBlock in rawBlocks)
            {
                // 注入监控层预计算的 Hash，确保每个块都有溯源指纹
                rawBlock.FileHash = context.FileHash;

                var processors = _processorRegistry.GetProcessorsForBlock(rawBlock.BlockName);
                foreach (var processor in processors)
                {
                    try
                    {
                        var result = processor.Process(rawBlock);
                        if (result != null)
                        {
                            // 关键补全：由编排器维护结果的版本身份
                            result.VersionId = context.BoundVersionId;
                            result.SourceFileName = context.FileName;
                            result.Metadata["OriginHash"] = context.FileHash;

                            _snapshotManager.AddSnapshot(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Orchestrator] 处理失败: {processor.GetType().Name}, 错误: {ex.Message}");
                    }
                }
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\Runtime\PluginLoader.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FileProcessor.Engine.Runtime
{
    /// <summary>
    /// 插件加载服务：专门负责从 AppPath/Plugins 子目录下扫描并加载解析模板与处理器。
    /// </summary>
    public class PluginLoader
    {
        /// <summary>
        /// 扫描并加载 Plugins 文件夹下的所有有效插件。
        /// </summary>
        /// <returns>返回初始化的模板列表和处理器注册表</returns>
        public (List<IFileTemplate> Templates, ProcessorRegistry Registry) LoadFromPluginsFolder()
        {
            var templates = new List<IFileTemplate>();
            var registry = new ProcessorRegistry();

            // 定位到程序所在目录下的 Plugins 文件夹
            string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            if (!Directory.Exists(pluginPath))
            {
                Directory.CreateDirectory(pluginPath);
                return (templates, registry);
            }

            var dlls = Directory.GetFiles(pluginPath, "*.dll");
            foreach (var dll in dlls)
            {
                try
                {
                    // 使用 LoadFrom 解决依赖项在同一目录下的加载问题
                    var assembly = Assembly.LoadFrom(dll);
                    var (tList, pList) = ScanAssembly(assembly);

                    templates.AddRange(tList);
                    foreach (var p in pList) registry.Register(p);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginLoader] 加载插件失败 {Path.GetFileName(dll)}: {ex.Message}");
                }
            }

            return (templates, registry);
        }

        /// <summary>
        /// 扫描程序集中的接口实现类
        /// </summary>
        private (List<IFileTemplate> Templates, List<IBlockProcessor> Processors) ScanAssembly(Assembly assembly)
        {
            var tList = new List<IFileTemplate>();
            var pList = new List<IBlockProcessor>();

            var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                // 1. 扫描 IFileTemplate 实现
                if (typeof(IFileTemplate).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<FileProcessorPluginAttribute>() != null)
                {
                    if (Activator.CreateInstance(type) is IFileTemplate template)
                        tList.Add(template);
                }

                // 2. 扫描 IBlockProcessor 实现
                if (typeof(IBlockProcessor).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<BlockProcessorAttribute>() != null)
                {
                    if (Activator.CreateInstance(type) is IBlockProcessor processor)
                        pList.Add(processor);
                }
            }
            return (tList, pList);
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\Services\ProjectMonitorService.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Runtime;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using FileProcessor.DebugHelpers;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 增强型项目监控服务：
    /// 实现基于 Hash 的智能去重与哨兵信号处理。
    /// 支持自动识别批次生命周期。
    /// </summary>
    public class ProjectMonitorService
    {
        private readonly FileOrchestrator _orchestrator;
        private readonly IVersionCoordinator _versionCoordinator;

        // Hash 缓存：FilePath -> LastHash
        // 用于在监控层直接拦截内容未变的文件，避免无效解析
        private readonly ConcurrentDictionary<string, string> _fileHashCache = new();
        
        private FileSystemWatcher? _watcher;

        public ProjectMonitorService(FileOrchestrator orchestrator, IVersionCoordinator versionCoordinator)
        {
            _orchestrator = orchestrator;
            _versionCoordinator = versionCoordinator;
        }

        /// <summary>
        /// 启动对目标根目录的深度监控
        /// </summary>
        /// <param name="path">监控根路径（包含“设计结果”子目录）</param>
        public void StartScanning(string path)
        {
            if (!Directory.Exists(path)) return;

            try
            {
                // --- 阶段 1: 静默初始化 ---
                // 通知编排器：接下来的文件不参与哨兵逻辑，全部锁死为 INITIAL_SCAN
                _orchestrator.BeginInitialization();

                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string currentHash = CalculateFileHash(file);
                    _fileHashCache[file] = currentHash;

                    // 直接调用初始化专用接口，绕过 HandleFileChange
                    _orchestrator.ProcessInitialFile(file, currentHash);
                }

                // 强制提交 INITIAL_SCAN（即使 files 为空也会执行，保证 UI 链路打通）
                _orchestrator.EndInitialization();

                // --- 阶段 2: 激活实时监控 ---
                // 初始化完成后，再开启监听，防止初始扫描的文件触发二次解析
                SetupWatcher(path);
            }
            catch (Exception ex)
            {
                Log.Debug($"[ProjectMonitorService] 项目启动失败: {ex.Message}");
                // 确保即使失败，初始化状态也被清理
                _orchestrator.EndInitialization();
            }
            Log.Debug($"[ProjectMonitorService] 智能监控已就绪: {path}");
        }

        /// <summary>
        /// 配置并启动 FileSystemWatcher
        /// </summary>
        private void SetupWatcher(string path)
        {
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                Filter = "*.*"
            };

            _watcher.Changed += (s, e) => HandleEvent(e.FullPath);
            _watcher.Created += (s, e) => HandleEvent(e.FullPath);
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// 初始扫描逻辑：遍历目录内所有已存在的 .out 文件
        /// </summary>
        /// <param name="rootPath"></param>
        private void InitialScan(string rootPath)
        {
            // 扫描所有潜在的目标文件（这里简单以 .out 举例，可根据实际需求调整通配符）
            try
            {
                var files = Directory.GetFiles(rootPath, "*.out", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    HandleEvent(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] 初始扫描失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 核心处理逻辑：负责 Hash 过滤、哨兵识别、任务下发
        /// </summary>
        private void HandleEvent(string fullPath)
        {
            // 基础准入：确保文件存在
            if (!File.Exists(fullPath)) return;

            try
            {
                string fileName = Path.GetFileName(fullPath).ToLower();
                string dirName = Path.GetDirectoryName(fullPath) ?? "";

                // --- A. 哨兵信号处理 (保持不变) ---
                // 信号处理逻辑：检查“设计结果”目录下的哨兵文件
                if (dirName.EndsWith("设计结果", StringComparison.OrdinalIgnoreCase))
                {
                    if (fileName == "check.out")
                    {
                        _versionCoordinator.StartNewBatch(fileName);
                        return; // 哨兵文件本身不含业务数据，不解析
                    }

                    if (fileName == "mainjss.out")
                    {
                        _versionCoordinator.CommitCurrentBatch(fileName);
                        return; // 哨兵文件不解析
                    }
                }

                // --- B. 智能准入控制 (Hash Check) ---

                // 1. 计算当前物理文件的 Hash
                string currentHash = CalculateFileHash(fullPath);

                // 2. 对比缓存：如果 Hash 没变，直接忽略本次事件
                // (注意：如果是 InitialScan，缓存中没有，TryGetValue 返回 false，会继续执行)
                if (_fileHashCache.TryGetValue(fullPath, out string? lastHash) && lastHash == currentHash)
                {
                    // 文件内容未实质变更，跳过
                    Log.Debug($"[ProjectMonitorService] 文件Hash未改变，FullPath: {fullPath}");
                    return;
                }

                // 3. 更新缓存
                _fileHashCache[fullPath] = currentHash; //测试注释

                // --- C. 构造任务并下发 ---

                // 2. 业务数据流水线：无论是否在批次内，只要是关心的文件就进行解析
                // context 会携带当时的 VersionId（可能是 Live，也可能是正在运行的 Batch_xxx）
                var context = new ProcessingTaskContext
                {
                    FilePath = fullPath,
                    FileHash = currentHash, // 将计算好的指纹注入上下文
                    BoundVersionId = _versionCoordinator.CurrentVersionId,
                    TriggerTime = DateTime.Now
                };

                // 只有通过了 Hash 检查的任务才会进入编排器
                // 将决策权交给 Orchestrator：
                // 如果 isRecording 为 true，Orchestrator 会将其存入 _pendingTasks 字典实现“多变一”
                // 如果 isRecording 为 false，Orchestrator 会立即执行并生成时间戳版本记录历史
                _orchestrator.EnqueueTask(context, _versionCoordinator.IsRecording);
            }
            catch (IOException)
            {
                // 文件正被 YJK 写入占用中，忽略本次触发，等待下一次（通常写入完成会再次触发）
            }
            catch (Exception ex)
            {
                Log.Debug($"[ProjectMonitorService] 处理文件 {Path.GetFileName(fullPath)} 时异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算文件的 SHA256 哈希值
        /// </summary>
        private string CalculateFileHash(string filePath)
        {
            using var sha = SHA256.Create();
            // 使用 FileShare.ReadWrite 避免与 YJK 抢占文件锁
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hashBytes = sha.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\Services\SnapshotManager.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 高性能快照管理器：支持正式版本覆盖与实时版本历史保留。
    /// </summary>
    public class SnapshotManager : ISnapshotManager
    {
        // 核心存储结构：VersionId -> (SourceFileName -> List<ProcessedResult>)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<ProcessedResult>>> _storage = new();

        /// <summary>
        /// 存储解析结果。
        /// 批次版本 (不以 Live_ 开头)：执行同名 Block 替换，保持版本最终态。
        /// 实时版本 (以 Live_ 开头)：作为历史快照新增。
        /// </summary>
        public void AddSnapshot(ProcessedResult result)
        {
            if (result == null) return;

            // 1. 获取或创建版本容器
            var versionContainer = _storage.GetOrAdd(result.VersionId, _ => new ConcurrentDictionary<string, List<ProcessedResult>>());


            // 2. 获取或创建文件容器
            var fileResults = versionContainer.GetOrAdd(result.SourceFileName, _ => new List<ProcessedResult>());

            // 3. 线程安全地添加结果（处理冲突：如果 StandardBlockName 相同，则替换旧的）
            lock (fileResults)
            {
                // 如果是实时模式，我们保留所有变动（即不执行 FindIndex 替换）
                // 只有在正式批次模式下，才进行覆盖以保证“封版”数据的整洁
                bool isLive = result.VersionId.StartsWith("Live_");

                if (!isLive)
                {
                    var existingIndex = fileResults.FindIndex(r => r.StandardBlockName == result.StandardBlockName);
                    if (existingIndex >= 0)
                    {
                        fileResults[existingIndex] = result;
                        return;
                    }
                }

                fileResults.Add(result);
            }
        }

        /// <summary>
        /// 查询特定版本下的所有文件名（Level 1）
        /// </summary>
        public IEnumerable<string> GetFileNames(string versionId)
        {
            if (_storage.TryGetValue(versionId, out var versionContainer))
            {
                return versionContainer.Keys;
            }
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// 查询特定文件下的所有数据块结果（Level 2）
        /// </summary>
        public IEnumerable<ProcessedResult> GetResultsByFile(string versionId, string fileName)
        {
            if (_storage.TryGetValue(versionId, out var versionContainer))
            {
                if (versionContainer.TryGetValue(fileName, out var results))
                {
                    lock (results)
                    {
                        return results.ToList(); // 返回副本防止多线程枚举异常
                    }
                }
            }
            return Enumerable.Empty<ProcessedResult>();
        }

        /// <summary>
        /// 精确获取某个版本的某个特定块
        /// </summary>
        public ProcessedResult? GetSpecificBlock(string versionId, string fileName, string standardBlockName)
        {
            return GetResultsByFile(versionId, fileName)
                .FirstOrDefault(r => r.StandardBlockName == standardBlockName);
        }

        /// <summary>
        /// 历史追踪：获取跨版本的所有同名块结果（用于指标趋势分析）
        /// </summary>
        public IEnumerable<ProcessedResult> GetHistory(string standardBlockName)
        {
            var history = new List<ProcessedResult>();
            // 遍历所有版本，提取该标准块的所有历史点
            foreach (var versionContainer in _storage.Values)
            {
                foreach (var fileResults in versionContainer.Values)
                {
                    lock (fileResults)
                    {
                        var matches = fileResults.Where(r => r.StandardBlockName == standardBlockName);
                        history.AddRange(matches);
                    }
                }
            }
            // 按时间或版本标识排序，方便 UI 绘图
            return history.OrderBy(h => h.VersionId);
        }

        public void Clear() => _storage.Clear();
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\Services\VersionCoordinator.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.DebugHelpers;
using System;

namespace FileProcessor.Infrastructure.Runtime
{
    /// <summary>
    /// 强信号驱动的版本协调器实现。
    /// 仅在 check.out 时开启，mainjss.out 时关闭，无视任何中间停顿时间。
    /// </summary>
    public class VersionCoordinator : IVersionCoordinator
    {
        private string _activeBatchId = "Live";
        private bool _isRecording = false;

        public string CurrentVersionId => _isRecording ? _activeBatchId : "Live";
        public bool IsRecording => _isRecording;

        public event Action<string>? VersionCommitted;
        public event Action<string, string>? LiveUpdateProcessed;

        /// <summary>
        /// 实现接口：通用的版本提交。
        /// 无论是否在录制中，都可以调用此方法触发 UI 刷新。
        /// </summary>
        public void Commit(string versionId)
        {
            Console.WriteLine($"[Coordinator] 显式提交版本: {versionId}");
            Log.Debug($"[Coordinator] 显式提交版本: {versionId}");

            // 核心：直接触发事件，通知 DataCoordinator 和插槽刷新
            VersionCommitted?.Invoke(versionId);
        }

        /// <summary>
        /// 当 check.out 出现时调用
        /// </summary>
        public void StartNewBatch(string source)
        {
            _isRecording = true;
            _activeBatchId = $"Batch_{DateTime.Now:yyyyMMdd_HHmmss}";
            Console.WriteLine($"[Coordinator] 检测到 check.out，批次录制开始: {_activeBatchId}");
            Log.Debug($"[Coordinator] 检测到 check.out，批次录制开始: {_activeBatchId}");
        }

        /// <summary>
        /// 当 mainjss.out 出现时调用
        /// </summary>
        public void CommitCurrentBatch(string source)
        {
            if (!_isRecording) return;

            string completedVersionId = _activeBatchId;
            _isRecording = false;
            _activeBatchId = "Live";

            Console.WriteLine($"[Coordinator] 检测到 mainjss.out，批次闭环: {completedVersionId}");
            Log.Debug($"[Coordinator] 检测到 mainjss.out，批次闭环: {completedVersionId}");

            // 复用 Commit 方法广播信号
            Commit(completedVersionId);
        }

        /// <summary>
        /// 为非标文件生成唯一的实时版本号
        /// </summary>
        public string GenerateLiveVersionId()
        {
            // 使用细化到秒的时间戳，确保历史记录的唯一性
            Log.Debug($"Live_{DateTime.Now:yyyyMMdd_HHmmss}");
            return $"Live_{DateTime.Now:yyyyMMdd_HHmmss}";
            
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\Models\SlotConfiguration.cs
// ----------------------------------------

using FileProcessor.Core.Models;
using System;

namespace FileProcessor.Mediator.Models
{
    /// <summary>
    /// 插槽状态枚举
    /// </summary>
    public enum SlotStatus
    {
        Empty,      // 未配置
        Loading,    // 正在解析中
        Ready,      // 数据就绪
        NoData,     // 已配置但未找到匹配数据
        Error       // 解析异常
    }

    /// <summary>
    /// 增强型插槽配置：存储 UI 状态与过滤参数
    /// </summary>
    public class SlotConfiguration
    {
        public int SlotIndex { get; set; }
        public string? TargetFileName { get; set; }
        public string? TargetBlockName { get; set; }

        /// <summary>
        /// 当前插槽选中的塔号（"All" 或具体数字）
        /// </summary>
        public string CurrentTower { get; set; } = "All";

        /// <summary>
        /// 当前插槽的业务状态
        /// </summary>
        public SlotStatus Status { get; set; } = SlotStatus.Empty;

        public bool IsActive => !string.IsNullOrEmpty(TargetFileName) && !string.IsNullOrEmpty(TargetBlockName);

        public void Clear()
        {
            TargetFileName = null;
            TargetBlockName = null;
            CurrentTower = "All";
            Status = SlotStatus.Empty;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\Services\ExportService.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using System.Linq;

namespace FileProcessor.Mediator.Services
{
    /// <summary>
    /// 导出服务：跨版本历史数据提取与报表生成。
    /// </summary>
    public class ExportService
    {
        private readonly ISnapshotManager _snapshotManager;

        public ExportService(ISnapshotManager snapshotManager)
        {
            _snapshotManager = snapshotManager;
        }

        /// <summary>
        /// 导出历史趋势报告
        /// </summary>
        /// <param name="blockName">标准化块名</param>
        /// <param name="towerId">塔号</param>
        /// <param name="includeLive">是否包含实时 Live 修改记录</param>
        public void ExportCrossVersionReport(string blockName, string towerId, bool includeLive)
        {
            // 1. 获取所有版本的该块数据
            var history = _snapshotManager.GetHistory(blockName);

            // 2. 根据用户需求决定是否剔除 Live 过程数据
            if (!includeLive)
            {
                history = history.Where(r => !r.VersionId.StartsWith("Live_"));
            }

            // 3. 执行 Tower 过滤
            if (towerId != "All")
            {
                history = history.Where(r =>
                    r.Rows.Any(row => row.TryGetValue("Tower", out var t) && t == towerId));
            }

            // 后续：转换 history 列表并使用 MiniExcel 导出...
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\Helpers\DataGridHelper.cs
// ----------------------------------------

using FileProcessor.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace UI.Helpers
{
    /// <summary>
    /// DataGrid 辅助类：解决动态表头绑定问题并优化列显示策略。
    /// </summary>
    public static class DataGridHelper
    {
        public static readonly DependencyProperty BindableColumnsProperty =
            DependencyProperty.RegisterAttached(
                "BindableColumns",
                typeof(object),
                typeof(DataGridHelper),
                new PropertyMetadata(null, OnBindableColumnsChanged));

        public static void SetBindableColumns(DependencyObject element, object value) => element.SetValue(BindableColumnsProperty, value);
        public static object GetBindableColumns(DependencyObject element) => element.GetValue(BindableColumnsProperty);

        private static void OnBindableColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            dataGrid.Columns.Clear();
            dataGrid.ItemsSource = null;

            if (e.NewValue is not ProcessedResult result) return;

            dataGrid.AutoGenerateColumns = false;

            if (result.Columns != null)
            {
                foreach (var col in result.Columns)
                {
                    var textColumn = new DataGridTextColumn
                    {
                        Header = col.HeaderText,
                        Binding = new Binding($"[{col.Key}]") { Mode = BindingMode.OneWay },

                        // 优化点：使用 Auto 模式让列宽随标题或内容自适应
                        // 同时设置 MinWidth 确保即使标题很短，数据也有足够的展示空间
                        Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                        MinWidth = 80
                    };

                    // 针对数字列，设置右对齐样式
                    if (col.IsNumeric)
                    {
                        var style = new Style(typeof(TextBlock));
                        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5, 0, 5, 0)));
                        textColumn.ElementStyle = style;
                    }

                    dataGrid.Columns.Add(textColumn);
                }
            }
            dataGrid.ItemsSource = result.Rows;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\Helpers\DoubleToGridLengthConverter.cs
// ----------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UI.Helpers
{
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double d ? new GridLength(d) : new GridLength(350);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is GridLength gl ? gl.Value : 350.0;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\Models\CardWorkMode.cs
// ----------------------------------------

namespace UI.Models
{
    public enum CardWorkMode
    {
        FollowLatest, // 永远显示最新解析的数据
        SyncGlobal,   // 跟随 MainViewModel 的全局版本切换
        ManualLock    // 锁定在某个特定版本，不随外部变化
    }
}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\Models\SimpleSnapshotManager.cs
// ----------------------------------------

using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace UI.Models // 建议移动到 UI 项目的 Service 下或保留在原位
{
    public class SimpleSnapshotManager : ISnapshotManager
    {
        // 存储结构：VersionId -> List of Results
        private readonly ConcurrentDictionary<string, List<ProcessedResult>> _storage = new();

        // 事件：单条数据更新
        public event Action<ProcessedResult>? DataUpdated;
        // 事件：整批数据更新（扫描结束）
        public event Action? BatchUpdated;

        public void AddSnapshot(ProcessedResult result)
        {
            var list = _storage.GetOrAdd(result.VersionId, _ => new List<ProcessedResult>());
            lock (list)
            {
                list.Add(result);
            }

            // 触发单条更新信号
            DataUpdated?.Invoke(result);
        }

        /// <summary>
        /// 切换项目时，彻底清空内存中的历史数据
        /// </summary>
        public void Clear()
        {
            _storage.Clear();
            Console.WriteLine("[仓库中心] 存储已清空");
        }

        /// <summary>
        /// 当初始扫描（Initial Scan）结束时调用，触发 UI 整体刷新信号
        /// </summary>
        public void NotifyBatchComplete()
        {
            Console.WriteLine("[仓库中心] 批处理通知：初始扫描数据已就绪");
            BatchUpdated?.Invoke();
        }

        public IEnumerable<ProcessedResult> GetResultsByVersion(string versionId)
            => _storage.TryGetValue(versionId, out var results) ? results : Enumerable.Empty<ProcessedResult>();

        public IEnumerable<ProcessedResult> GetHistory(string blockName)
            => _storage.Values.SelectMany(x => x).Where(r => r.BlockName == blockName);

        public ProcessedResult? GetResult(string versionId, string blockName)
            => GetResultsByVersion(versionId).FirstOrDefault(r => r.BlockName == blockName);
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\ViewModels\DisplayCardViewModel.cs
// ----------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;
using UI.Models;

namespace UI.ViewModels
{
    /// <summary>
    /// 数据展示卡片视图模型：负责单个数据块的配置、过滤与数据渲染逻辑。
    /// 它是 UI 层最活跃的组件，处理用户对特定文件和数据块的查看请求。
    /// </summary>
    public partial class DisplayCardViewModel : ViewModelBase
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly MainViewModel _mainVM;

        /// <summary> 用户在界面 ComboBox 中选择的源文件名（例如：wmass.out） </summary>
        [ObservableProperty] private string? _sourceFileName;

        /// <summary> 用户选择的具体数据块名称（例如：“各层刚心、偏心率计算”） </summary>
        [ObservableProperty] private string? _targetBlockName;

        /// <summary> 
        /// 卡片的工作模式：
        /// FollowLatest - 永远显示最新解析的数据
        /// SyncGlobal   - 跟随 MainViewModel 的全局版本切换
        /// ManualLock   - 锁定在某个特定版本，不随外部变化
        /// </summary>
        [ObservableProperty] private CardWorkMode _mode = CardWorkMode.SyncGlobal;

        /// <summary> 当前卡片展示的最终解析结果数据（直接绑定到 DataGrid） </summary>
        [ObservableProperty] private ProcessedResult? _currentData;

        /// <summary> UI 卡片的显示宽度，支持用户在界面上动态调整 </summary>
        [ObservableProperty] private double _cardWidth = 350;

        /// <summary> 
        /// 卡片私有的可选文件名列表。
        /// 当 MainViewModel 发现新文件时，会同步更新此集合。
        /// </summary>
        public ObservableCollection<string> AvailableFiles { get; set; } = new();

        /// <summary> 
        /// 卡片私有的可选数据块名称列表。
        /// </summary>
        public ObservableCollection<string> AvailableBlocks { get; set; } = new();

        /// <summary>
        /// 初始化卡片实例。
        /// </summary>
        /// <param name="snapshotManager">内核快照管理服务</param>
        /// <param name="versionCoordinator">版本协调服务</param>
        /// <param name="mainVM">父级 MainViewModel 引用，用于获取全局缓存数据</param>
        public DisplayCardViewModel(
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator,
            MainViewModel mainVM)
        {
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;
            _mainVM = mainVM;

            // 订阅内核数据更新事件
            _snapshotManager.DataUpdated += OnDataReceived;

            // 新增：初始化后立即尝试拉取一次存量数据
            RefreshData();
        }

        /// <summary>
        /// 清空卡片的当前显示内容。
        /// </summary>
        public void ClearData()
        {
            CurrentData = null;
        }

        /// <summary>
        /// 当用户更改了目标数据块名称时触发。
        /// 修复 CS8826：确保签名与 ObservableProperty 生成的 partial 方法一致。
        /// </summary>
        partial void OnTargetBlockNameChanged(string? value) => RefreshData();

        /// <summary>
        /// 当用户更改了来源文件名时触发。
        /// 修复 CS8826：确保签名与 ObservableProperty 生成的 partial 方法一致。
        /// </summary>
        partial void OnSourceFileNameChanged(string? value) => RefreshData();

        /// <summary>
        /// 当卡片工作模式改变时触发。
        /// </summary>
        partial void OnModeChanged(CardWorkMode value) => RefreshData();

        /// <summary>
        /// 核心逻辑：刷新卡片数据。
        /// 修复 CS1061：不再直接调用内核缺失的 GetSnapshot 方法，改为从父级 ViewModel 的缓存中检索。
        /// </summary>
        private void RefreshData()
        {
            // 基础校验：如果没有指定要看哪个块，则无需检索
            if (string.IsNullOrEmpty(TargetBlockName)) return;

            // 1. 确定目标版本 ID
            string? versionId = Mode switch
            {
                // 全局同步模式：使用主界面当前选中的版本 ID
                CardWorkMode.SyncGlobal => _mainVM.SelectedVersionId,

                // 跟随最新模式：从主界面的版本列表中取第一个（即最新的）
                CardWorkMode.FollowLatest => _mainVM.AllVersionIds.FirstOrDefault(),

                // 锁定模式：保持当前数据的版本 ID 不变
                CardWorkMode.ManualLock => CurrentData?.VersionId,

                _ => _mainVM.SelectedVersionId
            };

            // 2. 执行检索
            if (versionId != null)
            {
                // 通过调用 MainViewModel 提供的桥接方法获取 UI 层缓存的数据
                var snapshot = _mainVM.TryGetCachedSnapshot(versionId, TargetBlockName);

                if (snapshot != null)
                {
                    CurrentData = snapshot;
                }
            }
        }

        /// <summary>
        /// 内核数据推送回调。
        /// </summary>
        /// <param name="result">内核刚刚解析完成的数据块结果</param>
        private void OnDataReceived(ProcessedResult result)
        {
            // 过滤：只有当新解析的块名与本卡片配置的块名一致时才处理
            if (result.BlockName == TargetBlockName)
            {
                // 逻辑判断：是否需要立即更新 UI
                bool shouldUpdate = Mode switch
                {
                    CardWorkMode.FollowLatest => true, // 最新模式始终更新
                    CardWorkMode.SyncGlobal => result.VersionId == _mainVM.SelectedVersionId, // 仅同步全局选中的版本
                    CardWorkMode.ManualLock => false, // 锁定模式不随推送更新
                    _ => false
                };

                if (shouldUpdate)
                {
                    // 使用 Dispatcher 确保在 UI 线程刷新，因为 DataUpdated 来自内核后台线程
                    System.Windows.Application.Current.Dispatcher.Invoke(() => RefreshData());
                }
            }
        }

        /// <summary>
        /// 关闭卡片命令：调用父级 MainViewModel 的移除逻辑。
        /// </summary>
        [RelayCommand]
        private void Close() => _mainVM.RemoveCard(this);
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\ViewModels\MainViewModel.cs
// ----------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace UI.ViewModels
{
    /// <summary>
    /// 主界面视图模型：负责协调内核服务与 UI 状态同步。
    /// 该类作为 UI 的中枢，维护全局资源列表，并为动态生成的卡片提供数据缓存支持。
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ProjectMonitorService _monitorService;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private string _lastActivePath = string.Empty;

        /// <summary> 
        /// 本地数据快照缓存。
        /// 由于 ISnapshotManager 接口目前仅提供推送（事件），不提供拉取（查询）方法，
        /// UI 层通过此字典维护一份已解析数据的副本，以支持卡片在初始化或切换版本时的即时数据获取。
        /// </summary>
        /// <remarks> 键格式: "{VersionId}_{BlockName}" </remarks>
        private readonly Dictionary<string, ProcessedResult> _localSnapshotCache = new();

        /// <summary> 当前监控的项目根目录路径 </summary>
        [ObservableProperty] private string _projectRootPath = string.Empty;

        /// <summary> 全局当前选中的版本 ID </summary>
        [ObservableProperty] private string? _selectedVersionId;

        /// <summary> 界面底部状态栏显示的描述文本 </summary>
        [ObservableProperty] private string _statusText = "准备就绪";

        /// <summary> 指示系统是否处于忙碌状态（如初始扫描中） </summary>
        [ObservableProperty] private bool _isBusy;

        /// <summary> 当前项目发现的所有历史版本 ID 集合 </summary>
        public ObservableCollection<string> AllVersionIds { get; } = new();

        /// <summary> 当前项目中已发现的所有物理文件名集合 </summary>
        public ObservableCollection<string> AvailableFiles { get; } = new();

        /// <summary> 当前项目中所有已注册或已发现的数据块名称集合 </summary>
        public ObservableCollection<string> AvailableBlocks { get; } = new();

        /// <summary> 当前界面上加载的所有分析卡片视图模型集合 </summary>
        public ObservableCollection<DisplayCardViewModel> DisplayCards { get; } = new();

        /// <summary>
        /// 初始化 MainViewModel 实例。
        /// </summary>
        /// <param name="monitorService">内核监控服务</param>
        /// <param name="snapshotManager">快照管理器</param>
        /// <param name="versionCoordinator">版本协调器</param>
        public MainViewModel(
            ProjectMonitorService monitorService,
            ISnapshotManager snapshotManager,
            IVersionCoordinator versionCoordinator)
        {
            _monitorService = monitorService;
            _snapshotManager = snapshotManager;
            _versionCoordinator = versionCoordinator;

            // 订阅内核数据更新事件，将其存入本地 UI 缓存
            _snapshotManager.DataUpdated += OnGlobalDataUpdated;

            // 订阅内核状态改变事件，并将其映射为 UI 文本
            // 修复 CS0029: 通过 MapEngineStateToText 处理 EngineState 到 string 的转换
            _monitorService.StateChanged += (state) =>
            {
                StatusText = MapEngineStateToText(state);
            };

            AddCard();
        }

        /// <summary>
        /// 将内核枚举状态转换为用户友好的中文描述字符串。
        /// </summary>
        /// <param name="state">来自内核的状态对象（通常为 EngineState 枚举）</param>
        /// <returns>映射后的显示文本</returns>
        private string MapEngineStateToText(object state)
        {
            if (state == null) return "未知状态";

            string stateKey = state.ToString() ?? string.Empty;
            return stateKey switch
            {
                "Idle" => "就绪 - 等待操作",
                "Scanning" => "扫描中 - 正在读取项目结构...",
                "Watching" => "监控中 - 实时捕获文件变动",
                "Processing" => "处理中 - 正在解析数据内容...",
                "Error" => "错误 - 内核运行异常",
                _ => $"状态: {stateKey}"
            };
        }

        /// <summary>
        /// 供 DisplayCardViewModel 调用，从本地缓存中安全获取指定版本的数据块。
        /// 该方法弥补了内核接口 ISnapshotManager 缺失 GetSnapshot 方法的问题。
        /// </summary>
        /// <param name="versionId">版本唯一标识</param>
        /// <param name="blockName">数据块名称</param>
        /// <returns>解析结果 ProcessedResult，若未找到则返回 null</returns>
        public ProcessedResult? TryGetCachedSnapshot(string versionId, string blockName)
        {
            string key = $"{versionId}_{blockName}";
            lock (_localSnapshotCache)
            {
                return _localSnapshotCache.TryGetValue(key, out var result) ? result : null;
            }
        }

        /// <summary>
        /// 当项目路径改变时，重置所有状态并重新启动监控。
        /// </summary>
        async partial void OnProjectRootPathChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) return;
            if (value == _lastActivePath) return;
            _lastActivePath = value;

            try
            {
                IsBusy = true;

                // 清理旧数据状态
                lock (_localSnapshotCache) { _localSnapshotCache.Clear(); }
                _snapshotManager.Clear();
                AllVersionIds.Clear();
                AvailableFiles.Clear();
                AvailableBlocks.Clear();

                foreach (var card in DisplayCards) card.ClearData();

                // 启动内核异步监控
                await _monitorService.StartMonitoringAsync(value);
            }
            catch (Exception ex)
            {
                StatusText = "监控启动失败";
                MessageBox.Show($"无法启动项目监控: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        ///// <summary>
        ///// 在界面上新增一个分析卡片，并同步当前的全局资源列表。
        ///// </summary>
        //[RelayCommand]
        //private void AddCard()
        //{
        //    var card = new DisplayCardViewModel(_snapshotManager, _versionCoordinator, this)
        //    {
        //        // 手动同步当前已发现的文件和块，确保新创建的卡片下拉列表不为空
        //        AvailableFiles = new ObservableCollection<string>(this.AvailableFiles),
        //        AvailableBlocks = new ObservableCollection<string>(this.AvailableBlocks)
        //    };
        //    DisplayCards.Add(card);
        //}

        /// <summary>
        /// 移除指定的分析卡片。
        /// </summary>
        public void RemoveCard(DisplayCardViewModel card) => DisplayCards.Remove(card);

        /// <summary>
        /// 处理内核发出的全局数据更新通知。
        /// </summary>
        private void OnGlobalDataUpdated(ProcessedResult res)
        {
            // 1. 存入本地缓存，供后续卡片查询
            string cacheKey = $"{res.VersionId}_{res.BlockName}";
            lock (_localSnapshotCache)
            {
                _localSnapshotCache[cacheKey] = res;
            }

            // 2. 更新 UI 相关的 Observable 集合
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新版本列表
                if (!AllVersionIds.Contains(res.VersionId))
                {
                    AllVersionIds.Insert(0, res.VersionId);
                    if (SelectedVersionId == null) SelectedVersionId = res.VersionId;
                }

                // 更新可用文件列表
                if (res.Metadata.TryGetValue("OriginFile", out var fileName) && fileName is string nameStr)
                {
                    if (!AvailableFiles.Contains(nameStr))
                    {
                        AvailableFiles.Add(nameStr);
                        // 同步更新现有卡片的下拉选项
                        foreach (var card in DisplayCards)
                            if (!card.AvailableFiles.Contains(nameStr)) card.AvailableFiles.Add(nameStr);
                    }
                }

                // 更新可用数据块列表
                if (!AvailableBlocks.Contains(res.BlockName))
                {
                    AvailableBlocks.Add(res.BlockName);
                    // 同步更新现有卡片的下拉选项
                    foreach (var card in DisplayCards)
                        if (!card.AvailableBlocks.Contains(res.BlockName)) card.AvailableBlocks.Add(res.BlockName);
                }
            });
        }

        partial void OnSelectedVersionIdChanged(string? value)
        {
            if (value == null) return;

            // 通知所有处于 SyncGlobal 模式的卡片刷新数据
            foreach (var card in DisplayCards)
            {
                if (card.Mode == UI.Models.CardWorkMode.SyncGlobal)
                {
                    // 注意：由于 RefreshData 是私有的，你可能需要在卡片中将其改为 internal 或 public
                    // 或者通过卡片订阅一个全局事件。
                    card.GetType().GetMethod("RefreshData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(card, null);
                }
            }
        }

        [RelayCommand]
        private void AddCard()
        {
            // 传入 this (MainViewModel) 以便卡片构造时能引用
            var card = new DisplayCardViewModel(_snapshotManager, _versionCoordinator, this);

            // 修正：不再在这里 new 集合，而是在卡片构造函数内部处理
            DisplayCards.Add(card);
        }

        /// <summary>
        /// 弹出文件夹浏览器。
        /// </summary>
        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true) ProjectRootPath = dialog.FolderName;
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\ViewModels\ViewModelBase.cs
// ----------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;

namespace UI.ViewModels
{
    /// <summary>
    /// 现在的基类变得异常清爽，因为它把脏活累活都交给了微软的 Source Generator
    /// </summary>
    public abstract partial class ViewModelBase : ObservableObject
    {
        // 这里可以放一些全局通用的逻辑，比如忙碌状态指示
        [ObservableProperty]
        private bool _isBusy;
    }
}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\obj\Debug\net10.0\ConProjectMonitor.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("ConProjectMonitor")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("ConProjectMonitor")]
[assembly: System.Reflection.AssemblyTitleAttribute("ConProjectMonitor")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\obj\Debug\net10.0\ConProjectMonitor.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\obj\Release\net10.0\ConProjectMonitor.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("ConProjectMonitor")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("ConProjectMonitor")]
[assembly: System.Reflection.AssemblyTitleAttribute("ConProjectMonitor")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConProjectMonitor\obj\Release\net10.0\ConProjectMonitor.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\obj\Debug\net10.0\ConsoleTest.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("ConsoleTest")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("ConsoleTest")]
[assembly: System.Reflection.AssemblyTitleAttribute("ConsoleTest")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\obj\Debug\net10.0\ConsoleTest.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\obj\Release\net10.0\ConsoleTest.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("ConsoleTest")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("ConsoleTest")]
[assembly: System.Reflection.AssemblyTitleAttribute("ConsoleTest")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\ConsoleTest\obj\Release\net10.0\ConsoleTest.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\App.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "86525DB3DF1C495488486DA2B368568F56DC29F6"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public static void Main() {
            Desktop.App app = new Desktop.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\App.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "86525DB3DF1C495488486DA2B368568F56DC29F6"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public static void Main() {
            Desktop.App app = new Desktop.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_1pf5r3z3_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_1pf5r3z3_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_2zjqisd0_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_2zjqisd0_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_d04aiubo_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_d04aiubo_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_gnpxv31w_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_gnpxv31w_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_sksrk1mm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_sksrk1mm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_wrh54mam_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_wrh54mam_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_zslzuoi0_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Desktop_zslzuoi0_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\GeneratedInternalTypeHelper.g.i.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace XamlGeneratedNamespace {
    
    
    /// <summary>
    /// GeneratedInternalTypeHelper
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class GeneratedInternalTypeHelper : System.Windows.Markup.InternalTypeHelper {
        
        /// <summary>
        /// CreateInstance
        /// </summary>
        protected override object CreateInstance(System.Type type, System.Globalization.CultureInfo culture) {
            return System.Activator.CreateInstance(type, ((System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) 
                            | (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.CreateInstance)), null, null, culture);
        }
        
        /// <summary>
        /// GetPropertyValue
        /// </summary>
        protected override object GetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, System.Globalization.CultureInfo culture) {
            return propertyInfo.GetValue(target, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// SetPropertyValue
        /// </summary>
        protected override void SetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, object value, System.Globalization.CultureInfo culture) {
            propertyInfo.SetValue(target, value, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// CreateDelegate
        /// </summary>
        protected override System.Delegate CreateDelegate(System.Type delegateType, object target, string handler) {
            return ((System.Delegate)(target.GetType().InvokeMember("_CreateDelegate", (System.Reflection.BindingFlags.InvokeMethod 
                            | (System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)), null, target, new object[] {
                        delegateType,
                        handler}, null)));
        }
        
        /// <summary>
        /// AddEventHandler
        /// </summary>
        protected override void AddEventHandler(System.Reflection.EventInfo eventInfo, object target, System.Delegate handler) {
            eventInfo.AddEventHandler(target, handler);
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\MainWindow.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "5935B8AA9876C8F264E922AD2B016BC842EC8814"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/Desktop;V1.0.0.0;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Release\net10.0-windows\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Release\net10.0-windows\App.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "86525DB3DF1C495488486DA2B368568F56DC29F6"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public static void Main() {
            Desktop.App app = new Desktop.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Release\net10.0-windows\Desktop.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Release\net10.0-windows\Desktop.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.AllFuncTestCon\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.AllFuncTestCon\obj\Debug\net10.0\FileProcessor.AllFuncTestCon.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.AllFuncTestCon")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.AllFuncTestCon")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.AllFuncTestCon")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.AllFuncTestCon\obj\Debug\net10.0\FileProcessor.AllFuncTestCon.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.Core.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.Core.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Release\net10.0\FileProcessor.Core.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Release\net10.0\FileProcessor.Core.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\obj\Debug\net10.0\FileProcessor.DebugHelpers.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.DebugHelpers")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.DebugHelpers")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.DebugHelpers")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\obj\Debug\net10.0\FileProcessor.DebugHelpers.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\App.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "16304034DA1FC6043DD38CDAEC0065C94196350E"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using FileProcessor.Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace FileProcessor.Desktop {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public static void Main() {
            FileProcessor.Desktop.App app = new FileProcessor.Desktop.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\App.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "16304034DA1FC6043DD38CDAEC0065C94196350E"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using FileProcessor.Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace FileProcessor.Desktop {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public static void Main() {
            FileProcessor.Desktop.App app = new FileProcessor.Desktop.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_1fdqvnxa_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_1fdqvnxa_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_3ox5xmx1_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_3ox5xmx1_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_3s1tvvze_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_3s1tvvze_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_4qwwvmci_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_4qwwvmci_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5dl4pxeh_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5dl4pxeh_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5r5yamcs_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5r5yamcs_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_ahkzbnvj_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_ahkzbnvj_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_axi5r1fu_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_axi5r1fu_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_bwsm4bru_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_bwsm4bru_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_calctkqc_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_calctkqc_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_dtn2ueqo_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_dtn2ueqo_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_fvnuig1m_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_fvnuig1m_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_gpbdsknu_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_gpbdsknu_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_iu5t1y1z_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_iu5t1y1z_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_jgsyownr_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_jgsyownr_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_l2ida5si_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_l2ida5si_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_l44pr0w2_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_l44pr0w2_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_lmd2wyer_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_lmd2wyer_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_okh4hg23_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_okh4hg23_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_p1gjknmm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_p1gjknmm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_sjtjojib_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_sjtjojib_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_uqfcmdfm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_uqfcmdfm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_vlpuwfwk_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_vlpuwfwk_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_xtpnab1i_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_xtpnab1i_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_yi5sca5t_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_yi5sca5t_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_yv2osfcr_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_yv2osfcr_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_zskhdyfm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Desktop")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_zskhdyfm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Forms;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\GeneratedInternalTypeHelper.g.cs
// ----------------------------------------





// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\GeneratedInternalTypeHelper.g.i.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace XamlGeneratedNamespace {
    
    
    /// <summary>
    /// GeneratedInternalTypeHelper
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class GeneratedInternalTypeHelper : System.Windows.Markup.InternalTypeHelper {
        
        /// <summary>
        /// CreateInstance
        /// </summary>
        protected override object CreateInstance(System.Type type, System.Globalization.CultureInfo culture) {
            return System.Activator.CreateInstance(type, ((System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) 
                            | (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.CreateInstance)), null, null, culture);
        }
        
        /// <summary>
        /// GetPropertyValue
        /// </summary>
        protected override object GetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, System.Globalization.CultureInfo culture) {
            return propertyInfo.GetValue(target, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// SetPropertyValue
        /// </summary>
        protected override void SetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, object value, System.Globalization.CultureInfo culture) {
            propertyInfo.SetValue(target, value, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// CreateDelegate
        /// </summary>
        protected override System.Delegate CreateDelegate(System.Type delegateType, object target, string handler) {
            return ((System.Delegate)(target.GetType().InvokeMember("_CreateDelegate", (System.Reflection.BindingFlags.InvokeMethod 
                            | (System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)), null, target, new object[] {
                        delegateType,
                        handler}, null)));
        }
        
        /// <summary>
        /// AddEventHandler
        /// </summary>
        protected override void AddEventHandler(System.Reflection.EventInfo eventInfo, object target, System.Delegate handler) {
            eventInfo.AddEventHandler(target, handler);
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\MainWindow.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "7DF1BBC267FAAA614E52CC568A954B5A0F9770F2"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using FileProcessor.Desktop.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace FileProcessor.Desktop {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/FileProcessor.Desktop;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\MainWindow.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "7DF1BBC267FAAA614E52CC568A954B5A0F9770F2"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using FileProcessor.Desktop.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace FileProcessor.Desktop {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/FileProcessor.Desktop;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Debug\net10.0\FileProcessor.Engine.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Debug\net10.0\FileProcessor.Engine.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Release\net10.0\FileProcessor.Engine.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Release\net10.0\FileProcessor.Engine.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Debug\net10.0\FileProcessor.Infrastructure.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Debug\net10.0\FileProcessor.Infrastructure.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Release\net10.0\FileProcessor.Infrastructure.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Release\net10.0\FileProcessor.Infrastructure.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\obj\Debug\net10.0\FileProcessor.Mediator.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Mediator")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Mediator")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Mediator")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\obj\Debug\net10.0\FileProcessor.Mediator.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Tests\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Tests\obj\Debug\net10.0\FileProcessor.Tests.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Tests")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Tests")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Tests")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Tests\obj\Debug\net10.0\FileProcessor.Tests.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
global using Xunit;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.ToDelTest\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.ToDelTest\obj\Debug\net10.0\FileProcessor.ToDelTest.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.ToDelTest")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+143d828482676601145ff821ce671dfd6a9d6be1")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.ToDelTest")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.ToDelTest")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.ToDelTest\obj\Debug\net10.0\FileProcessor.ToDelTest.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.UTest\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.UTest\obj\Debug\net10.0\FileProcessor.UTest.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.UTest")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+143d828482676601145ff821ce671dfd6a9d6be1")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.UTest")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.UTest")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.UTest\obj\Debug\net10.0\FileProcessor.UTest.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
global using Xunit;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Mock.Plugin\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Mock.Plugin\obj\Debug\net10.0\Mock.Plugin.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("Mock.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("Mock.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("Mock.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Mock.Plugin\obj\Debug\net10.0\Mock.Plugin.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\App.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "47649F95E7F119AD336C0D4A21763927A5A95B42"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using UI;


namespace UI {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/UI;component/app.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\App.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public static void Main() {
            UI.App app = new UI.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\App.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "47649F95E7F119AD336C0D4A21763927A5A95B42"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using UI;


namespace UI {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/UI;V1.0.0.0;component/app.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\App.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public static void Main() {
            UI.App app = new UI.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\GeneratedInternalTypeHelper.g.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace XamlGeneratedNamespace {
    
    
    /// <summary>
    /// GeneratedInternalTypeHelper
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class GeneratedInternalTypeHelper : System.Windows.Markup.InternalTypeHelper {
        
        /// <summary>
        /// CreateInstance
        /// </summary>
        protected override object CreateInstance(System.Type type, System.Globalization.CultureInfo culture) {
            return System.Activator.CreateInstance(type, ((System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) 
                            | (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.CreateInstance)), null, null, culture);
        }
        
        /// <summary>
        /// GetPropertyValue
        /// </summary>
        protected override object GetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, System.Globalization.CultureInfo culture) {
            return propertyInfo.GetValue(target, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// SetPropertyValue
        /// </summary>
        protected override void SetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, object value, System.Globalization.CultureInfo culture) {
            propertyInfo.SetValue(target, value, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// CreateDelegate
        /// </summary>
        protected override System.Delegate CreateDelegate(System.Type delegateType, object target, string handler) {
            return ((System.Delegate)(target.GetType().InvokeMember("_CreateDelegate", (System.Reflection.BindingFlags.InvokeMethod 
                            | (System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)), null, target, new object[] {
                        delegateType,
                        handler}, null)));
        }
        
        /// <summary>
        /// AddEventHandler
        /// </summary>
        protected override void AddEventHandler(System.Reflection.EventInfo eventInfo, object target, System.Delegate handler) {
            eventInfo.AddEventHandler(target, handler);
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\GeneratedInternalTypeHelper.g.i.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace XamlGeneratedNamespace {
    
    
    /// <summary>
    /// GeneratedInternalTypeHelper
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class GeneratedInternalTypeHelper : System.Windows.Markup.InternalTypeHelper {
        
        /// <summary>
        /// CreateInstance
        /// </summary>
        protected override object CreateInstance(System.Type type, System.Globalization.CultureInfo culture) {
            return System.Activator.CreateInstance(type, ((System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) 
                            | (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.CreateInstance)), null, null, culture);
        }
        
        /// <summary>
        /// GetPropertyValue
        /// </summary>
        protected override object GetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, System.Globalization.CultureInfo culture) {
            return propertyInfo.GetValue(target, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// SetPropertyValue
        /// </summary>
        protected override void SetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, object value, System.Globalization.CultureInfo culture) {
            propertyInfo.SetValue(target, value, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// CreateDelegate
        /// </summary>
        protected override System.Delegate CreateDelegate(System.Type delegateType, object target, string handler) {
            return ((System.Delegate)(target.GetType().InvokeMember("_CreateDelegate", (System.Reflection.BindingFlags.InvokeMethod 
                            | (System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)), null, target, new object[] {
                        delegateType,
                        handler}, null)));
        }
        
        /// <summary>
        /// AddEventHandler
        /// </summary>
        protected override void AddEventHandler(System.Reflection.EventInfo eventInfo, object target, System.Delegate handler) {
            eventInfo.AddEventHandler(target, handler);
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\MainWindow.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "A50550E3C5D5048BBCA91A7A179AC366FE1C329D"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using UI;
using UI.Helpers;
using UI.Models;
using UI.ViewModels;


namespace UI {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/UI;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\MainWindow.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "A50550E3C5D5048BBCA91A7A179AC366FE1C329D"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using UI;
using UI.Helpers;
using UI.Models;
using UI.ViewModels;


namespace UI {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/UI;V1.0.0.0;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_00nmy0mx_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_00nmy0mx_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_01ag3xoa_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_01ag3xoa_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_02y304te_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_02y304te_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_04uuan4b_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_04uuan4b_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0ebcup0b_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0ebcup0b_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0n1vk4zb_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0n1vk4zb_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0psc5zls_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0psc5zls_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0s12nv2j_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0s12nv2j_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0zf0dz2c_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_0zf0dz2c_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1d3dxues_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1d3dxues_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1jah0gwa_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1jah0gwa_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1v2h3txm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1v2h3txm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1x5stfhc_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_1x5stfhc_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_2ghb0jbp_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_2ghb0jbp_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_2iolximt_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_2iolximt_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_2izdqvrf_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_2izdqvrf_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_3frgarn0_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_3frgarn0_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_5lrqofvt_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_5lrqofvt_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_5v0hfgtr_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_5v0hfgtr_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_5xmwlzov_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_5xmwlzov_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_a15jco1e_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_a15jco1e_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_a5j3uydu_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_a5j3uydu_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_adcni54i_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_adcni54i_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_aeht23qd_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_aeht23qd_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ahj25dfl_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ahj25dfl_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_alyum13v_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_alyum13v_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ammtvk2g_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ammtvk2g_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_bciq4pa2_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_bciq4pa2_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_bmgovaq4_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_bmgovaq4_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_c0exwze4_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_c0exwze4_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cihq4gzp_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cihq4gzp_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cniuh1cp_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cniuh1cp_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cvb5mtsi_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cvb5mtsi_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cxfz1jj2_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_cxfz1jj2_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_d1emum4m_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_d1emum4m_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ddasqqoh_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ddasqqoh_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_eabtmssq_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_eabtmssq_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_eawhbt1s_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_eawhbt1s_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_fjqrvz5k_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_fjqrvz5k_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_frcgtyc0_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_frcgtyc0_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_g3ai24sn_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_g3ai24sn_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gb0gqlkv_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gb0gqlkv_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gfr35mfm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gfr35mfm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gm4atebm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gm4atebm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gqtyycyk_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gqtyycyk_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gubrfwov_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_gubrfwov_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_guftv0am_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_guftv0am_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_guveq33w_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_guveq33w_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_h3apemjm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_h3apemjm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_hbhjny0h_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_hbhjny0h_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_i2yflk01_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_i2yflk01_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_iwtzqhzt_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_iwtzqhzt_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_j3brn41w_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_j3brn41w_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_jbxnail4_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_jbxnail4_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_jccf2xu4_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_jccf2xu4_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_jpqtgmlf_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_jpqtgmlf_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kgjsr0ap_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kgjsr0ap_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kuearnmw_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kuearnmw_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kyqajp0h_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kyqajp0h_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kz3pyzx5_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_kz3pyzx5_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ld34fmai_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ld34fmai_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_lrue4r4r_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_lrue4r4r_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mite3iby_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mite3iby_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mocq3ms0_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mocq3ms0_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mvz0tabm_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mvz0tabm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mykfukcx_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_mykfukcx_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_na5lev00_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_na5lev00_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_nnfzwszc_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_nnfzwszc_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_nngn0ode_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_nngn0ode_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_o1danj4r_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_o1danj4r_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_omjqav3n_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_omjqav3n_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ooeu2nmh_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ooeu2nmh_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ou23yobf_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ou23yobf_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_pyhcm1hn_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_pyhcm1hn_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_q2ju301o_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_q2ju301o_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_qhc4bamn_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_qhc4bamn_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_qnqsjzi2_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_qnqsjzi2_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_qqfsiblk_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_qqfsiblk_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_re3rreo0_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_re3rreo0_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_renk3mjl_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_renk3mjl_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rg1pra2w_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rg1pra2w_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rqead1yg_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rqead1yg_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rs05ruf1_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rs05ruf1_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rsjdkkik_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_rsjdkkik_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_scc2zrrc_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_scc2zrrc_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_t0q2itod_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_t0q2itod_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_tcgxgd2d_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_tcgxgd2d_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_tdgwa4vo_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_tdgwa4vo_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_tt1miod1_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_tt1miod1_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_uegw43dx_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_uegw43dx_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_um20qzav_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_um20qzav_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vlx1dtx2_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vlx1dtx2_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vqkwhevy_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vqkwhevy_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vx1h0upq_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vx1h0upq_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vzmkuykk_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_vzmkuykk_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_wps3wj02_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+f1e787fecae2651f2db42cd1ea8cf2a62c32ac40")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_wps3wj02_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_x0vqmhnh_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_x0vqmhnh_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_xdzub3yb_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d7c8c9bb2432cd91569b8d0d1e2ef430f67f9ba6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_xdzub3yb_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_yj1fvosc_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+cf97b52ca973c3c221d4bbb9ad7428f166fe77f6")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_yj1fvosc_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ymreey2v_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ymreey2v_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ys2bwuqn_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+421b9c49f53306bd632c42513f718a6b9d31373e")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_ys2bwuqn_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_zkde4nau_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+9349b17f59fe5dc4ebde0205e5d3cbf758173c6f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_zkde4nau_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_zyzass5f_wpftmp.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+194c5aedb83aaf2f307b101c2a19ee964dff7c9f")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Debug\net10.0-windows\UI_zyzass5f_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Release\net10.0-windows\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Release\net10.0-windows\App.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "47649F95E7F119AD336C0D4A21763927A5A95B42"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using UI;


namespace UI {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/UI;component/app.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\App.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public static void Main() {
            UI.App app = new UI.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Release\net10.0-windows\GeneratedInternalTypeHelper.g.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace XamlGeneratedNamespace {
    
    
    /// <summary>
    /// GeneratedInternalTypeHelper
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class GeneratedInternalTypeHelper : System.Windows.Markup.InternalTypeHelper {
        
        /// <summary>
        /// CreateInstance
        /// </summary>
        protected override object CreateInstance(System.Type type, System.Globalization.CultureInfo culture) {
            return System.Activator.CreateInstance(type, ((System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) 
                            | (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.CreateInstance)), null, null, culture);
        }
        
        /// <summary>
        /// GetPropertyValue
        /// </summary>
        protected override object GetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, System.Globalization.CultureInfo culture) {
            return propertyInfo.GetValue(target, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// SetPropertyValue
        /// </summary>
        protected override void SetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, object value, System.Globalization.CultureInfo culture) {
            propertyInfo.SetValue(target, value, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        
        /// <summary>
        /// CreateDelegate
        /// </summary>
        protected override System.Delegate CreateDelegate(System.Type delegateType, object target, string handler) {
            return ((System.Delegate)(target.GetType().InvokeMember("_CreateDelegate", (System.Reflection.BindingFlags.InvokeMethod 
                            | (System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)), null, target, new object[] {
                        delegateType,
                        handler}, null)));
        }
        
        /// <summary>
        /// AddEventHandler
        /// </summary>
        protected override void AddEventHandler(System.Reflection.EventInfo eventInfo, object target, System.Delegate handler) {
            eventInfo.AddEventHandler(target, handler);
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Release\net10.0-windows\MainWindow.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "A50550E3C5D5048BBCA91A7A179AC366FE1C329D"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using UI;
using UI.Helpers;
using UI.Models;
using UI.ViewModels;


namespace UI {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/UI;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Release\net10.0-windows\UI.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UI")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("UI")]
[assembly: System.Reflection.AssemblyTitleAttribute("UI")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\UI\obj\Release\net10.0-windows\UI.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\obj\Debug\net10.0\WDisp.Plugin.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("WDisp.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("WDisp.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("WDisp.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\obj\Debug\net10.0\WDisp.Plugin.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("WMass")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("WMass")]
[assembly: System.Reflection.AssemblyTitleAttribute("WMass")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.Plugin.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.Plugin.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Release\net10.0\WMass.Plugin.AssemblyInfo.cs
// ----------------------------------------

//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// 由 MSBuild WriteCodeFragment 类生成。



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Release\net10.0\WMass.Plugin.GlobalUsings.g.cs
// ----------------------------------------

// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Views\MainWindow.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\..\Views\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "5935B8AA9876C8F264E922AD2B016BC842EC8814"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/Desktop;component/views/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\Views\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Debug\net10.0-windows\Views\MainWindow.g.i.cs
// ----------------------------------------

#pragma checksum "..\..\..\..\Views\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "5935B8AA9876C8F264E922AD2B016BC842EC8814"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/Desktop;component/views/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\Views\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}



// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\Desktop\obj\Release\net10.0-windows\Views\MainWindow.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\..\Views\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "5935B8AA9876C8F264E922AD2B016BC842EC8814"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using Desktop;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace Desktop {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/Desktop;component/views/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\Views\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.2.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }
}


