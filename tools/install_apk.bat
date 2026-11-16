@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
rem Maden - APK'yi USB ile bagli telefona kurar (USB hata ayiklama acik olmali).
cd /d "%~dp0\.."
set "APK=%CD%\Builds\Maden.apk"
if not exist "%APK%" (
  echo HATA: %APK% yok. Once tools\build.bat calistirin.
  goto :fail
)

set "ADB="
where adb >nul 2>&1 && set "ADB=adb"
if not defined ADB (
  for /d %%D in ("%ProgramFiles%\Unity\Hub\Editor\6000.*") do (
    if exist "%%~D\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" set "ADB=%%~D\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
  )
)
if not defined ADB (
  echo HATA: adb bulunamadi. Unity Android Build Support kuruluysa adb onun icinde olmali.
  goto :fail
)

"%ADB%" devices
"%ADB%" install -r "%APK%"
if errorlevel 1 (
  echo HATA: Kurulum basarisiz. Telefonda USB hata ayiklama acik mi ve izin verdiniz mi?
  goto :fail
)
echo BASARILI: Maden kuruldu.
if not defined NOPAUSE pause
exit /b 0
:fail
if not defined NOPAUSE pause
exit /b 1
