using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;

namespace ConProjectMonitor
{
    public class MockOutTemplate : IFileTemplate
    {
        // 匹配所有以 .out 结尾的文件
        public string FileNamePattern => @".*\.out$";

        // 核心：对齐你要求的子目录逻辑
        public string SubDirectory => "设计结果";

        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            // 模拟拆分逻辑：每个文件我们固定拆出一个块
            yield return new RawDataBlock(
                blockName: $"Block_{fileName}",
                lines: new[] { "Header: Test", "Data: 123" },
                startLine: 1,
                filePath: filePath
            );
        }
    }
}