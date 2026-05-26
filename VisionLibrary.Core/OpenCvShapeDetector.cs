using OpenCvSharp;

namespace VisionLibrary.Core;

public sealed class OpenCvShapeDetector
{
    private readonly VisionOptions _options;

    public OpenCvShapeDetector(VisionOptions? options = null)
    {
        _options = options ?? new VisionOptions();
    }

    public VisionAnalysis Analyze(VisionSample sample, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        using var source = Cv2.ImRead(sample.ImagePath, ImreadModes.Color);
        if (source.Empty())
        {
            throw new FileNotFoundException("Не вдалося відкрити зображення для аналізу.", sample.ImagePath);
        }

        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();
        using var dilated = new Mat();
        using var annotated = source.Clone();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        var blur = EnsureOdd(_options.BlurSize);
        Cv2.GaussianBlur(gray, blurred, new Size(blur, blur), 0);
        Cv2.Canny(blurred, edges, _options.CannyLowThreshold, _options.CannyHighThreshold);

        var dilate = EnsureOdd(_options.DilateSize);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilate, dilate));
        Cv2.Dilate(edges, dilated, kernel);

        Cv2.FindContours(dilated, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var detections = new List<DetectionResult>();
        var imageArea = source.Width * source.Height;
        var minContourArea = Math.Max(_options.MinContourArea, imageArea * _options.MinContourAreaRatio);
        var maxContourArea = imageArea * _options.MaxContourAreaRatio;
        var candidates = contours
            .Select(contour => new { Contour = contour, Area = Cv2.ContourArea(contour), Rect = Cv2.BoundingRect(contour) })
            .Where(item => item.Area >= minContourArea && item.Area <= maxContourArea)
            .Where(item => item.Rect.Width >= 32 && item.Rect.Height >= 32)
            .Where(item => item.Rect.Width < source.Width - 8 && item.Rect.Height < source.Height - 8)
            .Select(item => new ContourCandidate(item.Contour, item.Area, item.Rect))
            .ToList();

        candidates = _options.PrioritizeLargeContours
            ? candidates.OrderByDescending(item => item.Area).ToList()
            : candidates.OrderBy(item => item.Rect.Y).ThenBy(item => item.Rect.X).ToList();

        if (_options.SuppressOverlappingContours)
        {
            candidates = SuppressOverlaps(candidates, _options.OverlapThreshold);
        }

        var orderedContours = candidates
            .Take(Math.Max(1, _options.MaxDetections))
            .OrderBy(item => item.Rect.Y)
            .ThenBy(item => item.Rect.X)
            .ToArray();

        var number = 1;
        foreach (var item in orderedContours)
        {
            var perimeter = Cv2.ArcLength(item.Contour, true);
            if (perimeter <= 0)
            {
                continue;
            }

            var approx = Cv2.ApproxPolyDP(item.Contour, 0.035 * perimeter, true);
            var circularity = 4.0 * Math.PI * item.Area / (perimeter * perimeter);
            var label = _options.ClassifySimpleShapes
                ? ClassifyShape(approx.Length, circularity, item.Rect)
                : "Об'єкт";
            var color = EstimateColor(source, item.Contour);
            var confidence = EstimateConfidence(label, item.Area, item.Rect, circularity);
            var result = new DetectionResult(number, label, color, confidence, item.Area, item.Rect.X, item.Rect.Y, item.Rect.Width, item.Rect.Height);
            detections.Add(result);

            DrawDetection(annotated, item.Rect, result);
            number++;
        }

        var annotatedPath = Path.Combine(outputDirectory, $"{sample.Id}_detected.png");
        var edgesPath = Path.Combine(outputDirectory, $"{sample.Id}_edges.png");

        using var edgesPreview = new Mat();
        Cv2.CvtColor(edges, edgesPreview, ColorConversionCodes.GRAY2BGR);
        Cv2.ImWrite(annotatedPath, annotated);
        Cv2.ImWrite(edgesPath, edgesPreview);

        return new VisionAnalysis(sample, sample.ImagePath, annotatedPath, edgesPath, detections);
    }

    private static int EnsureOdd(int value)
    {
        var normalized = Math.Max(1, value);
        return normalized % 2 == 0 ? normalized + 1 : normalized;
    }

    private static List<ContourCandidate> SuppressOverlaps(IEnumerable<ContourCandidate> candidates, double threshold)
    {
        var selected = new List<ContourCandidate>();

        foreach (var candidate in candidates)
        {
            var overlaps = selected.Any(existing =>
                IntersectionOverUnion(candidate.Rect, existing.Rect) > threshold ||
                IntersectionOverSmaller(candidate.Rect, existing.Rect) > 0.72);

            if (!overlaps)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static double IntersectionOverUnion(Rect first, Rect second)
    {
        var intersection = IntersectionArea(first, second);
        var union = RectArea(first) + RectArea(second) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static double IntersectionOverSmaller(Rect first, Rect second)
    {
        var smaller = Math.Min(RectArea(first), RectArea(second));
        return smaller <= 0 ? 0 : IntersectionArea(first, second) / smaller;
    }

    private static double IntersectionArea(Rect first, Rect second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);

        return Math.Max(0, right - left) * Math.Max(0, bottom - top);
    }

    private static double RectArea(Rect rect)
    {
        return Math.Max(0, rect.Width) * Math.Max(0, rect.Height);
    }

    private static string ClassifyShape(int vertices, double circularity, Rect rect)
    {
        if (vertices == 3)
        {
            return "Трикутник";
        }

        if (vertices == 4)
        {
            var ratio = rect.Width / (double)rect.Height;
            return ratio is > 0.86 and < 1.16 ? "Квадрат" : "Прямокутник";
        }

        if (circularity > 0.74)
        {
            return "Коло";
        }

        if (vertices is >= 5 and <= 7)
        {
            return "Багатокутник";
        }

        return "Контур";
    }

    private static string EstimateColor(Mat source, Point[] contour)
    {
        using var mask = new Mat(source.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.DrawContours(mask, new[] { contour }, -1, Scalar.White, -1);
        var mean = Cv2.Mean(source, mask);

        var b = mean.Val0;
        var g = mean.Val1;
        var r = mean.Val2;

        if (r > 175 && g > 165 && b < 130) return "Жовтий";
        if (r > 170 && g < 130 && b < 130) return "Червоний";
        if (g > 145 && r < 150 && b < 155) return "Зелений";
        if (b > 165 && r < 150) return "Синій";
        if (r > 145 && b > 145 && g < 145) return "Фіолетовий";
        if (r > 150 && g > 105 && b < 105) return "Помаранчевий";

        return "Змішаний";
    }

    private static double EstimateConfidence(string label, double area, Rect rect, double circularity)
    {
        var fillRatio = area / Math.Max(1, rect.Width * rect.Height);
        var baseScore = label switch
        {
            "Коло" => 0.74 + Math.Min(0.2, Math.Abs(circularity - 0.78)),
            "Трикутник" => 0.84,
            "Квадрат" => 0.86,
            "Прямокутник" => 0.84,
            "Багатокутник" => 0.78,
            "Об'єкт" => 0.74,
            _ => 0.7
        };

        var score = baseScore + Math.Min(0.1, fillRatio * 0.1);
        return Math.Round(Math.Clamp(score, 0.58, 0.98) * 100.0, 1);
    }

    private static void DrawDetection(Mat image, Rect rect, DetectionResult result)
    {
        var color = new Scalar(45, 112, 240);
        Cv2.Rectangle(image, rect, color, 3, LineTypes.AntiAlias);

        var label = $"{result.Number}. {ToEnglishLabel(result.Label)} {result.Confidence:0}%";
        var origin = new Point(rect.X + 6, rect.Y + 25);
        Cv2.Rectangle(image, new Rect(origin.X - 4, origin.Y - 22, Math.Min(230, Math.Max(130, label.Length * 12)), 30), new Scalar(255, 255, 255), -1);
        Cv2.PutText(image, label, origin, HersheyFonts.HersheySimplex, 0.62, new Scalar(25, 38, 64), 2, LineTypes.AntiAlias);
    }

    private static string ToEnglishLabel(string label)
    {
        return label switch
        {
            "Трикутник" => "Triangle",
            "Квадрат" => "Square",
            "Прямокутник" => "Rect",
            "Коло" => "Circle",
            "Багатокутник" => "Polygon",
            _ => "Object"
        };
    }

    private sealed record ContourCandidate(Point[] Contour, double Area, Rect Rect);
}
