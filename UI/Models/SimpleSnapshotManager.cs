using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using System.Collections.Concurrent;

namespace UI.Models // 建议移动到 UI 项目的 Service 下或保留在原位
{
    public class SimpleSnapshotManager : ISnapshotManager
    {
        // 存储结构：VersionId -> List of Results
        private readonly ConcurrentDictionary<string, List<ProcessedResult>> _storage = new();

        // 事件：单条数据更新
        public event Action<ProcessedResult>? DataUpdated;
        // 事件：整批数据更新（扫描结束）
        public event Action? BatchUpdated;

        public void AddSnapshot(ProcessedResult result)
        {
            var list = _storage.GetOrAdd(result.VersionId, _ => new List<ProcessedResult>());
            lock (list)
            {
                list.Add(result);
            }

            // 触发单条更新信号
            DataUpdated?.Invoke(result);
        }

        /// <summary>
        /// 切换项目时，彻底清空内存中的历史数据
        /// </summary>
        public void Clear()
        {
            _storage.Clear();
            Console.WriteLine("[仓库中心] 存储已清空");
        }

        /// <summary>
        /// 当初始扫描（Initial Scan）结束时调用，触发 UI 整体刷新信号
        /// </summary>
        public void NotifyBatchComplete()
        {
            Console.WriteLine("[仓库中心] 批处理通知：初始扫描数据已就绪");
            BatchUpdated?.Invoke();
        }

        public IEnumerable<ProcessedResult> GetResultsByVersion(string versionId)
            => _storage.TryGetValue(versionId, out var results) ? results : Enumerable.Empty<ProcessedResult>();

        public IEnumerable<ProcessedResult> GetHistory(string blockName)
            => _storage.Values.SelectMany(x => x).Where(r => r.BlockName == blockName);

        public ProcessedResult? GetResult(string versionId, string blockName)
            => GetResultsByVersion(versionId).FirstOrDefault(r => r.BlockName == blockName);
    }
}