using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using System.Text.RegularExpressions;

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
            var fileName = Path.GetFileName(filePath);

            // 1. 匹配文件模板 使用不区分大小写的匹配，避免大小写导致不能匹配
            var template = _templates.FirstOrDefault(t =>
                Regex.IsMatch(fileName, t.FileNamePattern, RegexOptions.IgnoreCase));

            if (template == null)
            {
                Console.WriteLine($"[Debug] 未匹配到模板，跳过解析。请确认文件名是否满足模板模式。");
                return null;
            }

            // 2. 调用插件基类 Parse (内部已处理流读取和 FileHash)
            // 假设 BaseFileTemplate 现在返回 RawDataBlock 的同时包含 Hash
            var rawBlocks = template.Parse(filePath).ToList();
            if (!rawBlocks.Any()) return null;

            var snapshot = new FileSnapshot
            {
                FileName = fileName,
                FilePath = filePath,
                FileHash = rawBlocks.First().FileHash, // 从块元数据中获取指纹
                Timestamp = DateTime.Now
            };

            // 3. 核心加工逻辑：将 Raw 转为 Processed
            foreach (var rawBlock in rawBlocks)
            {
                // 根据新接口逻辑：先找匹配的处理器，再按优先级排序
                var processor = _processorRegistry
                    .GetProcessorsForBlock(rawBlock.BlockName)
                    .OrderByDescending(p => p.Priority)
                    .FirstOrDefault();

                if (processor != null)
                {
                    var result = processor.Process(rawBlock);
                    // 存入快照，供 UI 使用
                    snapshot.DataBlocks[rawBlock.BlockName] = result;
                }
            }

            return snapshot;
        }
    }
}