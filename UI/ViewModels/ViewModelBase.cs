using CommunityToolkit.Mvvm.ComponentModel;

namespace UI.ViewModels
{
    /// <summary>
    /// 现在的基类变得异常清爽，因为它把脏活累活都交给了微软的 Source Generator
    /// </summary>
    public abstract partial class ViewModelBase : ObservableObject
    {
        // 这里可以放一些全局通用的逻辑，比如忙碌状态指示
        [ObservableProperty]
        private bool _isBusy;
    }
}
