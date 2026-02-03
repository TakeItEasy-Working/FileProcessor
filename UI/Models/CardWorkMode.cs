namespace UI.Models
{
    public enum CardWorkMode
    {
        FollowLatest, // 永远显示最新解析的数据
        SyncGlobal,   // 跟随 MainViewModel 的全局版本切换
        ManualLock    // 锁定在某个特定版本，不随外部变化
    }
}
