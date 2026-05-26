using OpenCvSharp;

namespace VisionLibrary.Core;

public static class SampleImageFactory
{
    public static IReadOnlyList<VisionSample> EnsureSamples(string directory)
    {
        Directory.CreateDirectory(directory);

        var samples = new[]
        {
            new VisionSample(
                "shapes",
                "Геометричні об'єкти",
                "Кольорові кола, трикутники та прямокутники для детектування контурів.",
                Path.Combine(directory, "sample_shapes.png")),
            new VisionSample(
                "markers",
                "Маркери на світлому фоні",
                "Об'єкти різного розміру з частковими перетинами та шумом.",
                Path.Combine(directory, "sample_markers.png")),
            new VisionSample(
                "letters",
                "Літери та прості фігури",
                "Контрастні літери разом із фігурами для перевірки меж алгоритму.",
                Path.Combine(directory, "sample_letters.png")),
        };

        CreateShapes(samples[0].ImagePath);
        CreateMarkers(samples[1].ImagePath);
        CreateLetters(samples[2].ImagePath);

        return samples;
    }

    private static void CreateShapes(string path)
    {
        using var image = NewCanvas();
        AddGrid(image);

        Cv2.Circle(image, new Point(190, 180), 82, new Scalar(64, 145, 255), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(image, new Rect(395, 95, 190, 145), new Scalar(99, 189, 87), -1, LineTypes.AntiAlias);
        DrawTriangle(image, new Point(760, 85), new Point(650, 265), new Point(870, 265), new Scalar(238, 117, 91));
        Cv2.Rectangle(image, new Rect(145, 385, 210, 120), new Scalar(182, 113, 222), -1, LineTypes.AntiAlias);
        Cv2.Circle(image, new Point(560, 450), 70, new Scalar(78, 196, 219), -1, LineTypes.AntiAlias);
        DrawRotatedRectangle(image, new Point2f(780, 440), new Size2f(180, 105), -15, new Scalar(62, 122, 226));

        AddNoise(image, 650, seed: 14);
        Cv2.ImWrite(path, image);
    }

    private static void CreateMarkers(string path)
    {
        using var image = NewCanvas();
        AddGrid(image);

        Cv2.Circle(image, new Point(210, 175), 96, new Scalar(61, 113, 235), -1, LineTypes.AntiAlias);
        Cv2.Circle(image, new Point(295, 245), 46, new Scalar(245, 247, 252), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(image, new Rect(440, 120, 160, 210), new Scalar(93, 183, 128), -1, LineTypes.AntiAlias);
        DrawTriangle(image, new Point(725, 130), new Point(625, 315), new Point(845, 300), new Scalar(222, 128, 70));
        DrawRotatedRectangle(image, new Point2f(280, 465), new Size2f(230, 90), 12, new Scalar(187, 93, 201));
        Cv2.Circle(image, new Point(615, 470), 78, new Scalar(70, 170, 220), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(image, new Rect(760, 405, 118, 118), new Scalar(84, 97, 219), -1, LineTypes.AntiAlias);

        AddNoise(image, 900, seed: 31);
        Cv2.ImWrite(path, image);
    }

    private static void CreateLetters(string path)
    {
        using var image = NewCanvas();
        AddGrid(image);

        Cv2.PutText(image, "AI", new Point(120, 255), HersheyFonts.HersheyDuplex, 4.6, new Scalar(70, 85, 190), 11, LineTypes.AntiAlias);
        Cv2.PutText(image, "CV", new Point(430, 255), HersheyFonts.HersheyDuplex, 4.4, new Scalar(52, 150, 98), 11, LineTypes.AntiAlias);
        Cv2.Circle(image, new Point(240, 440), 78, new Scalar(76, 178, 223), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(image, new Rect(455, 370, 170, 138), new Scalar(225, 124, 76), -1, LineTypes.AntiAlias);
        DrawTriangle(image, new Point(780, 355), new Point(680, 530), new Point(880, 530), new Scalar(173, 99, 214));

        AddNoise(image, 700, seed: 55);
        Cv2.ImWrite(path, image);
    }

    private static Mat NewCanvas()
    {
        return new Mat(new Size(960, 640), MatType.CV_8UC3, new Scalar(247, 249, 252));
    }

    private static void AddGrid(Mat image)
    {
        for (var x = 0; x < image.Width; x += 40)
        {
            Cv2.Line(image, new Point(x, 0), new Point(x, image.Height), new Scalar(235, 239, 246), 1);
        }

        for (var y = 0; y < image.Height; y += 40)
        {
            Cv2.Line(image, new Point(0, y), new Point(image.Width, y), new Scalar(235, 239, 246), 1);
        }
    }

    private static void DrawTriangle(Mat image, Point a, Point b, Point c, Scalar color)
    {
        Cv2.FillConvexPoly(image, new[] { a, b, c }, color, LineTypes.AntiAlias);
    }

    private static void DrawRotatedRectangle(Mat image, Point2f center, Size2f size, float angle, Scalar color)
    {
        var box = new RotatedRect(center, size, angle);
        var points = Array.ConvertAll(box.Points(), p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)));
        Cv2.FillConvexPoly(image, points, color, LineTypes.AntiAlias);
    }

    private static void AddNoise(Mat image, int count, int seed)
    {
        var random = new Random(seed);

        for (var i = 0; i < count; i++)
        {
            var point = new Point(random.Next(image.Width), random.Next(image.Height));
            var tone = random.Next(215, 246);
            Cv2.Circle(image, point, random.Next(1, 3), new Scalar(tone, tone, tone), -1, LineTypes.AntiAlias);
        }
    }
}
