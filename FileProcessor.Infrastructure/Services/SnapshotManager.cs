using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 高性能快照管理器：支持正式版本覆盖与实时版本历史保留。
    /// </summary>
    public class SnapshotManager : ISnapshotManager
    {
        // 核心存储结构：VersionId -> (SourceFileName -> List<ProcessedResult>)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<ProcessedResult>>> _storage = new();

        /// <summary>
        /// 存储解析结果。
        /// 批次版本 (不以 Live_ 开头)：执行同名 Block 替换，保持版本最终态。
        /// 实时版本 (以 Live_ 开头)：作为历史快照新增。
        /// </summary>
        public void AddSnapshot(ProcessedResult result)
        {
            if (result == null) return;

            // 1. 获取或创建版本容器
            var versionContainer = _storage.GetOrAdd(result.VersionId, _ => new ConcurrentDictionary<string, List<ProcessedResult>>());


            // 2. 获取或创建文件容器
            var fileResults = versionContainer.GetOrAdd(result.SourceFileName, _ => new List<ProcessedResult>());

            // 3. 线程安全地添加结果（处理冲突：如果 StandardBlockName 相同，则替换旧的）
            lock (fileResults)
            {
                // 如果是实时模式，我们保留所有变动（即不执行 FindIndex 替换）
                // 只有在正式批次模式下，才进行覆盖以保证“封版”数据的整洁
                bool isLive = result.VersionId.StartsWith("Live_");

                if (!isLive)
                {
                    var existingIndex = fileResults.FindIndex(r => r.StandardBlockName == result.StandardBlockName);
                    if (existingIndex >= 0)
                    {
                        fileResults[existingIndex] = result;
                        return;
                    }
                }

                fileResults.Add(result);
            }
        }

        /// <summary>
        /// 查询特定版本下的所有文件名（Level 1）
        /// </summary>
        public IEnumerable<string> GetFileNames(string versionId)
        {
            if (_storage.TryGetValue(versionId, out var versionContainer))
            {
                return versionContainer.Keys;
            }
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// 查询特定文件下的所有数据块结果（Level 2）
        /// </summary>
        public IEnumerable<ProcessedResult> GetResultsByFile(string versionId, string fileName)
        {
            if (_storage.TryGetValue(versionId, out var versionContainer))
            {
                if (versionContainer.TryGetValue(fileName, out var results))
                {
                    lock (results)
                    {
                        return results.ToList(); // 返回副本防止多线程枚举异常
                    }
                }
            }
            return Enumerable.Empty<ProcessedResult>();
        }

        /// <summary>
        /// 精确获取某个版本的某个特定块
        /// </summary>
        public ProcessedResult? GetSpecificBlock(string versionId, string fileName, string standardBlockName)
        {
            return GetResultsByFile(versionId, fileName)
                .FirstOrDefault(r => r.StandardBlockName == standardBlockName);
        }

        /// <summary>
        /// 历史追踪：获取跨版本的所有同名块结果（用于指标趋势分析）
        /// </summary>
        public IEnumerable<ProcessedResult> GetHistory(string standardBlockName)
        {
            var history = new List<ProcessedResult>();
            // 遍历所有版本，提取该标准块的所有历史点
            foreach (var versionContainer in _storage.Values)
            {
                foreach (var fileResults in versionContainer.Values)
                {
                    lock (fileResults)
                    {
                        var matches = fileResults.Where(r => r.StandardBlockName == standardBlockName);
                        history.AddRange(matches);
                    }
                }
            }
            // 按时间或版本标识排序，方便 UI 绘图
            return history.OrderBy(h => h.VersionId);
        }

        public void Clear() => _storage.Clear();
    }
}