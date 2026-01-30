using FileProcessor.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Engine.Services
{
    /// <summary>
    /// 负责存储处理结果、提供历史回滚及数据统计功能
    /// </summary>
    public class SnapshotManager
    {
        // 使用并发字典存储每个文件的版本链：Key 为文件名，Value 为按时间排序的快照列表
        private readonly ConcurrentDictionary<string, List<FileSnapshot>> _history = new();

        // 设置每个文件保留的最大快照数量，防止内存溢出
        private const int MaxHistoryPerFile = 50;

        /// <summary>
        /// 添加新快照
        /// </summary>
        public void AddSnapshot(FileSnapshot snapshot)
        {
            _history.AddOrUpdate(snapshot.FileName,
                _ => new List<FileSnapshot> { snapshot },
                (_, list) =>
                {
                    lock (list) // 保证版本链操作的线程安全
                    {
                        list.Add(snapshot);
                        // 保持按时间从新到旧排序
                        var updatedList = list.OrderByDescending(s => s.ProcessedTime).Take(MaxHistoryPerFile).ToList();
                        return updatedList;
                    }
                });
        }

        /// <summary>
        /// 回滚：获取指定文件上一个版本的有效数据
        /// </summary>
        public FileSnapshot? GetPreviousVersion(string fileName)
        {
            if (_history.TryGetValue(fileName, out var list))
            {
                lock (list)
                {
                    // Index 0 是当前最新，Index 1 是上一个版本
                    return list.Count > 1 ? list[1] : null;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定文件的所有历史版本（用于时间轴分析）
        /// </summary>
        public IEnumerable<FileSnapshot> GetHistory(string fileName)
        {
            return _history.TryGetValue(fileName, out var list) ? list : Enumerable.Empty<FileSnapshot>();
        }

        /// <summary>
        /// 统计分析：跨文件聚合特定类型数据块的最新值
        /// </summary>
        /// <param name="blockName">数据块名称</param>
        public IEnumerable<object> AggregateLatestData(string blockName)
        {
            var results = new List<object>();
            foreach (var historyList in _history.Values)
            {
                var latest = historyList.FirstOrDefault();
                if (latest != null && latest.DataBlocks.TryGetValue(blockName, out var data))
                {
                    results.Add(data);
                }
            }
            return results;
        }
    }
}
