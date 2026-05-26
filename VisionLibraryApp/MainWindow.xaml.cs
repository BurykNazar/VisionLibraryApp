using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using VisionLibrary.Core;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VisionLibraryApp;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<DetectionRow> _detections = new();
    private readonly OpenCvShapeDetector _detector = new();
    private readonly string _sampleDirectory;
    private readonly string _importDirectory;
    private readonly string _resultsDirectory;

    private IReadOnlyList<VisionSample> _samples = Array.Empty<VisionSample>();
    private VisionAnalysis? _currentAnalysis;
    private bool _isReady;

    public MainWindow()
    {
        InitializeComponent();
        SetWindowSize(1320, 830);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VisionLibraryLab");

        _sampleDirectory = Path.Combine(appData, "Samples");
        _importDirectory = Path.Combine(appData, "Imported");
        _resultsDirectory = Path.Combine(appData, "Results");

        DetectionsList.ItemsSource = _detections;
        Root.Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _samples = SampleImageFactory.EnsureSamples(_sampleDirectory);
        SampleComboBox.ItemsSource = _samples;
        SampleComboBox.SelectedIndex = 0;
        _isReady = true;
        RunDetection();
    }

    private void SetWindowSize(int width, int height)
    {
        if (AppWindow is null)
        {
            return;
        }

        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Title = "Vision Library Lab";

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    private void ContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 980;
        var compactImages = e.NewSize.Width < 1120;
        var compactDetails = e.NewSize.Width < 1180;

        MainContentRow.Height = narrow ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        StackedContentRow.Height = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SettingsColumn.Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(332);
        WorkspaceColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

        SettingsPanel.Margin = narrow ? new Thickness(0, 0, 0, 18) : new Thickness(0, 0, 24, 0);

        Grid.SetRow(SettingsPanel, 0);
        Grid.SetColumn(SettingsPanel, 0);
        Grid.SetRow(WorkspacePanel, narrow ? 1 : 0);
        Grid.SetColumn(WorkspacePanel, narrow ? 0 : 1);

        ApplyImageLayout(compactImages || narrow);
        ApplyDetailsLayout(compactDetails || narrow);
    }

    private void ApplyImageLayout(bool compact)
    {
        SourceImageColumn.Width = new GridLength(1, GridUnitType.Star);
        ResultImageColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ImagesSecondRow.Height = compact ? GridLength.Auto : new GridLength(0);

        Grid.SetColumn(ResultImageCard, compact ? 0 : 1);
        Grid.SetRow(ResultImageCard, compact ? 1 : 0);
    }

    private void ApplyDetailsLayout(bool compact)
    {
        DetectionsColumn.Width = new GridLength(1, GridUnitType.Star);
        LogColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        DetailsSecondRow.Height = compact ? GridLength.Auto : new GridLength(0);

        Grid.SetColumn(LogCard, compact ? 0 : 1);
        Grid.SetRow(LogCard, compact ? 1 : 0);
    }

    private void SampleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isReady && SampleComboBox.SelectedItem is VisionSample)
        {
            RunDetection();
        }
    }

    private void EdgesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateResultPreview();
    }

    private void RunDetectionButton_Click(object sender, RoutedEventArgs e)
    {
        RunDetection();
    }

    private async void ImportPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".webp");

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(file.Path))
            {
                LogTextBox.Text = "Не вдалося отримати локальний шлях до вибраного зображення.";
                return;
            }

            var imported = ImportedImageStore.Import(file.Path, _importDirectory);
            _samples = _samples.Concat(new[] { imported }).ToArray();
            SampleComboBox.ItemsSource = _samples;
            SampleComboBox.SelectedItem = imported;
            RunDetection();
        }
        catch (Exception ex)
        {
            LogTextBox.Text = ex.Message;
        }
    }

    private void OpenResultsButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_resultsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _resultsDirectory,
            UseShellExecute = true
        });
    }

    private void RunDetection()
    {
        if (SampleComboBox.SelectedItem is not VisionSample sample)
        {
            return;
        }

        try
        {
            _currentAnalysis = sample.IsImported
                ? new OpenCvFaceDetector().Analyze(sample, _resultsDirectory)
                : _detector.Analyze(sample, _resultsDirectory);
            SourceImage.Source = LoadBitmap(_currentAnalysis.SourceImagePath);
            UpdateResultPreview();
            UpdateMetrics(_currentAnalysis);
            UpdateDetections(_currentAnalysis);
            UpdateLog(_currentAnalysis);
        }
        catch (Exception ex)
        {
            LogTextBox.Text = ex.Message;
        }
    }

    private void UpdateResultPreview()
    {
        if (_currentAnalysis is null)
        {
            return;
        }

        var path = EdgesToggle.IsOn ? _currentAnalysis.EdgesImagePath : _currentAnalysis.AnnotatedImagePath;
        ResultImageTitle.Text = EdgesToggle.IsOn ? "Карта контурів Canny" : "Результат детекції";
        if (_currentAnalysis.Sample.IsImported)
        {
            ResultImageTitle.Text = EdgesToggle.IsOn ? "Підготовлене grayscale-зображення" : "Детекція облич";
        }

        ResultImage.Source = LoadBitmap(path);
    }

    private void UpdateMetrics(VisionAnalysis analysis)
    {
        DetectedCountText.Text = analysis.Detections.Count.ToString();
        SceneText.Text = analysis.Sample.Title;

        var average = analysis.Detections.Count == 0
            ? 0
            : analysis.Detections.Average(item => item.Confidence);

        ConfidenceText.Text = $"{average:0.0}%";
    }

    private void UpdateDetections(VisionAnalysis analysis)
    {
        _detections.Clear();

        foreach (var detection in analysis.Detections)
        {
            _detections.Add(new DetectionRow(
                detection.Number.ToString(),
                detection.Label,
                $"{detection.Color}; рамка {detection.Width}x{detection.Height}; площа {detection.Area:0}",
                $"{detection.Confidence:0.0}%"));
        }
    }

    private void UpdateLog(VisionAnalysis analysis)
    {
        var log = new StringBuilder()
            .AppendLine("OpenCV pipeline")
            .AppendLine($"Зображення: {analysis.Sample.Title}")
            .AppendLine(analysis.Sample.IsImported
                ? "Профіль: власне фото, OpenCV DNN face detection"
                : "Профіль: демонстраційні фігури")
            .AppendLine("1. Cv2.ImRead -> Mat")
            .AppendLine("2. Cv2.CvtColor(BGR2GRAY)")
            .AppendLine(analysis.Sample.IsImported ? "3. CvDnn.BlobFromImage" : "3. GaussianBlur + Canny")
            .AppendLine(analysis.Sample.IsImported ? "4. Net.Forward + confidence filter" : "4. Dilate + FindContours")
            .AppendLine(analysis.Sample.IsImported ? "5. Рамки навколо знайдених облич" : "5. BoundingRect + класифікація форми")
            .AppendLine()
            .AppendLine($"Знайдено об'єктів: {analysis.Detections.Count}");

        foreach (var detection in analysis.Detections)
        {
            log.AppendLine($"{detection.Number}. {detection.Label}, {detection.Color}, {detection.Confidence:0.0}%");
        }

        LogTextBox.Text = log.ToString();
    }

    private static BitmapImage LoadBitmap(string path)
    {
        return new BitmapImage(new Uri(path));
    }

    private sealed record DetectionRow(string Number, string Label, string Details, string Confidence);
}
