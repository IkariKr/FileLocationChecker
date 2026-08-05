using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace FileLocationChecker.Services
{
    /// <summary>
    /// Markdown 文件引用资源（图片、附件、链接）有效性校验服务。
    /// </summary>
    public class MdResourceCheckerService
    {
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().Build();

        // Obsidian Wiki 链接不是标准 Markdown 语法，需在排除代码区域后单独提取。
        private static readonly Regex WikiLinkRegex = new Regex(
            @"(?:!\[\[|\[\[)(?<path>[^|\]]+?)(?:\|[^\]]*)?\]\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex HtmlImgRegex = new Regex(
            @"<img\b[^>]*\bsrc\s*=\s*(?:""(?<path>[^""]*)""|'(?<path>[^']*)')",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex FenceRegex = new Regex(
            @"^[ ]{0,3}(?<marker>`{3,}|~{3,})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex HtmlCommentRegex = new Regex(
            @"<!--[\s\S]*?-->",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// 检查 Markdown 文件中引用的本地资源是否存在，并返回缺失的资源路径列表。
        /// </summary>
        /// <param name="mdFilePath">Markdown 文件完整路径。</param>
        /// <param name="vaultRoot">Vault 根目录（可选），用于根相对资源与路径边界校验。</param>
        public static List<string> CheckMissingResources(string mdFilePath, string? vaultRoot = null)
        {
            var missingList = new List<string>();
            if (!TryReadMarkdownFile(mdFilePath, out string content, out string mdDir))
                return missingList;

            foreach (var reference in ExtractAllResourceReferences(content))
            {
                string cleanPath = CleanResourcePath(reference.Path);
                if (string.IsNullOrEmpty(cleanPath) || IsExternalOrAnchorLink(cleanPath))
                    continue;

                if (!IsResourceFileExists(cleanPath, mdDir, vaultRoot, reference.IsWikiLink))
                    missingList.Add(cleanPath);
            }

            return missingList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 检索 Markdown 文件中引用的网络远程外链（http/https/ftp）。
        /// </summary>
        public static List<string> CheckExternalLinks(string mdFilePath)
        {
            var externalList = new List<string>();
            if (!TryReadMarkdownFile(mdFilePath, out string content, out _))
                return externalList;

            foreach (var reference in ExtractAllResourceReferences(content))
            {
                string cleanPath = CleanResourcePath(reference.Path);
                if (IsExternalWebLink(cleanPath))
                    externalList.Add(cleanPath);
            }

            return externalList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool TryReadMarkdownFile(string mdFilePath, out string content, out string mdDir)
        {
            content = string.Empty;
            mdDir = string.Empty;

            if (string.IsNullOrWhiteSpace(mdFilePath) || !File.Exists(mdFilePath))
                return false;

            string extension = Path.GetExtension(mdFilePath);
            if (!string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string? directory = Path.GetDirectoryName(mdFilePath);
            if (string.IsNullOrEmpty(directory))
                return false;

            try
            {
                content = File.ReadAllText(mdFilePath);
                mdDir = Path.GetFullPath(directory);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static List<ResourceReference> ExtractAllResourceReferences(string content)
        {
            var references = new List<ResourceReference>();
            MarkdownDocument document = Markdown.Parse(content, MarkdownPipeline);

            foreach (LinkInline link in document.Descendants<LinkInline>())
            {
                if (!string.IsNullOrWhiteSpace(link.Url))
                    references.Add(new ResourceReference(link.Url, isWikiLink: false));
            }

            foreach (HtmlInline htmlInline in document.Descendants<HtmlInline>())
                AddHtmlImageReferences(htmlInline.Tag, references);

            foreach (HtmlBlock htmlBlock in document.Descendants<HtmlBlock>())
                AddHtmlBlockImageReferences(content, htmlBlock, references);

            string nonCodeContent = SanitizeCodeRegions(content);
            foreach (Match match in WikiLinkRegex.Matches(nonCodeContent))
            {
                string path = match.Groups["path"].Value;
                if (!string.IsNullOrWhiteSpace(path))
                    references.Add(new ResourceReference(path, isWikiLink: true));
            }

            return references;
        }

        private static void AddHtmlBlockImageReferences(
            string content,
            HtmlBlock htmlBlock,
            ICollection<ResourceReference> references)
        {
            if (htmlBlock.Type is HtmlBlockType.Comment or HtmlBlockType.ScriptPreOrStyle)
                return;

            int start = htmlBlock.Span.Start;
            int length = htmlBlock.Span.Length;
            if (start < 0 || length <= 0 || start >= content.Length)
                return;

            int availableLength = Math.Min(length, content.Length - start);
            AddHtmlImageReferences(content.Substring(start, availableLength), references);
        }

        private static void AddHtmlImageReferences(string html, ICollection<ResourceReference> references)
        {
            if (html.StartsWith("<!--", StringComparison.Ordinal))
                return;

            foreach (Match match in HtmlImgRegex.Matches(html))
            {
                string path = match.Groups["path"].Value;
                if (!string.IsNullOrWhiteSpace(path))
                    references.Add(new ResourceReference(path, isWikiLink: false));
            }
        }

        private static string SanitizeCodeRegions(string content)
        {
            var sanitized = new StringBuilder(content);
            bool isInFence = false;
            char fenceCharacter = '\0';
            int fenceLength = 0;

            for (int lineStart = 0; lineStart < content.Length;)
            {
                int lineEnd = lineStart;
                while (lineEnd < content.Length && content[lineEnd] != '\r' && content[lineEnd] != '\n')
                    lineEnd++;

                string line = content.Substring(lineStart, lineEnd - lineStart);
                Match fenceMatch = FenceRegex.Match(line);
                bool maskLine = isInFence;

                if (!isInFence && fenceMatch.Success)
                {
                    string marker = fenceMatch.Groups["marker"].Value;
                    isInFence = true;
                    fenceCharacter = marker[0];
                    fenceLength = marker.Length;
                    maskLine = true;
                }
                else if (isInFence && fenceMatch.Success)
                {
                    string marker = fenceMatch.Groups["marker"].Value;
                    if (marker[0] == fenceCharacter && marker.Length >= fenceLength)
                        isInFence = false;
                }

                if (maskLine || IsIndentedCodeLine(line))
                    MaskRange(sanitized, lineStart, lineEnd);
                else
                    MaskInlineCode(content, sanitized, lineStart, lineEnd);

                lineStart = MoveToNextLine(content, lineEnd);
            }

            foreach (Match comment in HtmlCommentRegex.Matches(content))
                MaskRange(sanitized, comment.Index, comment.Index + comment.Length);

            return sanitized.ToString();
        }

        private static bool IsIndentedCodeLine(string line)
        {
            if (line.StartsWith('\t'))
                return true;

            int leadingSpaces = 0;
            while (leadingSpaces < line.Length && line[leadingSpaces] == ' ')
                leadingSpaces++;

            return leadingSpaces >= 4;
        }

        private static int MoveToNextLine(string content, int lineEnd)
        {
            int next = lineEnd;
            if (next < content.Length && content[next] == '\r')
                next++;
            if (next < content.Length && content[next] == '\n')
                next++;
            return next;
        }

        private static void MaskInlineCode(string source, StringBuilder destination, int start, int end)
        {
            for (int index = start; index < end; index++)
            {
                if (source[index] != '`')
                    continue;

                int delimiterLength = CountRun(source, index, end, '`');
                int closingIndex = FindClosingDelimiter(source, index + delimiterLength, end, delimiterLength);
                if (closingIndex < 0)
                {
                    index += delimiterLength - 1;
                    continue;
                }

                MaskRange(destination, index, closingIndex + delimiterLength);
                index = closingIndex + delimiterLength - 1;
            }
        }

        private static int FindClosingDelimiter(string source, int start, int end, int delimiterLength)
        {
            for (int index = start; index < end; index++)
            {
                if (source[index] != '`')
                    continue;

                int runLength = CountRun(source, index, end, '`');
                if (runLength == delimiterLength)
                    return index;

                index += runLength - 1;
            }

            return -1;
        }

        private static int CountRun(string source, int start, int end, char character)
        {
            int index = start;
            while (index < end && source[index] == character)
                index++;
            return index - start;
        }

        private static void MaskRange(StringBuilder content, int start, int end)
        {
            for (int index = start; index < end; index++)
                content[index] = ' ';
        }

        private static string CleanResourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string result = path.Trim();
            if (result.Length >= 2 && result[0] == '<' && result[^1] == '>')
                result = result[1..^1].Trim();

            int hashIndex = result.IndexOf('#');
            if (hashIndex >= 0)
                result = result[..hashIndex];

            int queryIndex = result.IndexOf('?');
            if (queryIndex >= 0)
                result = result[..queryIndex];

            try
            {
                result = Uri.UnescapeDataString(result);
            }
            catch (UriFormatException)
            {
                return result.Trim();
            }

            return result.Trim();
        }

        private static bool IsExternalWebLink(string path)
        {
            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExternalOrAnchorLink(string path)
        {
            return IsExternalWebLink(path) ||
                   path.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("#", StringComparison.Ordinal);
        }

        private static bool IsResourceFileExists(string path, string mdDir, string? vaultRoot, bool isWikiLink)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? normalizedVaultRoot = GetExistingDirectoryPath(vaultRoot);

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                if (normalizedVaultRoot == null)
                    return false;

                AddRelativeCandidate(candidates, normalizedVaultRoot, path.TrimStart('/'), normalizedVaultRoot);
            }
            else if (Path.IsPathFullyQualified(path))
            {
                TryAddAbsoluteCandidate(candidates, path);
            }
            else
            {
                string boundary = normalizedVaultRoot ?? mdDir;
                AddRelativeCandidate(candidates, mdDir, path, boundary);

                if (normalizedVaultRoot != null)
                    AddRelativeCandidate(candidates, normalizedVaultRoot, path, normalizedVaultRoot);
            }

            foreach (string candidate in candidates)
            {
                if (PathExists(candidate))
                    return true;

                if (isWikiLink && string.IsNullOrEmpty(Path.GetExtension(candidate)) && PathExists(candidate + ".md"))
                    return true;
            }

            return false;
        }

        private static string? GetExistingDirectoryPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static void AddRelativeCandidate(
            ISet<string> candidates,
            string baseDirectory,
            string relativePath,
            string boundaryDirectory)
        {
            try
            {
                string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                string candidate = Path.GetFullPath(Path.Combine(baseDirectory, normalizedRelativePath));
                if (IsWithinRoot(candidate, boundaryDirectory))
                    candidates.Add(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // 无法规范化的引用路径按缺失处理。
            }
        }

        private static void TryAddAbsoluteCandidate(ISet<string> candidates, string path)
        {
            try
            {
                candidates.Add(Path.GetFullPath(path));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // 无法规范化的引用路径按缺失处理。
            }
        }

        private static bool IsWithinRoot(string candidate, string root)
        {
            try
            {
                string relative = Path.GetRelativePath(root, candidate);
                return !string.Equals(relative, "..", StringComparison.Ordinal) &&
                       !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                       !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) &&
                       !Path.IsPathRooted(relative);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool PathExists(string path)
        {
            try
            {
                return File.Exists(path) || Directory.Exists(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private sealed class ResourceReference
        {
            public ResourceReference(string path, bool isWikiLink)
            {
                Path = path;
                IsWikiLink = isWikiLink;
            }

            public string Path { get; }

            public bool IsWikiLink { get; }
        }
    }
}
