using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;
using System.Text;
using System.Collections; // 用于遍历列表

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
using var monitor = new FileMonitorService(watchDir, orchestrator);

// 订阅事件：解析完成后自动入库
monitor.OnSnapshotCreated += (snapshot) =>
{
    snapshotManager.AddSnapshot(snapshot);
    Console.WriteLine($"\n[通知] 新文件已归档: {snapshot.FileName}");
    Console.WriteLine($"[调试] 识别到的块名列表:");
    foreach (var key in snapshot.DataBlocks.Keys)
    {
        Console.WriteLine($"   - '{key}'");
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
        // 目标块名
        string targetBlock = "各层刚心、偏心率、相邻层侧移刚度比等计算信息";
        var results = snapshotManager.AggregateLatestData(targetBlock);

        Console.WriteLine($"\n--- 原始数据验证: {targetBlock} ---");
        int fileCount = 0;

        foreach (var data in results)
        {
            fileCount++;
            Console.WriteLine($"[文件记录 {fileCount}]");

            // 使用 dynamic 访问 WMassProcessor 返回的匿名对象属性
            // 注意：如果跨程序集访问匿名对象可能有局限，这里推荐通过反射获取 Records
            var type = data.GetType();
            var recordsProp = type.GetProperty("Records");

            if (recordsProp != null)
            {
                var records = recordsProp.GetValue(data) as IEnumerable;
                if (records != null)
                {
                    Console.WriteLine(new string('-', 60));
                    // 打印表头
                    Console.WriteLine($"{"层号",-6} | {"塔号",-6} | {"Ratx(剪切刚度比)",-15} | {"Raty",-10}");
                    Console.WriteLine(new string('-', 60));

                    foreach (var row in records)
                    {
                        if (row is Dictionary<string, string> dict)
                        {
                            string floor = dict.GetValueOrDefault("层号", "??");
                            string tower = dict.GetValueOrDefault("塔号", "1");
                            string ratx = dict.GetValueOrDefault("Ratx", "N/A");
                            string raty = dict.GetValueOrDefault("Raty", "N/A");

                            // 使用对齐格式，让数值排成一列
                            Console.WriteLine($"{floor,-8} | {tower,-8} | {ratx,-18} | {raty,-10}");
                        }
                    }
                    Console.WriteLine(new string('-', 60));
                }
            }
            else
            {
                // 如果不是 WMassProcessor 处理的结构，则直接打印输出
                Console.WriteLine($"  {data}");
            }
        }

        if (fileCount == 0)
            Console.WriteLine("(!) 尚未在快照存储中找到该数据块。请检查文件名是否为 wmass.out 以及块名是否匹配。");

        Console.WriteLine("--------------------------------------------\n");
    }
}

Console.WriteLine("服务退出。");