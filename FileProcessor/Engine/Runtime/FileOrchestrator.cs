using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileProcessor.Engine.Runtime
{
    /// <summary>
    /// 文件解析编排器：核心 DLL 的执行中枢。
    /// 协调 Template 的切块与 Registry 的处理器分配。
    /// </summary>
    public class FileOrchestrator
    {
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ISnapshotManager _snapshotManager;
        private readonly ProcessorRegistry _processorRegistry;

        /// <summary>
        /// 获取当前引擎加载的所有文件模板
        /// </summary>
        public IEnumerable<IFileTemplate> Templates => _templates;

        public FileOrchestrator(
            IEnumerable<IFileTemplate> templates,
            ISnapshotManager snapshotManager,
            ProcessorRegistry processorRegistry)
        {
            _templates = templates;
            _snapshotManager = snapshotManager;
            _processorRegistry = processorRegistry;
        }

        /// <summary>
        /// 自动匹配模板并处理指定路径的文件
        /// </summary>
        public void ProcessFile(string filePath, string versionId)
        {
            var template = GetBestTemplate(filePath);
            if (template != null)
            {
                ProcessFile(filePath, versionId, template);
            }
        }

        /// <summary>
        /// 使用指定模板处理文件（高性能模式，跳过匹配逻辑）
        /// </summary>
        public void ProcessFile(string filePath, string versionId, IFileTemplate template)
        {
            if (!File.Exists(filePath)) return;

            // 调用 BaseFileTemplate 标准解析流程（含自动编码探测与哈希注入）
            var rawBlocks = template.Parse(filePath);

            foreach (var rawBlock in rawBlocks)
            {
                // 从注册表获取匹配此数据块的所有处理器
                var processors = _processorRegistry.GetProcessorsForBlock(rawBlock.BlockName);

                foreach (var processor in processors)
                {
                    try
                    {
                        var result = processor.Process(rawBlock);

                        // 注入版本信息与预计算的哈希值
                        result.VersionId = versionId;
                        result.OriginHash = rawBlock.FileHash;

                        // 存入快照管理器
                        _snapshotManager.AddSnapshot(result);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Orchestrator] 处理器 {processor.GetType().Name} 处理失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 根据文件名正则匹配最佳模板
        /// </summary>
        public IFileTemplate? GetBestTemplate(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return _templates.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.FileNamePattern) &&
                Regex.IsMatch(fileName, t.FileNamePattern));
        }
    }
}