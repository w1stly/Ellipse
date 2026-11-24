using System;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;

namespace Ellipse.EllipseConstructor.Models;

public partial class EllipseModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "Еліпс";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Focus1), nameof(Focus2), nameof(Focus1Text), nameof(Focus2Text), nameof(CanonicalEquation))]
    private double _centerX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Focus1), nameof(Focus2), nameof(Focus1Text), nameof(Focus2Text), nameof(CanonicalEquation))]
    private double _centerY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Area), nameof(Perimeter), nameof(FocalDistance), nameof(Eccentricity), nameof(Focus1), nameof(Focus2), nameof(Focus1Text), nameof(Focus2Text), nameof(CanonicalEquation))]
    private double _semiAxisA = 4.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Area), nameof(Perimeter), nameof(FocalDistance), nameof(Eccentricity), nameof(Focus1), nameof(Focus2), nameof(Focus1Text), nameof(Focus2Text), nameof(CanonicalEquation))]
    private double _semiAxisB = 2.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Focus1), nameof(Focus2), nameof(Focus1Text), nameof(Focus2Text))]
    private double _rotationAngle;

    [ObservableProperty]
    private Color _strokeColor = Color.FromArgb(255, 0, 120, 215);

    [ObservableProperty]
    private Color _fillColor = Color.FromArgb(255, 0, 120, 215);

    [ObservableProperty]
    private double _fillOpacity = 0.2;

    [ObservableProperty]
    private double _strokeThickness = 2.5;

    [ObservableProperty]
    private bool _isVisible = true;

    partial void OnSemiAxisAChanging(double value)
    {
        if (value <= 0.01) _semiAxisA = 0.01;
    }

    partial void OnSemiAxisBChanging(double value)
    {
        if (value <= 0.01) _semiAxisB = 0.01;
    }


    public double MajorSemiAxis => Math.Max(SemiAxisA, SemiAxisB);
    public double MinorSemiAxis => Math.Min(SemiAxisA, SemiAxisB);

    public double Area => Math.PI * SemiAxisA * SemiAxisB;

    public double Perimeter
    {
        get
        {
            double a = SemiAxisA;
            double b = SemiAxisB;
            return Math.PI * (3 * (a + b) - Math.Sqrt((3 * a + b) * (a + 3 * b)));
        }
    }

    public double FocalDistance => Math.Sqrt(Math.Abs(SemiAxisA * SemiAxisA - SemiAxisB * SemiAxisB));

    public double Eccentricity => MajorSemiAxis > 0 ? FocalDistance / MajorSemiAxis : 0;

    public Vector2 Focus1 => CalculateFocus(-1);
    public Vector2 Focus2 => CalculateFocus(1);

    public string Focus1Text => $"F₁ ({Focus1.X:F2}; {Focus1.Y:F2})";
    public string Focus2Text => $"F₂ ({Focus2.X:F2}; {Focus2.Y:F2})";

    public string CanonicalEquation
    {
        get
        {
            string xPart = Math.Abs(CenterX) < 0.001 ? "x²" : $"(x {(CenterX > 0 ? "-" : "+")} {Math.Abs(CenterX):F2})²";
            string yPart = Math.Abs(CenterY) < 0.001 ? "y²" : $"(y {(CenterY > 0 ? "-" : "+")} {Math.Abs(CenterY):F2})²";
            return $"{xPart} / {SemiAxisA:F2}² + {yPart} / {SemiAxisB:F2}² = 1";
        }
    }

    private Vector2 CalculateFocus(int sign)
    {
        double c = FocalDistance;
        double rad = RotationAngle * Math.PI / 180.0;

        double localX = SemiAxisA >= SemiAxisB ? sign * c : 0;
        double localY = SemiAxisA >= SemiAxisB ? 0 : sign * c;

        double rotX = localX * Math.Cos(rad) - localY * Math.Sin(rad);
        double rotY = localX * Math.Sin(rad) + localY * Math.Cos(rad);

        return new Vector2((float)(CenterX + rotX), (float)(CenterY + rotY));
    }
}