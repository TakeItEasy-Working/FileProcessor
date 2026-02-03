using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;
using System.Windows;
using UI.Models;
using UI.ViewModels;

namespace UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            base.OnStartup(e);

            // --- 1. 初始化底层核心零件 ---
            // VersionCoordinator 负责管理版本号（3000ms 倒计时）
            var coordinator = new VersionCoordinator(3000);

            // SimpleSnapshotManager 负责在内存中存储解析后的结果
            var snapshotManager = new SimpleSnapshotManager();

            // 使用 Mock 类来跑通流程
            // MockOutTemplate 负责模拟文件切块
            // MockBlockProcessor 负责模拟块解析成表格
            // App.xaml.cs 
            // 引用 WMass.Plugin 命名空间后：
            var templates = new List<IFileTemplate> { new WMass.Plugin.WMassTemplate() };
            var processors = new List<IBlockProcessor> { new WMass.Plugin.WMassProcessor() };

            // --- 2. 组装执行引擎 ---
            // FileOrchestrator 是实际干活的：拿着模板和处理器去解析文件，并存入快照管理器
            var orchestrator = new FileOrchestrator(templates, snapshotManager, processors);

            // 【核心修复】：按照 ProjectMonitorService 构造函数的定义进行组装
            // 参数顺序：FileOrchestrator, IVersionCoordinator, ISnapshotManager, IEnumerable<IFileTemplate>
            var monitorService = new ProjectMonitorService(
                orchestrator,
                coordinator,
                snapshotManager,
                templates);

            // --- 3. 组装 ViewModel ---
            // 将服务注入 MainViewModel，让 UI 能够控制引擎
            var mainVM = new MainViewModel(monitorService, snapshotManager, coordinator);

            // --- 4. 启动界面 ---
            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainVM;
            mainWindow.Show();
        }
    }
}
