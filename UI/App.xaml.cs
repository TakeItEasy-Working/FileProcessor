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