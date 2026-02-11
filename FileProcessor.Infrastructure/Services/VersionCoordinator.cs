using FileProcessor.Core.Contracts;
using System;

namespace FileProcessor.Infrastructure.Runtime
{
    /// <summary>
    /// 强信号驱动的版本协调器实现。
    /// 仅在 check.out 时开启，mainjss.out 时关闭，无视任何中间停顿时间。
    /// </summary>
    public class VersionCoordinator : IVersionCoordinator
    {
        private string _activeBatchId = "Live";
        private bool _isRecording = false;

        public string CurrentVersionId => _isRecording ? _activeBatchId : "Live";
        public bool IsRecording => _isRecording;

        public event Action<string>? VersionCommitted;
        public event Action<string, string>? LiveUpdateProcessed;

        /// <summary>
        /// 当 check.out 出现时调用
        /// </summary>
        public void StartNewBatch(string source)
        {
            _isRecording = true;
            _activeBatchId = $"Batch_{DateTime.Now:yyyyMMdd_HHmmss}";
            Console.WriteLine($"[Coordinator] 检测到 check.out，批次录制开始: {_activeBatchId}");
        }

        /// <summary>
        /// 当 mainjss.out 出现时调用
        /// </summary>
        public void CommitCurrentBatch(string source)
        {
            if (!_isRecording) return;

            string completedVersionId = _activeBatchId;
            _isRecording = false;
            _activeBatchId = "Live";

            Console.WriteLine($"[Coordinator] 检测到 mainjss.out，批次闭环: {completedVersionId}");

            // 触发事件，通知 UI 和 Orchestrator
            VersionCommitted?.Invoke(completedVersionId);
        }

        /// <summary>
        /// 为非标文件生成唯一的实时版本号
        /// </summary>
        public string GenerateLiveVersionId()
        {
            // 使用细化到秒的时间戳，确保历史记录的唯一性
            return $"Live_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
    }
}