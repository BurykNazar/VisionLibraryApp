using OpenCvSharp;
using VisionLibrary.Core;

if (args.Length >= 2 && args[0].Equals("--face", StringComparison.OrdinalIgnoreCase))
{
    var imagePath = Path.GetFullPath(args[1]);
    var outputDirectory = args.Length >= 3
        ? Path.GetFullPath(args[2])
        : Path.Combine(Path.GetDirectoryName(imagePath) ?? Directory.GetCurrentDirectory(), "face-results");

    var sample = new VisionSample(
        "manual-face-check",
        $"Власне фото: {Path.GetFileName(imagePath)}",
        "Manual face detection check.",
        imagePath,
        IsImported: true);

    var analysis = new OpenCvFaceDetector().Analyze(sample, outputDirectory);
    Console.WriteLine($"Faces: {analysis.Detections.Count}");
    Console.WriteLine($"Result: {analysis.AnnotatedImagePath}");
    foreach (var detection in analysis.Detections)
    {
        Console.WriteLine($"{detection.Number}. {detection.Label} {detection.Confidence:0.0}% {detection.X},{detection.Y},{detection.Width},{detection.Height}");
    }

    return;
}

var labRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : FindLabRoot();

var artifactsRoot = Path.Combine(labRoot, "artifacts");
var samplesDirectory = Path.Combine(artifactsRoot, "samples");
var resultsDirectory = Path.Combine(artifactsRoot, "results");
var videoDirectory = Path.Combine(artifactsRoot, "video");

Directory.CreateDirectory(samplesDirectory);
Directory.CreateDirectory(resultsDirectory);
Directory.CreateDirectory(videoDirectory);

var samples = SampleImageFactory.EnsureSamples(samplesDirectory);
var detector = new OpenCvShapeDetector();
var analyses = samples.Select(sample => detector.Analyze(sample, resultsDirectory)).ToArray();

var summaryPath = Path.Combine(artifactsRoot, "detection-summary.txt");
File.WriteAllLines(summaryPath, analyses.SelectMany(FormatAnalysis));

var videoPath = VideoBuilder.CreateDemoVideo(analyses, videoDirectory);

Console.WriteLine($"Samples: {samplesDirectory}");
Console.WriteLine($"Results: {resultsDirectory}");
Console.WriteLine($"Summary: {summaryPath}");
Console.WriteLine($"Video: {videoPath}");
foreach (var analysis in analyses)
{
    Console.WriteLine($"{analysis.Sample.Title}: {analysis.Detections.Count} detections");
}

static string FindLabRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "VisionLibraryLab.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static IEnumerable<string> FormatAnalysis(VisionAnalysis analysis)
{
    yield return analysis.Sample.Title;
    yield return analysis.Sample.Description;
    yield return $"Source: {analysis.SourceImagePath}";
    yield return $"Detected: {analysis.AnnotatedImagePath}";

    foreach (var item in analysis.Detections)
    {
        yield return $"{item.Number}. {item.Label}; color={item.Color}; confidence={item.Confidence:0.0}%; box={item.X},{item.Y},{item.Width},{item.Height}; area={item.Area:0}";
    }

    yield return "";
}

