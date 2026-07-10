using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ACPAMS.Data;
using ACPAMS.Models;
using DlibDotNet;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ACPAMS.Services
{
    public enum SuspiciousEventType
    {
        NoFaceDetected,
        MultipleFacesDetected,
        HeadTurnedLeft,
        HeadTurnedRight,
        HeadTiltedDown,
        ClipboardAttempt,
        AltTabAttempt,
        WindowFocusLost
    }

    public class MonitoringService : IDisposable
    {
        // ── Events ────────────────────────────────────────────────────────
        public event Action<System.Windows.Media.Imaging.BitmapSource>? FrameReady;
        public event Action<SuspiciousEventType, string>? SuspiciousActivityDetected;
        public event Action<double>? CheatingScoreUpdated;

        // ── OpenCV / Dlib ─────────────────────────────────────────────────
        private VideoCapture? _capture;
        private CascadeClassifier? _faceDetector;
        private ShapePredictor? _shapePredictor;
        private FrontalFaceDetector? _dlibDetector;

        // ── Threading ─────────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private readonly Dispatcher _dispatcher;

        // ── State ─────────────────────────────────────────────────────────
        private Guid _attemptId;
        private int _noFaceFrameCount = 0;
        private int _multiFaceFrameCount = 0;
        private const int FRAME_THRESHOLD = 45;
        private const double YAW_THRESHOLD = 25.0;
        private const double PITCH_THRESHOLD = 20.0;

        // ── Cheating Score System ─────────────────────────────────────────
        private double _cheatingScore = 0.0;
        private const double MAX_SCORE = 100.0;
        private const double SCORE_DECAY_RATE = 0.02;
        private const double BASE_SCORE_INCREMENT = 0.5;
        private const double EXPONENT_BASE = 1.08;

        private const int WIGGLE_ROOM_FRAMES = 60;
        private int _lookingAwayFrameCount = 0;
        private int _lookingAtScreenFrameCount = 0;
        private bool _inPenaltyZone = false;
        private int _penaltyFrameCount = 0;

        private readonly Dictionary<SuspiciousEventType, DateTime> _lastAlertTime = new();
        private const int ALERT_COOLDOWN_SECONDS = 10;

        // ── 3D model points for head pose estimation ──────────────────────
        private readonly double[,] _modelPoints = new double[,]
        {
            { 0.0,    0.0,    0.0   },
            { 0.0,  -330.0, -65.0  },
            { -225.0, 170.0, -135.0 },
            { 225.0,  170.0, -135.0 },
            { -150.0,-150.0, -125.0 },
            { 150.0, -150.0, -125.0 }
        };

        public MonitoringService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public bool Initialize(Guid attemptId)
        {
            _attemptId = attemptId;

            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var cascadePath = Path.Combine(baseDir, "Resources", "Models",
                    "haarcascade_frontalface_default.xml");
                var predictorPath = Path.Combine(baseDir, "Resources", "Models",
                    "shape_predictor_68_face_landmarks.dat");

                System.Diagnostics.Debug.WriteLine($"Looking for cascade at: {cascadePath}");
                System.Diagnostics.Debug.WriteLine($"Cascade exists: {File.Exists(cascadePath)}");
                System.Diagnostics.Debug.WriteLine($"Predictor exists: {File.Exists(predictorPath)}");

                if (!File.Exists(cascadePath) || !File.Exists(predictorPath))
                {
                    System.Diagnostics.Debug.WriteLine("Model files not found.");
                    return false;
                }

                _faceDetector = new CascadeClassifier(cascadePath);
                _dlibDetector = Dlib.GetFrontalFaceDetector();
                _shapePredictor = ShapePredictor.Deserialize(predictorPath);
                _capture = new VideoCapture(0);

                if (!_capture.IsOpened())
                {
                    _capture = new VideoCapture(1);
                    if (!_capture.IsOpened())
                    {
                        System.Diagnostics.Debug.WriteLine("Webcam not available.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MonitoringService init error: {ex.Message}");
                return false;
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _captureTask?.Wait(2000);
        }

        private void CaptureLoop(CancellationToken token)
        {
            using var frame = new Mat();

            while (!token.IsCancellationRequested)
            {
                if (_capture == null || !_capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(33);
                    continue;
                }

                using var display = frame.Clone();
                AnalyzeFrame(frame, display);

                try
                {
                    using var sysBitmap = display.ToBitmap();
                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        sysBitmap.GetHbitmap(),
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    bitmapSource.Freeze();
                    _dispatcher.BeginInvoke(() => FrameReady?.Invoke(bitmapSource));
                }
                catch { }

                Thread.Sleep(33);
            }
        }

        private void AnalyzeFrame(Mat frame, Mat display)
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            var faces = _faceDetector!.DetectMultiScale(
                gray, scaleFactor: 1.1, minNeighbors: 3,
                minSize: new OpenCvSharp.Size(60, 60));

            bool isLookingAway = false;

            // ── No face ───────────────────────────────────────────────────────
            if (faces.Length == 0)
            {
                isLookingAway = true;
                _noFaceFrameCount++;
                if (_noFaceFrameCount == FRAME_THRESHOLD)
                    RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected,
                        "No face detected — student may have looked away.");
            }
            else
            {
                _noFaceFrameCount = 0;
            }

            // ── Multiple faces ────────────────────────────────────────────────
            if (faces.Length > 1)
            {
                isLookingAway = true;
                _multiFaceFrameCount++;
                if (_multiFaceFrameCount == FRAME_THRESHOLD)
                    RaiseSuspiciousEvent(SuspiciousEventType.MultipleFacesDetected,
                        $"{faces.Length} faces detected — possible unauthorized assistance.");
            }
            else
            {
                _multiFaceFrameCount = 0;
            }

            // ── Head pose (only if exactly one face) ──────────────────────────
            if (faces.Length == 1)
            {
                try
                {
                    bool headTurned = EstimateHeadPose(frame, display, faces[0]);
                    if (headTurned) isLookingAway = true;
                }
                catch (Exception ex)
                {
                    // Side profile — treat as looking away
                    isLookingAway = true;
                    System.Diagnostics.Debug.WriteLine($"Head pose error: {ex.Message}");
                }
            }

            // ── Cheating score update ─────────────────────────────────────────
            UpdateCheatingScore(isLookingAway);

            // Draw face rectangles
            foreach (var face in faces)
                Cv2.Rectangle(display, face, Scalar.LimeGreen, 2);

            // Draw score on display
            Cv2.PutText(display, $"Score: {_cheatingScore:F1}",
                new OpenCvSharp.Point(10, display.Rows - 10),
                HersheyFonts.HersheySimplex, 0.5, Scalar.Red, 1);
        }

        private void UpdateCheatingScore(bool isLookingAway)
        {
            if (isLookingAway)
            {
                _lookingAtScreenFrameCount = 0;
                _lookingAwayFrameCount++;

                if (_lookingAwayFrameCount > WIGGLE_ROOM_FRAMES)
                {
                    _inPenaltyZone = true;
                    _penaltyFrameCount++;  // keeps accumulating even if briefly interrupted

                    double increment = BASE_SCORE_INCREMENT *
                        Math.Pow(EXPONENT_BASE, _penaltyFrameCount / 30.0);

                    _cheatingScore = Math.Min(_cheatingScore + increment, MAX_SCORE);

                    if (_cheatingScore >= 30 && _cheatingScore - increment < 30)
                        RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected,
                            $"Sustained suspicious behavior. Score: {_cheatingScore:F1}");
                    else if (_cheatingScore >= 60 && _cheatingScore - increment < 60)
                        RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected,
                            $"High cheating risk detected. Score: {_cheatingScore:F1}");
                    else if (_cheatingScore >= 90 && _cheatingScore - increment < 90)
                        RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected,
                            $"Critical cheating risk. Score: {_cheatingScore:F1}");
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Away frame: {_lookingAwayFrameCount}, penalty: {_penaltyFrameCount}, score: {_cheatingScore:F2}");
            }
            else
            {
                _lookingAwayFrameCount = 0;
                // DON'T reset _penaltyFrameCount here — only reset after sustained looking at screen
                _lookingAtScreenFrameCount++;

                // Only reset penalty and decay after looking at screen for 1 second (30 frames)
                if (_lookingAtScreenFrameCount > 30)
                {
                    _inPenaltyZone = false;
                    _penaltyFrameCount = 0;  // now reset penalty after confirmed looking at screen
                    if (_cheatingScore > 0)
                        _cheatingScore = Math.Max(0, _cheatingScore - SCORE_DECAY_RATE);
                }
            }

            // Notify UI every 10 frames
            int totalFrames = _lookingAwayFrameCount + _lookingAtScreenFrameCount;
            if (totalFrames % 10 == 0)
                _dispatcher.BeginInvoke(() => CheatingScoreUpdated?.Invoke(_cheatingScore));
        }
        private bool EstimateHeadPose(Mat frame, Mat display, OpenCvSharp.Rect face)
        {
            var dlibRect = new DlibDotNet.Rectangle(face.Left, face.Top, face.Right, face.Bottom);

            var tempPath = Path.Combine(Path.GetTempPath(), "acpams_frame.jpg");
            Cv2.ImWrite(tempPath, frame);

            using var dlibImg = Dlib.LoadImage<BgrPixel>(tempPath);
            var shape = _shapePredictor!.Detect(dlibImg, dlibRect);

            var imagePoints = new double[,]
            {
                { shape.GetPart(30).X, shape.GetPart(30).Y },
                { shape.GetPart(8).X,  shape.GetPart(8).Y  },
                { shape.GetPart(36).X, shape.GetPart(36).Y },
                { shape.GetPart(45).X, shape.GetPart(45).Y },
                { shape.GetPart(48).X, shape.GetPart(48).Y },
                { shape.GetPart(54).X, shape.GetPart(54).Y }
            };

            double focalLength = frame.Cols;
            var cameraMatrix = new double[,]
            {
                { focalLength, 0,           frame.Cols / 2.0 },
                { 0,           focalLength, frame.Rows / 2.0 },
                { 0,           0,           1.0              }
            };
            var distCoeffs = new double[] { 0, 0, 0, 0 };

            using var modelPts = Mat.FromArray(_modelPoints);
            using var imgPts = Mat.FromArray(imagePoints);
            using var camMat = Mat.FromArray(cameraMatrix);
            using var dist = Mat.FromArray(distCoeffs);
            using var rvec = new Mat();
            using var tvec = new Mat();

            Cv2.SolvePnP(modelPts, imgPts, camMat, dist, rvec, tvec);

            using var rmat = new Mat();
            Cv2.Rodrigues(rvec, rmat);

            var yaw = Math.Atan2(rmat.Get<double>(1, 0), rmat.Get<double>(0, 0)) * 180 / Math.PI;
            var pitch = Math.Atan2(-rmat.Get<double>(2, 0),
                            Math.Sqrt(rmat.Get<double>(2, 1) * rmat.Get<double>(2, 1) +
                                      rmat.Get<double>(2, 2) * rmat.Get<double>(2, 2))) * 180 / Math.PI;

            Cv2.PutText(display, $"Yaw: {yaw:F1}", new OpenCvSharp.Point(10, 30),
                HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 2);
            Cv2.PutText(display, $"Pitch: {pitch:F1}", new OpenCvSharp.Point(10, 55),
                HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 2);

            bool headTurned = false;

            if (yaw > YAW_THRESHOLD)
            {
                RaiseSuspiciousEvent(SuspiciousEventType.HeadTurnedRight,
                    $"Head turned right ({yaw:F1}°)");
                headTurned = true;
            }
            else if (yaw < -YAW_THRESHOLD)
            {
                RaiseSuspiciousEvent(SuspiciousEventType.HeadTurnedLeft,
                    $"Head turned left ({yaw:F1}°)");
                headTurned = true;
            }

            if (pitch < -PITCH_THRESHOLD)
            {
                RaiseSuspiciousEvent(SuspiciousEventType.HeadTiltedDown,
                    $"Head tilted down ({pitch:F1}°)");
                headTurned = true;
            }

            return headTurned;
        }

        private void RaiseSuspiciousEvent(SuspiciousEventType type, string description)
        {
            if (_lastAlertTime.TryGetValue(type, out var lastTime))
            {
                if ((DateTime.Now - lastTime).TotalSeconds < ALERT_COOLDOWN_SECONDS)
                    return;
            }
            _lastAlertTime[type] = DateTime.Now;

            _dispatcher.BeginInvoke(async () =>
            {
                SuspiciousActivityDetected?.Invoke(type, description);
                await LogToDatabase(type, description);
            });
        }

        private async Task LogToDatabase(SuspiciousEventType type, string description)
        {
            try
            {
                using var context = new AppDbContext();
                context.AuditLogs.Add(new AuditLog
                {
                    LogId = Guid.NewGuid(),
                    UserId = SessionManager.CurrentUser?.UserId,
                    Action = type.ToString(),
                    EntityType = "StudentExamAttempt",
                    EntityId = _attemptId.ToString(),
                    Details = description,
                    IpAddress = "localhost",
                    CreatedAt = DateTime.Now
                });
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audit log error: {ex.Message}");
            }
        }

        public void OnClipboardAttempt()
        {
            RaiseSuspiciousEvent(SuspiciousEventType.ClipboardAttempt,
                "Clipboard paste attempted during exam.");
        }

        public void OnAltTabAttempt()
        {
            RaiseSuspiciousEvent(SuspiciousEventType.AltTabAttempt,
                "Alt+Tab attempted during exam.");
        }

        public void OnWindowFocusLost()
        {
            RaiseSuspiciousEvent(SuspiciousEventType.WindowFocusLost,
                "Exam window lost focus.");
        }

        public void Dispose()
        {
            Stop();
            _capture?.Dispose();
            _faceDetector?.Dispose();
            _shapePredictor?.Dispose();
            _dlibDetector?.Dispose();
            _cts?.Dispose();
        }
    }
}