using System;
using System.IO;
using FileLocationChecker.Services;
using Xunit;

namespace FileLocationChecker.Tests
{
    /// <summary>
    /// TextDiffService 文本差异算法测试
    /// TextDiffService text difference algorithm unit tests
    /// </summary>
    public class TextDiffServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public TextDiffServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "DiffTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempPath);
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
        public void ComputeLineDiff_ShouldDetectAddedAndDeletedLines()
        {
            // Arrange
            string[] linesA = new[] { "Line 1", "Line 2", "Line 3" };
            string[] linesB = new[] { "Line 1", "Line 2 Modified", "Line 3", "Line 4" };

            // Act
            var diff = TextDiffService.ComputeLineDiff(linesA, linesB);

            // Assert
            Assert.NotEmpty(diff);
            Assert.Contains(diff, d => d.Kind == DiffKind.Deleted && d.Text == "Line 2");
            Assert.Contains(diff, d => d.Kind == DiffKind.Added && d.Text == "Line 2 Modified");
            Assert.Contains(diff, d => d.Kind == DiffKind.Added && d.Text == "Line 4");
        }

        [Fact]
        public void IsTextFile_ShouldReturnFalseForBinaryFile()
        {
            // Arrange
            string binPath = Path.Combine(_tempPath, "sample.bin");
            File.WriteAllBytes(binPath, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x00, 0x01 });

            string txtPath = Path.Combine(_tempPath, "sample.txt");
            File.WriteAllText(txtPath, "Hello World\r\nLine 2");

            // Act & Assert
            Assert.False(TextDiffService.IsTextFile(binPath));
            Assert.True(TextDiffService.IsTextFile(txtPath));
        }
    }
}
