using CommunityToolkit.Mvvm.ComponentModel;

namespace FileProcessor.Desktop.Models
{
    /// <summary>
    /// 版本记录 UI 模型，支持双向绑定 Note 属性
    /// </summary>
    public partial class VersionRecord : ObservableObject
    {
        [ObservableProperty]
        private string _versionId = string.Empty;

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        private string _note = string.Empty;
    }
}