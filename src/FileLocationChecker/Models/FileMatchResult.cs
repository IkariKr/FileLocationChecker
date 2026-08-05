using System;

namespace FileLocationChecker.Models
{
    /// <summary>
    /// 文件匹配结果状态
    /// File match result status
    /// </summary>
    public enum MatchStatus
    {
        /// <summary>
        /// 已找到匹配文件
        /// Matched file found
        /// </summary>
        Found,

        /// <summary>
        /// 未找到匹配文件
        /// No matched file found
        /// </summary>
        NotFound,

        /// <summary>
        /// 找到多个匹配文件
        /// Multiple matching files found
        /// </summary>
        MultipleMatches,

        /// <summary>
        /// 发生错误或无权限
        /// Error or permission denied
        /// </summary>
        Error
    }

    /// <summary>
    /// 文件匹配结果模型类
    /// File match result model class
    /// </summary>
    public class FileMatchResult
    {
        /// <summary>
        /// 序号
        /// Index number
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 文件夹 A 中的文件名或相对路径
        /// File name or relative path in Folder A
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 文件夹 A 中的完整文件路径
        /// Full file path in Folder A
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// 在文件夹 B 中查找到的对应文件路径 (若有多个以分号分隔)
        /// Matched file path(s) in Folder B
        /// </summary>
        public string TargetPath { get; set; } = string.Empty;

        /// <summary>
        /// 匹配到的文件数量
        /// Number of matched files in B
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// 匹配状态
        /// Match status
        /// </summary>
        public MatchStatus Status { get; set; }

        /// <summary>
        /// 状态描述文本
        /// Status display text
        /// </summary>
        public string StatusDisplay => Status switch
        {
            MatchStatus.Found => "已定位",
            MatchStatus.NotFound => "未找到",
            MatchStatus.MultipleMatches => $"找到 {MatchCount} 处匹配",
            MatchStatus.Error => "读取错误",
            _ => "未知"
        };

        /// <summary>
        /// 源文件 A 大小 (字节)
        /// Source file A size in bytes
        /// </summary>
        public long FileSizeA { get; set; }

        /// <summary>
        /// 源文件 A 可读的大小格式
        /// Formatted readable file size A
        /// </summary>
        public string FormattedSizeA { get; set; } = string.Empty;

        /// <summary>
        /// 目标文件 B 大小 (字节)
        /// Target file B size in bytes
        /// </summary>
        public long FileSizeB { get; set; }

        /// <summary>
        /// 目标文件 B 可读的大小格式
        /// Formatted readable file size B
        /// </summary>
        public string FormattedSizeB { get; set; } = "-";

        /// <summary>
        /// 兼容旧属性：源文件 A 大小
        /// Legacy property alias for FileSizeA
        /// </summary>
        public long FileSize
        {
            get => FileSizeA;
            set => FileSizeA = value;
        }

        /// <summary>
        /// 兼容旧属性：源文件 A 格式化大小
        /// Legacy property alias for FormattedSizeA
        /// </summary>
        public string FormattedSize
        {
            get => FormattedSizeA;
            set => FormattedSizeA = value;
        }

        /// <summary>
        /// 备注/错误消息
        /// Note or error message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 匹配配置项参数
    /// Match configuration options
    /// </summary>
    public class MatchOptions
    {
        /// <summary>
        /// 文件夹 A 的路径
        /// Path of Folder A
        /// </summary>
        public string FolderA { get; set; } = string.Empty;

        /// <summary>
        /// 文件夹 B 的路径
        /// Path of Folder B
        /// </summary>
        public string FolderB { get; set; } = string.Empty;

        /// <summary>
        /// 是否递归检查文件夹 A (默认 false)
        /// Whether to recursively check Folder A (Default: false)
        /// </summary>
        public bool RecursiveA { get; set; } = false;

        /// <summary>
        /// 是否检查文件名 (默认 true)
        /// Whether to match file name (Default: true)
        /// </summary>
        public bool CheckFileName { get; set; } = true;

        /// <summary>
        /// 是否检查文件大小 (默认 true)
        /// Whether to match file size (Default: true)
        /// </summary>
        public bool CheckFileSize { get; set; } = true;

        /// <summary>
        /// 文件大小对比的容差百分比 (0-100，默认 0)
        /// Size comparison tolerance percentage (0-100, Default: 0)
        /// </summary>
        public double SizeTolerancePercent { get; set; } = 0;

        /// <summary>
        /// 是否排除文件夹 A 自身的路径 (当 A 为 B 的子文件夹时，默认 true)
        /// Whether to exclude Folder A's own path when A is a subfolder of B (Default: true)
        /// </summary>
        public bool ExcludeAPath { get; set; } = true;
    }
}
