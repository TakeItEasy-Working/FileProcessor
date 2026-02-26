using FileProcessor.Core.Models;

namespace FileProcessor.Desktop.Models
{
    /// <summary>
    /// 插槽的独立显示状态器。
    /// 用于持久化记录用户的视图偏好（选中的列名 Key）。
    /// 它的生命周期与 SlotViewModel 绑定，实现“数据与视图偏好”的完全解耦，
    /// 确保在数据版本切换或视图模式切换时，用户的选择不会丢失。
    /// </summary>
    public class SlotDisplayState
    {
        /// <summary>
        /// 记忆：表格视图中第一列绑定的 Column Key
        /// </summary>
        public string? TableCol1Key { get; set; }

        /// <summary>
        /// 记忆：表格视图中第二列绑定的 Column Key
        /// </summary>
        public string? TableCol2Key { get; set; }

        /// <summary>
        /// 记忆：表格视图中第三列绑定的 Column Key
        /// </summary>
        public string? TableCol3Key { get; set; }

        /// <summary>
        /// 记忆：表格视图中第四列绑定的 Column Key
        /// </summary>
        public string? TableCol4Key { get; set; }

        /// <summary>
        /// 记忆：图表视图中 X 轴绑定的 Column Key
        /// </summary>
        public string? ChartXColKey { get; set; }

        /// <summary>
        /// 记忆：图表视图中 Y 轴绑定的 Column Key
        /// </summary>
        public string? ChartYColKey { get; set; }
    }
}