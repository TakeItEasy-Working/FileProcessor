using FileProcessor.Core.Attributes;
using FileProcessor.Core.Contracts;
using FileProcessor.Engine.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FileProcessor.Engine.Runtime
{
    /// <summary>
    /// 插件加载服务：专门负责从 AppPath/Plugins 子目录下扫描并加载解析模板与处理器。
    /// </summary>
    public class PluginLoader
    {
        /// <summary>
        /// 扫描并加载 Plugins 文件夹下的所有有效插件。
        /// </summary>
        /// <returns>返回初始化的模板列表和处理器注册表</returns>
        public (List<IFileTemplate> Templates, ProcessorRegistry Registry) LoadFromPluginsFolder()
        {
            var templates = new List<IFileTemplate>();
            var registry = new ProcessorRegistry();

            // 定位到程序所在目录下的 Plugins 文件夹
            string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            if (!Directory.Exists(pluginPath))
            {
                Directory.CreateDirectory(pluginPath);
                return (templates, registry);
            }

            var dlls = Directory.GetFiles(pluginPath, "*.dll");
            foreach (var dll in dlls)
            {
                try
                {
                    // 使用 LoadFrom 解决依赖项在同一目录下的加载问题
                    var assembly = Assembly.LoadFrom(dll);
                    var (tList, pList) = ScanAssembly(assembly);

                    templates.AddRange(tList);
                    foreach (var p in pList) registry.Register(p);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginLoader] 加载插件失败 {Path.GetFileName(dll)}: {ex.Message}");
                }
            }

            return (templates, registry);
        }

        /// <summary>
        /// 扫描程序集中的接口实现类
        /// </summary>
        private (List<IFileTemplate> Templates, List<IBlockProcessor> Processors) ScanAssembly(Assembly assembly)
        {
            var tList = new List<IFileTemplate>();
            var pList = new List<IBlockProcessor>();

            var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                // 1. 扫描 IFileTemplate 实现
                if (typeof(IFileTemplate).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<FileProcessorPluginAttribute>() != null)
                {
                    if (Activator.CreateInstance(type) is IFileTemplate template)
                        tList.Add(template);
                }

                // 2. 扫描 IBlockProcessor 实现
                if (typeof(IBlockProcessor).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<BlockProcessorAttribute>() != null)
                {
                    if (Activator.CreateInstance(type) is IBlockProcessor processor)
                        pList.Add(processor);
                }
            }
            return (tList, pList);
        }
    }
}