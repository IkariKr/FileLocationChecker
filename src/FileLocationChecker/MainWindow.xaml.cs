using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileLocationChecker.Models;
using FileLocationChecker.Services;
using Microsoft.Win32;

namespace FileLocationChecker
{
    /// <summary>
    /// 主窗口交互逻辑
    /// MainWindow interaction logic
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FileMatcherService _matcherService;
        private ObservableCollection<FileMatchResult> _resultsCollection;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 构造函数，初始化主窗口与数据绑定
        /// Constructor, initializes main window and data binding
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _matcherService = new FileMatcherService();
            _resultsCollection = new ObservableCollection<FileMatchResult>();
            DgResults.ItemsSource = _resultsCollection;
        }

        /// <summary>
        /// 浏览文件夹 A
        /// Browse Folder A
        /// </summary>
        private void BtnBrowseA_Click(object sender, RoutedEventArgs e)
        {
            string? selectedFolder = SelectFolder("选择源文件夹 A");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                TxtFolderA.Text = selectedFolder;
            }
        }

        /// <summary>
        /// 浏览文件夹 B
        /// Browse Folder B
        /// </summary>
        private void BtnBrowseB_Click(object sender, RoutedEventArgs e)
        {
            string? selectedFolder = SelectFolder("选择目标文件夹 B");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                TxtFolderB.Text = selectedFolder;
            }
        }

        /// <summary>
        /// 弹出文件夹选择框
        /// Show folder selection dialog
        /// </summary>
        private string? SelectFolder(string title)
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                Multiselect = false
            };

            bool? result = dialog.ShowDialog(this);
            return result == true ? dialog.FolderName : null;
        }

        /// <summary>
        /// 容差滑块数值改变事件 handler
        /// Size tolerance slider value changed event handler
        /// </summary>
        private void SldSizeTolerance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtToleranceValue != null)
            {
                TxtToleranceValue.Text = $"{(int)e.NewValue}%";
            }
        }

        /// <summary>
        /// 点击“开始查找”按钮触发比对流程
        /// Click 'Start' button to initiate comparison workflow
        /// </summary>
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            string folderA = TxtFolderA.Text.Trim();
            string folderB = TxtFolderB.Text.Trim();

            if (string.IsNullOrEmpty(folderA) || !Directory.Exists(folderA))
            {
                MessageBox.Show(this, "请选择有效的源文件夹 A！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(folderB) || !Directory.Exists(folderB))
            {
                MessageBox.Show(this, "请选择有效的目标文件夹 B！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool checkName = ChkCheckFileName.IsChecked == true;
            bool checkSize = ChkCheckFileSize.IsChecked == true;

            if (!checkName && !checkSize)
            {
                MessageBox.Show(this, "必须至少勾选【检查文件名】或【检查文件大小】中的一项！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 更新 UI 状态
            _resultsCollection.Clear();
            BtnStart.IsEnabled = false;
            BtnCancel.IsEnabled = true;
            PbProgress.Visibility = Visibility.Visible;
            PbProgress.IsIndeterminate = true;
            TxtSummary.Text = string.Empty;

            var options = new MatchOptions
            {
                FolderA = folderA,
                FolderB = folderB,
                RecursiveA = ChkRecursiveA.IsChecked == true,
                CheckFileName = checkName,
                CheckFileSize = checkSize,
                SizeTolerancePercent = SldSizeTolerance.Value,
                ExcludeAPath = ChkExcludeAPath.IsChecked == true,
                CheckMdResources = ChkCheckMdResources.IsChecked == true
            };

            _cts = new CancellationTokenSource();
            var stopwatch = Stopwatch.StartNew();

            var progress = new Progress<MatchProgressInfo>(info =>
            {
                TxtStatus.Text = info.StatusText;

                if (info.TotalCount > 0)
                {
                    PbProgress.IsIndeterminate = false;
                    PbProgress.Maximum = info.TotalCount;
                    PbProgress.Value = info.ProcessedCount;
                }

                if (info.LatestResult != null)
                {
                    _resultsCollection.Add(info.LatestResult);
                }
            });

            try
            {
                var finalResults = await _matcherService.MatchFilesAsync(options, progress, _cts.Token);
                stopwatch.Stop();

                int foundCount = 0;
                int notFoundCount = 0;
                int multipleCount = 0;

                foreach (var res in finalResults)
                {
                    if (res.Status == MatchStatus.Found) foundCount++;
                    else if (res.Status == MatchStatus.NotFound) notFoundCount++;
                    else if (res.Status == MatchStatus.MultipleMatches) multipleCount++;
                }

                TxtSummary.Text = $"总计: {finalResults.Count} | 已定位: {foundCount} | 未找到: {notFoundCount} | 多匹配: {multipleCount} | 耗时: {stopwatch.Elapsed.TotalSeconds:F2} 秒";
                TxtStatus.Text = "检索比对完成。";
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                TxtStatus.Text = "用户已取消任务。";
                TxtSummary.Text = "比对已中断。";
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MessageBox.Show(this, $"查找出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "查找失败。";
            }
            finally
            {
                BtnStart.IsEnabled = true;
                BtnCancel.IsEnabled = false;
                PbProgress.Visibility = Visibility.Collapsed;
                _cts.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 点击“停止”按钮取消当前查找任务
        /// Click 'Stop' button to cancel current matching task
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            BtnCancel.IsEnabled = false;
            TxtStatus.Text = "正在请求取消...";
        }

        /// <summary>
        /// 双击表格结果项查看/定位文件
        /// Double click result item to locate target file
        /// </summary>
        private void DgResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedTargetFolder();
        }

        private void MenuOpenFolderA_Click(object sender, RoutedEventArgs e)
        {
            if (DgResults.SelectedItem is FileMatchResult selected && !string.IsNullOrEmpty(selected.SourcePath))
            {
                OpenInExplorer(selected.SourcePath);
            }
        }

        private void MenuCompareDiff_Click(object sender, RoutedEventArgs e)
        {
            if (DgResults.SelectedItem is FileMatchResult selected)
            {
                if (string.IsNullOrEmpty(selected.SourcePath) || !File.Exists(selected.SourcePath))
                {
                    MessageBox.Show(this, "源文件 A 不存在，无法进行内容对比！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(selected.TargetPath))
                {
                    MessageBox.Show(this, "未在目标文件夹 B 中找到匹配的文件，无法进行内容对比！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 若有多重匹配，获取第一个目标文件路径
                string targetPath = selected.TargetPath.Split(';')[0].Trim();

                if (!File.Exists(targetPath))
                {
                    MessageBox.Show(this, $"目标文件 B 不存在或无效: {targetPath}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var diffWindow = new DiffViewerWindow(selected.SourcePath, targetPath)
                {
                    Owner = this
                };
                diffWindow.ShowDialog();
            }
        }

        private void MenuOpenFolderB_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedTargetFolder();
        }

        private void MenuCopyTargetPath_Click(object sender, RoutedEventArgs e)
        {
            if (DgResults.SelectedItem is FileMatchResult selected && !string.IsNullOrEmpty(selected.TargetPath))
            {
                Clipboard.SetText(selected.TargetPath);
                TxtStatus.Text = "已复制目标路径到剪贴板。";
            }
        }

        private void OpenSelectedTargetFolder()
        {
            if (DgResults.SelectedItem is FileMatchResult selected)
            {
                if (!string.IsNullOrEmpty(selected.TargetPath))
                {
                    // 若有多个路径取第一个
                    string firstPath = selected.TargetPath.Split(';')[0].Trim();
                    OpenInExplorer(firstPath);
                }
                else if (!string.IsNullOrEmpty(selected.SourcePath))
                {
                    OpenInExplorer(selected.SourcePath);
                }
            }
        }

        /// <summary>
        /// 在 Windows 资源管理器中打开并选中指定文件
        /// Open and select the specified file in Windows Explorer
        /// </summary>
        private static void OpenInExplorer(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"\"{filePath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件位置: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
