using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using System.Text.RegularExpressions;

namespace WMass.Plugin;

[FileProcessorPlugin]
public class WMassTemplate : IFileTemplate
{
    public string FileNamePattern => @"wmass.out";

    // 预编译一个简单的正则，用来判断“这是否是数据行”
    // 如果一行以数字开头，或者包含 "Floor No"，那它绝不是标题
    private static readonly Regex DataLinePattern = new Regex(@"^(\d|Floor|Tower|----)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? IdentifyBlockName(string currentLine, string nextLine)
    {
        // 只有当当前行包含星号时，才尝试识别
        if (currentLine.Contains("****"))
        {
            string candidate = nextLine?.Trim() ?? "";

            // --- 【核心修复逻辑】 ---

            // 1. 如果下一行是空的，或者是另一行分割线，忽略
            if (string.IsNullOrWhiteSpace(candidate)) return null;
            if (candidate.Contains("****")) return null;

            // 2. 【关键】如果下一行看起来像数据（比如包含 Floor No），说明当前行是标题下方的底线
            // 这时返回 null，保持当前块继续收集数据
            if (DataLinePattern.IsMatch(candidate))
            {
                return null;
            }

            // 3. 通过所有检查，认为这是一行真正的标题
            return candidate;
        }
        return null;
    }

    public bool IsIgnorableLine(string line)
    {
        // 只忽略空行和减号分割线
        // 注意：千万不要在这里忽略 "****"，否则 IdentifyBlockName 永远不会触发
        if (string.IsNullOrWhiteSpace(line)) return true;
        if (line.Contains("----")) return true;

        return false;
    }
}