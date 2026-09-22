using ACPAMS.Data;
using ACPAMS.Models;
using DlibDotNet;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ACPAMS.Services
{
    public enum SuspiciousEventType
    {
        NoFaceDetected,
        MultipleFacesDetected,
        ClipboardAttempt,
        AltTabAttempt,
        WindowFocusLost,
        GazeDiverted
    }

    public class MonitoringService : IDisposable
    {
        // ── Events ────────────────────────────────────────────────────────
        public event Action<System.Windows.Media.Imaging.BitmapSource>? FrameReady;
        public event Action<SuspiciousEventType, string>? SuspiciousActivityDetected;
        public event Action<double>? CheatingScoreUpdated;
        public event Action<string>? GazeDirectionUpdated;
        public event Action<double, bool>? EyeStatusUpdated;

        // ── OpenCV / Dlib ─────────────────────────────────────────────────
        private VideoCapture? _capture;
        private CascadeClassifier? _faceDetector;
        private ShapePredictor? _shapePredictor;
        private FrontalFaceDetector? _dlibDetector;
        private byte[]? _frameBuffer;

        // ── Threading ─────────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private readonly Dispatcher _dispatcher;

        // ── State ─────────────────────────────────────────────────────────
        private Guid _attemptId;
        private int _noFaceFrameCount = 0;
        private int _multiFaceFrameCount = 0;
        private static int FRAME_THRESHOLD => AppSettingsService.NoFaceFrameThreshold;
        private static double YAW_THRESHOLD => AppSettingsService.HeadYawThreshold;
        private static double PITCH_THRESHOLD => AppSettingsService.HeadPitchThreshold;
        private const double EAR_THRESHOLD = 0.20;
        private const int GAZE_DIVERTED_THRESHOLD = 45;
        private int _gazeDivertedFrameCount = 0;

        // ── Cheating Score System ─────────────────────────────────────────
        private double _cheatingScore = 0.0;
        private const double MAX_SCORE = 100.0;
        private static double SCORE_DECAY_RATE => AppSettingsService.ScoreDecayRate;
        private const double BASE_SCORE_INCREMENT = 0.5;
        private const double EXPONENT_BASE = 1.08;

        private static int WIGGLE_ROOM_FRAMES => AppSettingsService.WiggleRoomFrames;
        private int _lookingAwayFrameCount = 0;
        private int _lookingAtScreenFrameCount = 0;
        private bool _inPenaltyZone = false;
        private int _penaltyFrameCount = 0;

        private readonly Dictionary<SuspiciousEventType, DateTime> _lastAlertTime = new();
        private static int ALERT_COOLDOWN_SECONDS => AppSettingsService.AlertCooldownSeconds;

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
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch (ObjectDisposedException) { }

            try
            {
                _captureTask?.Wait(2000);
            }
            catch { }
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

            // ── Head pose & eye tracking (only if exactly one face) ───────────
            if (faces.Length == 1)
            {
                try
                {
                    bool headOrGazeDiverted = AnalyzePoseAndEyes(frame, gray, display, faces[0]);
                    if (headOrGazeDiverted) isLookingAway = true;
                }
                catch (Exception ex)
                {
                    isLookingAway = true;
                    System.Diagnostics.Debug.WriteLine($"Head/eye analysis error: {ex.Message}");
                }
            }
            else
            {
                _gazeDivertedFrameCount = 0;
                _dispatcher.BeginInvoke(() =>
                {
                    GazeDirectionUpdated?.Invoke("Unavailable");
                    EyeStatusUpdated?.Invoke(0.0, false);
                });
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

        private bool AnalyzePoseAndEyes(Mat frame, Mat gray, Mat display, OpenCvSharp.Rect face)
        {
            int left = Math.Clamp(face.Left, 0, frame.Cols - 1);
            int top = Math.Clamp(face.Top, 0, frame.Rows - 1);
            int right = Math.Clamp(face.Right, left + 1, frame.Cols - 1);
            int bottom = Math.Clamp(face.Bottom, top + 1, frame.Rows - 1);
            var dlibRect = new DlibDotNet.Rectangle(left, top, right, bottom);

            int stride = (int)frame.Step();
            int size = frame.Rows * stride;
            if (_frameBuffer == null || _frameBuffer.Length != size)
                _frameBuffer = new byte[size];

            Marshal.Copy(frame.Data, _frameBuffer, 0, size);

            // In-memory Dlib conversion via pinned byte buffer — zero disk I/O, no unpinned native pointer faults
            using var dlibImg = Dlib.LoadImageData<BgrPixel>(_frameBuffer, (uint)frame.Rows, (uint)frame.Cols, (uint)stride);
            using var shape = _shapePredictor!.Detect(dlibImg, dlibRect);

            // 1. Head Pose Estimation
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

            bool headTurned = (yaw > YAW_THRESHOLD || yaw < -YAW_THRESHOLD || pitch < -PITCH_THRESHOLD);

            // 2. Eye Aspect Ratio (EAR)
            double leftEar = CalculateEyeAspectRatio(shape, 36, 37, 38, 39, 40, 41);
            double rightEar = CalculateEyeAspectRatio(shape, 42, 43, 44, 45, 46, 47);
            double ear = (leftEar + rightEar) / 2.0;
            bool eyesOpen = ear >= EAR_THRESHOLD;

            // 3. Gaze Direction
            string gazeDirection = eyesOpen
                ? DetectGazeDirection(gray, shape, display)
                : "Closed";

            _dispatcher.BeginInvoke(() =>
            {
                EyeStatusUpdated?.Invoke(ear, eyesOpen);
                GazeDirectionUpdated?.Invoke(gazeDirection);
            });

            // 4. Gaze Diverted Alert
            bool gazeDiverted = eyesOpen && (gazeDirection == "Left" || gazeDirection == "Right");
            if (gazeDiverted)
            {
                _gazeDivertedFrameCount++;
                if (_gazeDivertedFrameCount == GAZE_DIVERTED_THRESHOLD)
                {
                    RaiseSuspiciousEvent(SuspiciousEventType.GazeDiverted,
                        $"Gaze diverted ({gazeDirection}) away from screen.");
                }
            }
            else
            {
                _gazeDivertedFrameCount = 0;
            }

            // Display overlay
            Cv2.PutText(display, $"Yaw: {yaw:F1}", new OpenCvSharp.Point(10, 25),
                HersheyFonts.HersheySimplex, 0.45, Scalar.Yellow, 1);
            Cv2.PutText(display, $"Pitch: {pitch:F1}", new OpenCvSharp.Point(10, 45),
                HersheyFonts.HersheySimplex, 0.45, Scalar.Yellow, 1);
            Cv2.PutText(display, $"Gaze: {gazeDirection}", new OpenCvSharp.Point(10, 65),
                HersheyFonts.HersheySimplex, 0.45, gazeDiverted ? Scalar.Red : Scalar.Cyan, 1);
            Cv2.PutText(display, $"EAR: {ear:F2} ({(eyesOpen ? "Open" : "Closed")})", new OpenCvSharp.Point(10, 85),
                HersheyFonts.HersheySimplex, 0.45, eyesOpen ? Scalar.LimeGreen : Scalar.Orange, 1);

            return headTurned || gazeDiverted;
        }

        private static double Distance(DlibDotNet.Point p1, DlibDotNet.Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double CalculateEyeAspectRatio(FullObjectDetection shape, int p1, int p2, int p3, int p4, int p5, int p6)
        {
            double vertical1 = Distance(shape.GetPart((uint)p2), shape.GetPart((uint)p6));
            double vertical2 = Distance(shape.GetPart((uint)p3), shape.GetPart((uint)p5));
            double horizontal = Distance(shape.GetPart((uint)p1), shape.GetPart((uint)p4));
            return horizontal > 0 ? (vertical1 + vertical2) / (2.0 * horizontal) : 0.0;
        }

        private static string DetectGazeDirection(Mat gray, FullObjectDetection shape, Mat display)
        {
            var leftEyeBox = GetEyeBoundingBox(shape, 36, 41, gray.Cols, gray.Rows);
            var rightEyeBox = GetEyeBoundingBox(shape, 42, 47, gray.Cols, gray.Rows);

            double leftRatio = GetPupilHorizontalRatio(gray, leftEyeBox);
            double rightRatio = GetPupilHorizontalRatio(gray, rightEyeBox);

            // Draw eye bounding boxes on preview
            Cv2.Rectangle(display, leftEyeBox, Scalar.Cyan, 1);
            Cv2.Rectangle(display, rightEyeBox, Scalar.Cyan, 1);

            double avgRatio = (leftRatio + rightRatio) / 2.0;

            if (avgRatio < 0.38)
                return "Left";
            if (avgRatio > 0.62)
                return "Right";

            return "Center";
        }

        private static OpenCvSharp.Rect GetEyeBoundingBox(FullObjectDetection shape, int startIdx, int endIdx, int imgWidth, int imgHeight)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = startIdx; i <= endIdx; i++)
            {
                var pt = shape.GetPart((uint)i);
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            int padX = Math.Max(2, (maxX - minX) / 8);
            int padY = Math.Max(2, (maxY - minY) / 6);

            int x = Math.Clamp(minX - padX, 0, imgWidth - 1);
            int y = Math.Clamp(minY - padY, 0, imgHeight - 1);
            int w = Math.Clamp(maxX - minX + padX * 2, 1, imgWidth - x);
            int h = Math.Clamp(maxY - minY + padY * 2, 1, imgHeight - y);

            return new OpenCvSharp.Rect(x, y, w, h);
        }

        private static double GetPupilHorizontalRatio(Mat gray, OpenCvSharp.Rect eyeBox)
        {
            if (eyeBox.Width < 5 || eyeBox.Height < 5 ||
                eyeBox.X < 0 || eyeBox.Y < 0 ||
                eyeBox.Right > gray.Cols || eyeBox.Bottom > gray.Rows)
                return 0.5;

            using var eyeROI = new Mat(gray, eyeBox);
            using var blurred = new Mat();
            Cv2.GaussianBlur(eyeROI, blurred, new OpenCvSharp.Size(7, 7), 0);

            Cv2.MinMaxLoc(blurred, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out _);

            using var thresh = new Mat();
            Cv2.Threshold(blurred, thresh, minVal + (maxVal - minVal) * 0.25, 255, ThresholdTypes.BinaryInv);

            var moments = Cv2.Moments(thresh, true);
            double pupilX = moments.M00 > 0 ? (moments.M10 / moments.M00) : minLoc.X;

            return Math.Clamp(pupilX / eyeBox.Width, 0.0, 1.0);
        }

        private void RaiseSuspiciousEvent(SuspiciousEventType type, string description)
        {
            if (_lastAlertTime.TryGetValue(type, out var lastTime))
            {
                if ((DateTime.Now - lastTime).TotalSeconds < ALERT_COOLDOWN_SECONDS)
                    return;
            }
            _lastAlertTime[type] = DateTime.Now;

            _dispatcher.BeginInvoke(() =>
            {
                SuspiciousActivityDetected?.Invoke(type, description);
            });

            _ = Task.Run(async () =>
            {
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
            _capture = null;
            _faceDetector?.Dispose();
            _faceDetector = null;
            _shapePredictor?.Dispose();
            _shapePredictor = null;
            _dlibDetector?.Dispose();
            _dlibDetector = null;
            _cts?.Dispose();
            _cts = null;
        }

        // ─── Get Active Students ──────────────────────────────────────
        public async Task<List<ActiveStudentDto>> GetActiveStudentsAsync(Guid? examId = null)
        {
            using var context = new AppDbContext();
            var currentInstructorId = SessionManager.CurrentInstructorId;
            var query = context.StudentExamAttempts
                .Include(a => a.Student)
                .Include(a => a.Exam)
                .Where(a => a.Status == "in_progress" || a.Status == "submitted");

            if (examId.HasValue)
            {
                query = query.Where(a => a.ExamId == examId);
            }
            else if (!string.IsNullOrEmpty(currentInstructorId))
            {
                query = query.Where(a => a.Exam != null && a.Exam.InstructorId == currentInstructorId);
            }

            var attempts = await query
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();

            var result = new List<ActiveStudentDto>();

            foreach (var attempt in attempts)
            {
                // Get recent flags for this attempt (excluding head movement events)
                var recentFlags = await context.AuditLogs
                    .Where(l => l.EntityId == attempt.AttemptId.ToString()
                                && l.EntityType == "StudentExamAttempt"
                                && l.CreatedAt >= DateTime.Now.AddMinutes(-5)
                                && !l.Action.Contains("HeadTurned")
                                && !l.Action.Contains("HeadTilted"))
                    .ToListAsync();

                // Get all flags count (excluding head movement events)
                var totalFlags = await context.AuditLogs
                    .CountAsync(l => l.EntityId == attempt.AttemptId.ToString()
                                     && l.EntityType == "StudentExamAttempt"
                                     && !l.Action.Contains("HeadTurned")
                                     && !l.Action.Contains("HeadTilted"));

                // Determine risk level
                var riskLevel = GetRiskLevel(totalFlags, recentFlags);

                result.Add(new ActiveStudentDto
                {
                    AttemptId = attempt.AttemptId,
                    StudentName = attempt.Student != null
                        ? $"{attempt.Student.FirstName} {attempt.Student.LastName}"
                        : "Unknown Student",
                    StudentInitials = attempt.Student != null
                        ? $"{attempt.Student.FirstName[0]}.{attempt.Student.LastName[0]}"
                        : "??",
                    DeviceName = !string.IsNullOrWhiteSpace(attempt.DeviceInfo) ? attempt.DeviceInfo : (attempt.IpAddress ?? "N/A"),
                    Status = attempt.Status ?? "in_progress",
                    StartedAt = attempt.StartedAt ?? DateTime.Now,
                    RiskLevel = riskLevel,
                    FlagCount = totalFlags,
                    RecentFlags = recentFlags.Take(3).Select(l => new FlagDto
                    {
                        Action = l.Action ?? "Unknown",
                        Details = l.Details ?? "",
                        CreatedAt = l.CreatedAt
                    }).ToList()
                });
            }

            return result;
        }

        // ─── Get Recent Flags ──────────────────────────────────────────
        public async Task<List<AuditLog>> GetRecentFlagsAsync(int minutes = 5)
        {
            using var context = new AppDbContext();
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return await context.AuditLogs
                .Include(l => l.User)
                .Where(l => l.EntityType == "StudentExamAttempt"
                            && l.CreatedAt >= cutoff
                            && (l.Action.Contains("NoFaceDetected") ||
                                l.Action.Contains("MultipleFaces") ||
                                l.Action.Contains("Clipboard") ||
                                l.Action.Contains("AltTab") ||
                                l.Action.Contains("WindowFocus")))
                .OrderByDescending(l => l.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        // ─── Issue Warning ─────────────────────────────────────────────
        public async Task<AuditLog> IssueWarningAsync(Guid attemptId, string warningType, string details, Guid? userId = null)
        {
            using var context = new AppDbContext();

            var log = new AuditLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                Action = $"Warning_{warningType}",
                EntityType = "StudentExamAttempt",
                EntityId = attemptId.ToString(),
                Details = $"{details} | Issued at: {DateTime.Now:HH:mm:ss}",
                CreatedAt = DateTime.Now
            };

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();

            return log;
        }

        // ─── Get Statistics ────────────────────────────────────────────
        public async Task<MonitoringStatsDto> GetStatisticsAsync(Guid? examId = null)
        {
            using var context = new AppDbContext();
            var currentInstructorId = SessionManager.CurrentInstructorId;
            var query = context.StudentExamAttempts.Include(a => a.Exam).Where(a => a.Status == "in_progress");
            if (examId.HasValue)
            {
                query = query.Where(a => a.ExamId == examId);
            }
            else if (!string.IsNullOrEmpty(currentInstructorId))
            {
                query = query.Where(a => a.Exam != null && a.Exam.InstructorId == currentInstructorId);
            }

            var activeCount = await query.CountAsync();
            var activeAttemptIds = await query.Select(a => a.AttemptId.ToString()).ToListAsync();

            var flagCount = await context.AuditLogs
                .Where(l => l.EntityType == "StudentExamAttempt"
                            && (activeAttemptIds.Count > 0 ? activeAttemptIds.Contains(l.EntityId) : false)
                            && l.CreatedAt >= DateTime.Now.AddMinutes(-30)
                            && !l.Action.Contains("HeadTurned")
                            && !l.Action.Contains("HeadTilted"))
                .CountAsync();

            var highRiskCount = await context.AuditLogs
                .Where(l => l.EntityType == "StudentExamAttempt"
                            && (activeAttemptIds.Count > 0 ? activeAttemptIds.Contains(l.EntityId) : false)
                            && (l.Action.Contains("NoFaceDetected") ||
                                l.Action.Contains("AltTab") ||
                                l.Action.Contains("WindowFocus"))
                            && l.CreatedAt >= DateTime.Now.AddMinutes(-30))
                .CountAsync();

            return new MonitoringStatsDto
            {
                ActiveStudents = activeCount,
                TotalFlags = flagCount,
                HighRiskFlags = highRiskCount
            };
        }

        // ─── Helper Methods ────────────────────────────────────────────
        private string GetRiskLevel(int totalFlags, List<AuditLog> recentFlags)
        {
            if (totalFlags >= 5 || recentFlags.Count >= 3)
                return "high";
            if (totalFlags >= 3 || recentFlags.Count >= 2)
                return "medium";
            return "low";
        }

        // ─── DTO Classes ──────────────────────────────────────────────
        public class ActiveStudentDto
        {
            public Guid AttemptId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string StudentInitials { get; set; } = string.Empty;
            public string DeviceName { get; set; } = string.Empty;
            public string HostName => DeviceName;
            public string Station { get => DeviceName; set => DeviceName = value; }
            public string Status { get; set; } = "in_progress";
            public DateTime StartedAt { get; set; }
            public string RiskLevel { get; set; } = "low";
            public int FlagCount { get; set; }
            public bool IsSelected { get; set; } = false;
            public List<FlagDto> RecentFlags { get; set; } = new();

        }

        public class FlagDto
        {
            public string Action { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public class MonitoringStatsDto
        {
            public int ActiveStudents { get; set; }
            public int TotalFlags { get; set; }
            public int HighRiskFlags { get; set; }
        }
    }
}