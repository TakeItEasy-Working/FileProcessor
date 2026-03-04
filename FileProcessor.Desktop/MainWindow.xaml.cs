using FileProcessor.DebugHelpers;
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

            Log.Debug($"[窗口启动] 文件:{System.DateTime.Now.ToString("f")}");

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