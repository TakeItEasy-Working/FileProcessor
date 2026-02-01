using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace UI.Models // 建议移动到 UI 项目的 Service 下或保留在原位
{
    public class SimpleSnapshotManager : ISnapshotManager
    {
        // 存储结构：VersionId -> List of Results
        private readonly ConcurrentDictionary<string, List<ProcessedResult>> _storage = new();

        // 1. 实现接口要求的事件
        public event Action<ProcessedResult>? DataUpdated;

        public void AddSnapshot(ProcessedResult result)
        {
            var list = _storage.GetOrAdd(result.VersionId, _ => new List<ProcessedResult>());
            lock (list)
            {
                list.Add(result);
            }

            // 2. 核心逻辑：存入数据后，立即喊一嗓子通知 UI
            // Invoke 会通知所有订阅了此事件的 DisplayCardViewModel
            DataUpdated?.Invoke(result);

            // 实时打印，方便验证
            Console.WriteLine($"[仓库存入] 块:{result.BlockName} | 版本:{result.VersionId}");
        }

        public IEnumerable<ProcessedResult> GetResultsByVersion(string versionId)
            => _storage.TryGetValue(versionId, out var results) ? results : Enumerable.Empty<ProcessedResult>();

        public IEnumerable<ProcessedResult> GetHistory(string blockName)
            => _storage.Values.SelectMany(x => x).Where(r => r.BlockName == blockName);

        public ProcessedResult? GetResult(string versionId, string blockName)
            => GetResultsByVersion(versionId).FirstOrDefault(r => r.BlockName == blockName);
    }
}