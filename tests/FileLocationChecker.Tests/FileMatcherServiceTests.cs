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
    }
}
