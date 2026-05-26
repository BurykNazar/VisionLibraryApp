namespace VisionLibrary.Core;

public static class ImportedImageStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp",
    };

    public static VisionSample Import(string sourcePath, string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Шлях до зображення не задано.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Вибране зображення не знайдено.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new NotSupportedException("Підтримуються тільки PNG, JPG, JPEG, BMP та WEBP.");
        }

        Directory.CreateDirectory(destinationDirectory);

        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{safeName}{extension}";
        var importedPath = Path.Combine(destinationDirectory, fileName);

        File.Copy(sourcePath, importedPath, overwrite: false);

        return new VisionSample(
            $"custom-{Path.GetFileNameWithoutExtension(fileName)}",
            $"Власне фото: {Path.GetFileName(sourcePath)}",
            "Зображення, завантажене користувачем для OpenCV-аналізу.",
            importedPath,
            IsImported: true);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }
}
