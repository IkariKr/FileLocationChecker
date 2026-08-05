using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileLocationChecker.Services
{
    /// <summary>
    /// Markdown 文件引用资源（图片、附件、链接）有效性校验服务
    /// Markdown file referenced resources (images, attachments, links) validity checking service
    /// </summary>
    public class MdResourceCheckerService
    {
        // 匹配标准的 Markdown 图片/链接语法: ![alt](path) 或 [text](path)（排除前面紧跟 ![[ 或 [[ 的 Wiki 链）
        private static readonly Regex MdLinkRegex = new Regex(
            @"(?<!\[)(?:!\[[^\]]*\]|\[[^\]]*\])\((?<path>[^)]+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 匹配 Obsidian 双链/嵌入语法: ![[path|alt]] 或 [[path|alt]]
        private static readonly Regex WikiLinkRegex = new Regex(
            @"(?:!\[\[|\[\[)(?<path>[^|\]]+?)(?:\|[^\]]+?)?\]\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 匹配 HTML <img> 标签的 src 属性
        private static readonly Regex HtmlImgRegex = new Regex(
            @"<img[^>]+src=[""'](?<path>[^""']+)[""']",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 检查 Markdown 文件中引用的本地资源是否存在，并返回缺失的资源路径列表
        /// Check if local resources referenced in a Markdown file exist, returning a list of missing resource paths
        /// </summary>
        /// <param name="mdFilePath">Markdown 文件完整路径 / Full path of Markdown file</param>
        /// <param name="folderB">目标根文件夹 B 的路径（可选，用于 Vault 根目录绝对匹配） / Folder B root path</param>
        /// <returns>缺失的资源路径列表 / List of missing resource paths</returns>
        public static List<string> CheckMissingResources(string mdFilePath, string? folderB = null)
        {
            var missingList = new List<string>();

            if (string.IsNullOrEmpty(mdFilePath) || !File.Exists(mdFilePath))
                return missingList;

            string ext = Path.GetExtension(mdFilePath).ToLowerInvariant();
            if (ext != ".md" && ext != ".markdown")
                return missingList;

            string? mdDir = Path.GetDirectoryName(mdFilePath);
            if (string.IsNullOrEmpty(mdDir))
                return missingList;

            string content;
            try
            {
                content = File.ReadAllText(mdFilePath);
            }
            catch
            {
                return missingList;
            }

            var extractedPaths = ExtractAllResourcePaths(content);

            foreach (var rawPath in extractedPaths)
            {
                string cleanPath = CleanResourcePath(rawPath);
                if (string.IsNullOrEmpty(cleanPath))
                    continue;

                // 过滤网络远程链接或锚点
                // Ignore web URLs, mailto or anchor links
                if (IsExternalOrAnchorLink(cleanPath))
                    continue;

                // 校验文件是否存在
                // Verify file existence
                if (!IsResourceFileExists(cleanPath, mdDir, folderB))
                {
                    missingList.Add(cleanPath);
                }
            }

            return missingList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 检索 Markdown 文件中引用的所有网络远程外链 (http/https/ftp)
        /// Search for all external remote links (http/https/ftp) in a Markdown file
        /// </summary>
        /// <param name="mdFilePath">Markdown 文件完整路径 / Full path of Markdown file</param>
        /// <returns>外链 URL 列表 / List of external link URLs</returns>
        public static List<string> CheckExternalLinks(string mdFilePath)
        {
            var externalList = new List<string>();

            if (string.IsNullOrEmpty(mdFilePath) || !File.Exists(mdFilePath))
                return externalList;

            string ext = Path.GetExtension(mdFilePath).ToLowerInvariant();
            if (ext != ".md" && ext != ".markdown")
                return externalList;

            string content;
            try
            {
                content = File.ReadAllText(mdFilePath);
            }
            catch
            {
                return externalList;
            }

            var extractedPaths = ExtractAllResourcePaths(content);

            foreach (var rawPath in extractedPaths)
            {
                string cleanPath = CleanResourcePath(rawPath);
                if (string.IsNullOrEmpty(cleanPath))
                    continue;

                if (IsExternalWebLink(cleanPath))
                {
                    externalList.Add(cleanPath);
                }
            }

            return externalList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsExternalWebLink(string path)
        {
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static List<string> ExtractAllResourcePaths(string content)
        {
            var list = new List<string>();

            foreach (Match match in MdLinkRegex.Matches(content))
            {
                string path = match.Groups["path"].Value;
                if (!string.IsNullOrEmpty(path)) list.Add(path);
            }

            foreach (Match match in WikiLinkRegex.Matches(content))
            {
                string path = match.Groups["path"].Value;
                if (!string.IsNullOrEmpty(path)) list.Add(path);
            }

            foreach (Match match in HtmlImgRegex.Matches(content))
            {
                string path = match.Groups["path"].Value;
                if (!string.IsNullOrEmpty(path)) list.Add(path);
            }

            return list;
        }

        private static string CleanResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string result = path.Trim();

            // 若 Markdown 包含带有引号的 title: [text](path "title")
            int quoteIndex = result.IndexOfAny(new[] { '"', '\'' });
            if (quoteIndex > 0)
            {
                result = result.Substring(0, quoteIndex).Trim();
            }

            // 去除 URL 锚点和 query 参数 (# / ?)
            int hashIndex = result.IndexOf('#');
            if (hashIndex >= 0)
            {
                result = result.Substring(0, hashIndex);
            }
            int queryIndex = result.IndexOf('?');
            if (queryIndex >= 0)
            {
                result = result.Substring(0, queryIndex);
            }

            // 解码 URL 编码字符（如 %20 -> 空格）
            try
            {
                result = Uri.UnescapeDataString(result);
            }
            catch { }

            return result.Trim();
        }

        private static bool IsExternalOrAnchorLink(string path)
        {
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("#"))
            {
                return true;
            }
            return false;
        }

        private static bool IsResourceFileExists(string relativePath, string mdDir, string? baseFolder)
        {
            try
            {
                // 1. 优先校验相对于当前 MD 文件所在目录的路径
                // 1. Check relative to current MD file directory
                string fullPath1 = Path.GetFullPath(Path.Combine(mdDir, relativePath));
                if (File.Exists(fullPath1) || Directory.Exists(fullPath1))
                    return true;

                // 2. 校验相对于基准根目录 (Folder A 或 Folder B) 的路径
                // 2. Check relative to base folder (Folder A or Folder B)
                if (!string.IsNullOrEmpty(baseFolder) && Directory.Exists(baseFolder))
                {
                    string fullPath2 = Path.GetFullPath(Path.Combine(baseFolder, relativePath));
                    if (File.Exists(fullPath2) || Directory.Exists(fullPath2))
                        return true;
                }

                // 3. 智能向上回退搜寻父目录中的相对资源文件（支持剥离当前目录重叠前缀）
                // 3. Smart fallback searching parent directories (supports stripping overlapping dir prefixes)
                string normalizedRel = relativePath.Replace('\\', '/').TrimStart('/');
                var currDirInfo = new DirectoryInfo(mdDir);

                while (currDirInfo != null)
                {
                    string parentPath = currDirInfo.FullName;

                    // A) 直接拼接 parentPath + relativePath
                    string testPath1 = Path.GetFullPath(Path.Combine(parentPath, normalizedRel));
                    if (File.Exists(testPath1) || Directory.Exists(testPath1))
                        return true;

                    // B) 尝试按斜杠拆分相对路径，匹配当前 parentPath 下是否存在后半部分子路径
                    string[] relSegments = normalizedRel.Split('/');
                    for (int s = 1; s < relSegments.Length; s++)
                    {
                        string partialRel = string.Join("/", relSegments, s, relSegments.Length - s);
                        string testPath2 = Path.GetFullPath(Path.Combine(parentPath, partialRel));
                        if (File.Exists(testPath2) || Directory.Exists(testPath2))
                            return true;
                    }

                    // 若已到达驱动器根目录 (如 H:\)，终止循环
                    if (currDirInfo.Parent == null || string.Equals(currDirInfo.FullName, currDirInfo.Root.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    currDirInfo = currDirInfo.Parent;
                }
            }
            catch { }

            return false;
        }
    }
}
