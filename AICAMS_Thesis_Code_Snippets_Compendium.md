# AI-CAMS: Master Compendium of Technical Implementation & Source Code Snippets
**System Title**: AI-assisted Centralized Assessment Monitoring System (AI-CAMS)  
**Authors**: ARNJ Tech (Alexius Seth, Rodrigo, Nathaniel, Jairus)  
**Purpose**: Reference document for Chapter 3 (Methodology & Architecture) and Chapter 4 (System Implementation)

---

## Table of Contents
1. [Module 1: Computer Vision & Biometric Integrity Pipeline](#module-1-computer-vision--biometric-integrity-pipeline)
   - 1.1 Temporal Cheating Risk Scoring & Clean Behavior Decay Algorithm (`MonitoringService.cs`)
   - 1.2 3D Head Pose Estimation via Perspective-n-Point (`SolvePnP`) (`MonitoringService.cs`)
2. [Module 2: Kiosk Lockdown & Low-Level OS Interception](#module-2-kiosk-lockdown--low-level-os-interception)
   - 2.1 Win32 Low-Level Keyboard Hook (`WH_KEYBOARD_LL`) (`StudentExamPage.xaml.cs`)
   - 2.2 Emergency Physical Proctor Bypass (`Ctrl + Shift + F12`) (`MainWindow.xaml.cs`)
3. [Module 3: Dynamic Assessment Delivery & Student Interface](#module-3-dynamic-assessment-delivery--student-interface)
   - 3.1 Polymorphic UI Canvas Routing by Question Modality (`StudentExamPage.xaml.cs`)
   - 3.2 Deterministic Attempt-Seeded Question Randomization (`ExamService.cs`)
   - 3.3 Fault-Tolerant Session Resumption & Absolute Deadline Calculation (`StudentExamPage.xaml.cs`)
4. [Module 4: Isolated Child-Process Execution Sandbox](#module-4-isolated-child-process-execution-sandbox)
   - 4.1 Process Sandbox with I/O Redirection & 5-Second Hard Timeout (`CodeRunnerService.cs`)
5. [Module 5: Automated Evaluation & Generative AI Rubric Engine](#module-5-automated-evaluation--generative-ai-rubric-engine)
   - 5.1 Deterministic Objective Auto-Grading & Option Resolution (`AutoCheckerService.cs`)
   - 5.2 Two-Phase Score Aggregation & Attempt Total Synchronization (`AutoCheckerService.cs`)
   - 5.3 Google Gemini REST Integration & Structured JSON Schema Parsing (`GeminiRubricGradingService.cs`)
6. [Module 6: Live Faculty Surveillance & Bi-Directional LAN Control](#module-6-live-faculty-surveillance--bi-directional-lan-control)
   - 6.1 Real-Time Examinee Grid Aggregation with Priority Risk Ranking (`MonitoringTab.xaml.cs`)
   - 6.2 Remote Kiosk Interventions: Warnings & Dynamic Time Extensions (`StudentExamPage.xaml.cs`)
7. [Module 7: Security, Cryptography & Institutional Reporting](#module-7-security-cryptography--institutional-reporting)
   - 7.1 Salted BCrypt Hashing & Constant-Time Verification (`AuthService.cs`)
   - 7.2 Vector PDF Institutional Report Synthesis via QuestPDF (`PdfExportService.cs`)

---

## Module 1: Computer Vision & Biometric Integrity Pipeline

### 1.1 Temporal Cheating Risk Scoring & Clean Behavior Decay Algorithm
- **File**: `AICAMS/Services/MonitoringService.cs`
- **Description**: Implements a continuous temporal scoring model. Rather than triggering binary alerts on momentary glances, it uses a grace period (`WIGGLE_ROOM_FRAMES`), an exponential penalty curve for sustained infractions, and a restorative decay reward for verified compliant behavior.

```csharp
private void UpdateCheatingScore(bool isLookingAway)
{
    var now = DateTime.UtcNow;

    if (isLookingAway)
    {
        _lastAnomalyTime = now;
        _lookingAtScreenFrameCount = 0;
        _lookingAwayFrameCount++;

        // Grace period (Wiggle room) before penalizing natural eye shifts
        if (_lookingAwayFrameCount > WIGGLE_ROOM_FRAMES)
        {
            _inPenaltyZone = true;
            _penaltyFrameCount++;

            // Exponential penalty escalation capped to prevent abrupt spikes
            double increment = Math.Min(BASE_SCORE_INCREMENT * Math.Pow(EXPONENT_BASE, _penaltyFrameCount / 30.0), 3.0);
            _cheatingScore = Math.Min(_cheatingScore + increment, MAX_SCORE);

            // Progressive threshold notifications (Suspicious -> High -> Critical)
            if (_cheatingScore >= 30 && _cheatingScore - increment < 30)
                RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected, $"Sustained suspicious behavior. Score: {_cheatingScore:F1}");
            else if (_cheatingScore >= 60 && _cheatingScore - increment < 60)
                RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected, $"High cheating risk detected. Score: {_cheatingScore:F1}");
            else if (_cheatingScore >= 90 && _cheatingScore - increment < 90)
                RaiseSuspiciousEvent(SuspiciousEventType.NoFaceDetected, $"Critical cheating risk. Score: {_cheatingScore:F1}");
        }
    }
    else
    {
        _lookingAwayFrameCount = 0;
        _lookingAtScreenFrameCount++;

        // Reset penalty accumulator only after sustained screen focus (> 1 second at 30 FPS)
        if (_lookingAtScreenFrameCount > 30)
        {
            _inPenaltyZone = false;
            _penaltyFrameCount = 0;
            if (_cheatingScore > 0)
                _cheatingScore = Math.Max(0, _cheatingScore - SCORE_DECAY_RATE);
        }

        // Clean Behavior Decay: Every 60 consecutive seconds of clean behavior decrements risk score by 2
        if ((now - _lastAnomalyTime).TotalSeconds >= 60 && (now - _lastCleanDecayTime).TotalSeconds >= 60)
        {
            _cheatingScore = Math.Max(0.0, _cheatingScore - 2.0);
            _lastCleanDecayTime = now;
        }
    }

    // Broadcast updated score to UI telemetry indicators every 10 frames
    int totalFrames = _lookingAwayFrameCount + _lookingAtScreenFrameCount;
    if (totalFrames % 10 == 0)
        _dispatcher.BeginInvoke(() => CheatingScoreUpdated?.Invoke(_cheatingScore));
}
```

### 1.2 3D Head Pose Estimation via Perspective-n-Point (`SolvePnP`)
- **File**: `AICAMS/Services/MonitoringService.cs`
- **Description**: Maps 2D landmarks detected by Dlib to a canonical 3D facial coordinate frame using OpenCV's `SolvePnP`, extracting Yaw (rotation left/right) and Pitch (rotation up/down) angles to detect head turns without manual tracking markers.

```csharp
// Canonical 3D anthropometric facial landmark points (in millimeters)
private readonly double[,] _modelPoints = new double[,]
{
    { 0.0,    0.0,    0.0   },  // Nose tip (Landmark 30)
    { 0.0,  -330.0, -65.0  },  // Chin (Landmark 8)
    { -225.0, 170.0, -135.0 },  // Left eye outer corner (Landmark 36)
    { 225.0,  170.0, -135.0 },  // Right eye outer corner (Landmark 45)
    { -150.0,-150.0, -125.0 },  // Left mouth corner (Landmark 48)
    { 150.0, -150.0, -125.0 }   // Right mouth corner (Landmark 54)
};

// Solve Perspective-n-Point against calibrated camera intrinsics matrix
Cv2.SolvePnP(modelPointsMat, imagePointsMat, cameraMatrix, distCoeffs, rvec, tvec);
Cv2.Rodrigues(rvec, rotationMatrix);

// Decompose rotation matrix into Euler Angles
double yaw = Math.Atan2(rotationMatrix.At<double>(1, 0), rotationMatrix.At<double>(0, 0)) * (180.0 / Math.PI);
double pitch = Math.Atan2(-rotationMatrix.At<double>(2, 0), 
    Math.Sqrt(Math.Pow(rotationMatrix.At<double>(2, 1), 2) + Math.Pow(rotationMatrix.At<double>(2, 2), 2))) * (180.0 / Math.PI);

bool isHeadTurned = Math.Abs(yaw) > YAW_THRESHOLD || Math.Abs(pitch) > PITCH_THRESHOLD;
```

---

## Module 2: Kiosk Lockdown & Low-Level OS Interception

### 2.1 Win32 Low-Level Keyboard Hook (`WH_KEYBOARD_LL`)
- **File**: `AICAMS/Views/Student/StudentExamPage.xaml.cs`
- **Description**: Registers an OS-level keyboard hook using `SetWindowsHookEx` that intercepts system keys (`Alt+Tab`, `Windows Key`) before the Windows OS shell can process task-switching.

```csharp
private void SetupKeyboardHook()
{
    _keyboardProc = KeyboardHookCallback;
    using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
    using var curModule = curProcess.MainModule!;
    
    // Install WH_KEYBOARD_LL hook (idHook = 13)
    _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
        13, _keyboardProc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
}

private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
{
    if (nCode >= 0)
    {
        int vkCode = Marshal.ReadInt32(lParam);
        bool altDown = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;

        // Intercept and swallow Alt+Tab (VK_TAB = 9 with Alt held)
        if (vkCode == 9 && altDown)
        {
            Dispatcher.BeginInvoke(() => {
                _monitoring?.OnAltTabAttempt();
                ShowAlert("Alt+Tab Blocked", "Switching windows is prohibited during the examination.");
            });
            return 1; // Non-zero value swallows the keystroke from the operating system
        }

        // Intercept Windows Key (VK_LWIN = 91, VK_RWIN = 92) to suppress Start Menu
        if (vkCode == 91 || vkCode == 92) return 1;
    }
    return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
}
```

### 2.2 Emergency Physical Proctor Bypass (`Ctrl + Shift + F12`)
- **File**: `AICAMS/MainWindow.xaml.cs`
- **Description**: Provides a hidden physical hardware override for lab proctors in the event of an OS driver lockup, camera freeze, or keyboard hook fault.

```csharp
protected override void OnPreviewKeyDown(KeyEventArgs e)
{
    base.OnPreviewKeyDown(e);

    // Emergency testing & hardware failsafe bypass: Ctrl + Shift + F12
    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F12)
    {
        this.Topmost = false;                    // Relinquish topmost foreground lock
        this.WindowState = WindowState.Minimized; // Minimize kiosk window to restore desktop access
        e.Handled = true;
    }
}
```

---

## Module 3: Dynamic Assessment Delivery & Student Interface

### 3.1 Polymorphic UI Canvas Routing by Question Modality
- **File**: `AICAMS/Views/Student/StudentExamPage.xaml.cs`
- **Description**: Employs polymorphic runtime binding to dynamically swap question-type-specific controls (code editor, option selector, text area, enumeration) into a single container frame.

```csharp
private async void LoadQuestion(int index)
{
    if (index < 0 || index >= _questions.Count) return;
    _currentIndex = index;

    var question = _questions[index];
    var existingAnswer = SessionManager.CurrentAttempt != null
        ? await _examService.GetAnswerAsync(SessionManager.CurrentAttempt.AttemptId, question.QuestionId)
        : null;

    // Polymorphic UI Canvas selection based on database question modality
    UserControl canvas = question.QuestionType switch
    {
        "identification" or "essay" =>
            new Canvas.TextInputCanvas(question, existingAnswer, index + 1, _questions.Count),
        "multiple_choice" or "true_false" =>
            new Canvas.OptionSelectorCanvas(question, existingAnswer),
        "enumeration" =>
            new Canvas.EnumerationCanvas(question, existingAnswer, index + 1, _questions.Count),
        "coding" =>
            new Canvas.CodingCanvas(question, existingAnswer), // AvalonEdit syntax editor + execution console
        _ => new Canvas.TextInputCanvas(question, existingAnswer, index + 1, _questions.Count)
    };

    ExamCanvasHost.Content = canvas;
    UpdateNavPane();
}
```

### 3.2 Deterministic Attempt-Seeded Question Randomization
- **File**: `AICAMS/Services/ExamService.cs`
- **Description**: Guarantees that questions appear in a distinct shuffled order for every student to curb neighboring screen peeking, while ensuring that the exact same order is deterministically restored if the student re-opens the exam after a reboot.

```csharp
// Seeded pseudo-random shuffling using AttemptId ensures consistent ordering per attempt across reboots
if (examInfo != null && examInfo.RandomizeQuestions && attemptId.HasValue)
{
    var rng = new Random(attemptId.Value.GetHashCode());
    return questions.OrderBy(_ => rng.Next()).ToList();
}
```

### 3.3 Fault-Tolerant Session Resumption & Absolute Deadline Calculation
- **File**: `AICAMS/Views/Student/StudentExamPage.xaml.cs`
- **Description**: Prevents timer-reset exploits on power loss by synchronizing the remaining seconds against the centralized database start timestamp.

```csharp
// Debounced auto-save executes every 20 seconds and on question transitions
_autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
_autoSaveTimer.Tick += async (s, e) => await SaveCurrentAnswer();
_autoSaveTimer.Start();

// Absolute deadline calculation guarantees elapsed time persists across computer reboots
int totalDurationMinutes = (exam.DurationMinutes ?? 60) + (SessionManager.CurrentAttempt?.ExtraTimeMinutes ?? 0);
if (SessionManager.CurrentAttempt?.StartedAt != null)
{
    var deadline = SessionManager.CurrentAttempt.StartedAt.Value.AddMinutes(totalDurationMinutes);
    _remainingSeconds = Math.Max(0, (int)(deadline - DateTime.Now).TotalSeconds);
}
```

---

## Module 4: Isolated Child-Process Execution Sandbox

### 4.1 Process Sandbox with I/O Redirection & 5-Second Hard Timeout
- **File**: `AICAMS/Services/CodeRunnerService.cs`
- **Description**: Executes student-authored code (C, Java, Python) in a sandboxed child process within an isolated temp directory, streaming custom standard input and killing the process tree if execution exceeds 5 seconds.

```csharp
private async Task<(bool Success, string Output, string Error)> RunProcessAsync(
    string fileName, string arguments, string workingDirectory, string? stdinInput = null)
{
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory, // Isolated dynamic directory
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true                // Prevents console pop-up windows
        }
    };

    var stdout = new StringBuilder();
    var stderr = new StringBuilder();
    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    // Stream interactive stdin input if test cases require input arguments
    if (!string.IsNullOrEmpty(stdinInput))
    {
        await process.StandardInput.WriteAsync(stdinInput);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();
    }

    // 5,000ms hard timeout guard against infinite loops and fork bombs
    using var cts = new CancellationTokenSource(TimeoutMs);
    try {
        await process.WaitForExitAsync(cts.Token);
        return (process.ExitCode == 0, stdout.ToString(), stderr.ToString());
    }
    catch (OperationCanceledException) {
        try { process.Kill(entireProcessTree: true); } catch { }
        return (false, string.Empty, "Execution timed out (5-second limit exceeded). Possible infinite loop.");
    }
}
```

---

## Module 5: Automated Evaluation & Generative AI Rubric Engine

### 5.1 Deterministic Objective Auto-Grading & Option Resolution
- **File**: `AICAMS/Services/AutoCheckerService.cs`
- **Description**: Performs immediate, deterministic evaluation for objective questions (Multiple Choice, True/False, Identification) by normalizing whitespace and resolving option letters against full option text.

```csharp
public static bool IsAnswerCorrect(string? studentAnswerText, Question question)
{
    var sTrim = (studentAnswerText ?? string.Empty).Trim();
    var cTrim = (question.CorrectAnswer ?? string.Empty).Trim();

    if (string.IsNullOrEmpty(sTrim) || string.IsNullOrEmpty(cTrim))
        return false;

    // Direct case-insensitive string equality
    if (string.Equals(sTrim, cTrim, StringComparison.OrdinalIgnoreCase))
        return true;

    // Multiple Choice: cross-reference letter keys (e.g. 'B') and full option texts
    if (question.QuestionType == "multiple_choice")
    {
        var (sLetter, sText) = ResolveOption(sTrim, question.Options);
        var (cLetter, cText) = ResolveOption(cTrim, question.Options);

        if (!string.IsNullOrEmpty(sLetter) && string.Equals(sLetter, cLetter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(sText) && string.Equals(sText, cText, StringComparison.OrdinalIgnoreCase))
            return true;
    }

    return false;
}
```

### 5.2 Two-Phase Score Aggregation & Attempt Total Synchronization
- **File**: `AICAMS/Services/AutoCheckerService.cs`
- **Description**: Commits scored points per individual `StudentAnswer` row, then recalculates and locks the cached `TotalScore` on `StudentExamAttempt` for fast institutional dashboard queries.

```csharp
private static async Task RecomputeAttemptTotalAsync(AppDbContext context, Guid attemptId)
{
    var attempt = await context.StudentExamAttempts
        .Include(a => a.Answers)
        .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

    // Verify all individual questions possess an assigned score before caching final total
    if (attempt != null && attempt.Answers.Count > 0 && attempt.Answers.All(a => a.ObtainedPoints.HasValue))
    {
        attempt.TotalScore = attempt.Answers.Sum(a => a.ObtainedPoints ?? 0);
        await context.SaveChangesAsync();
    }
}
```

### 5.3 Google Gemini REST Integration & Structured JSON Schema Parsing
- **File**: `AICAMS/Services/GeminiRubricGradingService.cs`
- **Description**: Calls the Google Gemini 3.5 Flash endpoint using DPAPI-decrypted credentials and deterministic prompt engineering to parse subjective answers against institutional rubric criteria.

```csharp
// Strict JSON schema prompt formatting
var prompt = $@"
Evaluate the following student response according to the defined academic rubric:
Rubric Criteria: {criteriaJson}
Question: {questionText}
Student Answer: {studentAnswer}

Return ONLY valid JSON matching this exact structure:
{{
  ""assignedScore"": <decimal>,
  ""confidenceScore"": <decimal between 0.0 and 1.0>,
  ""criteriaEvaluations"": [
    {{ ""criteriaId"": ""<guid>"", ""level"": ""Excellent|Good|Fair|Poor"", ""feedback"": ""<string>"" }}
  ],
  ""rationale"": ""<string>""
}}";

string decryptedKey = AppSettingsService.GetGeminiApiKey(); // Protected via Windows DPAPI
var response = await _httpClient.PostAsJsonAsync(endpoint, requestPayload);
```

---

## Module 6: Live Faculty Surveillance & Bi-Directional LAN Control

### 6.1 Real-Time Examinee Grid Aggregation with Priority Risk Ranking
- **File**: `AICAMS/Views/Faculty/Tabs/MonitoringTab.xaml.cs`
- **Description**: Implements a non-blocking asynchronous 3-second heartbeat polling cycle that aggregates client states and sorts examinees by threat level.

```csharp
private async Task RefreshDataAsync()
{
    using var context = new AppDbContext();
    var monitoringService = new MonitoringService(Dispatcher);

    // Multi-tier priority ordering: Critical Risk (100) -> Higher Score -> Anomaly Count
    ActiveStudents = (await monitoringService.GetActiveStudentsAsync(_currentExamId))
        .OrderByDescending(s => s.IsMaxRiskReached)
        .ThenByDescending(s => s.RiskScore)
        .ThenByDescending(s => s.FlagCount)
        .ToList();

    RecentFlags = await monitoringService.GetRecentFlagsAsync(5);
    Stats = await monitoringService.GetStatisticsAsync(_currentExamId);

    Dispatcher.Invoke(() => UpdateUI());
}
```

### 6.2 Remote Kiosk Interventions: Warnings & Dynamic Time Extensions
- **File**: `AICAMS/Views/Student/StudentExamPage.xaml.cs`
- **Description**: Student client polling loop that receives live instructor directives across the LAN, triggering on-screen warnings or instantly recalculating exam deadlines.

```csharp
// Background notification listener on student client
var latestWarning = await context.Notifications
    .Where(n => n.UserId == SessionManager.CurrentUser.UserId 
             && n.Category == "KioskWarning" && !n.IsRead)
    .OrderByDescending(n => n.CreatedAt)
    .FirstOrDefaultAsync();

if (latestWarning != null)
{
    latestWarning.IsRead = true;
    await context.SaveChangesAsync();
    DisplayKioskWarning(latestWarning.Message); // Renders modal overlay on student screen
}

// Dynamically apply proctor-granted extra time (+5, +10 mins)
if (syncResult.ExtraMinutes.HasValue && SessionManager.CurrentAttempt != null)
{
    SessionManager.CurrentAttempt.ExtraTimeMinutes = syncResult.ExtraMinutes.Value;
    UpdateTimerDisplay(); // Instantly adjusts remaining time countdown
}
```

---

## Module 7: Security, Cryptography & Institutional Reporting

### 7.1 Salted BCrypt Hashing & Constant-Time Verification
- **File**: `AICAMS/Services/AuthService.cs`
- **Description**: Enforces security hygiene via adaptive work-factor BCrypt credential hashing and constant-time string comparison (`CryptographicOperations.FixedTimeEquals`) to protect onboarding secrets against side-channel timing attacks.

```csharp
// Adaptive salted BCrypt password validation
bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

// Constant-time comparison prevents timing side-channel attacks on faculty verification codes
public static bool VerifyFacultyRegistrationCode(string? submittedCode)
{
    string submittedHash = ComputeSha256Hash(submittedCode);
    string storedHash = App.Configuration["Faculty:RegistrationCodeHash"];

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(submittedHash),
        Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant()));
}
```

### 7.2 Vector PDF Institutional Report Synthesis via QuestPDF
- **File**: `AICAMS/Services/PdfExportService.cs`
- **Description**: Generates formal, printable institutional assessment audit reports compiling class averages, completion rates, and an Integrity Index representing the percentage of examinees free from major proctoring flags.

```csharp
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.Header().Element(ComposeHeader); // Institutional header & logos
        page.Content().Element(ComposeContent); // KPI summary cards & grade distributions
        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });
    });
}).GeneratePdf(filePath);
```
