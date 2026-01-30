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
            //Console.WriteLine($"[Debug] 处理文件: '{fileName}'");

            //// 打印可用模板，便于调试
            //foreach (var t in _templates)
            //{
            //    try
            //    {
            //        Console.WriteLine($"[Debug] 可用模板模式: '{t.FileNamePattern}'");
            //    }
            //    catch { }
            //}

            // 使用不区分大小写的匹配，避免大小写导致不能匹配
            var template = _templates.FirstOrDefault(t =>
                Regex.IsMatch(fileName, t.FileNamePattern, RegexOptions.IgnoreCase));

            if (template == null)
            {
                Console.WriteLine($"[Debug] 未匹配到模板，跳过解析。请确认文件名是否满足模板模式（区分大小写可能导致匹配失败）。");
                return null;
            }

            // 2. 解析块
            var rawBlocks = TextParser.Parse(filePath, template);
            var processedData = new Dictionary<string, object>();

            // 3. 分发处理
            foreach (var rawBlock in rawBlocks)
            {
                var processor = _processorRegistry.FindProcessor(rawBlock.BlockName);
                if (processor != null)
                {
                    var result = processor.Process(rawBlock);
                    processedData[rawBlock.BlockName] = result;
                }
            }

            // 4. 生成快照
            return new FileSnapshot(
                Guid.NewGuid(),
                fileName,
                DateTime.Now,
                new FileInfo(filePath).Length,
                processedData
            );
        }
    }
}