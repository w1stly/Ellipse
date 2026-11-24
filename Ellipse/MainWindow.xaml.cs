using Ellipse.EllipseConstructor.Models;
using Ellipse.EllipseConstructor.ViewModels;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace Ellipse.EllipseConstructor;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    private const float BasePixelsPerUnit = 40f;
    private float _zoom = 1.0f;
    private Vector2 _panOffset = Vector2.Zero;

    private bool _isPanning;
    private bool _isDrawingInteractive;
    private Vector2 _lastPointerPos;
    private Vector2 _interactiveStartWorld;
    private EllipseModel? _interactiveEllipse;

    public int ModeComboIndex
    {
        get => (int)ViewModel.CurrentMode;
        set => ViewModel.CurrentMode = (ConstructionMode)value;
    }

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        ViewModel.RequestCanvasInvalidate += () => MainCanvas.Invalidate();
    }

    public string FormatDouble(double value) => value.ToString("F2");


    private float PixelsPerUnit => BasePixelsPerUnit * _zoom;

    private Vector2 WorldToScreen(Vector2 world, float width, float height)
    {
        return new Vector2(
            width / 2f + _panOffset.X + world.X * PixelsPerUnit,
            height / 2f + _panOffset.Y - world.Y * PixelsPerUnit);
    }

    private Vector2 ScreenToWorld(Vector2 screen, float width, float height)
    {
        return new Vector2(
            (screen.X - width / 2f - _panOffset.X) / PixelsPerUnit,
            (height / 2f + _panOffset.Y - screen.Y) / PixelsPerUnit);
    }


    private void MainCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        RenderScene(args.DrawingSession, (float)sender.ActualWidth, (float)sender.ActualHeight);
    }

    private void RenderScene(CanvasDrawingSession ds, float width, float height)
    {
        bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;
        Color gridColor = isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(35, 0, 0, 0);
        Color axisColor = isDark ? Color.FromArgb(180, 220, 220, 220) : Color.FromArgb(180, 40, 40, 40);
        Color textColor = isDark ? Colors.LightGray : Colors.DimGray;

        ds.Antialiasing = CanvasAntialiasing.Antialiased;

        if (ViewModel.ShowGrid)
        {
            DrawGrid(ds, width, height, gridColor, textColor);
        }

        if (ViewModel.ShowAxes)
        {
            var origin = WorldToScreen(Vector2.Zero, width, height);
            ds.DrawLine(0, origin.Y, width, origin.Y, axisColor, 1.5f);
            ds.DrawLine(origin.X, 0, origin.X, height, axisColor, 1.5f);

            using var axisFormat = new CanvasTextFormat { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            ds.DrawText("X", width - 20, Math.Clamp(origin.Y + 6, 4, height - 24), axisColor, axisFormat);
            ds.DrawText("Y", Math.Clamp(origin.X + 8, 4, width - 24), 6, axisColor, axisFormat);
        }

        foreach (var ellipse in ViewModel.Ellipses)
        {
            if (!ellipse.IsVisible) continue;
            DrawEllipse(ds, ellipse, width, height, ellipse == ViewModel.SelectedEllipse);
        }
    }

    private void DrawGrid(CanvasDrawingSession ds, float width, float height, Color gridColor, Color textColor)
    {
        float stepWorld = _zoom < 0.4f ? 5f : _zoom < 0.8f ? 2f : 1f;
        var topLeft = ScreenToWorld(Vector2.Zero, width, height);
        var bottomRight = ScreenToWorld(new Vector2(width, height), width, height);

        float startX = MathF.Floor(topLeft.X / stepWorld) * stepWorld;
        float endX = MathF.Ceiling(bottomRight.X / stepWorld) * stepWorld;
        float startY = MathF.Floor(bottomRight.Y / stepWorld) * stepWorld;
        float endY = MathF.Ceiling(topLeft.Y / stepWorld) * stepWorld;

        var origin = WorldToScreen(Vector2.Zero, width, height);
        using var labelFormat = new CanvasTextFormat { FontSize = 10 };

        for (float x = startX; x <= endX; x += stepWorld)
        {
            var p = WorldToScreen(new Vector2(x, 0), width, height);
            ds.DrawLine(p.X, 0, p.X, height, gridColor, 1f);
            if (Math.Abs(x) > 0.01f)
                ds.DrawText(x.ToString("0.#"), p.X + 3, Math.Clamp(origin.Y + 3, 2, height - 16), textColor, labelFormat);
        }

        for (float y = startY; y <= endY; y += stepWorld)
        {
            var p = WorldToScreen(new Vector2(0, y), width, height);
            ds.DrawLine(0, p.Y, width, p.Y, gridColor, 1f);
            if (Math.Abs(y) > 0.01f)
                ds.DrawText(y.ToString("0.#"), Math.Clamp(origin.X + 4, 2, width - 26), p.Y - 14, textColor, labelFormat);
        }
    }

    private void DrawEllipse(CanvasDrawingSession ds, EllipseModel e, float width, float height, bool isSelected)
    {
        var centerScreen = WorldToScreen(new Vector2((float)e.CenterX, (float)e.CenterY), width, height);
        float rx = (float)(e.SemiAxisA * PixelsPerUnit);
        float ry = (float)(e.SemiAxisB * PixelsPerUnit);
        float angleRad = (float)(-e.RotationAngle * Math.PI / 180.0); // Negated for screen Y-down

        var previousTransform = ds.Transform;
        ds.Transform = Matrix3x2.CreateRotation(angleRad, centerScreen) * previousTransform;

        Color fill = Color.FromArgb((byte)(e.FillOpacity * 255), e.FillColor.R, e.FillColor.G, e.FillColor.B);
        ds.FillEllipse(centerScreen, rx, ry, fill);

        ds.DrawEllipse(centerScreen, rx, ry, e.StrokeColor, (float)e.StrokeThickness);

        if (ViewModel.ShowHelpers && isSelected)
        {
            using var dashedStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
            Color helperColor = Color.FromArgb(150, e.StrokeColor.R, e.StrokeColor.G, e.StrokeColor.B);

            ds.DrawRectangle(centerScreen.X - rx, centerScreen.Y - ry, rx * 2, ry * 2, helperColor, 1f, dashedStyle);

            ds.DrawLine(centerScreen.X - rx, centerScreen.Y, centerScreen.X + rx, centerScreen.Y, helperColor, 1f, dashedStyle);
            ds.DrawLine(centerScreen.X, centerScreen.Y - ry, centerScreen.X, centerScreen.Y + ry, helperColor, 1f, dashedStyle);
        }

        ds.Transform = previousTransform;

        if (ViewModel.ShowHelpers && isSelected)
        {
            using var labelFormat = new CanvasTextFormat { FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            ds.FillCircle(centerScreen, 4f, e.StrokeColor);

            var f1 = WorldToScreen(e.Focus1, width, height);
            var f2 = WorldToScreen(e.Focus2, width, height);
            ds.FillCircle(f1, 4f, Colors.Crimson);
            ds.FillCircle(f2, 4f, Colors.Crimson);
            ds.DrawText("F₁", f1.X + 6, f1.Y - 14, Colors.Crimson, labelFormat);
            ds.DrawText("F₂", f2.X + 6, f2.Y - 14, Colors.Crimson, labelFormat);
        }
    }


    private void MainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(MainCanvas);
        var pos = new Vector2((float)pt.Position.X, (float)pt.Position.Y);
        MainCanvas.CapturePointer(e.Pointer);

        if (ViewModel.CurrentMode == ConstructionMode.Interactive && pt.Properties.IsLeftButtonPressed)
        {
            _isDrawingInteractive = true;
            _interactiveStartWorld = ScreenToWorld(pos, (float)MainCanvas.ActualWidth, (float)MainCanvas.ActualHeight);
            _interactiveEllipse = ViewModel.CreateInteractiveEllipse(_interactiveStartWorld.X, _interactiveStartWorld.Y);
        }
        else
        {
            _isPanning = true;
            _lastPointerPos = pos;
        }
    }

    private void MainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(MainCanvas);
        var pos = new Vector2((float)pt.Position.X, (float)pt.Position.Y);
        var world = ScreenToWorld(pos, (float)MainCanvas.ActualWidth, (float)MainCanvas.ActualHeight);

        ViewModel.CursorCoordsText = $"X: {world.X:F2}  Y: {world.Y:F2}";

        if (_isDrawingInteractive && _interactiveEllipse is not null)
        {
            _interactiveEllipse.CenterX = Math.Round((_interactiveStartWorld.X + world.X) / 2.0, 2);
            _interactiveEllipse.CenterY = Math.Round((_interactiveStartWorld.Y + world.Y) / 2.0, 2);
            _interactiveEllipse.SemiAxisA = Math.Max(0.1, Math.Round(Math.Abs(world.X - _interactiveStartWorld.X) / 2.0, 2));
            _interactiveEllipse.SemiAxisB = Math.Max(0.1, Math.Round(Math.Abs(world.Y - _interactiveStartWorld.Y) / 2.0, 2));
            MainCanvas.Invalidate();
        }
        else if (_isPanning)
        {
            _panOffset += pos - _lastPointerPos;
            _lastPointerPos = pos;
            MainCanvas.Invalidate();
        }
    }

    private void MainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPanning = false;
        _isDrawingInteractive = false;
        _interactiveEllipse = null;
        MainCanvas.ReleasePointerCapture(e.Pointer);
    }

    private void MainCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(MainCanvas);
        int delta = pt.Properties.MouseWheelDelta;
        float factor = delta > 0 ? 1.1f : 0.9f;

        _zoom = Math.Clamp(_zoom * factor, 0.2f, 5.0f);
        ViewModel.ZoomPercentText = $"{(int)(_zoom * 100)}%";
        MainCanvas.Invalidate();
    }


    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _zoom = 1.0f;
        _panOffset = Vector2.Zero;
        ViewModel.ZoomPercentText = "100%";
        MainCanvas.Invalidate();
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        RootGrid.RequestedTheme = RootGrid.ActualTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;
        MainCanvas.Invalidate();
    }

    private void VisibilityCheck_Click(object sender, RoutedEventArgs e) => MainCanvas.Invalidate();

    private void StrokeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (ViewModel.SelectedEllipse is not null)
        {
            ViewModel.SelectedEllipse.FillColor = args.NewColor;
            MainCanvas.Invalidate();
        }
    }

    private async void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = "Побудова_еліпсів"
        };
        picker.FileTypeChoices.Add("Зображення PNG", new List<string> { ".png" });

        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hWnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        float width = (float)MainCanvas.ActualWidth;
        float height = (float)MainCanvas.ActualHeight;

        using var renderTarget = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(RootGrid.ActualTheme == ElementTheme.Dark ? Color.FromArgb(255, 32, 32, 32) : Colors.White);
            RenderScene(ds, width, height);
        }

        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
    }
}