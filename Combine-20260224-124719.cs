// ========================================
// 自动生成的合并文件
// 生成时间: 2026/2/24 12:47:19
// ========================================


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Attributes\BlockProcessorAttribute.cs
// ----------------------------------------

using System;
namespace FileProcessor.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class BlockProcessorAttribute : Attribute
    {
        public string BlockName { get; }
        public int Priority { get; init; } = 0;
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
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FileProcessorPluginAttribute : Attribute
    {
        public string FileNamePattern { get; }
        public string SubDirectory { get; }
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
        string TargetBlockName { get; }
        int Priority { get; }
        bool CanProcess(string blockName);
        string StandardBlockName { get; }
        string DefaultCategory { get; }
        ProcessedResult Process(RawDataBlock block);
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Contracts\IFileTemplate.cs
// ----------------------------------------

using FileProcessor.Core.Models;
namespace FileProcessor.Core.Contracts
{
    public interface IFileTemplate
    {
        string FileNamePattern { get; }
        IEnumerable<RawDataBlock> Parse(string filePath);
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
    public interface ISnapshotManager
    {
        void AddSnapshot(ProcessedResult result);
        IEnumerable<string> GetFileNames(string versionId);
        IEnumerable<ProcessedResult> GetResultsByFile(string versionId, string fileName);
        ProcessedResult? GetSpecificBlock(string versionId, string fileName, string standardBlockName);
        IEnumerable<ProcessedResult> GetHistory(string standardBlockName);
        void Clear();
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Contracts\IVersionCoordinator.cs
// ----------------------------------------

namespace FileProcessor.Core.Contracts
{
    public interface IVersionCoordinator
    {
        string CurrentVersionId { get; }
        bool IsRecording { get; }
        void StartNewBatch(string source);
        void CommitCurrentBatch(string source);
        string GenerateLiveVersionId();
        void Commit(string versionId);
        event Action<string> VersionCommitted;
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
    public abstract class BaseFileTemplate : IFileTemplate
    {
        public virtual string FileNamePattern =>
            this.GetType().GetCustomAttribute<FileProcessorPluginAttribute>()?.FileNamePattern ?? string.Empty;
        public virtual string SubDirectory =>
            this.GetType().GetCustomAttribute<FileProcessorPluginAttribute>()?.SubDirectory ?? string.Empty;
        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            if (!File.Exists(filePath)) yield break;          
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Encoding encoding = DetectEncoding(fs);
            var lines = new List<string>();
            using (var reader = new StreamReader(fs, encoding))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            var blocks = SplitBlocks(lines, filePath);  
            foreach (var block in blocks)
            {                
                yield return block;
            }
        }
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
                const int sampleSize = 8192; 
                Span<byte> buffer = stackalloc byte[sampleSize];
                int read = stream.Read(buffer);
                if (stream.CanSeek)
                    stream.Position = originalPosition;
                if (read == 0)
                    return Encoding.UTF8; 
                if (read >= 3 &&
                    buffer[0] == 0xEF &&
                    buffer[1] == 0xBB &&
                    buffer[2] == 0xBF)
                    return new UTF8Encoding(true);
                if (read >= 4 &&
                    buffer[0] == 0xFF &&
                    buffer[1] == 0xFE &&
                    buffer[2] == 0x00 &&
                    buffer[3] == 0x00)
                    return Encoding.UTF32;
                if (read >= 4 &&
                    buffer[0] == 0x00 &&
                    buffer[1] == 0x00 &&
                    buffer[2] == 0xFE &&
                    buffer[3] == 0xFF)
                    return new UTF32Encoding(true, true);
                if (read >= 2 &&
                    buffer[0] == 0xFF &&
                    buffer[1] == 0xFE)
                    return Encoding.Unicode;
                if (read >= 2 &&
                    buffer[0] == 0xFE &&
                    buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode;
                if (IsValidUtf8(buffer.Slice(0, read)))
                    return new UTF8Encoding(false);
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
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }
                int remaining;
                if (b >= 0xC2 && b <= 0xDF)
                {
                    remaining = 1;
                }
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
                else if (b == 0xED) 
                {
                    if (i + 2 >= data.Length ||
                        data[i + 1] < 0x80 || data[i + 1] > 0x9F ||
                        !IsContinuation(data[i + 2]))
                        return false;
                    i += 3;
                    hasMultibyte = true;
                    continue;
                }
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
    public record FileSnapshot
    {
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string FileHash { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public Dictionary<string, ProcessedResult> DataBlocks { get; init; } = new();
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
    public class ProcessedResult
    {
        public string VersionId { get; set; } = "Live";
        public string SourceFileName { get; set; } = string.Empty;
        public string RawBlockName { get; set; } = string.Empty;
        public string StandardBlockName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = "常规";
        public List<Dictionary<string, string>> Rows { get; init; } = new();
        public List<ColumnDefinition> Columns { get; init; } = new();
        public Dictionary<string, object> Metadata { get; init; } = new();
        public string OriginHash { get; set; } = string.Empty;
        public DateTime ProcessTime { get; set; } = DateTime.Now;
    }
    public record ColumnDefinition(string Key, string Header);
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\Models\ProcessingTaskContext.cs
// ----------------------------------------

namespace FileProcessor.Core.Models
{
    public record ProcessingTaskContext
    {
        public string FilePath { get; init; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public string FileHash { get; init; } = string.Empty;
        public string BoundVersionId { get; init; } = "Live";
        public DateTime TriggerTime { get; init; } = DateTime.Now;
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
    public class RawDataBlock
    {
        public string BlockName { get; }
        public string[] Lines { get; }
        public int StartLineNumber { get; }
        public string SourceFilePath { get; }
        public string FileHash { get; set; } = string.Empty; 
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
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.Core.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Debug\net10.0\FileProcessor.Core.GlobalUsings.g.cs
// ----------------------------------------

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

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Release\net10.0\FileProcessor.Core.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Core")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Core\obj\Release\net10.0\FileProcessor.Core.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


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
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\obj\Debug\net10.0\FileProcessor.DebugHelpers.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.DebugHelpers")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.DebugHelpers")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.DebugHelpers")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.DebugHelpers\obj\Debug\net10.0\FileProcessor.DebugHelpers.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\App.xaml.cs
// ----------------------------------------

using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;
namespace FileProcessor.Desktop
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
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
    ResourceDictionaryLocation.None,            
    ResourceDictionaryLocation.SourceAssembly   
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
        private ProjectMonitorService _monitor;
        private FileOrchestrator _orchestrator;
        public MainWindow()
        {
            InitializeComponent();
            var loader = new PluginLoader();
            var (templates, registry) = loader.LoadFromPluginsFolder();
            var sharedSnapshot = new SnapshotManager();
            var sharedVersionCoord = new VersionCoordinator();
            _orchestrator = new FileOrchestrator(templates, sharedSnapshot, registry, sharedVersionCoord);
            var dataCoordinator = new DataCoordinator(sharedSnapshot, sharedVersionCoord);
            _monitor = new ProjectMonitorService(_orchestrator, sharedVersionCoord);
            this.DataContext = new MainViewModel(dataCoordinator, _monitor, sharedVersionCoord);
        }
        private void DataGrid_AutoGeneratingColumn(object sender, System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dg && dg.ItemsSource is DataView dv)
            {
                var col = dv.Table.Columns[e.PropertyName];
                if (col != null && !string.IsNullOrEmpty(col.Caption))
                {
                    e.Column.Header = col.Caption; 
                }
            }
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
        public static DataTable ToDataTable(this ProcessedResult result)
        {
            var dt = new DataTable();
            if (result.Columns == null || result.Rows == null) return dt;
            foreach (var colDef in result.Columns)
            {
                DataColumn dc = new DataColumn(colDef.Key)
                {
                    Caption = colDef.Header
                };
                dt.Columns.Add(dc);
            }
            foreach (var rowDict in result.Rows)
            {
                var dr = dt.NewRow();
                foreach (var colDef in result.Columns)
                {
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
        private readonly IVersionCoordinator _versionCoordinator; 
        public MainViewModel(DataCoordinator coordinator, ProjectMonitorService monitor, IVersionCoordinator versionCoordinator)
        {
            _coordinator = coordinator;
            _monitor = monitor;
            _versionCoordinator = versionCoordinator;
            _versionCoordinator.VersionCommitted += OnVersionCommitted;
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
                StatusColor = "#F39C12"; 
                VersionHistory.Clear();
                VersionHistory.Add("LIVE 实时状态");
                await Task.Run(() => _monitor.StartScanning(path));
                StatusText = $"监控运行中: {System.IO.Path.GetFileName(path)}";
                StatusColor = "#2ECC71"; 
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动监控失败: " + ex.Message);
                StatusText = "初始化失败";
                StatusColor = "#E74C3C";
            }
        }
        private void OnVersionCommitted(string versionId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!VersionHistory.Contains(versionId))
                {
                    VersionHistory.Add(versionId);
                }
                if (versionId == "INITIAL_SCAN")
                {
                    SelectedVersion = "INITIAL_SCAN";
                }
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
            _coordinator.SlotDataChanged += (idx, data) => {
                if (idx == _index)
                {
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
            string? cachedFile = SelectedFile;
            string? cachedBlock = SelectedBlock;
            var files = _coordinator.GetAvailableFiles();
            AvailableFiles.Clear();
            foreach (var f in files) AvailableFiles.Add(f);
            if (!string.IsNullOrEmpty(cachedFile) && AvailableFiles.Contains(cachedFile))
            {
                SelectedFile = cachedFile;
                AvailableBlocks.Clear();
                var blocks = _coordinator.GetBlocksForFile(cachedFile);
                foreach (var b in blocks) AvailableBlocks.Add(b);
                if (!string.IsNullOrEmpty(cachedBlock) && AvailableBlocks.Contains(cachedBlock))
                {
                    SelectedBlock = cachedBlock;
                }
            }
            else
            {
                SelectedBlock = null;
                AvailableBlocks.Clear();
            }
        }
        partial void OnSelectedFileChanged(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                AvailableBlocks.Clear();
                SelectedBlock = null;
                return;
            }
            if (AvailableBlocks.Count == 0)
            {
                AvailableBlocks.Clear();
                foreach (var b in _coordinator.GetBlocksForFile(value)) AvailableBlocks.Add(b);
            }
        }
        partial void OnSelectedBlockChanged(string? value)
        {
            if (!string.IsNullOrEmpty(SelectedFile) && !string.IsNullOrEmpty(value))
            {
                var candidates = _coordinator.GetBlocksForFile(SelectedFile);
                _coordinator.ConfigureSlot(_index, SelectedFile, value);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\App.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\App.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "16304034DA1FC6043DD38CDAEC0065C94196350E"
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
    public partial class App : System.Windows.Application {
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            #line default
            #line hidden
        }
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
    public partial class App : System.Windows.Application {
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            #line 5 "..\..\..\App.xaml"
            this.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
            #line default
            #line hidden
        }
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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_1fdqvnxa_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_3ox5xmx1_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_3s1tvvze_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_4qwwvmci_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5dl4pxeh_wpftmp.AssemblyInfo.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5dl4pxeh_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_5r5yamcs_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_ahkzbnvj_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_axi5r1fu_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_bwsm4bru_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_calctkqc_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_dtn2ueqo_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_fvnuig1m_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_gpbdsknu_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_iu5t1y1z_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_jgsyownr_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_l2ida5si_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_l44pr0w2_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_lmd2wyer_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_okh4hg23_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_p1gjknmm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_sjtjojib_wpftmp.AssemblyInfo.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_sjtjojib_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_uqfcmdfm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_vlpuwfwk_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_xtpnab1i_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_yi5sca5t_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_yv2osfcr_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\FileProcessor.Desktop_zskhdyfm_wpftmp.GlobalUsings.g.cs
// ----------------------------------------

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

namespace XamlGeneratedNamespace {
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class GeneratedInternalTypeHelper : System.Windows.Markup.InternalTypeHelper {
        protected override object CreateInstance(System.Type type, System.Globalization.CultureInfo culture) {
            return System.Activator.CreateInstance(type, ((System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) 
                            | (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.CreateInstance)), null, null, culture);
        }
        protected override object GetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, System.Globalization.CultureInfo culture) {
            return propertyInfo.GetValue(target, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        protected override void SetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object target, object value, System.Globalization.CultureInfo culture) {
            propertyInfo.SetValue(target, value, System.Reflection.BindingFlags.Default, null, null, culture);
        }
        protected override System.Delegate CreateDelegate(System.Type delegateType, object target, string handler) {
            return ((System.Delegate)(target.GetType().InvokeMember("_CreateDelegate", (System.Reflection.BindingFlags.InvokeMethod 
                            | (System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)), null, target, new object[] {
                        delegateType,
                        handler}, null)));
        }
        protected override void AddEventHandler(System.Reflection.EventInfo eventInfo, object target, System.Delegate handler) {
            eventInfo.AddEventHandler(target, handler);
        }
    }
}


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Desktop\obj\Debug\net10.0-windows\MainWindow.g.cs
// ----------------------------------------

#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "7DF1BBC267FAAA614E52CC568A954B5A0F9770F2"
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
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        private bool _contentLoaded;
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
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        private bool _contentLoaded;
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "10.0.3.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/FileProcessor.Desktop;V1.0.0.0;component/mainwindow.xaml", System.UriKind.Relative);
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
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\Registration\ProcessorRegistry.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using System.Reflection;
namespace FileProcessor.Engine.Registration
{
    public class ProcessorRegistry
    {
        private readonly Dictionary<string, List<IBlockProcessor>> _lookup = new();
        private readonly List<IBlockProcessor> _allProcessors = new();
        public void Register(IBlockProcessor processor)
        {
            _allProcessors.Add(processor);
            var attrs = processor.GetType().GetCustomAttributes<BlockProcessorAttribute>();
            foreach (var attr in attrs)
            {
                if (!_lookup.ContainsKey(attr.BlockName))
                    _lookup[attr.BlockName] = new List<IBlockProcessor>();
                _lookup[attr.BlockName].Add(processor);
            }
        }
        public IEnumerable<IBlockProcessor> GetProcessorsForBlock(string blockName)
        {
            if (_lookup.TryGetValue(blockName, out var matched))
                return matched.OrderByDescending(p => p.Priority);
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
    public class FileOrchestrator
    {
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ISnapshotManager _snapshotManager;
        private readonly ProcessorRegistry _processorRegistry;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly ConcurrentDictionary<string, ProcessingTaskContext> _pendingTasks = new();
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
        public void BeginInitialization() => _isInitializing = true;
        public void EndInitialization()
        {
            _isInitializing = false;
            _versionCoordinator.Commit(InitialVersionId);
        }
        public void ProcessInitialFile(string filePath, string hash)
        {
            var context = new ProcessingTaskContext
            {
                FilePath = filePath,
                FileHash = hash,
                BoundVersionId = InitialVersionId, 
                TriggerTime = DateTime.Now
            };
            ExecuteSingleTask(context);
        }
        public void EnqueueTask(ProcessingTaskContext context, bool isRecording)
        {
            if (_isInitializing)
            {
                ProcessInitialFile(context.FilePath, context.FileHash);
                return;
            }
            if (isRecording)
            {
                _pendingTasks[context.FilePath] = context;
            }
            else
            {
                var liveVersionId = _versionCoordinator.GenerateLiveVersionId();
                var liveContext = context with { BoundVersionId = liveVersionId };
                ExecuteSingleTask(liveContext);
            }
        }
        public void FlushBatchTasks()
        {
            var tasks = _pendingTasks.Values.ToList();
            _pendingTasks.Clear();
            Parallel.ForEach(tasks, task =>
            {
                ExecuteSingleTask(task);
            });
        }
        private void ExecuteSingleTask(ProcessingTaskContext context)
        {
            var template = _templates.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.FileNamePattern) &&
                Regex.IsMatch(context.FileName, t.FileNamePattern));
            if (template == null) return;
            var rawBlocks = template.Parse(context.FilePath);
            foreach (var rawBlock in rawBlocks)
            {
                rawBlock.FileHash = context.FileHash;
                var processors = _processorRegistry.GetProcessorsForBlock(rawBlock.BlockName);
                foreach (var processor in processors)
                {
                    try
                    {
                        var result = processor.Process(rawBlock);
                        if (result != null)
                        {
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
    public class PluginLoader
    {
        public (List<IFileTemplate> Templates, ProcessorRegistry Registry) LoadFromPluginsFolder()
        {
            var templates = new List<IFileTemplate>();
            var registry = new ProcessorRegistry();
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
        private (List<IFileTemplate> Templates, List<IBlockProcessor> Processors) ScanAssembly(Assembly assembly)
        {
            var tList = new List<IFileTemplate>();
            var pList = new List<IBlockProcessor>();
            var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);
            foreach (var type in types)
            {
                if (typeof(IFileTemplate).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<FileProcessorPluginAttribute>() != null)
                {
                    if (Activator.CreateInstance(type) is IFileTemplate template)
                        tList.Add(template);
                }
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
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Debug\net10.0\FileProcessor.Engine.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Debug\net10.0\FileProcessor.Engine.GlobalUsings.g.cs
// ----------------------------------------

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

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Release\net10.0\FileProcessor.Engine.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Engine")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Engine\obj\Release\net10.0\FileProcessor.Engine.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


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
    public class ProjectMonitorService
    {
        private readonly FileOrchestrator _orchestrator;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly ConcurrentDictionary<string, string> _fileHashCache = new();
        private FileSystemWatcher? _watcher;
        public ProjectMonitorService(FileOrchestrator orchestrator, IVersionCoordinator versionCoordinator)
        {
            _orchestrator = orchestrator;
            _versionCoordinator = versionCoordinator;
        }
        public void StartScanning(string path)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                _orchestrator.BeginInitialization();
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string currentHash = CalculateFileHash(file);
                    _fileHashCache[file] = currentHash;
                    _orchestrator.ProcessInitialFile(file, currentHash);
                }
                _orchestrator.EndInitialization();
                SetupWatcher(path);
            }
            catch (Exception ex)
            {
                Log.Debug($"[ProjectMonitorService] 项目启动失败: {ex.Message}");
                _orchestrator.EndInitialization();
            }
            Log.Debug($"[ProjectMonitorService] 智能监控已就绪: {path}");
        }
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
        private void InitialScan(string rootPath)
        {
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
        private void HandleEvent(string fullPath)
        {
            if (!File.Exists(fullPath)) return;
            try
            {
                string fileName = Path.GetFileName(fullPath).ToLower();
                string dirName = Path.GetDirectoryName(fullPath) ?? "";
                if (dirName.EndsWith("设计结果", StringComparison.OrdinalIgnoreCase))
                {
                    if (fileName == "check.out")
                    {
                        _versionCoordinator.StartNewBatch(fileName);
                        return; 
                    }
                    if (fileName == "mainjss.out")
                    {
                        _versionCoordinator.CommitCurrentBatch(fileName);
                        return; 
                    }
                }
                string currentHash = CalculateFileHash(fullPath);
                if (_fileHashCache.TryGetValue(fullPath, out string? lastHash) && lastHash == currentHash)
                {
                    Log.Debug($"[ProjectMonitorService] 文件Hash未改变，FullPath: {fullPath}");
                    return;
                }
                _fileHashCache[fullPath] = currentHash; 
                var context = new ProcessingTaskContext
                {
                    FilePath = fullPath,
                    FileHash = currentHash, 
                    BoundVersionId = _versionCoordinator.CurrentVersionId,
                    TriggerTime = DateTime.Now
                };
                _orchestrator.EnqueueTask(context, _versionCoordinator.IsRecording);
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                Log.Debug($"[ProjectMonitorService] 处理文件 {Path.GetFileName(fullPath)} 时异常: {ex.Message}");
            }
        }
        private string CalculateFileHash(string filePath)
        {
            using var sha = SHA256.Create();
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
    public class SnapshotManager : ISnapshotManager
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<ProcessedResult>>> _storage = new();
        public void AddSnapshot(ProcessedResult result)
        {
            if (result == null) return;
            var versionContainer = _storage.GetOrAdd(result.VersionId, _ => new ConcurrentDictionary<string, List<ProcessedResult>>());
            var fileResults = versionContainer.GetOrAdd(result.SourceFileName, _ => new List<ProcessedResult>());
            lock (fileResults)
            {
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
        public IEnumerable<string> GetFileNames(string versionId)
        {
            if (_storage.TryGetValue(versionId, out var versionContainer))
            {
                return versionContainer.Keys;
            }
            return Enumerable.Empty<string>();
        }
        public IEnumerable<ProcessedResult> GetResultsByFile(string versionId, string fileName)
        {
            if (_storage.TryGetValue(versionId, out var versionContainer))
            {
                if (versionContainer.TryGetValue(fileName, out var results))
                {
                    lock (results)
                    {
                        return results.ToList(); 
                    }
                }
            }
            return Enumerable.Empty<ProcessedResult>();
        }
        public ProcessedResult? GetSpecificBlock(string versionId, string fileName, string standardBlockName)
        {
            return GetResultsByFile(versionId, fileName)
                .FirstOrDefault(r => r.StandardBlockName == standardBlockName);
        }
        public IEnumerable<ProcessedResult> GetHistory(string standardBlockName)
        {
            var history = new List<ProcessedResult>();
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
    public class VersionCoordinator : IVersionCoordinator
    {
        private string _activeBatchId = "Live";
        private bool _isRecording = false;
        public string CurrentVersionId => _isRecording ? _activeBatchId : "Live";
        public bool IsRecording => _isRecording;
        public event Action<string>? VersionCommitted;
        public event Action<string, string>? LiveUpdateProcessed;
        public void Commit(string versionId)
        {
            Console.WriteLine($"[Coordinator] 显式提交版本: {versionId}");
            Log.Debug($"[Coordinator] 显式提交版本: {versionId}");
            VersionCommitted?.Invoke(versionId);
        }
        public void StartNewBatch(string source)
        {
            _isRecording = true;
            _activeBatchId = $"Batch_{DateTime.Now:yyyyMMdd_HHmmss}";
            Console.WriteLine($"[Coordinator] 检测到 check.out，批次录制开始: {_activeBatchId}");
            Log.Debug($"[Coordinator] 检测到 check.out，批次录制开始: {_activeBatchId}");
        }
        public void CommitCurrentBatch(string source)
        {
            if (!_isRecording) return;
            string completedVersionId = _activeBatchId;
            _isRecording = false;
            _activeBatchId = "Live";
            Console.WriteLine($"[Coordinator] 检测到 mainjss.out，批次闭环: {completedVersionId}");
            Log.Debug($"[Coordinator] 检测到 mainjss.out，批次闭环: {completedVersionId}");
            Commit(completedVersionId);
        }
        public string GenerateLiveVersionId()
        {
            Log.Debug($"Live_{DateTime.Now:yyyyMMdd_HHmmss}");
            return $"Live_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Debug\net10.0\FileProcessor.Infrastructure.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Debug\net10.0\FileProcessor.Infrastructure.GlobalUsings.g.cs
// ----------------------------------------

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

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Release\net10.0\FileProcessor.Infrastructure.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Infrastructure")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Infrastructure\obj\Release\net10.0\FileProcessor.Infrastructure.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


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
    public class DataCoordinator
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IVersionCoordinator _versionCoordinator;
        private readonly List<SlotConfiguration> _slots = new();
        private string _lastAutoVersionId;
        public string ActiveViewVersionId { get; private set; }
        public bool IsFollowingLive => ActiveViewVersionId == _lastAutoVersionId;
        public event Action<int, ProcessedResult?>? SlotDataChanged;
        public event Action<int, SlotStatus>? SlotStatusChanged;
        public DataCoordinator(ISnapshotManager snapshotManager, IVersionCoordinator versionCoordinator)
        {
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
            _versionCoordinator = versionCoordinator ?? throw new ArgumentNullException(nameof(versionCoordinator));
            for (int i = 0; i < 8; i++) _slots.Add(new SlotConfiguration { SlotIndex = i });
            ActiveViewVersionId = _versionCoordinator.CurrentVersionId;
            _lastAutoVersionId = ActiveViewVersionId;
            _versionCoordinator.VersionCommitted += OnVersionCommitted;
            _versionCoordinator.LiveUpdateProcessed += OnLiveUpdateProcessed;
        }
        #region UI 交互接口 (Commands)
        public void ConfigureSlot(int index, string fileName, string standardBlockName)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            slot.TargetFileName = fileName;
            slot.TargetBlockName = standardBlockName;
            RefreshSlot(index);
        }
        public void SwitchViewVersion(string versionId)
        {
            if (versionId == "LIVE 实时状态")
            {
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
                    versionId = _versionCoordinator.CurrentVersionId;
                }
            }
            if (string.IsNullOrEmpty(versionId) || ActiveViewVersionId == versionId) return;
            ActiveViewVersionId = versionId;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive) RefreshSlot(i);
            }
        }
        public void SetTowerFilter(int index, string towerId)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].CurrentTower = towerId;
            RefreshSlot(index);
        }
        public void RefreshSlot(int index)
        {
            System.Diagnostics.Trace.WriteLine($"==== [Slot {index}] 进入刷新逻辑 ====");
            var slot = _slots[index];
            System.Diagnostics.Debug.WriteLine($"[Slot {index}] 尝试刷新. 文件: {slot.TargetFileName}, 块: {slot.TargetBlockName}, 版本: {ActiveViewVersionId}");
            if (!slot.IsActive) return;
            UpdateStatus(index, SlotStatus.Loading);
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
            var filteredData = ApplyTowerFilter(rawData, slot.CurrentTower);
            UpdateStatus(index, SlotStatus.Ready);
            SlotDataChanged?.Invoke(index, filteredData);
        }
        #endregion
        #region 元数据发现接口 (Query)
        public IEnumerable<string> GetAvailableFiles()
        {
            var versionToQuery = ActiveViewVersionId ?? _lastAutoVersionId;
            if (string.IsNullOrEmpty(versionToQuery)) return Enumerable.Empty<string>();
            return _snapshotManager.GetFileNames(versionToQuery);
        }
        public IEnumerable<string> GetBlocksForFile(string fileName)
        {
            var results = _snapshotManager.GetResultsByFile(ActiveViewVersionId, fileName);
            return results.Select(r => !string.IsNullOrEmpty(r.DisplayName) ? r.DisplayName : r.RawBlockName).Distinct();
        }
        public IEnumerable<ProcessedResult> GetHistoryTrend(string standardBlockName)
        {
            return _snapshotManager.GetHistory(standardBlockName);
        }
        public SlotConfiguration GetSlotInfo(int index) => _slots[index];
        #endregion
        #region 私有辅助逻辑
        private void OnVersionCommitted(string versionId)
        {
            bool shouldFollow = (ActiveViewVersionId == _lastAutoVersionId);
            _lastAutoVersionId = versionId;
            if (shouldFollow)
            {
                ActiveViewVersionId = versionId;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].IsActive) RefreshSlot(i);
                }
            }
        }
        private void OnLiveUpdateProcessed(string versionId, string fileName)
        {
            if (!IsFollowingLive && ActiveViewVersionId != "Live") return;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsActive && string.Equals(slot.TargetFileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    RefreshSlot(i);
                }
            }
        }
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
                Rows = original.Rows.Where(r => r.TryGetValue("Tower", out var v) && v == towerId).ToList(),
                Metadata = original.Metadata
            };
        }
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
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\Models\SlotConfiguration.cs
// ----------------------------------------

using FileProcessor.Core.Models;
using System;
namespace FileProcessor.Mediator.Models
{
    public enum SlotStatus
    {
        Empty,      
        Loading,    
        Ready,      
        NoData,     
        Error       
    }
    public class SlotConfiguration
    {
        public int SlotIndex { get; set; }
        public string? TargetFileName { get; set; }
        public string? TargetBlockName { get; set; }
        public string CurrentTower { get; set; } = "All";
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
    public class ExportService
    {
        private readonly ISnapshotManager _snapshotManager;
        public ExportService(ISnapshotManager snapshotManager)
        {
            _snapshotManager = snapshotManager;
        }
        public void ExportCrossVersionReport(string blockName, string towerId, bool includeLive)
        {
            var history = _snapshotManager.GetHistory(blockName);
            if (!includeLive)
            {
                history = history.Where(r => !r.VersionId.StartsWith("Live_"));
            }
            if (towerId != "All")
            {
                history = history.Where(r =>
                    r.Rows.Any(row => row.TryGetValue("Tower", out var t) && t == towerId));
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\obj\Debug\net10.0\FileProcessor.Mediator.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("FileProcessor.Mediator")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("FileProcessor.Mediator")]
[assembly: System.Reflection.AssemblyTitleAttribute("FileProcessor.Mediator")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\FileProcessor.Mediator\obj\Debug\net10.0\FileProcessor.Mediator.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\WDispProcessor.cs
// ----------------------------------------

using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Text.RegularExpressions;
namespace WDisp.Plugin
{
    [BlockProcessor("位移输出")]
    public class WDispProcessor : IBlockProcessor
    {
        public string TargetBlockName => "位移输出";
        public int Priority => 10;
        public string StandardBlockName => "WDisp_DisplacementData";
        public string DefaultCategory => "位移信息";
        public bool CanProcess(string blockName) => blockName.Contains("位移");
        public ProcessedResult Process(RawDataBlock block)
        {
            var rows = new List<Dictionary<string, string>>();
            var allColumns = new HashSet<string> { "Floor", "Tower" };
            var lines = block.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("---") && !l.Contains("***"))
                .ToList();
            if (lines.Count == 0) return null;
            int stride = 0;
            var headerLines = new List<string>();
            while (stride < lines.Count && !Regex.IsMatch(lines[stride].Trim(), @"^\d+"))
            {
                headerLines.Add(lines[stride]);
                stride++;
            }
            if (stride == 0 || stride >= lines.Count) return null;
            var headerGroups = BuildHeaderGroups(headerLines);
            foreach (var group in headerGroups)
                foreach (var col in group) allColumns.Add(col);
            string lastFloor = "1";
            string lastTower = "1";
            for (int i = stride; i + stride <= lines.Count; i += stride)
            {
                var logicalRow = new Dictionary<string, string>();
                for (int s = 0; s < stride; s++)
                {
                    string currentLine = lines[i + s];
                    int indent = GetIndentCount(currentLine); 
                    var parts = Regex.Split(currentLine.Trim(), @"\s+").ToList();
                    if (parts.Count == 0) continue;
                    if (s == 0)
                    {
                        if (indent < 8 && Regex.IsMatch(parts[0], @"^\d+$"))
                        {
                            lastFloor = parts[0];
                            if (parts.Count > 1 && parts[1].Length < 4 && Regex.IsMatch(parts[1], @"^\d+$"))
                            {
                                lastTower = parts[1];
                            }
                        }
                        logicalRow["Floor"] = lastFloor;
                        logicalRow["Tower"] = lastTower;
                        int headerOffset = (indent > 8) ? 2 : 0;
                        MapPartsToHeaders(parts, headerOffset, headerGroups[s], logicalRow);
                    }
                    else
                    {
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
    [FileProcessorPlugin(@"wdisp.out", "设计结果")]
    public class WDispTemplate : BaseFileTemplate
    {
        protected override IEnumerable<RawDataBlock> SplitBlocks(List<string> lines, string filePath)
        {
            string currentTitle = "文件头部";
            List<string> currentBlockLines = new List<string>();
            int startLineNumber = 1;
            string marker = "===";
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (line.Contains(marker))
                {
                    if (currentBlockLines.Count > 0)
                    {
                        yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                    }
                    currentTitle = line.Replace(marker, "").Trim();
                    currentBlockLines = new List<string>();
                    startLineNumber = i + 1;
                    continue;
                }
                currentBlockLines.Add(line);
            }
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\obj\Debug\net10.0\WDisp.Plugin.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("WDisp.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("WDisp.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("WDisp.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WDisp.Plugin\obj\Debug\net10.0\WDisp.Plugin.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;


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
        public string TargetBlockName => "各层刚心、偏心率、相邻层侧移刚度比等计算信息";
        public int Priority => 10;
        public string StandardBlockName => "WMass_StiffnessAndCentroid";
        public string DefaultCategory => "计算信息";
        public bool CanProcess(string blockName) => blockName.Contains("刚心");
        public ProcessedResult Process(RawDataBlock block)
        {
            var result = new ProcessedResult
            {
                RawBlockName = block.BlockName,
                StandardBlockName = this.StandardBlockName,
                DisplayName = "各层刚心及刚度比",
                Category = this.DefaultCategory,
                SourceFileName = "wmass.out" 
            };
            var floorRecords = new List<Dictionary<string, string>>();
            var discoveredKeys = new HashSet<string>(); 
            Dictionary<string, string>? currentFloorRow = null;
            var summaryData = new List<Dictionary<string, string>>();
            foreach (var rawLine in block.Lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.Contains("----"))
                    continue;
                if (line.Contains("Floor No."))
                {
                    currentFloorRow = new Dictionary<string, string>();
                    floorRecords.Add(currentFloorRow);
                    var floorMatch = Regex.Match(line, @"Floor No\.\s*(?<v>\d+)");
                    var towerMatch = Regex.Match(line, @"Tower No\.\s*(?<v>\d+)");
                    if (floorMatch.Success) currentFloorRow["Floor"] = floorMatch.Groups["v"].Value;
                    if (towerMatch.Success) currentFloorRow["Tower"] = towerMatch.Groups["v"].Value;
                    discoveredKeys.Add("Floor");
                    discoveredKeys.Add("Tower");
                    continue;
                }
                if (currentFloorRow != null)
                {
                    var matches = Regex.Matches(line, @"(?<key>[A-Za-z0-9&]+)\s*=\s*(?<value>[^ \t]+)");
                    foreach (Match m in matches)
                    {
                        string key = m.Groups["key"].Value.Trim();
                        string val = m.Groups["value"].Value.Trim();
                        val = Regex.Replace(val, @"\(.*?\)", "");
                        currentFloorRow[key] = val;
                        discoveredKeys.Add(key);
                    }
                }
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
            foreach (var key in discoveredKeys)
            {
                result.Columns.Add(new ColumnDefinition(key, key));
            }
            result.Rows.AddRange(floorRecords);
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
    [FileProcessorPlugin(@"wmass.out", "设计结果")]
    public class WMassTemplate : BaseFileTemplate
    {
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
                    int closingIndex = -1;
                    for (int j = i + 1; j < lines.Count && j < i + 20; j++)
                    {
                        if (majorMarkers.Any(m => lines[j].Contains(m)))
                        {
                            closingIndex = j;
                            break;
                        }
                    }
                    if (closingIndex > i + 1)
                    {
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
                            if (currentBlockLines.Count > 0)
                            {
                                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
                            }
                            currentTitle = headerRegion[0];
                            currentBlockLines = new List<string>();
                            if (headerRegion.Count > 1)
                            {
                                currentBlockLines.AddRange(headerRegion.Skip(1));
                            }
                            startLineNumber = closingIndex + 1;
                            i = closingIndex;
                            continue;
                        }
                    }
                }
                currentBlockLines.Add(line);
            }
            if (currentBlockLines.Count > 0)
            {
                yield return new RawDataBlock(currentTitle, currentBlockLines.ToArray(), startLineNumber, filePath);
            }
        }
    }
}

// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("WMass")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("WMass")]
[assembly: System.Reflection.AssemblyTitleAttribute("WMass")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.GlobalUsings.g.cs
// ----------------------------------------

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

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+0a8e3eb4a4dd6a919cad1a68ea470184e3a09fae")]
[assembly: System.Reflection.AssemblyProductAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Debug\net10.0\WMass.Plugin.GlobalUsings.g.cs
// ----------------------------------------

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

using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Release\net10.0\WMass.Plugin.AssemblyInfo.cs
// ----------------------------------------

using System;
using System.Reflection;
[assembly: System.Reflection.AssemblyCompanyAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+85461cd3a4b7437568d6c1d38870a653bd654186")]
[assembly: System.Reflection.AssemblyProductAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("WMass.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]


// ----------------------------------------
// 文件: D:\3.Source\FileProcessor\WMass.Plugin\obj\Release\net10.0\WMass.Plugin.GlobalUsings.g.cs
// ----------------------------------------

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

