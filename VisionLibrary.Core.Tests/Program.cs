using VisionLibrary.Core;
using OpenCvSharp;

var root = Path.Combine(Path.GetTempPath(), "VisionLibraryCoreTests", Guid.NewGuid().ToString("N"));
var sourceDirectory = Path.Combine(root, "source");
var importDirectory = Path.Combine(root, "imports");
var resultDirectory = Path.Combine(root, "results");

Directory.CreateDirectory(sourceDirectory);

var samples = SampleImageFactory.EnsureSamples(sourceDirectory);
var source = samples[0].ImagePath;

var imported = ImportedImageStore.Import(source, importDirectory);

Assert(File.Exists(imported.ImagePath), "Imported file was not copied.");
Assert(imported.ImagePath.StartsWith(importDirectory, StringComparison.OrdinalIgnoreCase), "Imported file is outside target directory.");
Assert(imported.Title.Contains("Власне фото", StringComparison.OrdinalIgnoreCase), "Imported sample title should identify a user photo.");
Assert(Path.GetExtension(imported.ImagePath).Equals(Path.GetExtension(source), StringComparison.OrdinalIgnoreCase), "Imported extension changed.");
Assert(imported.IsImported, "Imported sample should be marked as imported.");

var analysis = new OpenCvShapeDetector().Analyze(imported, resultDirectory);
Assert(analysis.Detections.Count > 0, "Detector should process imported image.");

var busyPhoto = CreateBusyPhoto(root);
var busyImported = ImportedImageStore.Import(busyPhoto, importDirectory);
var photoAnalysis = new OpenCvShapeDetector(VisionOptions.ForNaturalPhoto()).Analyze(busyImported, resultDirectory);

Assert(photoAnalysis.Detections.Count <= 5, "Natural-photo profile should limit noisy false detections.");
Assert(photoAnalysis.Detections.All(item => item.Label == "Об'єкт"), "Natural-photo profile should not pretend noisy contours are simple shapes.");

var faceSample = new VisionSample(
    "lena-face",
    "Тестове фото з обличчям",
    "Перевірка Haar Cascade face detection.",
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "lena.jpg")),
    IsImported: true);

var faceAnalysis = new OpenCvFaceDetector().Analyze(faceSample, resultDirectory);
Assert(faceAnalysis.Detections.Count >= 1, "Face detector should find a frontal face.");
Assert(faceAnalysis.Detections.All(item => item.Label == "Обличчя"), "Face detector should label faces explicitly.");
Assert(File.Exists(faceAnalysis.AnnotatedImagePath), "Face detector should save annotated image.");

var partiallyCoveredFace = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "VisionLibraryLab",
    "Imported",
    "20260526_222716_70421a5508084f6394d5d82c8e20d3ad_Screenshot 2025-11-01 212232.png");

if (File.Exists(partiallyCoveredFace))
{
    var coveredSample = new VisionSample(
        "covered-face",
        "Фото з частково перекритим обличчям",
        "Regression check for user-imported image.",
        partiallyCoveredFace,
        IsImported: true);

    var coveredAnalysis = new OpenCvFaceDetector().Analyze(coveredSample, resultDirectory);
    Assert(coveredAnalysis.Detections.Any(item => item.Width >= 120 && item.Height >= 120), "Face detector should find the visible face region, not a small false positive.");
}

Console.WriteLine("VisionLibrary.Core.Tests passed");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string CreateBusyPhoto(string root)
{
    var path = Path.Combine(root, "busy-photo.png");
    using var image = new Mat(new Size(960, 640), MatType.CV_8UC3, new Scalar(120, 125, 132));
    var random = new Random(2026);

    for (var i = 0; i < 260; i++)
    {
        var color = new Scalar(random.Next(70, 190), random.Next(70, 190), random.Next(70, 190));
        var p1 = new Point(random.Next(image.Width), random.Next(image.Height));
        var p2 = new Point(random.Next(image.Width), random.Next(image.Height));
        Cv2.Line(image, p1, p2, color, random.Next(1, 5), LineTypes.AntiAlias);
    }

    Cv2.Ellipse(image, new Point(490, 300), new Size(170, 235), -8, 0, 360, new Scalar(168, 145, 132), -1, LineTypes.AntiAlias);
    Cv2.Rectangle(image, new Rect(250, 360, 420, 190), new Scalar(65, 70, 86), -1, LineTypes.AntiAlias);
    Cv2.Circle(image, new Point(430, 250), 16, new Scalar(35, 40, 45), -1, LineTypes.AntiAlias);
    Cv2.Circle(image, new Point(540, 245), 16, new Scalar(35, 40, 45), -1, LineTypes.AntiAlias);
    Cv2.ImWrite(path, image);

    return path;
}
