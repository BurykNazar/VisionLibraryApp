# VisionLibraryLab

Лабораторна робота №5 з дисципліни "Системи штучного інтелекту".

Тема: "Бібліотеки машинного зору".

## Склад рішення

- `VisionLibrary.Core` - генерація демонстраційних зображень і OpenCV/OpenCvSharp-детектор контурів.
- `VisionLibraryApp` - WinUI 3 + XAML застосунок із піктограмою, адаптивним інтерфейсом і переглядом результатів.
- `VisionLibrary.Tools` - консольний інструмент для створення зображень, summary та відеодемонстрації.
- `artifacts` - готові зображення, скриншоти, відео та рендер звіту.

Додаток також підтримує завантаження власного зображення через кнопку
`Завантажити фото`. Підтримуються `png`, `jpg`, `jpeg`, `bmp` і `webp`.
Для власних фотографій використовується не контурна класифікація фігур, а
OpenCV DNN SSD face detector (`deploy.prototxt` +
`res10_300x300_ssd_iter_140000.caffemodel`) для пошуку облич. Haar Cascade
залишений як fallback, якщо DNN-модель недоступна. Контурний режим залишається
для демонстраційних геометричних сцен.

## Запуск

```powershell
dotnet build .\VisionLibraryLab.sln
dotnet run --project .\VisionLibraryApp\VisionLibraryApp.csproj
```

## Генерація артефактів

```powershell
dotnet run --project .\VisionLibrary.Tools\VisionLibrary.Tools.csproj -- .
```

Готовий звіт:

- `СШІ_КН-2427Б_Соколовський_Бурик_ЛР5.docx`
- `СШІ_КН-2427Б_Соколовський_Бурик_ЛР5.pdf`
