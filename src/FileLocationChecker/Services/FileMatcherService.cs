using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileLocationChecker.Models;

namespace FileLocationChecker.Services
{
    /// <summary>
    /// 进度报告消息结构体
    /// Progress report message struct
    /// </summary>
    public struct MatchProgressInfo
    {
        /// <summary>
        /// 已处理文件数
        /// Number of processed files
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// 总文件数
        /// Total count of files
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 状态描述消息
        /// Status message
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// 最新生成的匹配条目
        /// Newly generated match item
        /// </summary>
        public FileMatchResult? LatestResult { get; set; }
    }

    /// <summary>
    /// 文件匹配服务类，负责文件夹中文件的扫描与定位
    /// File matcher service class responsible for scanning and locating files across folders
    /// </summary>
    public class FileMatcherService
    {
        /// <summary>
        /// 异步执行文件夹匹配检查
        /// Asynchronously perform folder matching check
        /// </summary>
        /// <param name="options">匹配配置参数 / Match options</param>
        /// <param name="progress">进度回调接口 / Progress callback</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>匹配结果列表 / List of match results</returns>
        public async Task<List<FileMatchResult>> MatchFilesAsync(
            MatchOptions options,
            IProgress<MatchProgressInfo>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var results = new List<FileMatchResult>();

                if (string.IsNullOrEmpty(options.FolderA) || !Directory.Exists(options.FolderA))
                {
                    throw new DirectoryNotFoundException($"文件夹 A 不存在或无效: {options.FolderA}");
                }

                if (string.IsNullOrEmpty(options.FolderB) || !Directory.Exists(options.FolderB))
                {
                    throw new DirectoryNotFoundException($"文件夹 B 不存在或无效: {options.FolderB}");
                }

                // 1. 扫描文件夹 B 并建立查找索引
                // 1. Scan Folder B and build lookup index
                progress?.Report(new MatchProgressInfo
                {
                    StatusText = "正在扫描文件夹 B 并建立索引..."
                });

                var targetFilesIndex = BuildFolderBIndex(options.FolderB, options, cancellationToken);

                // 2. 获取文件夹 A 中的源文件列表
                // 2. Fetch source file list from Folder A
                progress?.Report(new MatchProgressInfo
                {
                    StatusText = "正在检索文件夹 A 中的文件列表..."
                });

                SearchOption searchOptionA = options.RecursiveA ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                List<string> filesInA = EnumerateFilesSafe(options.FolderA, searchOptionA, cancellationToken);

                int totalCount = filesInA.Count;
                int processed = 0;

                progress?.Report(new MatchProgressInfo
                {
                    ProcessedCount = 0,
                    TotalCount = totalCount,
                    StatusText = $"文件夹 A 中共发现 {totalCount} 个文件，开始检查定位..."
                });

                // 3. 开始遍历 A 中的文件进行对比
                // 3. Traverse files in A to perform comparison
                for (int i = 0; i < totalCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string fileAPath = filesInA[i];
                    processed++;

                    var result = CompareFileAgainstIndex(i + 1, fileAPath, options, targetFilesIndex);
                    results.Add(result);

                    progress?.Report(new MatchProgressInfo
                    {
                        ProcessedCount = processed,
                        TotalCount = totalCount,
                        StatusText = $"正在处理 ({processed}/{totalCount}): {result.FileName}",
                        LatestResult = result
                    });
                }

                progress?.Report(new MatchProgressInfo
                {
                    ProcessedCount = totalCount,
                    TotalCount = totalCount,
                    StatusText = "检查完成！"
                });

                return results;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 安全地递归或单层枚举文件夹中的所有文件
        /// Safely enumerate all files in a folder recursively or top-directory only
        /// </summary>
        private List<string> EnumerateFilesSafe(string rootPath, SearchOption searchOption, CancellationToken token)
        {
            var filesList = new List<string>();
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string currentDir = stack.Pop();

                try
                {
                    string[] currentFiles = Directory.GetFiles(currentDir);
                    filesList.AddRange(currentFiles);

                    if (searchOption == SearchOption.AllDirectories)
                    {
                        string[] subDirs = Directory.GetDirectories(currentDir);
                        foreach (var dir in subDirs)
                        {
                            stack.Push(dir);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // 忽略无权限查看的系统文件夹 / Ignore unauthorized folders
                }
                catch (DirectoryNotFoundException)
                {
                    // 忽略缺失的路径 / Ignore missing paths
                }
            }

            return filesList;
        }

        /// <summary>
        /// 建立文件夹 B 的高效多重条件字典索引
        /// Build high-efficiency multi-condition dictionary index for Folder B
        /// </summary>
        private Dictionary<string, List<string>> BuildFolderBIndex(
            string folderB,
            MatchOptions options,
            CancellationToken token)
        {
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // B 文件夹始终进行递归检索
            // Folder B is always searched recursively
            List<string> filesInB = EnumerateFilesSafe(folderB, SearchOption.AllDirectories, token);

            foreach (var filePath in filesInB)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    string key = GenerateIndexKey(filePath, options);

                    if (!index.TryGetValue(key, out var list))
                    {
                        list = new List<string>();
                        index[key] = list;
                    }
                    list.Add(filePath);
                }
                catch (Exception)
                {
                    // 忽略无法获取 FileInfo 的异常 / Ignore exceptions when getting FileInfo
                }
            }

            return index;
        }

