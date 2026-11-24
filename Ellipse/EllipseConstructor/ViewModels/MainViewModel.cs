using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ellipse.EllipseConstructor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace Ellipse.EllipseConstructor.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private int _ellipseCounter = 1;

        public event Action? RequestCanvasInvalidate;

        public ObservableCollection<EllipseModel> Ellipses { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedEllipse))]
        private EllipseModel? _selectedEllipse;

        public bool HasSelectedEllipse => SelectedEllipse is not null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsByCenterMode), nameof(IsByFociMode), nameof(IsInteractiveMode), nameof(ActiveToolHint))]
        private ConstructionMode _currentMode = ConstructionMode.ByCenter;

        public bool IsByCenterMode => CurrentMode == ConstructionMode.ByCenter;
        public bool IsByFociMode => CurrentMode == ConstructionMode.ByFoci;
        public bool IsInteractiveMode => CurrentMode == ConstructionMode.Interactive;

        [ObservableProperty] private double _focus1X = -3.0;
        [ObservableProperty] private double _focus1Y = 0.0;
        [ObservableProperty] private double _focus2X = 3.0;
        [ObservableProperty] private double _focus2Y = 0.0;
        [ObservableProperty] private double _fociMajorAxis2A = 10.0;

        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private string _cursorCoordsText = "X: 0.00  Y: 0.00";
        [ObservableProperty] private string _zoomPercentText = "100%";

        public string ActiveToolHint => CurrentMode switch
        {
            ConstructionMode.ByCenter => "Режим: Параметрична побудова (перетягування ЛКМ — панорамування)",
            ConstructionMode.ByFoci => "Режим: Побудова за фокусами (введіть координати F₁, F₂ та 2a)",
            ConstructionMode.Interactive => "Режим: Інтерактивне малювання (затисніть ЛКМ на полотні, щоб намалювати еліпс)",
            _ => string.Empty
        };

        [ObservableProperty] private bool _showGrid = true;
        [ObservableProperty] private bool _showAxes = true;
        [ObservableProperty] private bool _showHelpers = true;

        public MainViewModel()
        {
            AddDefaultEllipse();
        }

        partial void OnSelectedEllipseChanged(EllipseModel? oldValue, EllipseModel? newValue)
        {
            if (oldValue is not null)
                oldValue.PropertyChanged -= OnSelectedEllipsePropertyChanged;

            if (newValue is not null)
                newValue.PropertyChanged += OnSelectedEllipsePropertyChanged;

            RequestCanvasInvalidate?.Invoke();
        }

        private void OnSelectedEllipsePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RequestCanvasInvalidate?.Invoke();
        }

        partial void OnShowGridChanged(bool value) => RequestCanvasInvalidate?.Invoke();
        partial void OnShowAxesChanged(bool value) => RequestCanvasInvalidate?.Invoke();
        partial void OnShowHelpersChanged(bool value) => RequestCanvasInvalidate?.Invoke();

        [RelayCommand]
        private void AddDefaultEllipse()
        {
            ClearError();
            var colors = new[]
            {
            Color.FromArgb(255, 0, 120, 215),
            Color.FromArgb(255, 16, 124, 16),
            Color.FromArgb(255, 209, 52, 56),
            Color.FromArgb(255, 136, 23, 152),
            Color.FromArgb(255, 216, 59, 1)
        };

            var color = colors[(_ellipseCounter - 1) % colors.Length];
            var ellipse = new EllipseModel
            {
                Name = $"Еліпс {_ellipseCounter++}",
                CenterX = 0,
                CenterY = 0,
                SemiAxisA = 4.0,
                SemiAxisB = 2.5,
                StrokeColor = color,
                FillColor = color
            };

            Ellipses.Add(ellipse);
            SelectedEllipse = ellipse;
            RequestCanvasInvalidate?.Invoke();
        }

        [RelayCommand]
        private void BuildFromFoci()
        {
            ClearError();

            double dx = Focus2X - Focus1X;
            double dy = Focus2Y - Focus1Y;
            double distBetweenFoci = Math.Sqrt(dx * dx + dy * dy);

            if (FociMajorAxis2A <= 0)
            {
                SetError("Довжина великої осі (2a) має бути більшою за 0.");
                return;
            }

            if (FociMajorAxis2A <= distBetweenFoci)
            {
                SetError($"Значення 2a ({FociMajorAxis2A:F2}) має бути строго більшим за відстань між фокусами ({distBetweenFoci:F2}).");
                return;
            }

            double a = FociMajorAxis2A / 2.0;
            double c = distBetweenFoci / 2.0;
            double b = Math.Sqrt(a * a - c * c);
            double centerX = (Focus1X + Focus2X) / 2.0;
            double centerY = (Focus1Y + Focus2Y) / 2.0;
            double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            var ellipse = new EllipseModel
            {
                Name = $"Еліпс {_ellipseCounter++} (фокуси)",
                CenterX = Math.Round(centerX, 2),
                CenterY = Math.Round(centerY, 2),
                SemiAxisA = Math.Round(a, 2),
                SemiAxisB = Math.Round(b, 2),
                RotationAngle = Math.Round(angleDeg, 1),
                StrokeColor = Color.FromArgb(255, 136, 23, 152),
                FillColor = Color.FromArgb(255, 136, 23, 152)
            };

            Ellipses.Add(ellipse);
            SelectedEllipse = ellipse;
            RequestCanvasInvalidate?.Invoke();
        }

        [RelayCommand]
        private void DeleteSelectedEllipse()
        {
            if (SelectedEllipse is null) return;
            Ellipses.Remove(SelectedEllipse);
            SelectedEllipse = Ellipses.Count > 0 ? Ellipses[^1] : null;
            RequestCanvasInvalidate?.Invoke();
        }

        [RelayCommand]
        private void ClearAll()
        {
            ClearError();
            Ellipses.Clear();
            SelectedEllipse = null;
            RequestCanvasInvalidate?.Invoke();
        }

        [RelayCommand]
        private void NewProject()
        {
            ClearAll();
            _ellipseCounter = 1;
            AddDefaultEllipse();
        }

        public EllipseModel CreateInteractiveEllipse(double centerX, double centerY)
        {
            var ellipse = new EllipseModel
            {
                Name = $"Еліпс {_ellipseCounter++}",
                CenterX = Math.Round(centerX, 2),
                CenterY = Math.Round(centerY, 2),
                SemiAxisA = 0.1,
                SemiAxisB = 0.1,
                StrokeColor = Color.FromArgb(255, 0, 153, 188),
                FillColor = Color.FromArgb(255, 0, 153, 188)
            };

            Ellipses.Add(ellipse);
            SelectedEllipse = ellipse;
            return ellipse;
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }
    }
}
