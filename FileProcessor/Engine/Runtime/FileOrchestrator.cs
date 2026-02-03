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

        /// <summary>
        /// 原有方法：根据路径匹配模板并处理
        /// </summary>
        public void ProcessFile(string filePath, string versionId)
        {
            var fileName = Path.GetFileName(filePath);
            var template = _templates.FirstOrDefault(t =>
                System.Text.RegularExpressions.Regex.IsMatch(fileName, t.FileNamePattern));

            if (template != null)
            {
                ProcessFile(filePath, versionId, template);
            }
        }

        /// <summary>
        /// 新增重载：直接传入模板，跳过匹配逻辑（供 ProjectMonitorService 初始扫描使用）
        /// </summary>
        public void ProcessFile(string filePath, string versionId, IFileTemplate template)
        {
            if (!File.Exists(filePath)) return;

            string fileHash = ComputeHash(filePath);
            var rawBlocks = template.Parse(filePath);

            foreach (var rawBlock in rawBlocks)
            {
                rawBlock.FileHash = fileHash;

                var processor = _processors
                    .Where(p => p.CanProcess(rawBlock.BlockName))
                    .OrderByDescending(p => p.Priority)
                    .FirstOrDefault();

                if (processor != null)
                {
                    var result = processor.Process(rawBlock);
                    var finalizedResult = WrapWithVersionMetadata(result, versionId, fileHash);
                    _snapshotManager.AddSnapshot(finalizedResult);
                }
            }
        }

        private ProcessedResult WrapWithVersionMetadata(ProcessedResult result, string vid, string hash)
        {
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
            // 商业软件建议：如果文件很大，初始扫描时可考虑只用文件大小+修改时间做弱 Hash 提高速度
            using var sha = SHA256.Create();
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}