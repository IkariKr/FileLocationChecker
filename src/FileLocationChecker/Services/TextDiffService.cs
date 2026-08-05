using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileLocationChecker.Services
{
    /// <summary>
    /// 差异类型
    /// Diff kind
    /// </summary>
    public enum DiffKind
    {
        /// <summary>
        /// 无变化
        /// Unchanged
        /// </summary>
        Unchanged,

        /// <summary>
        /// 新增行 (B 存在，A 不存在)
        /// Added line (Present in B, not in A)
        /// </summary>
        Added,

        /// <summary>
        /// 删除行 (A 存在，B 不存在)
        /// Deleted line (Present in A, not in B)
        /// </summary>
        Deleted
    }

    /// <summary>
    /// 单行差异条目数据模型
    /// Single line diff item model
    /// </summary>
    public class DiffLineItem
    {
        /// <summary>
        /// A 文件中的行号
        /// Line number in File A
        /// </summary>
        public int? LineA { get; set; }

        /// <summary>
        /// B 文件中的行号
        /// Line number in File B
        /// </summary>
        public int? LineB { get; set; }

        /// <summary>
        /// 文本内容
        /// Text content
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 差异类型
        /// Diff kind
        /// </summary>
        public DiffKind Kind { get; set; }

        /// <summary>
        /// 差异前缀标记 (+, -, 空)
        /// Diff prefix marker
        /// </summary>
        public string Prefix => Kind switch
        {
            DiffKind.Added => "+",
            DiffKind.Deleted => "-",
            _ => " "
        };
    }

    /// <summary>
    /// 文本差异对比计算服务
    /// Text difference comparison service
    /// </summary>
    public class TextDiffService
    {
        /// <summary>
        /// 检查指定文件是否为可读取的文本文件
        /// Check if the specified file is a readable text file
        /// </summary>
        /// <param name="filePath">文件路径 / File path</param>
        /// <returns>如果是文本文件返回 true / Returns true if text file</returns>
        public static bool IsTextFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                byte[] buffer = new byte[1024];
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (buffer[i] == 0) // 包含 NUL 字节通常是二进制文件 / Contains NUL byte
                            return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 对比两个文本文件的逐行差异
        /// Compare line-by-line difference between two text files
        /// </summary>
        /// <param name="fileAPath">文件 A 路径 / File A path</param>
        /// <param name="fileBPath">文件 B 路径 / File B path</param>
        /// <returns>逐行差异结果列表 / List of line diff items</returns>
        public static List<DiffLineItem> CompareFiles(string fileAPath, string fileBPath)
        {
            string[] linesA = File.Exists(fileAPath) ? File.ReadAllLines(fileAPath, Encoding.UTF8) : Array.Empty<string>();
            string[] linesB = File.Exists(fileBPath) ? File.ReadAllLines(fileBPath, Encoding.UTF8) : Array.Empty<string>();

            return ComputeLineDiff(linesA, linesB);
        }

        /// <summary>
        /// 使用动态规划 (LCS) 算法计算两组字符串数组的差异
        /// Compute diff between two string arrays using dynamic programming (LCS)
        /// </summary>
        public static List<DiffLineItem> ComputeLineDiff(string[] linesA, string[] linesB)
        {
            int n = linesA.Length;
            int m = linesB.Length;

            int[,] dp = new int[n + 1, m + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (linesA[i] == linesB[j])
                        dp[i + 1, j + 1] = dp[i, j] + 1;
                    else
                        dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            var result = new List<DiffLineItem>();
            int currA = n, currB = m;

            while (currA > 0 || currB > 0)
            {
                if (currA > 0 && currB > 0 && linesA[currA - 1] == linesB[currB - 1])
                {
                    result.Add(new DiffLineItem
                    {
                        LineA = currA,
                        LineB = currB,
                        Text = linesA[currA - 1],
                        Kind = DiffKind.Unchanged
                    });
                    currA--;
                    currB--;
                }
                else if (currB > 0 && (currA == 0 || dp[currA, currB - 1] >= dp[currA - 1, currB]))
                {
                    result.Add(new DiffLineItem
                    {
                        LineA = null,
                        LineB = currB,
                        Text = linesB[currB - 1],
                        Kind = DiffKind.Added
                    });
                    currB--;
                }
                else if (currA > 0 && (currB == 0 || dp[currA, currB - 1] < dp[currA - 1, currB]))
                {
                    result.Add(new DiffLineItem
                    {
                        LineA = currA,
                        LineB = null,
                        Text = linesA[currA - 1],
                        Kind = DiffKind.Deleted
                    });
                    currA--;
                }
            }

            result.Reverse();
            return result;
        }
    }
}
