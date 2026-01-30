using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Engine.Runtime
{
    public class FileOrchestrator
    {
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ProcessorRegistry _processorRegistry;

        public FileOrchestrator(IEnumerable<IFileTemplate> templates, ProcessorRegistry registry)
        {
            _templates = templates;
            _processorRegistry = registry;
        }

        public FileSnapshot? ProcessFile(string filePath)
        {
            // 1. 匹配模板 (高内聚：由文件名决定解析方式)
            var fileName = Path.GetFileName(filePath);
            var template = _templates.FirstOrDefault(t =>
                System.Text.RegularExpressions.Regex.IsMatch(fileName, t.FileNamePattern));

            if (template == null) return null;

            // 2. 解析块
            var rawBlocks = TextParser.Parse(filePath, template);
            var processedData = new Dictionary<string, object>();


            // 3. 分发处理 (低耦合：跨文件共用处理器)
            foreach (var rawBlock in rawBlocks)
            {
                var processor = _processorRegistry.FindProcessor(rawBlock.BlockName);
                if (processor != null)
                {
                    var result = processor.Process(rawBlock);
                    // 存储处理后的数据，Key 使用块名（或根据需求增加唯一性）
                    processedData[rawBlock.BlockName] = result;
                }
            }


            // 4. 生成快照
            return new FileSnapshot(
                Guid.NewGuid(),
                fileName,
                DateTime.Now,
                new FileInfo(filePath).Length, // 简化版 Hash，实际建议用 MD5
                processedData
            );
        }
    }
}
