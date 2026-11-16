@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
rem Maden - Windows APK derleme betigi.
rem Kullanim: tools\build.bat   (istege bagli: UNITY_PATH ortam degiskeni ile Unity.exe yolu)

cd /d "%~dp0\.."
set "PROJECT=%CD%"

if defined UNITY_PATH (
  set "UNITY=%UNITY_PATH%"
) else (
  set "UNITY="
  for /d %%D in ("%ProgramFiles%\Unity\Hub\Editor\6000.3.*") do (
    if exist "%%~D\Editor\Unity.exe" set "UNITY=%%~D\Editor\Unity.exe"
  )
  if not defined UNITY (
    for /d %%D in ("%ProgramFiles%\Unity\Hub\Editor\6000.*") do (
      if exist "%%~D\Editor\Unity.exe" set "UNITY=%%~D\Editor\Unity.exe"
    )
  )
)

if not defined UNITY (
  echo HATA: Unity 6000.3 bulunamadi. Unity Hub'dan kurun veya UNITY_PATH degiskenini ayarlayin.
  call :speak "Hata. Unity bulunamadi."
  goto :end_fail
)
echo Unity: %UNITY%
echo Proje: %PROJECT%

echo [1/2] Proje ayarlari uygulaniyor...
"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -buildTarget Android -executeMethod MadenBuild.Configure -logFile "%PROJECT%\configure.log"
if errorlevel 1 (
  echo HATA: Ayar adimi basarisiz. configure.log dosyasina bakin.
  call :speak "Hata. Ayar adimi basarisiz."
  goto :end_fail
)

echo [2/2] APK derleniyor. Bu birkac dakika surebilir...
"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -buildTarget Android -executeMethod MadenBuild.BuildAndroid -logFile "%PROJECT%\build.log"
if errorlevel 1 (
  echo HATA: Derleme basarisiz. build.log dosyasinin sonuna bakin:
  powershell -NoProfile -Command "Get-Content -Tail 40 '%PROJECT%\build.log'"
  call :speak "Hata. Derleme basarisiz."
  goto :end_fail
)

if not exist "%PROJECT%\Builds\Maden.apk" (
  echo HATA: Builds\Maden.apk olusmadi.
  call :speak "Hata. APK olusmadi."
  goto :end_fail
)
echo.
echo BASARILI: %PROJECT%\Builds\Maden.apk
call :speak "Derleme tamamlandi. APK hazir."
if not defined NOPAUSE pause
exit /b 0

:end_fail
if not defined NOPAUSE pause
exit /b 1

:speak
powershell -NoProfile -Command "try { Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('%~1') } catch {}" >nul 2>&1
exit /b 0
