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
    /// 插件加载服务：负责从目录或当前程序域中扫描并实例化 Template 和 Processor。
    /// 这是实现核心 DLL 与具体业务插件解耦的关键类。
    /// </summary>
    public class PluginLoader
    {
        /// <summary>
        /// 从指定目录加载外部 DLL 插件
        /// </summary>
        /// <param name="pluginPath">插件 DLL 所在目录</param>
        /// <returns>加载到的模板列表和处理器注册表</returns>
        public (List<IFileTemplate> Templates, ProcessorRegistry Registry) LoadPlugins(string pluginPath)
        {
            var templates = new List<IFileTemplate>();
            var registry = new ProcessorRegistry();

            if (Directory.Exists(pluginPath))
            {
                var dlls = Directory.GetFiles(pluginPath, "*.dll");
                foreach (var dll in dlls)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(dll);
                        var result = ScanAssembly(assembly);
                        templates.AddRange(result.Templates);
                        foreach (var p in result.Processors) registry.Register(p);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PluginLoader] 加载 DLL 失败: {dll}, {ex.Message}");
                    }
                }
            }

            return (templates, registry);
        }

        /// <summary>
        /// 扫描当前程序域（包括主程序集和已引用的项目）中的插件
        /// </summary>
        public (List<IFileTemplate> Templates, ProcessorRegistry Registry) LoadFromCurrentDomain()
        {
            var templates = new List<IFileTemplate>();
            var registry = new ProcessorRegistry();

            // 扫描所有已加载的程序集（排除系统程序集以提高速度）
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"));

            foreach (var assembly in assemblies)
            {
                var result = ScanAssembly(assembly);
                templates.AddRange(result.Templates);
                foreach (var p in result.Processors) registry.Register(p);
            }

            return (templates, registry);
        }

        /// <summary>
        /// 核心扫描逻辑：识别带有特性的类
        /// </summary>
        private (List<IFileTemplate> Templates, List<IBlockProcessor> Processors) ScanAssembly(Assembly assembly)
        {
            var tList = new List<IFileTemplate>();
            var pList = new List<IBlockProcessor>();

            var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                // 1. 识别并实例化 FileTemplate
                if (typeof(IFileTemplate).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<FileProcessorPluginAttribute>() != null)
                {
                    if (Activator.CreateInstance(type) is IFileTemplate template)
                        tList.Add(template);
                }

                // 2. 识别并实例化 BlockProcessor
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