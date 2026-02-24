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

        // --- 状态恢复防抖锁 ---
        private bool _isRestoringState = false;

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

        /// <summary>
        /// 刷新可用文件列表，并在刷新前后保持用户的选择状态（记忆功能）。
        /// 核心修复：通过强制置空打断 MVVM 相同值不通知的拦截机制，确保 ComboBox 视图同步。
        /// </summary>
        public void RefreshFileList()
        {
            // 开启防抖锁：防止在强制置空和恢复时，触发 OnSelectedFileChanged 导致不必要的联动
            _isRestoringState = true;
            try
            {
                // 1. 【记忆】暂存当前用户的选择状态
                string? cachedFile = SelectedFile;
                string? cachedBlock = SelectedBlock;

                // ---------------------------------------------------------
                // 【核心修复点】强制置空
                // 必须在这里置为 null，否则后续恢复相同字符串时，MVVM 框架
                // 会因为“值未改变”而拒绝通知 UI，导致 ComboBox 选项视觉丢失。
                // ---------------------------------------------------------
                SelectedFile = null;
                SelectedBlock = null;

                // 2. 刷新文件下拉列表
                AvailableFiles.Clear();
                foreach (var f in _coordinator.GetAvailableFiles())
                {
                    AvailableFiles.Add(f);
                }

                // 3. 【恢复】检查刚才选的文件是否在新列表中
                if (!string.IsNullOrEmpty(cachedFile) && AvailableFiles.Contains(cachedFile))
                {
                    // 此时从 null 变回有值，必定触发 PropertyChanged，UI 下拉框会立刻选中此项
                    SelectedFile = cachedFile;

                    // 4. 【级联恢复】手动重新加载该文件对应的可用数据块列表
                    AvailableBlocks.Clear();
                    foreach (var b in _coordinator.GetBlocksForFile(cachedFile))
                    {
                        AvailableBlocks.Add(b);
                    }

                    // 检查刚才选的数据块是否也在新列表中
                    if (!string.IsNullOrEmpty(cachedBlock) && AvailableBlocks.Contains(cachedBlock))
                    {
                        // 强制唤醒第二个 ComboBox
                        SelectedBlock = cachedBlock;
                    }
                }
                else
                {
                    // 如果文件真的被删除了，或者之前没有选择，保持清空状态
                    AvailableBlocks.Clear();
                }
            }
            finally
            {
                // 无论是否报错，强制释放防抖锁，恢复正常的 UI 事件响应
                _isRestoringState = false;
            }
        }

        /// <summary>
        /// 当用户在 UI 上手动更改选中的文件时触发。
        /// 负责清空旧数据块并加载新文件的数据块列表。
        /// </summary>
        /// <param name="value">新选中的文件名</param>
        partial void OnSelectedFileChanged(string? value)
        {
            // 【保护机制】如果当前正在执行记忆恢复逻辑，则跳过默认的清理动作
            // 数据的级联恢复交由 RefreshFileList 方法手动精确控制
            if (_isRestoringState) return;

            // 如果是用户手动切换文件，重置次级选项
            AvailableBlocks.Clear();
            SelectedBlock = null;

            if (string.IsNullOrEmpty(value)) return;

            // 加载新文件对应的可用数据块
            foreach (var b in _coordinator.GetBlocksForFile(value))
            {
                AvailableBlocks.Add(b);
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