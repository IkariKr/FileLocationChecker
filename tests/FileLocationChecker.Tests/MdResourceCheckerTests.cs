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

        [Fact]
        public void CheckMissingResources_ShouldSupportCommonMarkDestinationsAndReferences()
        {
            string assetsDir = Path.Combine(_tempPath, "assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, "my file (1).png"), "image data");
            File.WriteAllText(Path.Combine(assetsDir, "O'Reilly.pdf"), "pdf data");
            File.WriteAllText(Path.Combine(assetsDir, "encoded file.png"), "image data");
            File.WriteAllText(Path.Combine(assetsDir, "root.png"), "image data");
            File.WriteAllText(Path.Combine(assetsDir, "fragment.png"), "image data");
            Directory.CreateDirectory(Path.Combine(assetsDir, "folder-link"));
            string absoluteFile = Path.Combine(assetsDir, "absolute.png");
            File.WriteAllText(absoluteFile, "image data");

            string mdPath = Path.Combine(_tempPath, "syntax.md");
            File.WriteAllText(mdPath, $@"
![Image](<assets/my file (1).png> ""image title"")
[Book](assets/O'Reilly.pdf)
[Root](/assets/root.png)
[Absolute]({absoluteFile.Replace('\\', '/')})
[Fragment](assets/fragment.png#preview)
[Query](assets/fragment.png?v=1)
[Folder](assets/folder-link)
[Anchor](#local-heading)
[Mail](mailto:test@example.com)
[Data](data:text/plain,example)
[Encoded][encoded]
[Missing][missing]

[encoded]: assets/encoded%20file.png
[missing]: assets/missing.png
");

            var missing = MdResourceCheckerService.CheckMissingResources(mdPath, _tempPath);

            Assert.Single(missing);
            Assert.Equal("assets/missing.png", missing[0]);
        }

        [Fact]
        public void CheckMissingResources_ShouldIgnoreLinksInCodeAndHtmlComments()
        {
            string mdPath = Path.Combine(_tempPath, "code.md");
            File.WriteAllText(mdPath, @"
`![Inline](missing-inline.png)`

```markdown
![Fenced](missing-fenced.png)
[External](https://example.com/fenced)
```
    ![[missing-indented.png]]
    <img src=""https://example.com/indented.png"">
<!-- <img src=""missing-comment.png""> ![[missing-comment.md]] -->
");

            Assert.Empty(MdResourceCheckerService.CheckMissingResources(mdPath, _tempPath));
            Assert.Empty(MdResourceCheckerService.CheckExternalLinks(mdPath));
        }

        [Fact]
        public void CheckMissingResources_ShouldResolveWikiNotesAndAliases()
        {
            string notesDir = Path.Combine(_tempPath, "notes");
            Directory.CreateDirectory(notesDir);
            File.WriteAllText(Path.Combine(notesDir, "Daily Note.md"), "# Daily Note");

            string mdPath = Path.Combine(_tempPath, "wiki.md");
            File.WriteAllText(mdPath, "[[notes/Daily Note#Today|Daily]]\n![[notes/Daily Note]]");

            Assert.Empty(MdResourceCheckerService.CheckMissingResources(mdPath, _tempPath));
        }

        [Fact]
        public void CheckMissingResources_ShouldUseVaultRootWithoutParentDirectoryGuessing()
        {
            string vaultRoot = Path.Combine(_tempPath, "VaultRoot");
            string docsDir = Path.Combine(vaultRoot, "docs");
            string outsideAssetsDir = Path.Combine(_tempPath, "assets");
            Directory.CreateDirectory(docsDir);
            Directory.CreateDirectory(outsideAssetsDir);
            File.WriteAllText(Path.Combine(outsideAssetsDir, "only-outside.png"), "image data");

            string mdPath = Path.Combine(docsDir, "links.md");
            File.WriteAllText(mdPath, "![Missing](assets/only-outside.png)\n![Escape](../../assets/only-outside.png)");

            var missing = MdResourceCheckerService.CheckMissingResources(mdPath, vaultRoot);

            Assert.Equal(2, missing.Count);
            Assert.Contains("assets/only-outside.png", missing);
            Assert.Contains("../../assets/only-outside.png", missing);
        }
    }
}