        /// <summary>
        /// 根据配置选择生成索引 Key
        /// Generate index key based on matching options
        /// </summary>
        private string GenerateIndexKey(string filePath, MatchOptions options)
        {
            if (options.CheckFileName)
            {
                // 若检查文件名，直接以小写文件名作为主索引 Key
                // If checking file name, use lowercase file name as primary index key
                return Path.GetFileName(filePath).ToLowerInvariant();
            }

            if (options.CheckFileSize && options.SizeTolerancePercent <= 0)
            {
                // 若仅检查精确文件大小，以字节数作为 Key
                // If only checking exact file size, use byte count as key
                var fileInfo = new FileInfo(filePath);
                return fileInfo.Length.ToString();
            }

            // 其他情况索引到通配 Key
            // Fallback to wildcard key
            return "*";
        }

        /// <summary>
        /// 将 A 中的文件与 B 的索引字典匹配
        /// Match file from A against the index of B
        /// </summary>
        private FileMatchResult CompareFileAgainstIndex(
            int indexNumber,
            string fileAPath,
            MatchOptions options,
            Dictionary<string, List<string>> indexB)
        {
            var result = new FileMatchResult
            {
                Index = indexNumber,
                SourcePath = fileAPath,
                FileName = Path.GetFileName(fileAPath)
            };

            try
            {
                var fileInfoA = new FileInfo(fileAPath);
                result.FileSizeA = fileInfoA.Length;
                result.FormattedSizeA = FormatBytes(fileInfoA.Length);

                string searchKey = GenerateIndexKey(fileAPath, options);

                if (indexB.TryGetValue(searchKey, out var matchedPaths) && matchedPaths.Count > 0)
                {
                    List<string> candidatePaths = new List<string>();

                    foreach (var path in matchedPaths)
                    {
                        // 1. 文件大小与容差匹配校验
                        if (options.CheckFileSize)
                        {
                            try
                            {
                                var fileInfoB = new FileInfo(path);
                                if (!IsSizeWithinTolerance(fileInfoA.Length, fileInfoB.Length, options.SizeTolerancePercent))
                                {
                                    continue;
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        // 2. 排除 A 自身路径校验
                        if (options.ExcludeAPath && IsSameOrSubPath(path, options.FolderA))
                        {
                            continue;
                        }

                        candidatePaths.Add(path);
                    }

                    result.MatchCount = candidatePaths.Count;
                    result.TargetPath = string.Join("; ", candidatePaths);

                    if (candidatePaths.Count == 1)
                    {
                        result.Status = MatchStatus.Found;
                        try
                        {
                            var infoB = new FileInfo(candidatePaths[0]);
                            result.FileSizeB = infoB.Length;
                            result.FormattedSizeB = FormatBytes(infoB.Length);
                        }
                        catch { }
                    }
                    else if (candidatePaths.Count > 1)
                    {
                        result.Status = MatchStatus.MultipleMatches;
                        try
                        {
                            var infoB = new FileInfo(candidatePaths[0]);
                            result.FileSizeB = infoB.Length;
                            result.FormattedSizeB = FormatBytes(infoB.Length);
                        }
                        catch { }
                    }
                    else
                    {
                        result.Status = MatchStatus.NotFound;
                        result.TargetPath = string.Empty;
                        result.FormattedSizeB = "-";
                    }

                    // 检查 Markdown 文件引用的资源有效性
                    if (candidatePaths.Count > 0 && options.CheckMdResources)
                    {
                        var allMissing = new List<string>();
                        foreach (var path in candidatePaths)
                        {
                            var missing = MdResourceCheckerService.CheckMissingResources(path, options.FolderB);
                            allMissing.AddRange(missing);
                        }

                        if (allMissing.Count > 0)
                        {
                            var distinctMissing = allMissing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            result.HasMissingResources = true;
                            result.MissingResourcesText = $"⚠️ Markdown 缺失引用的资源 ({distinctMissing.Count} 个):\n" +
                                                          string.Join("\n", distinctMissing.Select(m => $"  • {m}"));
                        }
                    }
                }
                else
                {
                    result.Status = MatchStatus.NotFound;
                    result.TargetPath = string.Empty;
                    result.FormattedSizeB = "-";
                }
            }
            catch (Exception ex)
            {
                result.Status = MatchStatus.Error;
                result.Message = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 判断文件字节大小是否在容差百分比范围内
        /// Check if file byte size is within specified tolerance percentage
        /// </summary>
        public static bool IsSizeWithinTolerance(long sizeA, long sizeB, double tolerancePercent)
        {
            if (tolerancePercent <= 0)
            {
                return sizeA == sizeB;
            }

            if (sizeA == sizeB)
                return true;

            long max = Math.Max(sizeA, sizeB);
            if (max == 0)
                return true;

            double diff = Math.Abs(sizeA - sizeB);
            double ratio = (diff / (double)max) * 100.0;
            return ratio <= tolerancePercent;
        }

        /// <summary>
        /// 检查目标路径是否与基准文件夹相同或是其子路径
        /// Check if target path is identical to or a sub-path of the base folder
        /// </summary>
        private static bool IsSameOrSubPath(string targetPath, string baseFolder)
        {
            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(baseFolder))
                return false;

            try
            {
                string fullTarget = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullBase = Path.GetFullPath(baseFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(fullTarget, fullBase, StringComparison.OrdinalIgnoreCase))
                    return true;

                return fullTarget.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       fullTarget.StartsWith(fullBase + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化文件字节大小为 KB/MB/GB
        /// Format file byte size into KB/MB/GB
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0)
                return "0 B";
            long bytesAbs = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytesAbs, 1024)));
            double num = Math.Round(bytesAbs / Math.Pow(1024, place), 1);
            return (Math.Sign(bytes) * num).ToString() + " " + suf[place];
        }
    }
}
