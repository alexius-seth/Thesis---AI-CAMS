# AI-CAMS Deployment Prerequisites

This directory contains offline prerequisite runtime installers required for AI-CAMS deployment across laboratory workstations.

---

## 1. Microsoft Visual C++ 2015–2022 Redistributable (x64)

- **Target File:** `VC_redist.x64.exe`
- **Architecture:** x64 (64-bit Windows)
- **Official Download:** [https://aka.ms/vs/17/release/vc_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### Purpose & Native Dependencies
AI-CAMS incorporates high-performance native computer vision C++ runtimes:
- **OpenCvSharp** (`OpenCvSharpExtern.dll`): Frame capture, resolution normalization, and image matrix processing.
- **DlibDotNet** (`DlibDotNetNative.dll`): 68-point facial landmark regression, head pose estimation, and gaze angle calculation.

Both native dynamic-link libraries link against the Microsoft Visual C++ 14.x Runtime (`MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`). Without this redistributable installed, computer vision proctoring and webcam feeds will fail to initialize.

---

## 2. Inno Setup Packaging & Silent Installation Behavior

When compiling the Windows installer via `installer.iss`:
1. **Packaging:** If `VC_redist.x64.exe` is present in this folder (or the root `Prerequisites\` directory), Inno Setup automatically bundles it into `AICAMS_Setup.exe`.
2. **Registry Inspection:** During installation on the client/host machine, the installer checks the Windows Registry:
   - `HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64` (`Installed == 1`)
   - `HKLM\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X64` (`Installed == 1`)
3. **Conditional Execution:**
   - **If Missing:** The installer executes `VC_redist.x64.exe /quiet /norestart` silently in the background before completing installation.
   - **If Already Present:** The installer skips execution automatically, ensuring rapid deployment across lab machines.

