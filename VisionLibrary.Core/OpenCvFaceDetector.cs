using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace VisionLibrary.Core;

public sealed class OpenCvFaceDetector
{
    private const string CascadeFileName = "haarcascade_frontalface_default.xml";
    private const string DnnConfigFileName = "deploy.prototxt";
    private const string DnnModelFileName = "res10_300x300_ssd_iter_140000.caffemodel";

    private readonly string _cascadePath;
    private readonly string _dnnConfigPath;
    private readonly string _dnnModelPath;

    public OpenCvFaceDetector(string? cascadePath = null, string? dnnConfigPath = null, string? dnnModelPath = null)
    {
        _cascadePath = cascadePath ?? ResolveCascadePath();
        _dnnConfigPath = dnnConfigPath ?? ResolveDataPath(DnnConfigFileName);
        _dnnModelPath = dnnModelPath ?? ResolveDataPath(DnnModelFileName);
    }

    public VisionAnalysis Analyze(VisionSample sample, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        using var source = Cv2.ImRead(sample.ImagePath, ImreadModes.Color);
        if (source.Empty())
        {
            throw new FileNotFoundException("Не вдалося відкрити зображення для аналізу.", sample.ImagePath);
        }

        using var annotated = source.Clone();

        var detections = CanUseDnn()
            ? DetectWithDnn(source)
            : DetectWithHaar(source);

        var number = 1;
        foreach (var detection in detections)
        {
            var result = detection with { Number = number };
            DrawFace(annotated, new Rect(result.X, result.Y, result.Width, result.Height), result);
            number++;
        }

        var annotatedPath = Path.Combine(outputDirectory, $"{sample.Id}_faces.png");
        var previewPath = Path.Combine(outputDirectory, $"{sample.Id}_face_gray.png");
        SavePreview(source, previewPath);
        Cv2.ImWrite(annotatedPath, annotated);

        return new VisionAnalysis(sample, sample.ImagePath, annotatedPath, previewPath, detections.Select((item, index) => item with { Number = index + 1 }).ToArray());
    }

    public static string ResolveCascadePath()
    {
        return ResolveDataPath(CascadeFileName);
    }

    private IReadOnlyList<DetectionResult> DetectWithDnn(Mat source)
    {
        using var blob = CvDnn.BlobFromImage(
            source,
            scaleFactor: 1.0,
            size: new Size(300, 300),
            mean: new Scalar(104.0, 177.0, 123.0),
            swapRB: false,
            crop: false);

        using var net = CvDnn.ReadNetFromCaffe(_dnnConfigPath, _dnnModelPath);
        if (net is null || net.Empty())
        {
            return DetectWithHaar(source);
        }

        net.SetInput(blob);

        using var output = net.Forward();
        var reshaped = output.Reshape(1, output.Size(2));

        var detections = new List<DetectionResult>();
        for (var i = 0; i < reshaped.Rows; i++)
        {
            var confidence = reshaped.At<float>(i, 2);
            if (confidence < 0.45f)
            {
                continue;
            }

            var left = ClampToImage((int)Math.Round(reshaped.At<float>(i, 3) * source.Width), source.Width);
            var top = ClampToImage((int)Math.Round(reshaped.At<float>(i, 4) * source.Height), source.Height);
            var right = ClampToImage((int)Math.Round(reshaped.At<float>(i, 5) * source.Width), source.Width);
            var bottom = ClampToImage((int)Math.Round(reshaped.At<float>(i, 6) * source.Height), source.Height);
            var width = right - left;
            var height = bottom - top;

            if (width < 32 || height < 32)
            {
                continue;
            }

            detections.Add(new DetectionResult(0, "Обличчя", "DNN", Math.Round(confidence * 100.0, 1), width * height, left, top, width, height));
        }

        return detections
            .OrderByDescending(item => item.Confidence)
            .Take(8)
            .ToArray();
    }

    private IReadOnlyList<DetectionResult> DetectWithHaar(Mat source)
    {
        using var gray = new Mat();
        using var equalized = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, equalized);

        using var classifier = new CascadeClassifier(_cascadePath);
        if (classifier.Empty())
        {
            throw new FileNotFoundException("Не вдалося завантажити Haar Cascade для детекції облич.", _cascadePath);
        }

        var minFace = Math.Max(48, Math.Min(source.Width, source.Height) / 10);
        var faces = classifier
            .DetectMultiScale(
                equalized,
                scaleFactor: 1.08,
                minNeighbors: 4,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new Size(minFace, minFace))
            .OrderByDescending(RectArea)
            .Take(8)
            .ToArray();

        var detections = new List<DetectionResult>();

        foreach (var face in faces)
        {
            var confidence = EstimateFaceConfidence(face, source.Width * source.Height);
            detections.Add(new DetectionResult(0, "Обличчя", "Haar", confidence, RectArea(face), face.X, face.Y, face.Width, face.Height));
        }

        return detections;
    }

    private static void SavePreview(Mat source, string path)
    {
        using var gray = new Mat();
        using var equalized = new Mat();
        using var grayPreview = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, equalized);
        Cv2.CvtColor(equalized, grayPreview, ColorConversionCodes.GRAY2BGR);
        Cv2.ImWrite(path, grayPreview);
    }

    private bool CanUseDnn() => File.Exists(_dnnConfigPath) && File.Exists(_dnnModelPath);

    private static double EstimateFaceConfidence(Rect face, int imageArea)
    {
        var ratio = RectArea(face) / Math.Max(1, imageArea);
        var score = 0.72 + Math.Min(0.2, ratio * 2.0);
        return Math.Round(Math.Clamp(score, 0.68, 0.96) * 100.0, 1);
    }

    private static void DrawFace(Mat image, Rect rect, DetectionResult result)
    {
        var color = new Scalar(20, 184, 166);
        Cv2.Rectangle(image, rect, color, 3, LineTypes.AntiAlias);

        var label = $"{result.Number}. Face {result.Confidence:0}%";
        var origin = new Point(rect.X + 6, Math.Max(28, rect.Y + 25));
        Cv2.Rectangle(image, new Rect(origin.X - 4, origin.Y - 22, Math.Min(190, Math.Max(120, label.Length * 12)), 30), new Scalar(255, 255, 255), -1);
        Cv2.PutText(image, label, origin, HersheyFonts.HersheySimplex, 0.62, new Scalar(25, 38, 64), 2, LineTypes.AntiAlias);
    }

    private static double RectArea(Rect rect)
    {
        return Math.Max(0, rect.Width) * Math.Max(0, rect.Height);
    }

    private static int ClampToImage(int value, int max)
    {
        return Math.Clamp(value, 0, Math.Max(0, max - 1));
    }

    private static string ResolveDataPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "VisionLibrary.Core", "Data", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", fileName),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }
}
