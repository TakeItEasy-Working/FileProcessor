using System;

namespace FileProcessor.Mediator.Models
{
    /// <summary>
    /// 插槽配置模型：定义 UI 上 8 个展示位各自“关注”的数据源。
    /// </summary>
    public class SlotConfiguration
    {
        /// <summary>
        /// 插槽索引（0-7）
        /// </summary>
        public int SlotIndex { get; set; }

        /// <summary>
        /// 目标文件名（例如：wmass.out）
        /// </summary>
        public string? TargetFileName { get; set; }

        /// <summary>
        /// 目标标准化数据块 ID（例如：WMass_StiffnessAndCentroid）
        /// </summary>
        public string? TargetBlockName { get; set; }

        /// <summary>
        /// 获取该插槽是否已配置了有效的数据源
        /// </summary>
        public bool IsActive => !string.IsNullOrEmpty(TargetFileName) && !string.IsNullOrEmpty(TargetBlockName);

        /// <summary>
        /// 重置插槽配置为空
        /// </summary>
        public void Clear()
        {
            TargetFileName = null;
            TargetBlockName = null;
        }
    }
}