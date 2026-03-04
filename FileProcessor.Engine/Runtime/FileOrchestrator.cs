using FileProcessor.Core.Contracts;
using FileProcessor.Core.Models;
using FileProcessor.DebugHelpers;
using FileProcessor.Engine.Registration;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FileProcessor.Engine.Runtime
{
    /// <summary>
    /// 文件解析编排器：核心执行中枢。
    /// 支持“延迟解析”策略，在 Batch 周期内只暂存任务，直到收到发令枪信号。
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

        // 标记当前是否处于项目初次加载阶段
        private bool _isInitializing = false;
        private string _currentInitVersionId = "Init_Standby";

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

        public void BeginInitialization()
        {
            _isInitializing = true;
            // 每次启动监控时，生成带有精确时间戳的唯一初始版本号
            _currentInitVersionId = $"Init_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        public void EndInitialization()
        {
            _isInitializing = false;
            // 提交这个动态生成的版本号
            _versionCoordinator.Commit(_currentInitVersionId);
        }

        public void ProcessInitialFile(string filePath, string hash)
        {
            var context = new ProcessingTaskContext
            {
                FilePath = filePath,
                FileHash = hash,
                BoundVersionId = _currentInitVersionId,
                TriggerTime = DateTime.Now
            };
            ExecuteSingleTask(context);
        }

        /// <summary>
        /// 接收监控层发来的处理请求
        /// </summary>
        public void EnqueueTask(ProcessingTaskContext context, bool isRecording)
        {
            if (_isInitializing)
            {
                ProcessInitialFile(context.FilePath, context.FileHash);
                return;
            }

            if (isRecording)
            {
                // A轨：录制模式 (只存不练)
                _pendingTasks[context.FilePath] = context;

                // ==========================================
                // 【核心修复 1】：识别发令枪！
                // 看到 IsFinalCommit，立刻清空并执行所有暂存任务！
                // ==========================================
                if (context.IsFinalCommit)
                {
                    FlushBatchTasks();
                }
            }
            else
            {
                // B轨：实时模式
                // ==========================================
                // 【核心修复 2】：尊重外部传来的版本号
                // 如果 Monitor 已经给分配了 Live_xxx，就用它的；否则才兜底生成
                // ==========================================
                string versionId = context.BoundVersionId.StartsWith("Live_")
                    ? context.BoundVersionId
                    : _versionCoordinator.GenerateLiveVersionId();

                var liveContext = context with { BoundVersionId = versionId };
                ExecuteSingleTask(liveContext);
            }
        }

        /// <summary>
        /// 清空并并行执行所有暂存任务
        /// </summary>
        public void FlushBatchTasks()
        {
            var tasks = _pendingTasks.Values.ToList();
            _pendingTasks.Clear();

            // 批次任务通常较大，并行执行
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
                rawBlock.FileHash = context.FileHash;

                var processors = _processorRegistry.GetProcessorsForBlock(rawBlock.BlockName);
                foreach (var processor in processors)
                {
                    try
                    {
                        var result = processor.Process(rawBlock);
                        if (result != null)
                        {
                            result.VersionId = context.BoundVersionId;
                            result.SourceFileName = context.FileName;
                            result.Metadata["OriginHash"] = context.FileHash;

                            _snapshotManager.AddSnapshot(result);
                            Log.Debug($"[Orchestrator入库] 文件:{result.SourceFileName}, 块:{result.StandardBlockName}, 版本号:{result.VersionId}");
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