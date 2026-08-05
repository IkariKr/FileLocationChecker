using System;
using System.IO;
using System.Threading.Tasks;
using FileLocationChecker.Models;
using FileLocationChecker.Services;
using Xunit;

namespace FileLocationChecker.Tests
{
    /// <summary>
    /// FileMatcherService 核心匹配引擎测试
    /// FileMatcherService core matching engine unit tests
    /// </summary>
    public class FileMatcherServiceTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly string _folderA;
        private readonly string _folderB;
        private readonly FileMatcherService _service;

        public FileMatcherServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "FileMatcherTests_" + Guid.NewGuid().ToString("N"));
            _folderA = Path.Combine(_tempPath, "A");
            _folderB = Path.Combine(_tempPath, "B");

            Directory.CreateDirectory(_folderA);
            Directory.CreateDirectory(_folderB);

            _service = new FileMatcherService();
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempPath))
            {
                try
                {
                    Directory.Delete(_tempPath, true);
                }
                catch { }
            }
        }

        [Fact]
        public async Task MatchFilesAsync_ShouldFindExactMatch_WhenFileNameAndSizeMatch()
        {
            // Arrange
            string fileA = Path.Combine(_folderA, "test1.txt");
            string fileB = Path.Combine(_folderB, "subDir", "test1.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(fileB)!);

            File.WriteAllText(fileA, "Hello World");
            File.WriteAllText(fileB, "Hello World");

            var options = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                RecursiveA = false,
                CheckFileName = true,
                CheckFileSize = true
            };

            // Act
            var results = await _service.MatchFilesAsync(options);

            // Assert
            Assert.Single(results);
            Assert.Equal(MatchStatus.Found, results[0].Status);
            Assert.Equal(fileB, results[0].TargetPath);
            Assert.Equal("11 B", results[0].FormattedSizeA);
            Assert.Equal("11 B", results[0].FormattedSizeB);
        }

        [Fact]
        public async Task MatchFilesAsync_ShouldReportNotFound_WhenSizeDiffersAndSizeCheckEnabled()
        {
            // Arrange
            string fileA = Path.Combine(_folderA, "doc.txt");
            string fileB = Path.Combine(_folderB, "doc.txt");

            File.WriteAllText(fileA, "Short");
            File.WriteAllText(fileB, "Much longer content here");

            var options = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                RecursiveA = false,
                CheckFileName = true,
                CheckFileSize = true
            };

            // Act
            var results = await _service.MatchFilesAsync(options);

            // Assert
            Assert.Single(results);
            Assert.Equal(MatchStatus.NotFound, results[0].Status);
        }

        [Fact]
        public async Task MatchFilesAsync_ShouldFindMultipleMatches_WhenFileExistsInMultipleSubfolders()
        {
            // Arrange
            string fileA = Path.Combine(_folderA, "image.png");
            string fileB1 = Path.Combine(_folderB, "folder1", "image.png");
            string fileB2 = Path.Combine(_folderB, "folder2", "image.png");

            Directory.CreateDirectory(Path.GetDirectoryName(fileB1)!);
            Directory.CreateDirectory(Path.GetDirectoryName(fileB2)!);

            File.WriteAllBytes(fileA, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(fileB1, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(fileB2, new byte[] { 1, 2, 3 });

            var options = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                RecursiveA = false,
                CheckFileName = true,
                CheckFileSize = true
            };

            // Act
            var results = await _service.MatchFilesAsync(options);

            // Assert
            Assert.Single(results);
            Assert.Equal(MatchStatus.MultipleMatches, results[0].Status);
            Assert.Equal(2, results[0].MatchCount);
        }

        [Fact]
        public async Task MatchFilesAsync_ShouldSupportRecursiveA_WhenOptionChecked()
        {
            // Arrange
            string subA = Path.Combine(_folderA, "nested");
            Directory.CreateDirectory(subA);
            string fileA = Path.Combine(subA, "nested_file.txt");
            string fileB = Path.Combine(_folderB, "nested_file.txt");

            File.WriteAllText(fileA, "data");
            File.WriteAllText(fileB, "data");

            var optionsNonRecursive = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                RecursiveA = false,
                CheckFileName = true,
                CheckFileSize = true
            };

            var optionsRecursive = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                RecursiveA = true,
                CheckFileName = true,
                CheckFileSize = true
            };

            // Act & Assert
            var results1 = await _service.MatchFilesAsync(optionsNonRecursive);
            Assert.Empty(results1);

            var results2 = await _service.MatchFilesAsync(optionsRecursive);
            Assert.Single(results2);
            Assert.Equal(MatchStatus.Found, results2[0].Status);
        }

        [Fact]
        public async Task MatchFilesAsync_ShouldExcludeSelf_WhenAIsSubfolderOfBAndExcludeAPathIsTrue()
        {
            // Arrange: B 包含 A，且 A 中有一个文件 test.txt。B 另外还有一个 other/test.txt。
            // Arrange: B contains A, and A contains test.txt. B also contains another test.txt in other/.
            string subFolderA = Path.Combine(_folderB, "FolderA");
            Directory.CreateDirectory(subFolderA);
            string otherFolderB = Path.Combine(_folderB, "OtherDir");
            Directory.CreateDirectory(otherFolderB);

            string fileInA = Path.Combine(subFolderA, "sample.txt");
            string fileInB1 = Path.Combine(subFolderA, "sample.txt"); // 属于 A 自身的路径
            string fileInB2 = Path.Combine(otherFolderB, "sample.txt"); // 属于 B 其他地方的匹配项

            File.WriteAllText(fileInA, "test data");
            File.WriteAllText(fileInB2, "test data");

            // 情况 1: 勾选 ExcludeAPath = true，应该排除 fileInB1 (A 自身)，仅找到 fileInB2
            var optionsWithExclude = new MatchOptions
            {
                FolderA = subFolderA,
                FolderB = _folderB,
                RecursiveA = false,
                CheckFileName = true,
                CheckFileSize = true,
                ExcludeAPath = true
            };

            // Act
            var results1 = await _service.MatchFilesAsync(optionsWithExclude);

            // Assert
            Assert.Single(results1);
            Assert.Equal(MatchStatus.Found, results1[0].Status);
            Assert.Equal(fileInB2, results1[0].TargetPath);

            // 情况 2: 取消勾选 ExcludeAPath = false，会同时匹配到 fileInB1 和 fileInB2 -> MultipleMatches
            var optionsWithoutExclude = new MatchOptions
            {
                FolderA = subFolderA,
                FolderB = _folderB,
                RecursiveA = false,
                CheckFileName = true,
                CheckFileSize = true,
                ExcludeAPath = false
            };

            var results2 = await _service.MatchFilesAsync(optionsWithoutExclude);
            Assert.Single(results2);
            Assert.Equal(MatchStatus.MultipleMatches, results2[0].Status);
            Assert.Equal(2, results2[0].MatchCount);
        }

        [Fact]
        public async Task MatchFilesAsync_ShouldRespectSizeTolerancePercent()
        {
            // Arrange: file A is 131 bytes, file B is 129 bytes (diff ~1.5%)
            string fileA = Path.Combine(_folderA, "doc.txt");
            string fileB = Path.Combine(_folderB, "doc.txt");

            string textA = "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789\n\n";
            string textB = "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";

            File.WriteAllText(fileA, textA);
            File.WriteAllText(fileB, textB);

            // 0% 容差：应该无法匹配
            var optionsZeroTol = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                CheckFileName = true,
                CheckFileSize = true,
                SizeTolerancePercent = 0
            };

            var results1 = await _service.MatchFilesAsync(optionsZeroTol);
            Assert.Single(results1);
            Assert.Equal(MatchStatus.NotFound, results1[0].Status);

            // 3% 容差 (允许 1.5% 误差)：应该匹配成功
            var options3PercentTol = new MatchOptions
            {
                FolderA = _folderA,
                FolderB = _folderB,
                CheckFileName = true,
                CheckFileSize = true,
                SizeTolerancePercent = 3.0
            };

            var results2 = await _service.MatchFilesAsync(options3PercentTol);
            Assert.Single(results2);
            Assert.Equal(MatchStatus.Found, results2[0].Status);
            Assert.Equal(fileB, results2[0].TargetPath);
        }
    }
}
