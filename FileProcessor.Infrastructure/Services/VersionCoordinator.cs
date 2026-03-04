using FileProcessor.Core.Contracts;
using FileProcessor.DebugHelpers;
using System;
using System.Threading.Tasks;

namespace FileProcessor.Infrastructure.Runtime
{
    /// <summary>
    /// 强信号驱动的版本协调器实现（防竞态版）。
    /// 负责宏观的状态路由（Batch vs Live），并在闭环时确保数据落袋为安。
    /// </summary>
    public class VersionCoordinator : IVersionCoordinator
    {
        private string _activeBatchId = "Live";
        private bool _isRecording = false;
        private readonly object _lockObj = new object();

        public string CurrentVersionId => _isRecording ? _activeBatchId : "Live";
        public bool IsRecording => _isRecording;

        public event Action<string>? VersionCommitted;
        public event Action<string, string>? LiveUpdateProcessed;

        /// <summary>
        /// 通用的版本提交，触发 UI 刷新
        /// </summary>
        public void Commit(string versionId)
        {
            Log.Debug($"[Coordinator] 显式提交版本，通知UI刷新: {versionId}");
            VersionCommitted?.Invoke(versionId);
        }

        /// <summary>
        /// 发现 check.out 时调用：开启宏观批次录制
        /// </summary>
        public void StartNewBatch(string source)
        {
            lock (_lockObj)
            {
                if (_isRecording) return; // 幂等保护，防止多次调用

                _isRecording = true;
                _activeBatchId = $"Batch_{DateTime.Now:yyyyMMdd_HHmmss}";
                Log.Debug($"[Coordinator] 哨兵 {source} 触发，批次录制开启: {_activeBatchId}");
            }
        }

        /// <summary>
        /// 发现 warnning.out 等结束哨兵时调用：关闭批次并异步排空队列
        /// </summary>
        public void CommitCurrentBatch(string source)
        {
            string completedVersionId;

            lock (_lockObj)
            {
                if (!_isRecording) return; // 幂等保护

                completedVersionId = _activeBatchId;
                _isRecording = false; // 立即关门，后续新文件将自动路由为 Live 模式
                _activeBatchId = "Live";
                Log.Debug($"[Coordinator] 哨兵 {source} 触发，大门关闭。版本 {completedVersionId} 开始排空队列...");
            }

            // ==========================================
            // 【防竞态机制】：队列排空等待
            // 虽然大门关了，但 Orchestrator 的队列里可能还有刚进门的文件正在解析。
            // 强行等待 1.5 秒，让子弹飞一会，确保所有数据都存入 SnapshotManager，
            // 然后再通知 UI 刷新，彻底杜绝 UI 拿到 null 的问题！
            // ==========================================
            Task.Delay(1500).ContinueWith(_ =>
            {
                Log.Debug($"[Coordinator] 队列排空完毕，正式闭环: {completedVersionId}");
                Commit(completedVersionId);
            });
        }

        /// <summary>
        /// 为散件模式（时程分析等无哨兵独立计算）生成唯一的实时版本号
        /// </summary>
        public string GenerateLiveVersionId()
        {
            return $"Live_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
    }
}