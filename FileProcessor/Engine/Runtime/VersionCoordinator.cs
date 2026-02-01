using FileProcessor.Core.Contracts;
using Timer = System.Timers.Timer;

namespace FileProcessor.Engine.Runtime
{
    public class VersionCoordinator : IVersionCoordinator
    {
        private string _currentVersionId = "Initial";
        private readonly Timer _aggregationTimer;
        private readonly double _silencePeriodMs = 3000; // 3秒静默期

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

        public void NotifySentinelChanged(string sentinelName)
        {
            // 如果不在录制中，或者收到了高优先级哨兵（如 dsnctrl.ini），则开启新版本
            if (!IsRecording || sentinelName.EndsWith(".ini"))
            {
                _currentVersionId = $"V_{DateTime.Now:yyyyMMdd_HHmmss}";
                Console.WriteLine($"[版本中心] 哨兵触发 ({sentinelName})，开启新版本: {_currentVersionId}");
            }

            // 重置静默期计时器
            _aggregationTimer.Stop();
            _aggregationTimer.Start();
        }

        private void OnTimerElapsed()
        {
            Console.WriteLine($"[版本中心] 窗口期结束，版本锁定: {_currentVersionId}");
            VersionCommitted?.Invoke(_currentVersionId);
        }
    }
}
