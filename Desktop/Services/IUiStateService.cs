using System;
using System.Collections.ObjectModel;
using FileProcessor.Core.Models;
using Desktop.Models;

namespace Desktop.Services
{
    /// <summary>
    /// UI 状态服务接口：管理版本历史、文件列表及备注信息。
    /// </summary>
    public interface IUiStateService
    {
        /// <summary>
        /// 带有备注和编号的版本历史列表
        /// </summary>
        ObservableCollection<UiVersionMetadata> VersionHistory { get; }

        /// <summary>
        /// 当前监控目录下发现的所有文件名列表
        /// </summary>
        ObservableCollection<string> AvailableFiles { get; }

        /// <summary>
        /// 系统支持的所有数据块（卡片类型）列表
        /// </summary>
        ObservableCollection<string> AvailableBlocks { get; }

        /// <summary>
        /// 设置并启动项目路径监控
        /// </summary>
        void SetProjectPath(string path);

        /// <summary>
        /// 获取特定版本下，某个文件中的某个数据块结果
        /// </summary>
        ProcessedResult? GetResult(string versionId, string fileName, string blockName);

        /// <summary>
        /// 当新版本产生或数据更新时触发
        /// </summary>
        event Action<string> DataUpdated;
    }
}