using System;
using System.Collections.Generic;

namespace FileProcessor.Core.Models
{
    /// <summary>
    /// 标准化处理结果：承载解析后的表格数据及业务元数据。
    /// 改为 class 以优化大数据量下的集合操作性能。
    /// </summary>
    public class ProcessedResult
    {
        // === 1. 身份与索引 (Identity & Indexing) ===

        /// <summary>
        /// 关联的版本 ID（如 Batch_20231027_1000 或 Live）
        /// </summary>
        public string VersionId { get; set; } = "Live";

        /// <summary>
        /// 数据源文件名（如 wmass.out），用于 UI 第一级选择
        /// </summary>
        public string SourceFileName { get; set; } = string.Empty;

        /// <summary>
        /// 原始块名（保留源文件中的原始标题，用于溯源）
        /// </summary>
        public string RawBlockName { get; set; } = string.Empty;

        /// <summary>
        /// 标准化块标识（如 StandardBlock_Stiffness），用于跨软件对比
        /// </summary>
        public string StandardBlockName { get; set; } = string.Empty;

        // === 2. UI 展示元数据 (UI Metadata) ===

        /// <summary>
        /// 友好显示名（用于 UI 第二级选择，如“各层刚度信息”）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 业务分类（如“位移结果”、“刚度信息”），用于 UI 侧边栏自动分组
        /// </summary>
        public string Category { get; set; } = "常规";

        // === 3. 核心数据体 (Core Data) ===

        /// <summary>
        /// 表格化数据行：Key 使用标准 ID（如 Floor, Tower, DriftRatioX）
        /// </summary>
        public List<Dictionary<string, string>> Rows { get; init; } = new();

        /// <summary>
        /// 列定义：定义标准 ID 对应的 UI 显示标题和排序规则
        /// </summary>
        public List<ColumnDefinition> Columns { get; init; } = new();

        /// <summary>
        /// 扩展元数据：存储非表格化的摘要信息或原始指纹
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();

        // === 4. 审计信息 (Audit) ===

        /// <summary>
        /// 原始文件的哈希值，确保数据的确定性
        /// </summary>
        public string OriginHash { get; set; } = string.Empty;

        /// <summary>
        /// 解析完成的时间戳（用于审计及解决并发冲突）
        /// </summary>
        public DateTime ProcessTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 列元数据定义
    /// </summary>
    public record ColumnDefinition(string Key, string Header);
}