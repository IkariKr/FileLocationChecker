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
![WikiMissing](missing_doc.pdf)
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
        public void CheckMissingResources_ShouldFindResourceWithOverlappingDirectoryPrefix()
        {
            // 测试重叠前缀目录（如 2018.1.1.md 的场景）
            // Test overlapping directory prefix (e.g. 2018.1.1.md scenario)
            string root = Path.Combine(_tempPath, "VaultRoot");
            string subDir = Path.Combine(root, "02 Daily", "2018");
            string attachDir = Path.Combine(subDir, "attachments");
            Directory.CreateDirectory(attachDir);

            string realFile = Path.Combine(attachDir, "pic001.png");
            File.WriteAllText(realFile, "image data");

            // md 文件在 subDir 中，引用的相对路径包含了从 Vault 根目录算起的全路径 "02 Daily/2018/attachments/pic001.png"
            string mdFile = Path.Combine(subDir, "2018.1.1.md");
            File.WriteAllText(mdFile, "![[02 Daily/2018/attachments/pic001.png]]");

            // Act
            var missing = MdResourceCheckerService.CheckMissingResources(mdFile, root);

            // Assert: 不应该认为缺失！
            Assert.Empty(missing);
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
