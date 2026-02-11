using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using FileProcessor.Engine.Runtime;
using FileProcessor.Infrastructure.Runtime;
using FileProcessor.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WMass.Plugin;
using Xunit;

namespace FileProcessor.UTest
{
    public class WMassLogicTests
    {
        /// <summary>
        /// 验证是否能正确识别并切分这种多行带星号的数据块
        /// </summary>
        [Fact]
        public void Test_WMass_RealFormat_Splitting()
        {
            // 1. 构造真实格式的字符串
            var sb = new StringBuilder();
            sb.AppendLine("   计算用时：00:03:54");
            sb.AppendLine("     **********************************************************");
            sb.AppendLine("               各层刚心、偏心率、相邻层侧移刚度比等计算信息");
            sb.AppendLine("  Floor No     : 层号");
            sb.AppendLine("     **********************************************************");
            sb.AppendLine("  Floor No. 1      Tower No. 1");
            sb.AppendLine("  Xstif=   230.3179(m)      Ystif=    69.2699(m)      Alf  =    45.0000(Degree)");
            sb.AppendLine("  Eex  =     0.0467         Eey  =     0.0581");
            sb.AppendLine("     ----------------------------------------------------------");
            sb.AppendLine("  Floor No. 2      Tower No. 1");
            sb.AppendLine("  Xstif=   229.3921(m)      Ystif=    71.5629(m)");

            var lines = sb.ToString().Split("\r\n").ToList();
            var template = new TestWMassTemplate();

            // 2. 执行切块
            var blocks = template.ExposeSplitBlocks(lines, "wmass.out").ToList();

            // 3. 验证
            Assert.NotEmpty(blocks);
            var targetBlock = blocks.FirstOrDefault(b => b.BlockName.Contains("各层刚心"));
            Assert.NotNull(targetBlock);

            // 验证块内是否包含了关键行
            Assert.Contains(targetBlock.Lines, l => l.Contains("Floor No. 1"));
            Assert.Contains(targetBlock.Lines, l => l.Contains("Xstif="));
        }

        /// <summary>
        /// 验证 Processor 是否能处理这种跨行、带单位、带等号的复杂格式
        /// </summary>
        [Fact]
        public void Test_WMass_RealFormat_Parsing()
        {
            // 1. 准备一个真实的层数据采样块
            // 模拟处理器接收到的多行数据（从 Floor No. 1 到分隔线前）
            var rawData = new[] {
                "  Floor No. 1      Tower No. 1",
                "  Xstif=   230.3179(m)      Ystif=    69.2699(m)      Alf  =    45.0000(Degree)",
                "  Xmass=   228.3240(m)      Ymass=    67.7515(m)      Gmass & G= 3700.3789 & 3462.8433(t)",
                "  Eex  =     0.0467         Eey  =     0.0581",
                "  Ratx =     1.0000         Raty =     1.0000"
            };

            var block = new RawDataBlock("各层刚心...计算信息", rawData, 10, "wmass.out");
            var processor = new WMassProcessor();

            // 2. 解析
            var result = processor.Process(block);

            // 3. 断言
            // 注意：这种格式下，一行层数据会被解析成一个 Row。
            // 你的 Processor 逻辑需要能够跨行寻找同一个 Floor No. 下的所有字段。
            Assert.NotEmpty(result.Rows);
            var row = result.Rows[0];

            Assert.Equal("230.3179", row["Xstif"]);
            Assert.Equal("69.2699", row["Ystif"]);
            Assert.Equal("0.0467", row["Eex"]);
        }

        

        // 包装类以便访问受保护方法
        private class TestWMassTemplate : WMassTemplate
        {
            public IEnumerable<RawDataBlock> ExposeSplitBlocks(List<string> lines, string path)
            {
                var method = typeof(WMassTemplate).GetMethod("SplitBlocks",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return (IEnumerable<RawDataBlock>)method.Invoke(this, new object[] { lines, path });
            }
        }
    }
}