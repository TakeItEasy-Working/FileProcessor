using FileProcessor.Core.Models;
using System;

namespace FileProcessor.Mediator.Models
{
    /// <summary>
    /// 插槽状态枚举
    /// </summary>
    public enum SlotStatus
    {
        Empty,      // 未配置
        Loading,    // 正在解析中
        Ready,      // 数据就绪
        NoData,     // 已配置但未找到匹配数据
        Error       // 解析异常
    }

    /// <summary>
    /// 增强型插槽配置：存储 UI 状态与过滤参数
    /// </summary>
    public class SlotConfiguration
    {
        public int SlotIndex { get; set; }
        public string? TargetFileName { get; set; }
        public string? TargetBlockName { get; set; }

        /// <summary>
        /// 当前插槽选中的塔号（"All" 或具体数字）
        /// </summary>
        public string CurrentTower { get; set; } = "All";

        /// <summary>
        /// 当前插槽的业务状态
        /// </summary>
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