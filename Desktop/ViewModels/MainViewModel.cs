//using System;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Windows.Data;
//using Desktop.Services;
//using FileProcessor.Core.Models;

//namespace Desktop.ViewModels
//{
//    /// <summary>
//    /// 主界面 ViewModel：负责协调 UI 状态与用户交互逻辑。
//    /// </summary>
//    public class MainViewModel : INotifyPropertyChanged
//    {
//        private readonly IUiStateService _uiStateService;

//        private string? _selectedVersion;
//        private string? _selectedFile;
//        private string? _selectedBlock;
//        private ProcessedResult? _currentResult;

//        /// <summary>
//        /// 构造函数，注入 UI 状态服务
//        /// </summary>
//        public MainViewModel(IUiStateService uiStateService)
//        {
//            _uiStateService = uiStateService;

//            // 监听数据更新事件，实现“自动跟随”最新版本
//            _uiStateService.DataUpdated += OnDataUpdated;

//            // 初始选中第一个可用的版本（如果有）
//            SelectedVersion = _uiStateService.AllVersionIds.FirstOrDefault();
//        }

//        #region 数据绑定属性

//        /// <summary>
//        /// 版本下拉列表数据源
//        /// </summary>
//        public ObservableCollection<string> VersionIds => _uiStateService.AllVersionIds;

//        /// <summary>
//        /// 文件下拉列表数据源
//        /// </summary>
//        public ObservableCollection<string> FileNames => _uiStateService.AvailableFiles;

//        /// <summary>
//        /// 数据块下拉列表数据源
//        /// </summary>
//        public ObservableCollection<string> BlockNames => _uiStateService.AvailableBlocks;

//        /// <summary>
//        /// 当前选中的版本
//        /// </summary>
//        public string? SelectedVersion
//        {
//            get => _selectedVersion;
//            set
//            {
//                if (SetProperty(ref _selectedVersion, value))
//                    RefreshData();
//            }
//        }

//        /// <summary>
//        /// 当前选中的文件
//        /// </summary>
//        public string? SelectedFile
//        {
//            get => _selectedFile;
//            set
//            {
//                if (SetProperty(ref _selectedFile, value))
//                    RefreshData();
//            }
//        }

//        /// <summary>
//        /// 当前选中的数据块
//        /// </summary>
//        public string? SelectedBlock
//        {
//            get => _selectedBlock;
//            set
//            {
//                if (SetProperty(ref _selectedBlock, value))
//                    RefreshData();
//            }
//        }

//        /// <summary>
//        /// 当前需要展示的解析结果对象
//        /// UI 中的 DataGrid 应绑定此对象的 Rows 和 Columns
//        /// </summary>
//        public ProcessedResult? CurrentResult
//        {
//            get => _currentResult;
//            private set => SetProperty(ref _currentResult, value);
//        }

//        #endregion

//        /// <summary>
//        /// 核心业务逻辑：根据当前选中的三维坐标，从服务中提取数据
//        /// </summary>
//        private void RefreshData()
//        {
//            if (string.IsNullOrEmpty(SelectedVersion) ||
//                string.IsNullOrEmpty(SelectedFile) ||
//                string.IsNullOrEmpty(SelectedBlock))
//            {
//                CurrentResult = null;
//                return;
//            }

//            // 获取最新处理结果
//            CurrentResult = _uiStateService.GetResult(SelectedVersion, SelectedFile, SelectedBlock);
//        }

//        /// <summary>
//        /// 当内核提交新版本时的回调
//        /// </summary>
//        private void OnDataUpdated(string newVersionId)
//        {
//            // 如果用户当前没有手动锁定某个历史版本，则自动切换到最新版本
//            // 逻辑：如果当前选中的是集合中的最后一个，则认为处于“跟随模式”
//            SelectedVersion = newVersionId;
//        }

//        #region INotifyPropertyChanged 实现

//        public event PropertyChangedEventHandler? PropertyChanged;

//        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
//        {
//            if (Equals(storage, value)) return false;
//            storage = value;
//            OnPropertyChanged(propertyName);
//            return true;
//        }

//        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        #endregion
//    }
//}