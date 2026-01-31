using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Registration;
using System.Reflection;

namespace FileProcessor.Engine.Runtime
{
    public class PluginLoader
    {
        public (List<IFileTemplate> Templates, ProcessorRegistry Processors) LoadPlugins(string pluginPath)
        {
            var templates = new List<IFileTemplate>();
            var registry = new ProcessorRegistry();

            if (!Directory.Exists(pluginPath)) return (templates, registry);

            var dlls = Directory.GetFiles(pluginPath, "*.dll");

            foreach (var dll in dlls)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);

                    foreach (var type in types)
                    {
                        // 1. 处理文件模板 (IFileTemplate)
                        if (typeof(IFileTemplate).IsAssignableFrom(type))
                        {
                            if (Activator.CreateInstance(type) is IFileTemplate template)
                            {
                                templates.Add(template);
                                Console.WriteLine($"[加载] 发现模板: {type.Name}");
                            }
                        }

                        // 2. 处理块处理器 (IBlockProcessor)
                        // 修正点：只要实现了接口，就直接注册，不再强制检查特性
                        if (typeof(IBlockProcessor).IsAssignableFrom(type))
                        {
                            if (Activator.CreateInstance(type) is IBlockProcessor processor)
                            {
                                registry.Register(processor);

                                // 打印加载信息，方便调试
                                string source = type.Name;
                                string target = processor.TargetBlockName;
                                Console.WriteLine($"[加载] 注册处理器: {source} -> 目标块: {target}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[错误] 加载插件 {dll} 失败: {ex.Message}");
                }
                //var assembly = Assembly.LoadFrom(dll);
                //var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);

                //foreach (var type in types)
                //{
                //    // 1. 处理文件模板
                //    if (typeof(IFileTemplate).IsAssignableFrom(type))
                //    {
                //        if (Activator.CreateInstance(type) is IFileTemplate template)
                //            templates.Add(template);
                //    }

                //    // 2. 处理块处理器
                //    if (typeof(IBlockProcessor).IsAssignableFrom(type))
                //    {
                //        if (Activator.CreateInstance(type) is IBlockProcessor processor)
                //        {
                //            // 尝试通过特性获取注册名
                //            var attrs = type.GetCustomAttributes<BlockProcessorAttribute>();
                //            if (attrs != null && attrs.Any())
                //            {
                //                foreach (var attr in attrs)
                //                {
                //                    // 为每一个标记的 BlockName 注册该处理器
                //                    registry.Register(processor);
                //                    Console.WriteLine($"[加载] 注册处理器: {type.Name} -> {attr.BlockName}");
                //                }
                //            }
                //            // 即使没有特性，也可以通过 Registry 的模糊匹配逻辑发现
                //        }
                //    }
                //}
            }
            return (templates, registry);
        }
    }
}