internal static class VideoBuilder
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int FramesPerSecond = 20;

    public static string CreateDemoVideo(IReadOnlyList<VisionAnalysis> analyses, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var mp4Path = Path.Combine(outputDirectory, "VisionLibraryDemo.mp4");
        using var writer = new VideoWriter(mp4Path, FourCC.MP4V, FramesPerSecond, new Size(Width, Height));

        if (writer.IsOpened())
        {
            WriteTimeline(writer, analyses);
            return mp4Path;
        }

        var aviPath = Path.Combine(outputDirectory, "VisionLibraryDemo.avi");
        using var fallbackWriter = new VideoWriter(aviPath, FourCC.MJPG, FramesPerSecond, new Size(Width, Height));
        if (!fallbackWriter.IsOpened())
        {
            throw new InvalidOperationException("OpenCV не зміг відкрити VideoWriter для створення демонстраційного відео.");
        }

        WriteTimeline(fallbackWriter, analyses);
        return aviPath;
    }

    private static void WriteTimeline(VideoWriter writer, IReadOnlyList<VisionAnalysis> analyses)
    {
        WriteHold(writer, CreateIntroFrame(), seconds: 5);

        foreach (var analysis in analyses)
        {
            WriteHold(writer, CreateAnalysisFrame(analysis, "1. Load sample image", false, false), seconds: 5);
            WriteHold(writer, CreateAnalysisFrame(analysis, "2. Convert to grayscale and smooth noise", true, false), seconds: 5);
            WriteHold(writer, CreateAnalysisFrame(analysis, "3. Canny edges and contour extraction", true, true), seconds: 5);
            WriteHold(writer, CreateAnalysisFrame(analysis, "4. Draw boxes and classify shapes", false, true), seconds: 5);
        }

        WriteHold(writer, CreateOutroFrame(analyses), seconds: 5);
    }

    private static void WriteHold(VideoWriter writer, Mat frame, int seconds)
    {
        using (frame)
        {
            var total = seconds * FramesPerSecond;
            for (var i = 0; i < total; i++)
            {
                writer.Write(frame);
            }
        }
    }

    private static Mat CreateIntroFrame()
    {
        var frame = NewFrame();
        PutTitle(frame, "VisionLibraryApp");
        PutSubtitle(frame, "C# WinUI + XAML + OpenCvSharp");
        PutText(frame, "Demo: image processing, Canny edges, contour detection, shape classification", 120, 280, 0.86, new Scalar(74, 85, 104), 2);
        PutText(frame, "Laboratory work 5: computer vision libraries", 120, 330, 0.86, new Scalar(74, 85, 104), 2);
        DrawAccent(frame);
        return frame;
    }

    private static Mat CreateOutroFrame(IReadOnlyList<VisionAnalysis> analyses)
    {
        var frame = NewFrame();
        PutTitle(frame, "Detection completed");
        PutSubtitle(frame, "OpenCV pipeline processed all demo images");

        var total = analyses.Sum(item => item.Detections.Count);
        PutText(frame, $"Samples processed: {analyses.Count}", 120, 275, 0.9, new Scalar(31, 41, 55), 2);
        PutText(frame, $"Detected objects: {total}", 120, 325, 0.9, new Scalar(31, 41, 55), 2);
        PutText(frame, "Results are saved to artifacts/results and used in the report.", 120, 375, 0.8, new Scalar(74, 85, 104), 2);
        DrawAccent(frame);
        return frame;
    }

    private static Mat CreateAnalysisFrame(VisionAnalysis analysis, string step, bool showEdges, bool showResult)
    {
        var frame = NewFrame();
        PutText(frame, "VisionLibraryApp", 55, 62, 1.0, new Scalar(31, 41, 55), 2);
        PutText(frame, step, 55, 105, 0.72, new Scalar(74, 85, 104), 2);

        using var source = Cv2.ImRead(analysis.SourceImagePath);
        using var result = Cv2.ImRead(showResult ? analysis.AnnotatedImagePath : analysis.SourceImagePath);
        using var edges = Cv2.ImRead(analysis.EdgesImagePath);

        DrawPanel(frame, new Rect(48, 132, 560, 420), "Source");
        DrawPanel(frame, new Rect(672, 132, 560, 420), showEdges ? "Edges / Result" : "Detection result");

        PasteImage(frame, source, new Rect(72, 178, 512, 340));
        PasteImage(frame, showEdges ? edges : result, new Rect(696, 178, 512, 340));

        DrawPanel(frame, new Rect(48, 585, 1184, 78), "Objects");
        PutText(frame, $"{analysis.Sample.Title}: {analysis.Detections.Count} detected objects", 72, 638, 0.72, new Scalar(31, 41, 55), 2);

        var x = 560;
        foreach (var detection in analysis.Detections.Take(4))
        {
            PutText(frame, $"{detection.Number}:{ToEnglish(detection.Label)} {detection.Confidence:0}%", x, 638, 0.58, new Scalar(37, 99, 235), 2);
            x += 160;
        }

        return frame;
    }

    private static Mat NewFrame()
    {
        var frame = new Mat(new Size(Width, Height), MatType.CV_8UC3, new Scalar(246, 248, 252));
        Cv2.Rectangle(frame, new Rect(0, 0, Width, Height), new Scalar(246, 248, 252), -1);
        return frame;
    }

    private static void DrawPanel(Mat frame, Rect rect, string label)
    {
        Cv2.Rectangle(frame, rect, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(frame, rect, new Scalar(220, 226, 235), 2, LineTypes.AntiAlias);
        PutText(frame, label, rect.X + 24, rect.Y + 34, 0.58, new Scalar(100, 116, 139), 2);
    }

    private static void PasteImage(Mat frame, Mat image, Rect target)
    {
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(target.Width, target.Height));
        resized.CopyTo(new Mat(frame, target));
    }

    private static void PutTitle(Mat frame, string text)
    {
        PutText(frame, text, 120, 180, 1.6, new Scalar(31, 41, 55), 3);
    }

    private static void PutSubtitle(Mat frame, string text)
    {
        PutText(frame, text, 120, 230, 0.9, new Scalar(74, 85, 104), 2);
    }

    private static void PutText(Mat frame, string text, int x, int y, double scale, Scalar color, int thickness)
    {
        Cv2.PutText(frame, text, new Point(x, y), HersheyFonts.HersheySimplex, scale, color, thickness, LineTypes.AntiAlias);
    }

    private static void DrawAccent(Mat frame)
    {
        Cv2.Circle(frame, new Point(1025, 330), 118, new Scalar(37, 99, 235), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(frame, new Rect(845, 415, 250, 115), new Scalar(20, 184, 166), -1, LineTypes.AntiAlias);
        Cv2.Line(frame, new Point(900, 190), new Point(1120, 540), new Scalar(245, 158, 11), 18, LineTypes.AntiAlias);
    }

    private static string ToEnglish(string label)
    {
        return label switch
        {
            "Трикутник" => "Tri",
            "Квадрат" => "Sq",
            "Прямокутник" => "Rect",
            "Коло" => "Circle",
            "Багатокутник" => "Poly",
            _ => "Obj"
        };
    }
}
