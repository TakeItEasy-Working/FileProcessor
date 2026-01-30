using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// --- 路径配置 ---
string pluginDir = Path.Combine(AppContext.BaseDirectory, "Plugins");
string watchDir = Path.Combine(AppContext.BaseDirectory, "WatchFolder");

if (!Directory.Exists(pluginDir)) Directory.CreateDirectory(pluginDir);
if (!Directory.Exists(watchDir)) Directory.CreateDirectory(watchDir);

Console.WriteLine("=== .NET 10.0 文件分析系统运行中 ===");

// 1. 初始化引擎：加载 DLL 插件
var loader = new PluginLoader();
var (templates, registry) = loader.LoadPlugins(pluginDir);
Console.WriteLine($"[系统] 已加载模板: {templates.Count} 个");

// 2. 初始化核心逻辑与存储
var orchestrator = new FileOrchestrator(templates, registry);
var snapshotManager = new SnapshotManager();

// 3. 启动监控服务
// 注意：此时 Monitor 内部已经修改，支持 OnSnapshotCreated 事件
using var monitor = new FileMonitorService(watchDir, orchestrator);

// 【核心修复】：订阅事件。当 Monitor 解析完文件得到 Snapshot 时，自动塞进存储器
// 监听事件
monitor.OnSnapshotCreated += (snapshot) =>
{
    snapshotManager.AddSnapshot(snapshot);
    Console.WriteLine($"\n[通知] 新文件已归档: {snapshot.FileName}");
    Console.WriteLine($"[调试] 识别到的块名列表:");
    foreach (var key in snapshot.DataBlocks.Keys)
    {
        Console.WriteLine($"   - '{key}'"); // 打印出来，看看是否有空格或符号差异
    }
};
Console.WriteLine($"[系统] 正在监听: {watchDir}");
Console.WriteLine("操作指引: 将 wmass.out 放入文件夹。输入 's' 查看统计, 'q' 退出。");

// 4. 交互循环
while (true)
{
    var key = Console.ReadKey(true).KeyChar;
    if (key == 'q') break;

    if (key == 's')
    {
        // 这里的 Key 必须与 WMassTemplate.IdentifyBlockName 返回的字符串完全一致
        string targetBlock = "各层刚心、偏心率、相邻层侧移刚度比等计算信息";
        var results = snapshotManager.AggregateLatestData(targetBlock);

        Console.WriteLine($"\n--- 统计报告: {targetBlock} ---");
        int count = 0;
        foreach (var data in results)
        {
            count++;
            // 这里的 data 是 WMassProcessor.Process 返回的匿名对象
            Console.WriteLine($"[记录 {count}] {data}");
        }

        if (count == 0) Console.WriteLine("(!) 尚未在快照存储中找到该数据块，请确认文件已解析且块名匹配。");
        Console.WriteLine("------------------------------\n");
    }
}

Console.WriteLine("服务退出。");