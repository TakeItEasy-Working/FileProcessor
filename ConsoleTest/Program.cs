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
using var monitor = new FileMonitorService(watchDir, orchestrator);

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