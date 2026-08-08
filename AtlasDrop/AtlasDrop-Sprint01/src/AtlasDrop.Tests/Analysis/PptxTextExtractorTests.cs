using Xunit;
using AtlasDrop.Analysis.Office;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace AtlasDrop.Tests.Analysis;

public sealed class PptxTextExtractorTests
{
    [Theory]
    [InlineData("document.pptx", true)]
    [InlineData("DOCUMENT.PPTX", true)]
    [InlineData("document.ppt", false)]
    [InlineData("document.pdf", false)]
    public void CanHandle_only_pptx(string fileName, bool expected)
    {
        var extractor = new PptxTextExtractor();
        Assert.Equal(expected, extractor.CanHandle(fileName));
    }

    [Fact]
    public async Task Extracts_text_from_slide()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "test.pptx");
            CreatePresentation(file, new[]
            {
                new[] { "Atlas Drop", "Courbevoie 2026" }
            });

            var result = await new PptxTextExtractor().ExtractAsync(file);

            Assert.Contains("[Diapositive 1]", result.Text);
            Assert.Contains("Atlas Drop", result.Text);
            Assert.Contains("Courbevoie 2026", result.Text);
            Assert.Equal("openxml-pptx", result.DetectedEncoding);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Extracts_multiple_slides()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "multi.pptx");
            CreatePresentation(file, new[]
            {
                new[] { "Première" },
                new[] { "Deuxième" }
            });

            var result = await new PptxTextExtractor().ExtractAsync(file);

            Assert.Contains("[Diapositive 1]", result.Text);
            Assert.Contains("[Diapositive 2]", result.Text);
            Assert.Contains("Première", result.Text);
            Assert.Contains("Deuxième", result.Text);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Character_limit_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "large.pptx");
            CreatePresentation(file, new[]
            {
                new[] { new string('X', 1000) }
            });

            var result = await new PptxTextExtractor(maxCharacters: 100).ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.True(result.Text.Length <= 100);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Slide_limit_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "slides.pptx");
            CreatePresentation(file, new[]
            {
                new[] { "A" },
                new[] { "B" },
                new[] { "C" }
            });

            var result = await new PptxTextExtractor(maxSlides: 2).ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.Contains("[Diapositive 2]", result.Text);
            Assert.DoesNotContain("[Diapositive 3]", result.Text);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_pptx_is_rejected()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.pptx");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => new PptxTextExtractor().ExtractAsync(missing));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "fake.txt");
            await File.WriteAllTextAsync(file, "fake");

            await Assert.ThrowsAsync<NotSupportedException>(
                () => new PptxTextExtractor().ExtractAsync(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "cancel.pptx");
            CreatePresentation(file, new[]
            {
                new[] { "A" }
            });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new PptxTextExtractor().ExtractAsync(file, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Original_pptx_is_not_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "readonly.pptx");
            CreatePresentation(file, new[]
            {
                new[] { "Original" }
            });

            var beforeLength = new FileInfo(file).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(file);

            _ = await new PptxTextExtractor().ExtractAsync(file);

            Assert.Equal(beforeLength, new FileInfo(file).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static void CreatePresentation(
        string path,
        IReadOnlyList<string[]> slidesText)
    {
        using var document = PresentationDocument.Create(
            path,
            PresentationDocumentType.Presentation);

        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        var slideIdList = new P.SlideIdList();
        presentationPart.Presentation.Append(slideIdList);

        uint slideId = 256;

        foreach (var texts in slidesText)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = CreateSlide(texts);

            slideIdList.Append(new P.SlideId
            {
                Id = slideId++,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            });
        }

        presentationPart.Presentation.Save();
    }

    private static P.Slide CreateSlide(IEnumerable<string> texts)
    {
        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));

        uint shapeId = 2;

        foreach (var text in texts)
        {
            var shape = new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties
                    {
                        Id = shapeId++,
                        Name = "Text"
                    },
                    new P.NonVisualShapeDrawingProperties(
                        new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(),
                new P.TextBody(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(
                            new A.RunProperties { Language = "fr-FR" },
                            new A.Text(text)))));

            shapeTree.Append(shape);
        }

        return new P.Slide(
            new P.CommonSlideData(shapeTree),
            new P.ColorMapOverride(new A.MasterColorMapping()));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
