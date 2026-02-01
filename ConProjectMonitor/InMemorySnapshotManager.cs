using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace ConProjectMonitor
{
    public class InMemorySnapshotManager : ISnapshotManager
    {
        // 存储结构：VersionId -> List of Results
        private readonly ConcurrentDictionary<string, List<ProcessedResult>> _storage = new();

        public void AddSnapshot(ProcessedResult result)
        {
            var list = _storage.GetOrAdd(result.VersionId, _ => new List<ProcessedResult>());
            lock (list)
            {
                list.Add(result);
            }
            // 实时打印，方便验证
            Console.WriteLine($"[仓库存入] 块:{result.BlockName} | 版本:{result.VersionId} | 哈希:{result.OriginHash.Substring(0, 8)}...");
        }

        public IEnumerable<ProcessedResult> GetResultsByVersion(string versionId)
            => _storage.TryGetValue(versionId, out var results) ? results : Enumerable.Empty<ProcessedResult>();

        public IEnumerable<ProcessedResult> GetHistory(string blockName)
            => _storage.Values.SelectMany(x => x).Where(r => r.BlockName == blockName);

        public ProcessedResult? GetResult(string versionId, string blockName)
            => GetResultsByVersion(versionId).FirstOrDefault(r => r.BlockName == blockName);
    }
}