using ConProjectMonitor;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Runtime;
using FileProcessor.Engine.Services;
using System.Configuration;
using System.Data;
using System.Windows;
using UI.Models;
using UI.ViewModels;

namespace UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- 1. 准备底层零件 (这里可以直接用你验证过的 Mock 或具体实现) ---
            var coordinator = new VersionCoordinator(3000);
            var snapshotManager = new SimpleSnapshotManager(); // 这里需要一个实现了 ISnapshotManager 的类

            // 假设你已经把 Mock 类搬到了 UI 或 Core 中
            var templates = new List<IFileTemplate> { new MockOutTemplate() };
            var processors = new List<IBlockProcessor> { new MockBlockProcessor() };

            // --- 2. 组装引擎 ---
            var orchestrator = new FileOrchestrator(templates, snapshotManager, processors);
            var monitorService = new ProjectMonitorService(orchestrator, coordinator, templates);

            // --- 3. 组装 ViewModel ---
            var mainVM = new MainViewModel(monitorService, snapshotManager, coordinator);

            // --- 4. 启动界面 ---
            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainVM;
            mainWindow.Show();
        }
    }

}
