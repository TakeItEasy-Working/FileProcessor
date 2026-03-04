using FileProcessor.Core.Models;
using FileProcessor.Core.Helpers;
using FileProcessor.DebugHelpers;
using System.Text.RegularExpressions;

namespace FileProcessor.Infrastructure.Services
{
    public class PdfExportService
    {
        public async Task ExportVersionDataToPdfAsync(string targetFolder, string versionId, string note, IEnumerable<ProcessedResult> versionData)
        {
            if (string.IsNullOrEmpty(targetFolder) || !versionData.Any()) return;

            string safeNote = FileHelper.GetSafeFileName(note);
            string suffix = string.IsNullOrWhiteSpace(safeNote) ? versionId : $"{versionId}_{safeNote}";
            var fileGroups = versionData.GroupBy(r => r.SourceFileName).ToList();

            await Task.Run(() =>
            {
                foreach (var group in fileGroups)
                {
                    string safeSourceName = FileHelper.GetSafeFileName(Path.GetFileNameWithoutExtension(group.Key));
                    string pdfFileName = $"{safeSourceName}_{suffix}.pdf";
                    string pdfFilePath = Path.Combine(targetFolder, pdfFileName);

                    // 智能防御一：如果该文件下任何一个数据块的列数 > 6，强制采用横向排版
                    bool useLandscape = group.Any(r => r.Columns.Count > 6);

                    // 实例化构建器
                    var builder = new EngineeringReportBuilder(group.Key, versionId, note);

                    // 堆砌积木
                    foreach (var result in group)
                    {
                        //builder.AddSectionTitle(result.StandardBlockName);
                        //builder.AddEngineeringTable(result);

                        // 一步到位，自动防止标题和表格脱节
                        builder.AddEngineeringDataBlock(result.StandardBlockName, result);
                    }

                    // 生成 PDF
                    builder.BuildAndSave(pdfFilePath, useLandscape);
                    Log.Debug($"[PdfExport] 成功导出文件: {pdfFileName}");
                }
            });
        }

        /// <summary>
        /// 通用 DataTable 导出为 PDF
        /// </summary>
        public void ExportDataTableToPdf(System.Data.DataTable dataTable, string filePath, string title, string note)
        {
            bool useLandscape = dataTable.Columns.Count > 6;
            var builder = new EngineeringReportBuilder(title, "对比报表", note);
            //builder.AddSectionTitle("CROSS-VERSION COMPARISON");
            //builder.AddDataTable(dataTable);

            // 一步到位
            builder.AddComparisonDataBlock("CROSS-VERSION COMPARISON", dataTable);

            builder.BuildAndSave(filePath, useLandscape);
        }
    }
}