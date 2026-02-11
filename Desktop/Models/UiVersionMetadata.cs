using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Desktop.Models
{
    /// <summary>
    /// UI 专用版本元数据：在内核 VersionId 基础上增加备注和编号功能。
    /// </summary>
    public class UiVersionMetadata : INotifyPropertyChanged
    {
        private string _note = string.Empty;

        /// <summary>
        /// 显示编号（如 1, 2, 3...）
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// 内核生成的原始 VersionId
        /// </summary>
        public string VersionId { get; init; } = string.Empty;

        /// <summary>
        /// 格式化后的时间显示
        /// </summary>
        public string TimeLabel { get; init; } = string.Empty;

        /// <summary>
        /// 用户手动输入的备注
        /// </summary>
        public string Note
        {
            get => _note;
            set
            {
                if (_note != value)
                {
                    _note = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}