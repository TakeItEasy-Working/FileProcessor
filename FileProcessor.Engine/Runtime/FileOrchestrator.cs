using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.Engine.Registration;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FileProcessor.Engine.Runtime
{
    /// <summary>
    /// 文件解析编排器：核心执行中枢。
    /// 支持“延迟解析”策略，在 Batch 周期内只暂存任务，直到收到提交信号。
    /// 实现批次暂存与实时解析的双轨制分流。
    /// </summary>
    public class FileOrchestrator
    {
        private readonly IEnumerable<IFileTemplate> _templates;
        private readonly ISnapshotManager _snapshotManager;
        private readonly ProcessorRegistry _processorRegistry;
        private readonly IVersionCoordinator _versionCoordinator;

        // 暂存录制期间的文件变动，Key 为 FilePath，确保同一个文件只有一条记录
        private readonly ConcurrentDictionary<string, ProcessingTaskContext> _pendingTasks = new();

        public FileOrchestrator(
            IEnumerable<IFileTemplate> templates,
            ISnapshotManager snapshotManager,
            ProcessorRegistry processorRegistry,
            IVersionCoordinator versionCoordinator)
        {
            _templates = templates;
            _snapshotManager = snapshotManager;
            _processorRegistry = processorRegistry;
            _versionCoordinator = versionCoordinator;
        }

        /// <summary>
        /// 接收监控层发来的处理请求
        /// </summary>
        /// <param name="context">任务上下文</param>
        /// <param name="isRecording">是否处于批次录制状态</param>
        public void EnqueueTask(ProcessingTaskContext context, bool isRecording)
        {
            if (isRecording)
            {
                // A轨：录制模式
                // 核心逻辑：如果在录制中，只存不练，且相同路径会覆盖之前的 Context
                _pendingTasks[context.FilePath] = context;
            }
            else
            {
                // B轨：实时模式 - 自动注入时间戳版本并立即执行
                var liveVersionId = _versionCoordinator.GenerateLiveVersionId();
                var liveContext = context with { BoundVersionId = liveVersionId };
                // 非录制状态（Live 模式），直接执行
                ExecuteSingleTask(liveContext);
            }
        }

        /// <summary>
        /// 当 mainjss.out 到达时，由外部调用清空并执行所有暂存任务
        /// </summary>
        public void FlushBatchTasks()
        {
            var tasks = _pendingTasks.Values.ToList();
            _pendingTasks.Clear();

            // 批次任务通常较大，建议并行执行
            Parallel.ForEach(tasks, task =>
            {
                ExecuteSingleTask(task);
            });
        }

        /// <summary>
        /// 核心执行单元：将解析结果打上版本标签并存入快照库
        /// </summary>
        private void ExecuteSingleTask(ProcessingTaskContext context)
        {
            var template = _templates.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.FileNamePattern) &&
                Regex.IsMatch(context.FileName, t.FileNamePattern));

            if (template == null) return;

            var rawBlocks = template.Parse(context.FilePath);

            foreach (var rawBlock in rawBlocks)
            {
                // 注入监控层预计算的 Hash，确保每个块都有溯源指纹
                rawBlock.FileHash = context.FileHash;

                var processors = _processorRegistry.GetProcessorsForBlock(rawBlock.BlockName);
                foreach (var processor in processors)
                {
                    try
                    {
                        var result = processor.Process(rawBlock);
                        if (result != null)
                        {
                            // 关键补全：由编排器维护结果的版本身份
                            result.VersionId = context.BoundVersionId;
                            result.SourceFileName = context.FileName;
                            result.Metadata["OriginHash"] = context.FileHash;

                            _snapshotManager.AddSnapshot(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Orchestrator] 处理失败: {processor.GetType().Name}, 错误: {ex.Message}");
                    }
                }
            }
        }
    }
}