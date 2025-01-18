using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace SkiaSharpFiddle
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Compiler compiler = new Compiler();

        [ObservableProperty]
        private string sourceCode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DrawingSize))]
        [NotifyPropertyChangedFor(nameof(ImageInfo))]
        private int drawingWidth = 256;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DrawingSize))]
        [NotifyPropertyChangedFor(nameof(ImageInfo))]
        private int drawingHeight = 256;

        [ObservableProperty]
        private ColorCombination colorCombination;

        [ObservableProperty]
        private SKImage rasterDrawing;
        [ObservableProperty]
        private SKImage gpuDrawing;
        [ObservableProperty]
        private Mode mode = Mode.Ready;

        private CancellationTokenSource cancellation;
        private CompilationResult lastResult;

        public MainViewModel()
        {
            var color = SKImageInfo.PlatformColorType;
            var colorString = color == SKColorType.Bgra8888 ? "BGRA" : "RGBA";
            ColorCombinations = new ColorCombination[]
            {
                new ColorCombination(colorString, color, null),
                new ColorCombination($"{colorString} (sRGB)", color, SKColorSpace.CreateSrgb()),
                new ColorCombination("F16 (sRGB Linear)", SKColorType.RgbaF16, SKColorSpace.CreateSrgbLinear()),
            };

            CompilationMessages = new ObservableRangeCollection<CompilationMessage>();

            var skiaAss = typeof(SKSurface).Assembly;
            if (skiaAss.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) is AssemblyInformationalVersionAttribute informational)
                SkiaSharpVersion = informational.InformationalVersion;
            else if (skiaAss.GetCustomAttribute(typeof(AssemblyFileVersionAttribute)) is AssemblyFileVersionAttribute fileVersion)
                SkiaSharpVersion = fileVersion.Version;
            else if (skiaAss.GetCustomAttribute(typeof(AssemblyVersionAttribute)) is AssemblyVersionAttribute version)
                SkiaSharpVersion = version.Version;
            else
                SkiaSharpVersion = "<unknown>";
        }

        public ColorCombination[] ColorCombinations { get; }

        public SKSizeI DrawingSize => new SKSizeI(DrawingWidth, DrawingHeight);

        public SKImageInfo ImageInfo => new SKImageInfo(DrawingWidth, DrawingHeight);

        public ObservableRangeCollection<CompilationMessage> CompilationMessages { get; }

        public string SkiaSharpVersion { get; }

        partial void OnDrawingWidthChanged(int value)
        {
            GenerateDrawings();
        }

        partial void OnDrawingHeightChanged(int value)
        {
            GenerateDrawings();
        }

        partial void OnSourceCodeChanged(string value)
        {
            HandleSourceCodeChangedAsync();
        }

        private async void HandleSourceCodeChangedAsync()
        {
            cancellation?.Cancel();
            cancellation = new CancellationTokenSource();

            Mode = Mode.Working;

            try
            {
                lastResult = await compiler.CompileAsync(SourceCode, cancellation.Token);
                CompilationMessages.ReplaceRange(lastResult.CompilationMessages);

                Mode = lastResult.HasErrors ? Mode.Error : Mode.Ready;
            }
            catch (OperationCanceledException)
            {
            }

            GenerateDrawings();
        }

        partial void OnColorCombinationChanged(ColorCombination value)
        {
            GenerateDrawings();
        }

        private void GenerateDrawings()
        {
            GenerateRasterDrawing();
            GenerateGpuDrawing();
        }

        private void GenerateRasterDrawing()
        {
            var old = RasterDrawing;

            var info = ImageInfo;
            using (var surface = SKSurface.Create(info))
            {
                Draw(surface, info);
                RasterDrawing = surface.Snapshot();
            }

            old?.Dispose();
        }

        private void GenerateGpuDrawing()
        {
            // TODO: implement offscreen GPU drawing
        }

        private void Draw(SKSurface surface, SKImageInfo info)
        {
            var messages = lastResult?.Draw(surface, info.Size);

            if (messages?.Any() == true)
                CompilationMessages.ReplaceRange(messages);
        }
    }
}
