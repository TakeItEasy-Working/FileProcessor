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