using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using FileProcessor.DebugHelpers;
using FileProcessor.Core.Helpers;

namespace FileProcessor.Infrastructure.Services
{
    /// <summary>
    /// 模型归档服务：负责精准提取 YJK 核心文件并打包，支持动态重命名
    /// </summary>
    public class ArchiveService
    {
        /// <summary>
        /// 异步创建模型压缩包
        /// </summary>
        public async Task CreateArchiveAsync(string projectPath, string versionId, string note)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;

            try
            {
                string archiveDir = Path.Combine(projectPath, "Archive");
                if (!Directory.Exists(archiveDir))
                {
                    Directory.CreateDirectory(archiveDir);
                }

                // 清洗 Note 中的非法文件名字符
                string safeNote = FileHelper.GetSafeFileName(note);
                string zipFileName = string.IsNullOrWhiteSpace(safeNote)
                    ? $"{versionId}.zip"
                    : $"{versionId}_{safeNote}.zip";

                string zipFilePath = Path.Combine(archiveDir, zipFileName);

                // 如果已经存在同名包，先删除（通常是覆盖逻辑）
                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

                // 开启异步流写入，避免阻塞主线程
                using var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create);

                // 1. 打包固定文件/文件夹
                string[] fixedPaths = {
                    @"中间数据\dsnjc.data",
                    "dsnctrl.ini",
                    "fea.dat",
                    "Jccad_0",
                    "SPara.par",
                    "spretobase.dat",
                    "spretobase2.dat",
                    "sscs_local.ydb",
                    "yjkTransLoad.sav"
                };

                foreach (var relPath in fixedPaths)
                {
                    string fullPath = Path.Combine(projectPath, relPath);
                    await AddPathToZipAsync(zipArchive, fullPath, relPath);
                }

                // 2. 打包“衬图”文件夹下的所有文件
                string chentuPath = Path.Combine(projectPath, "衬图");
                await AddPathToZipAsync(zipArchive, chentuPath, "衬图");

                // 3. 动态寻找 YJK 模型文件并打包相关文件
                var yjkFile = Directory.GetFiles(projectPath, "*.yjk")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (yjkFile != null)
                {
                    string modelName = Path.GetFileNameWithoutExtension(yjkFile); // 例如 "Pro"

                    // 打包 Pro.*
                    var sameNameFiles = Directory.GetFiles(projectPath, $"{modelName}.*");
                    foreach (var file in sameNameFiles)
                    {
                        await AddFileToZipAsync(zipArchive, file, Path.GetFileName(file));
                    }

                    // 打包 *_Pro--*.txt (即包含模型名称的版本文件)
                    var txtFiles = Directory.GetFiles(projectPath, $"*_{modelName}--*.txt");
                    foreach (var file in txtFiles)
                    {
                        await AddFileToZipAsync(zipArchive, file, Path.GetFileName(file));
                    }
                }

                Log.Debug($"[ArchiveService] 版本 {versionId} 打包完成: {zipFileName}");
            }
            catch (Exception ex)
            {
                Log.Debug($"[ArchiveService] 打包失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 当用户修改备注时，动态重命名已存在的压缩包
        /// </summary>
        public void UpdateArchiveNote(string projectPath, string versionId, string newNote)
        {
            if (string.IsNullOrEmpty(projectPath)) return;

            try
            {
                string archiveDir = Path.Combine(projectPath, "Archive");
                if (!Directory.Exists(archiveDir)) return;

                // 寻找该版本对应的压缩包（匹配以 versionId 开头的 zip）
                var existingZips = Directory.GetFiles(archiveDir, $"{versionId}*.zip");
                if (existingZips.Length == 0) return;

                string existingZipPath = existingZips[0]; // 理论上只会有一个

                string safeNote = FileHelper.GetSafeFileName(newNote);
                string newZipFileName = string.IsNullOrWhiteSpace(safeNote)
                    ? $"{versionId}.zip"
                    : $"{versionId}_{safeNote}.zip";

                string newZipPath = Path.Combine(archiveDir, newZipFileName);

                if (existingZipPath != newZipPath && !File.Exists(newZipPath))
                {
                    File.Move(existingZipPath, newZipPath);
                    Log.Debug($"[ArchiveService] 压缩包重命名为: {newZipFileName}");
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"[ArchiveService] 重命名压缩包失败: {ex.Message}");
            }
        }

        // ================= 辅助方法 =================

        private async Task AddPathToZipAsync(ZipArchive zip, string sourceFullPath, string entryBasePath)
        {
            if (File.Exists(sourceFullPath))
            {
                await AddFileToZipAsync(zip, sourceFullPath, entryBasePath);
            }
            else if (Directory.Exists(sourceFullPath))
            {
                // 如果是文件夹，遍历内部所有文件加入 ZIP
                var files = Directory.GetFiles(sourceFullPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // 计算在 ZIP 内的相对路径
                    string relativePath = Path.GetRelativePath(sourceFullPath, file);
                    string zipEntryName = Path.Combine(entryBasePath, relativePath).Replace("\\", "/");
                    await AddFileToZipAsync(zip, file, zipEntryName);
                }
            }
        }

        private async Task AddFileToZipAsync(ZipArchive zip, string filePath, string entryName)
        {
            try
            {
                // 使用 FileShare.ReadWrite 强行读取（哪怕 YJK 还没完全释放句柄也能读）
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var entry = zip.CreateEntry(entryName.Replace("\\", "/"), CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                await fs.CopyToAsync(entryStream);
            }
            catch { /* 忽略个别因权限锁定而无法读取的文件 */ }
        }
    }
}