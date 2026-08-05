using System;
using System.IO;
using FileLocationChecker.Services;
using Xunit;

namespace FileLocationChecker.Tests
{
    /// <summary>
    /// MdResourceCheckerService Markdown 资源检索测试
    /// MdResourceCheckerService Markdown resource check unit tests
    /// </summary>
    public class MdResourceCheckerTests : IDisposable
    {
        private readonly string _tempPath;

        public MdResourceCheckerTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "MdResourceTests_" + Guid.NewGuid().ToString("N"));
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
        public void CheckMissingResources_ShouldDetectMissingImagesAndAttachments()
        {
            // Arrange
            string mdPath = Path.Combine(_tempPath, "test.md");
            string attachDir = Path.Combine(_tempPath, "attachments");
            Directory.CreateDirectory(attachDir);

            // 真实的资源：real_photo.png
            File.WriteAllText(Path.Combine(attachDir, "real_photo.png"), "image data");

            // MD 内容引用了真实的图片、不存在的图片 missing_file.png 以及 Wiki 链 missing_doc.pdf
            string mdContent = @"
# Sample MD

![Real](attachments/real_photo.png)
![Missing](attachments/missing_file.png)
![WikiMissing]([[missing_doc.pdf]])
[Web Link](https://www.yuque.com/attachments/123.mp4)
";
            File.WriteAllText(mdPath, mdContent);

            // Act
            var missing = MdResourceCheckerService.CheckMissingResources(mdPath, _tempPath);

            // Assert
            Assert.Equal(2, missing.Count);
            Assert.Contains(missing, m => m.Contains("missing_file.png"));
            Assert.Contains(missing, m => m.Contains("missing_doc.pdf"));
            Assert.DoesNotContain(missing, m => m.Contains("real_photo.png"));
        }

        [Fact]
        public void CheckExternalLinks_ShouldDetectWebUrls()
        {
            // Arrange
            string mdPath = Path.Combine(_tempPath, "web.md");
            string mdContent = @"
# External Links MD

![Local](attachments/pic.png)
[Yuque](https://www.yuque.com/attachments/123.mp4)
<img src=""http://example.com/image.png"">
";
            File.WriteAllText(mdPath, mdContent);

            // Act
            var extLinks = MdResourceCheckerService.CheckExternalLinks(mdPath);

            // Assert
            Assert.Equal(2, extLinks.Count);
            Assert.Contains(extLinks, l => l == "https://www.yuque.com/attachments/123.mp4");
            Assert.Contains(extLinks, l => l == "http://example.com/image.png");
            Assert.DoesNotContain(extLinks, l => l.Contains("attachments/pic.png"));
        }
    }
}
