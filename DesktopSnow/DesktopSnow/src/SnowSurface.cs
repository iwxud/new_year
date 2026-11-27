using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopSnow
{
    internal sealed class SnowSurface : FrameworkElement
    {
        private const double PileCellWidth = 6.0;
        private const double SnowPlowWidth = 178.0;
        private const double SnowPlowHeight = 68.0;
        private const double AutomaticPlowThreshold = 0.76;

        private readonly Random _random;
        private readonly List<SnowParticle> _particles;
        private readonly SolidColorBrush[] _flakeBrushes;
        private readonly Pen[] _crystalPens;
        private readonly SolidColorBrush _pileBrush;
        private readonly double _floorRatio;
        private readonly SolidColorBrush _truckBodyBrush;
        private readonly SolidColorBrush _truckBodyLightBrush;
        private readonly SolidColorBrush _truckDarkBrush;
        private readonly SolidColorBrush _truckWindowBrush;
        private readonly SolidColorBrush _truckPlowBrush;
        private readonly SolidColorBrush _truckPlowLightBrush;
        private readonly SolidColorBrush _truckHeadlightBrush;
        private readonly SolidColorBrush _truckBeaconBrightBrush;
        private readonly SolidColorBrush _truckBeaconDimBrush;
        private readonly SolidColorBrush _truckSnowCloudBrush;
        private readonly SolidColorBrush _truckShadowBrush;
        private readonly SolidColorBrush _truckHubBrush;
        private readonly Pen _truckOutlinePen;
        private readonly Pen _truckChassisPen;
        private readonly Pen _truckWheelPen;
        private readonly Pen _truckPlowHighlightPen;
        private readonly StreamGeometry _truckCabGeometry;
        private readonly StreamGeometry _truckWindowGeometry;
        private readonly StreamGeometry _truckPlowGeometry;

        private double[] _pile;
        private double _spawnRemainder;
        private double _elapsed;
        private double _settleRemainder;
        private double _wind;
        private double _targetWind;
        private double _nextWindChange;
        private bool _initialSnowSeeded;
        private bool _pileGeometryDirty;
        private StreamGeometry _pileGeometry;
        private bool _snowPlowActive;
        private double _snowPlowX;
        private double _snowPlowSpeed;
        private double _nextAutomaticPlowTime;

        public SnowSurface(double floorRatio, int randomSeed)
        {
            _floorRatio = Math.Max(0.05, Math.Min(1.0, floorRatio));
            _random = new Random(randomSeed);
            _particles = new List<SnowParticle>(600);
            _pile = new double[0];
            _pileGeometryDirty = true;

            _flakeBrushes = new SolidColorBrush[5];
            _crystalPens = new Pen[5];
            for (int index = 0; index < _flakeBrushes.Length; index++)
            {
                byte alpha = (byte)(155 + index * 25);
                SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
                brush.Freeze();
                _flakeBrushes[index] = brush;

                Pen pen = new Pen(brush, index < 2 ? 0.75 : 1.0);
                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
                pen.Freeze();
                _crystalPens[index] = pen;
            }

            _pileBrush = new SolidColorBrush(Color.FromArgb(250, 255, 255, 255));
            _pileBrush.Freeze();

            _truckBodyBrush = CreateFrozenBrush(255, 239, 117, 18);
            _truckBodyLightBrush = CreateFrozenBrush(255, 255, 183, 77);
            _truckDarkBrush = CreateFrozenBrush(255, 38, 50, 56);
            _truckWindowBrush = CreateFrozenBrush(245, 179, 229, 252);
            _truckPlowBrush = CreateFrozenBrush(255, 144, 164, 174);
            _truckPlowLightBrush = CreateFrozenBrush(255, 207, 216, 220);
            _truckHeadlightBrush = CreateFrozenBrush(255, 255, 241, 118);
            _truckBeaconBrightBrush = CreateFrozenBrush(255, 255, 193, 7);
            _truckBeaconDimBrush = CreateFrozenBrush(210, 255, 143, 0);
            _truckSnowCloudBrush = CreateFrozenBrush(205, 255, 255, 255);
            _truckShadowBrush = CreateFrozenBrush(85, 0, 0, 0);
            _truckHubBrush = CreateFrozenBrush(255, 176, 190, 197);

            _truckOutlinePen = CreateFrozenPen(_truckDarkBrush, 1.5);
            _truckChassisPen = CreateFrozenPen(_truckDarkBrush, 3.0);
            _truckWheelPen = CreateFrozenPen(_truckHubBrush, 1.2);
            _truckPlowHighlightPen = CreateFrozenPen(_truckPlowLightBrush, 2.0);

            _truckCabGeometry = CreateClosedGeometry(
                new Point(91.0, 54.0),
                new Point(91.0, 21.0),
                new Point(100.0, 9.0),
                new Point(128.0, 9.0),
                new Point(145.0, 25.0),
                new Point(145.0, 54.0));
            _truckWindowGeometry = CreateClosedGeometry(
                new Point(101.0, 27.0),
                new Point(104.0, 15.0),
                new Point(126.0, 15.0),
                new Point(137.0, 27.0));
            _truckPlowGeometry = CreateClosedGeometry(
                new Point(141.0, 43.0),
                new Point(175.0, 49.0),
                new Point(164.0, 65.0),
                new Point(141.0, 59.0));

            IsHitTestVisible = false;
            SnapsToDevicePixels = false;
        }

        public int ParticleCount
        {
            get { return _particles.Count; }
        }

        public void UpdateSnow(double deltaSeconds, SnowIntensity intensity, bool paused)
        {
            if (ActualWidth < 10.0 || ActualHeight < 10.0)
            {
                return;
            }

            EnsurePileSize();

            if (paused)
            {
                return;
            }

            deltaSeconds = Math.Max(0.001, Math.Min(0.075, deltaSeconds));
            _elapsed += deltaSeconds;

            UpdateWind(deltaSeconds);

            int maximumParticles = GetMaximumParticles(intensity);
            if (!_initialSnowSeeded)
            {
                SeedInitialSnow(maximumParticles);
                _initialSnowSeeded = true;
            }

            double spawnRate = GetSpawnRate(intensity);
            _spawnRemainder += spawnRate * deltaSeconds;
            while (_spawnRemainder >= 1.0 && _particles.Count < maximumParticles)
            {
                _particles.Add(CreateParticle(false));
                _spawnRemainder -= 1.0;
            }

            double floor = GetFloor();
            for (int index = _particles.Count - 1; index >= 0; index--)
            {
                SnowParticle particle = _particles[index];
                double wave = Math.Sin(_elapsed * particle.WaveFrequency + particle.Phase) * particle.WaveSpeed;

                particle.X += (particle.HorizontalDrift + _wind + wave) * deltaSeconds;
                particle.Y += particle.VerticalSpeed * deltaSeconds;
                particle.Rotation += particle.RotationSpeed * deltaSeconds;

                if (particle.X < -12.0)
                {
                    particle.X = ActualWidth + 10.0;
                }
                else if (particle.X > ActualWidth + 12.0)
                {
                    particle.X = -10.0;
                }

                double pileHeight = GetPileHeight(particle.X);
                if (particle.Y + particle.Radius >= floor - pileHeight)
                {
                    DepositSnow(particle.X, particle.Radius);
                    _particles.RemoveAt(index);
                }
                else if (particle.Y > ActualHeight + 30.0)
                {
                    _particles.RemoveAt(index);
                }
            }

            _settleRemainder += deltaSeconds;
            if (_settleRemainder >= 0.24)
            {
                SettlePile();
                _settleRemainder = 0.0;
            }

            if (!_snowPlowActive &&
                _elapsed >= _nextAutomaticPlowTime &&
                GetPileFillRatio() >= AutomaticPlowThreshold)
            {
                StartSnowPlow();
            }

            if (_snowPlowActive)
            {
                UpdateSnowPlow(deltaSeconds);
            }

            InvalidateVisual();
        }

        public void ClearPile()
        {
            _snowPlowActive = false;
            _nextAutomaticPlowTime = _elapsed + 12.0;
            ClearPileValues();
            InvalidateVisual();
        }

        public void StartSnowPlow()
        {
            if (_snowPlowActive || ActualWidth < 10.0 || ActualHeight < 10.0)
            {
                return;
            }

            EnsurePileSize();
            _snowPlowActive = true;
            _snowPlowX = -SnowPlowWidth - 28.0;
            _snowPlowSpeed = Math.Max(
                205.0,
                Math.Min(340.0, (ActualWidth + SnowPlowWidth * 2.0) / 7.2));
            _nextAutomaticPlowTime = _elapsed + 20.0;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Never paint on the taskbar or its notification area. A small
            // sub-pixel inset also prevents anti-aliased edges from bleeding
            // over the top row of the taskbar.
            double clipBottom = Math.Max(0.0, GetFloor() - 0.35);
            RectangleGeometry workAreaClip = new RectangleGeometry(
                new Rect(0.0, 0.0, Math.Max(0.0, ActualWidth), clipBottom));
            drawingContext.PushClip(workAreaClip);

            DrawPile(drawingContext);

            for (int index = 0; index < _particles.Count; index++)
            {
                SnowParticle particle = _particles[index];
                int brushIndex = Math.Max(0, Math.Min(4, (int)Math.Round((particle.Opacity - 0.58) / 0.09)));
                Point center = new Point(particle.X, particle.Y);

                if (particle.IsCrystal)
                {
                    DrawCrystal(drawingContext, center, particle, _crystalPens[brushIndex]);
                }
                else
                {
                    drawingContext.DrawEllipse(
                        _flakeBrushes[brushIndex],
                        null,
                        center,
                        particle.Radius,
                        particle.Radius);
                }
            }

            DrawSnowPlow(drawingContext);
            drawingContext.Pop();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            EnsurePileSize();
            _pileGeometryDirty = true;
        }

        private SnowParticle CreateParticle(bool placeInsideScreen)
        {
            double sizeRandom = _random.NextDouble();
            double radius = 0.75 + sizeRandom * sizeRandom * 3.25;
            bool crystal = radius > 2.35 && _random.NextDouble() < 0.42;

            SnowParticle particle = new SnowParticle();
            particle.X = _random.NextDouble() * Math.Max(20.0, ActualWidth);
            particle.Y = placeInsideScreen
                ? -10.0 + _random.NextDouble() * Math.Max(20.0, GetFloor() - 5.0)
                : -10.0 - _random.NextDouble() * 24.0;
            particle.Radius = radius;
            particle.VerticalSpeed = 72.0 + _random.NextDouble() * 88.0 + radius * 5.0;
            particle.HorizontalDrift = -9.0 + _random.NextDouble() * 18.0;
            particle.WaveSpeed = 7.0 + _random.NextDouble() * 16.0;
            particle.WaveFrequency = 0.75 + _random.NextDouble() * 1.05;
            particle.Phase = _random.NextDouble() * Math.PI * 2.0;
            particle.Opacity = 0.64 + _random.NextDouble() * 0.36;
            particle.Rotation = _random.NextDouble() * Math.PI;
            particle.RotationSpeed = -0.7 + _random.NextDouble() * 1.4;
            particle.IsCrystal = crystal;
            return particle;
        }

        private void SeedInitialSnow(int maximumParticles)
        {
            int initialCount = Math.Max(18, (int)(maximumParticles * 0.48));
            for (int index = 0; index < initialCount; index++)
            {
                _particles.Add(CreateParticle(true));
            }
        }

        private void UpdateWind(double deltaSeconds)
        {
            if (_elapsed >= _nextWindChange)
            {
                _targetWind = -10.0 + _random.NextDouble() * 20.0;
                _nextWindChange = _elapsed + 4.0 + _random.NextDouble() * 7.0;
            }

            double blend = Math.Min(1.0, deltaSeconds * 0.45);
            _wind += (_targetWind - _wind) * blend;
        }

        private int GetMaximumParticles(SnowIntensity intensity)
        {
            double widthFactor = Math.Max(0.55, ActualWidth / 1920.0);
            int baseCount;
            switch (intensity)
            {
                case SnowIntensity.Light:
                    baseCount = 210;
                    break;
                case SnowIntensity.Heavy:
                    baseCount = 720;
                    break;
                default:
                    baseCount = 430;
                    break;
            }

            return Math.Max(90, (int)(baseCount * widthFactor));
        }

        private double GetSpawnRate(SnowIntensity intensity)
        {
            double widthFactor = Math.Max(0.55, ActualWidth / 1920.0);
            double baseRate;
            switch (intensity)
            {
                case SnowIntensity.Light:
                    baseRate = 25.0;
                    break;
                case SnowIntensity.Heavy:
                    baseRate = 91.0;
                    break;
                default:
                    baseRate = 51.0;
                    break;
            }

            return baseRate * widthFactor;
        }

        private void EnsurePileSize()
        {
            int desiredSize = Math.Max(4, (int)Math.Ceiling(Math.Max(1.0, ActualWidth) / PileCellWidth) + 1);
            if (_pile.Length == desiredSize)
            {
                return;
            }

            double[] oldPile = _pile;
            double[] resizedPile = new double[desiredSize];
            if (oldPile.Length > 1)
            {
                for (int index = 0; index < desiredSize; index++)
                {
                    double oldPosition = (double)index / Math.Max(1, desiredSize - 1) * (oldPile.Length - 1);
                    int left = Math.Min(oldPile.Length - 1, (int)Math.Floor(oldPosition));
                    int right = Math.Min(oldPile.Length - 1, left + 1);
                    double fraction = oldPosition - left;
                    resizedPile[index] = oldPile[left] * (1.0 - fraction) + oldPile[right] * fraction;
                }
            }

            _pile = resizedPile;
            _pileGeometryDirty = true;
        }

        private double GetFloor()
        {
            return Math.Max(1.0, Math.Min(ActualHeight - 1.0, ActualHeight * _floorRatio));
        }

        private double GetPileHeight(double x)
        {
            if (_pile.Length == 0)
            {
                return 0.0;
            }

            double position = Math.Max(0.0, Math.Min(_pile.Length - 1.0, x / PileCellWidth));
            int left = Math.Min(_pile.Length - 1, (int)Math.Floor(position));
            int right = Math.Min(_pile.Length - 1, left + 1);
            double fraction = position - left;
            return _pile[left] * (1.0 - fraction) + _pile[right] * fraction;
        }

        private void DepositSnow(double x, double particleRadius)
        {
            if (_pile.Length == 0)
            {
                return;
            }

            int center = Math.Max(0, Math.Min(_pile.Length - 1, (int)Math.Round(x / PileCellWidth)));
            double amount = Math.Max(0.55, particleRadius * 0.92);
            double maximumHeight = GetMaximumPileHeight();

            AddPileHeight(center, amount, maximumHeight);
            AddPileHeight(center - 1, amount * 0.38, maximumHeight);
            AddPileHeight(center + 1, amount * 0.38, maximumHeight);

            _pileGeometryDirty = true;
        }

        private void AddPileHeight(int index, double amount, double maximumHeight)
        {
            if (index < 0 || index >= _pile.Length)
            {
                return;
            }

            _pile[index] = Math.Min(maximumHeight, _pile[index] + amount);
        }

        private void SettlePile()
        {
            if (_pile.Length < 3)
            {
                return;
            }

            double[] settled = new double[_pile.Length];
            settled[0] = _pile[0] * 0.88 + _pile[1] * 0.12;
            settled[_pile.Length - 1] = _pile[_pile.Length - 1] * 0.88 + _pile[_pile.Length - 2] * 0.12;

            for (int index = 1; index < _pile.Length - 1; index++)
            {
                double neighbourAverage = (_pile[index - 1] + _pile[index + 1]) * 0.5;
                settled[index] = _pile[index] * 0.90 + neighbourAverage * 0.10;
            }

            _pile = settled;
            _pileGeometryDirty = true;
        }

        private double GetMaximumPileHeight()
        {
            return Math.Max(24.0, Math.Min(58.0, GetFloor() * 0.085));
        }

        private double GetPileFillRatio()
        {
            if (_pile.Length == 0)
            {
                return 0.0;
            }

            double total = 0.0;
            for (int index = 0; index < _pile.Length; index++)
            {
                total += _pile[index];
            }

            return total / (_pile.Length * GetMaximumPileHeight());
        }

        private void UpdateSnowPlow(double deltaSeconds)
        {
            _snowPlowX += _snowPlowSpeed * deltaSeconds;

            // The blade is at the front of the truck. Keep everything behind
            // it clear, including snow that lands while the truck is passing.
            double bladeX = _snowPlowX + 166.0;
            int clearThrough = Math.Min(_pile.Length - 1, (int)Math.Floor(bladeX / PileCellWidth));
            if (clearThrough >= 0)
            {
                for (int index = 0; index <= clearThrough; index++)
                {
                    _pile[index] = 0.0;
                }
                _pileGeometryDirty = true;
            }

            if (_snowPlowX > ActualWidth + 34.0)
            {
                _snowPlowActive = false;
                _nextAutomaticPlowTime = _elapsed + 20.0;
                ClearPileValues();
            }
        }

        private void ClearPileValues()
        {
            for (int index = 0; index < _pile.Length; index++)
            {
                _pile[index] = 0.0;
            }

            _pileGeometryDirty = true;
        }

        private void DrawPile(DrawingContext drawingContext)
        {
            if (_pile.Length < 2)
            {
                return;
            }

            if (_pileGeometryDirty || _pileGeometry == null)
            {
                if (_pileGeometry != null && _pileGeometry.IsFrozen)
                {
                    _pileGeometry = null;
                }

                double floor = GetFloor();
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(new Point(0.0, floor), true, true);
                    context.LineTo(new Point(0.0, floor - _pile[0]), true, false);

                    for (int index = 1; index < _pile.Length; index++)
                    {
                        double x = Math.Min(ActualWidth, index * PileCellWidth);
                        context.LineTo(new Point(x, floor - _pile[index]), true, true);
                    }

                    context.LineTo(new Point(ActualWidth, floor), true, false);
                }

                geometry.Freeze();
                _pileGeometry = geometry;
                _pileGeometryDirty = false;
            }

            drawingContext.DrawGeometry(_pileBrush, null, _pileGeometry);
        }

        private void DrawSnowPlow(DrawingContext drawingContext)
        {
            if (!_snowPlowActive)
            {
                return;
            }

            double top = GetFloor() - SnowPlowHeight;
            drawingContext.PushTransform(new TranslateTransform(_snowPlowX, top));

            // Soft road shadow.
            drawingContext.DrawEllipse(
                _truckShadowBrush,
                null,
                new Point(88.0, 62.0),
                78.0,
                5.0);

            // Rear hopper and its highlighted upper edge.
            drawingContext.DrawRoundedRectangle(
                _truckBodyBrush,
                _truckOutlinePen,
                new Rect(10.0, 20.0, 88.0, 35.0),
                5.0,
                5.0);
            drawingContext.DrawRoundedRectangle(
                _truckBodyLightBrush,
                null,
                new Rect(16.0, 14.0, 68.0, 12.0),
                4.0,
                4.0);
            drawingContext.DrawRectangle(
                _truckBodyLightBrush,
                null,
                new Rect(17.0, 34.0, 73.0, 5.0));

            // Cab, windshield and front lights.
            drawingContext.DrawGeometry(_truckBodyBrush, _truckOutlinePen, _truckCabGeometry);
            drawingContext.DrawGeometry(_truckWindowBrush, _truckOutlinePen, _truckWindowGeometry);
            drawingContext.DrawRoundedRectangle(
                _truckHeadlightBrush,
                null,
                new Rect(137.0, 32.0, 8.0, 7.0),
                2.0,
                2.0);
            drawingContext.DrawRectangle(
                _truckDarkBrush,
                null,
                new Rect(105.0, 33.0, 24.0, 2.0));

            // Chassis and snow blade.
            drawingContext.DrawLine(_truckChassisPen, new Point(8.0, 54.0), new Point(151.0, 54.0));
            drawingContext.DrawGeometry(_truckPlowBrush, _truckOutlinePen, _truckPlowGeometry);
            drawingContext.DrawLine(
                _truckPlowHighlightPen,
                new Point(147.0, 48.0),
                new Point(168.0, 52.0));

            DrawTruckWheel(drawingContext, new Point(43.0, 56.0));
            DrawTruckWheel(drawingContext, new Point(119.0, 56.0));

            // Flashing amber beacon.
            SolidColorBrush beaconBrush = ((int)(_elapsed * 4.0) % 2 == 0)
                ? _truckBeaconBrightBrush
                : _truckBeaconDimBrush;
            drawingContext.DrawRoundedRectangle(
                beaconBrush,
                _truckOutlinePen,
                new Rect(108.0, 2.0, 12.0, 8.0),
                4.0,
                4.0);

            // A small moving cloud makes the blade visibly push the snow.
            double puff = Math.Sin(_elapsed * 5.0) * 2.0;
            drawingContext.DrawEllipse(_truckSnowCloudBrush, null, new Point(174.0, 58.0 + puff), 8.0, 5.0);
            drawingContext.DrawEllipse(_truckSnowCloudBrush, null, new Point(183.0, 54.0 - puff), 6.0, 4.0);
            drawingContext.DrawEllipse(_truckSnowCloudBrush, null, new Point(188.0, 61.0), 5.0, 3.0);

            drawingContext.Pop();
        }

        private void DrawTruckWheel(DrawingContext drawingContext, Point center)
        {
            drawingContext.DrawEllipse(_truckDarkBrush, null, center, 11.0, 11.0);
            drawingContext.DrawEllipse(_truckHubBrush, null, center, 5.0, 5.0);

            double angle = _elapsed * 5.5;
            for (int spoke = 0; spoke < 2; spoke++)
            {
                double spokeAngle = angle + spoke * Math.PI / 2.0;
                double x = Math.Cos(spokeAngle) * 4.0;
                double y = Math.Sin(spokeAngle) * 4.0;
                drawingContext.DrawLine(
                    _truckWheelPen,
                    new Point(center.X - x, center.Y - y),
                    new Point(center.X + x, center.Y + y));
            }
        }

        private static SolidColorBrush CreateFrozenBrush(byte alpha, byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Pen CreateFrozenPen(Brush brush, double thickness)
        {
            Pen pen = new Pen(brush, thickness);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        private static StreamGeometry CreateClosedGeometry(params Point[] points)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                if (points.Length > 0)
                {
                    context.BeginFigure(points[0], true, true);
                    for (int index = 1; index < points.Length; index++)
                    {
                        context.LineTo(points[index], true, false);
                    }
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static void DrawCrystal(DrawingContext drawingContext, Point center, SnowParticle particle, Pen pen)
        {
            double length = Math.Max(2.8, particle.Radius * 1.75);
            double cosine = Math.Cos(particle.Rotation);
            double sine = Math.Sin(particle.Rotation);

            for (int arm = 0; arm < 3; arm++)
            {
                double angle = arm * Math.PI / 3.0;
                double x = Math.Cos(angle) * length;
                double y = Math.Sin(angle) * length;

                double rotatedX = x * cosine - y * sine;
                double rotatedY = x * sine + y * cosine;
                Point start = new Point(center.X - rotatedX, center.Y - rotatedY);
                Point end = new Point(center.X + rotatedX, center.Y + rotatedY);
                drawingContext.DrawLine(pen, start, end);
            }

            drawingContext.DrawEllipse(pen.Brush, null, center, 0.75, 0.75);
        }
    }
}
