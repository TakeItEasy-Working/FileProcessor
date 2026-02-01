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
    }
}
