using FileProcessor.Core.Contracts;
using Timer = System.Timers.Timer;

namespace FileProcessor.Engine.Runtime
{
    public class VersionCoordinator : IVersionCoordinator
    {
        private string _currentVersionId = "Initial";
        private readonly Timer _aggregationTimer;
        private readonly double _silencePeriodMs;

        public string CurrentVersionId => _currentVersionId;
        public bool IsRecording => _aggregationTimer.Enabled;

        public event Action<string>? VersionCommitted;

        public VersionCoordinator(double silencePeriod = 3000)
        {
            _silencePeriodMs = silencePeriod;
            _aggregationTimer = new Timer(_silencePeriodMs);
            _aggregationTimer.AutoReset = false;
            _aggregationTimer.Elapsed += (s, e) => OnTimerElapsed();
        }

        /// <summary>
        /// 原有逻辑：由特定的哨兵文件变动触发
        /// </summary>
        public void NotifySentinelChanged(string sentinelName)
        {
            // 内部逻辑已抽象到 TriggerNewVersion
            TriggerNewVersion();
        }

        /// <summary>
        /// 实现接口：主动触发一个新版本的产生并启动计时
        /// </summary>
        public void TriggerNewVersion()
        {
            _currentVersionId = $"V_{DateTime.Now:yyyyMMdd_HHmmss}";
            Console.WriteLine($"[版本中心] 信号触发，开启新版本: {_currentVersionId}");

            // 重置计时器：在静默期内（如3秒）没有新信号，则认为该版本录制结束
            _aggregationTimer.Stop();
            _aggregationTimer.Start();
        }

        /// <summary>
        /// 实现接口：强制设定版本号（通常用于 Initial_Scan）
        /// 此时会停止计时器，确保扫描期间不会因为超时而自动提交版本
        /// </summary>
        public void ForceVersion(string versionId)
        {
            _aggregationTimer.Stop();
            _currentVersionId = versionId;
            Console.WriteLine($"[版本中心] 强制锁定版本: {_currentVersionId} (计时器已关闭)");
        }

        /// <summary>
        /// 实现接口：获取当前正在处理的版本 ID
        /// </summary>
        public string GetCurrentVersion() => _currentVersionId;

        private void OnTimerElapsed()
        {
            Console.WriteLine($"[版本中心] 静默期结束，版本提交: {_currentVersionId}");
            VersionCommitted?.Invoke(_currentVersionId);
        }
    }
}
