using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.IO;
using System.Collections.Generic;

namespace UI.Models // 建议放在 UI 项目下专门的 Mocks 或 Services 文件夹中
{
    public class MockOutTemplate : IFileTemplate
    {
        // 匹配所有以 .out 结尾的文件
        public string FileNamePattern => @".*\.out$";

        // 核心：告诉 MonitorService 到哪个子目录下寻找此类文件
        public string SubDirectory => "设计结果";

        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            // 1. 获取文件名，作为块名的一部分
            string fileName = Path.GetFileName(filePath);

            // 2. 读取文件内容（模拟读取）
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch
            {
                // 如果文件被占用，返回一个简单的模拟内容
                lines = new[] { "File is being accessed", "Value: Pending" };
            }

            // 3. 模拟拆分逻辑：
            // 我们假设每个 .out 文件都是一个整体，作为一个 Block 返回
            yield return new RawDataBlock(
                blockName: $"Block_{fileName}", // 这里的名称要和卡片里的 TargetBlockName 对应
                lines: lines,
                startLine: 1,
                filePath: filePath
            );
        }
    }
}