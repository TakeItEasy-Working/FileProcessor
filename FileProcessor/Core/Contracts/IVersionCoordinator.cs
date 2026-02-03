namespace FileProcessor.Core.Contracts
{
    public interface IVersionCoordinator
    {
        // 当前正在录制的版本 ID
        string CurrentVersionId { get; }

        // 是否正在录制窗口内
        bool IsRecording { get; }

        // 外部信号触发：通知哨兵变动
        void NotifySentinelChanged(string sentinelName);

        // 当一个完整的版本解析并聚合完成后触发
        event Action<string> VersionCommitted;

        // --- 新增：强制设定版本号 ---
        // 用于 Initial_Scan，此时不启动 3s 倒计时计时器，直接锁定 ID
        void ForceVersion(string versionId);

        // --- 新增：主动触发新版本 ---
        // 对应 ProjectMonitorService 中的 _versionCoordinator.TriggerNewVersion()
        void TriggerNewVersion();

        // --- 新增：获取当前活跃的版本 ID ---
        string GetCurrentVersion();
    }
}
