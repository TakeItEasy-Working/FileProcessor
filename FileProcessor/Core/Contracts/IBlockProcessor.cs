using FileProcessor.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileProcessor.Core.Contracts
{
    public interface IBlockProcessor
    {
        /// <summary>
        /// 定义该处理器能够处理的块名称（如 "[UserConfig]"）
        /// </summary>
        bool CanProcess(string blockName);

        /// <summary>
        /// 处理逻辑：将字符串数组转换为具体的业务对象
        /// </summary>
        object Process(RawDataBlock block);

        /// <summary>
        /// 优先级：当存在多个处理器时，值越大优先级越高
        /// </summary>
        int Priority => 0;
    }
}
