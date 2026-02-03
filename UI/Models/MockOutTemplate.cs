using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System;
using System.IO;
using System.Collections.Generic;

namespace UI.Models
{
    public class MockOutTemplate : IFileTemplate
    {
        public string FileNamePattern => "wmass.out"; // 匹配你的卡片默认设置
        public string SubDirectory => "设计结果";

        public IEnumerable<RawDataBlock> Parse(string filePath)
        {
            // 模拟产生一个符合 WMass 业务标题的块
            yield return new RawDataBlock(
                "各层刚心、偏心率、相邻层侧移刚度比等计算信息",
                new[] { "模拟数据行1", "模拟数据行2" },
                1,
                filePath);

            // 模拟产生第二个块
            yield return new RawDataBlock(
                "楼层位移总结",
                new[] { "位移数据行1" },
                10,
                filePath);
        }
    }
}