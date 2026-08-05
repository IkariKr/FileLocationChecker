using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using FileLocationChecker.Services;

namespace FileLocationChecker
{
    /// <summary>
    /// 文本文件差异对比弹窗 Window
    /// Text file diff viewer Window
    /// </summary>
    public partial class DiffViewerWindow : Window
    {
        /// <summary>
        /// 构造函数，传入文件 A 与文件 B 的路径并执行差异对比
        /// Constructor, accepts file A and B paths and executes diff comparison
        /// </summary>
        /// <param name="fileAPath">文件 A 完整路径 / File A full path</param>
        /// <param name="fileBPath">文件 B 完整路径 / File B full path</param>
        public DiffViewerWindow(string fileAPath, string fileBPath)
        {
            InitializeComponent();

            TxtPathA.Text = string.IsNullOrEmpty(fileAPath) ? "(不存在)" : GetFileInfoText(fileAPath);
            TxtPathB.Text = string.IsNullOrEmpty(fileBPath) ? "(不存在)" : GetFileInfoText(fileBPath);

            LoadDiff(fileAPath, fileBPath);
        }

        private static string GetFileInfoText(string path)
        {
            if (!File.Exists(path))
                return $"{path} (文件不存在)";
            var info = new FileInfo(path);
            return $"{path}  [{info.Length} 字节]";
        }

        private void LoadDiff(string fileAPath, string fileBPath)
        {
            if (!TextDiffService.IsTextFile(fileAPath) || !TextDiffService.IsTextFile(fileBPath))
            {
                TxtStats.Text = "⚠️ 识别到可能为二进制文件，无法显示文本差异对比。";
                LvDiffLines.ItemsSource = null;
                return;
            }

            try
            {
                List<DiffLineItem> diffItems = TextDiffService.CompareFiles(fileAPath, fileBPath);
                LvDiffLines.ItemsSource = diffItems;

                int added = 0;
                int deleted = 0;
                int unchanged = 0;

                foreach (var item in diffItems)
                {
                    if (item.Kind == DiffKind.Added) added++;
                    else if (item.Kind == DiffKind.Deleted) deleted++;
                    else unchanged++;
                }

                TxtStats.Text = $"差异统计： 新增 {added} 行  |  删除 {deleted} 行  |  未改动 {unchanged} 行 (共 {diffItems.Count} 行)";
            }
            catch (Exception ex)
            {
                TxtStats.Text = $"对比出错: {ex.Message}";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
