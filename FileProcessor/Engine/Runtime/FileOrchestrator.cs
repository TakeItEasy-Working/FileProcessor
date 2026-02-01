using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Security.Cryptography;

namespace FileProcessor.Engine.Runtime
{
    public class FileOrchestrator
    {
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IEnumerable<IBlockProcessor> _processors;

        public FileOrchestrator(
            IEnumerable<IFileTemplate> templates,
            ISnapshotManager snapshotManager,
            IEnumerable<IBlockProcessor> processors)
        {
            _templates = templates;
            _snapshotManager = snapshotManager;
            _processors = processors;
        }

        public void ProcessFile(string filePath, string versionId)
        {
            var fileName = Path.GetFileName(filePath);

            var template = _templates.FirstOrDefault(t =>
                System.Text.RegularExpressions.Regex.IsMatch(fileName, t.FileNamePattern));

            if (template == null) return;

            string fileHash = ComputeHash(filePath);
            var rawBlocks = template.Parse(filePath);

            foreach (var rawBlock in rawBlocks)
            {
                // 确保 RawDataBlock 携带最新的 Hash 供 Processor 使用
                rawBlock.FileHash = fileHash;

                // 寻找处理器：按 Priority 排序确保最匹配的先执行
                var processor = _processors
                    .Where(p => p.CanProcess(rawBlock.BlockName))
                    .OrderByDescending(p => p.Priority)
                    .FirstOrDefault();

                if (processor != null)
                {
                    // 对齐方法名：使用 Process(rawBlock)
                    var result = processor.Process(rawBlock);

                    // 最终注入：利用 object initializer 或修改字段
                    // 注意：因为你的 ProcessedResult 属性是 init，
                    // 如果不是 record 类型，我们需要在构造时确定，或者通过反射/扩展方法修改
                    // 这里我们假设我们在 SnapshotManager 存储前统一包装

                    var finalizedResult = WrapWithVersionMetadata(result, versionId, fileHash);

                    _snapshotManager.AddSnapshot(finalizedResult);
                }
            }
        }

        private ProcessedResult WrapWithVersionMetadata(ProcessedResult result, string vid, string hash)
        {
            // 处理 init 属性的限制，如果是 class 则通常需要副本
            // 这里体现了 Orchestrator 的“包装”职责
            return new ProcessedResult
            {
                BlockName = result.BlockName,
                DisplayName = result.DisplayName,
                Category = result.Category,
                Rows = result.Rows,
                Columns = result.Columns,
                Metadata = result.Metadata,
                ProcessTime = DateTime.Now,
                VersionId = vid,
                OriginHash = hash
            };
        }

        private string ComputeHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}