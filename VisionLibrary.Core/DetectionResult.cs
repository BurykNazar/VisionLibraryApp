namespace VisionLibrary.Core;

public sealed record DetectionResult(
    int Number,
    string Label,
    string Color,
    double Confidence,
    double Area,
    int X,
    int Y,
    int Width,
    int Height);

public sealed record VisionSample(
    string Id,
    string Title,
    string Description,
    string ImagePath,
    bool IsImported = false);

public sealed record VisionAnalysis(
    VisionSample Sample,
    string SourceImagePath,
    string AnnotatedImagePath,
    string EdgesImagePath,
    IReadOnlyList<DetectionResult> Detections);

public sealed class VisionOptions
{
    public int BlurSize { get; init; } = 5;
    public int CannyLowThreshold { get; init; } = 42;
    public int CannyHighThreshold { get; init; } = 130;
    public int MinContourArea { get; init; } = 1_600;
    public double MinContourAreaRatio { get; init; }
    public double MaxContourAreaRatio { get; init; } = 0.92;
    public int DilateSize { get; init; } = 3;
    public int MaxDetections { get; init; } = 50;
    public bool SuppressOverlappingContours { get; init; }
    public double OverlapThreshold { get; init; } = 0.35;
    public bool PrioritizeLargeContours { get; init; }
    public bool ClassifySimpleShapes { get; init; } = true;

    public static VisionOptions ForNaturalPhoto()
    {
        return new VisionOptions
        {
            BlurSize = 9,
            CannyLowThreshold = 80,
            CannyHighThreshold = 190,
            MinContourArea = 6_500,
            MinContourAreaRatio = 0.018,
            MaxContourAreaRatio = 0.88,
            DilateSize = 5,
            MaxDetections = 5,
            SuppressOverlappingContours = true,
            OverlapThreshold = 0.28,
            PrioritizeLargeContours = true,
            ClassifySimpleShapes = false,
        };
    }
}
