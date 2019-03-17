@ECHO OFF

REM Start with the MOTRd files
set archivename="MOTRd.zip"
set sourcepath="C:\Users\Large\source\repos\MOTRd"
set sourcepathbin="%sourcepath%\MOTRd\bin\Release\"
set sourcepathmime="%sourcepath%\WebSockets\"
set zipfile=7za.exe a -tzip -bd -bb0 "%archivename%"
del %archivename%

REM Add the files we want to the archive
%zipfile% "%sourcepath%\MOTRd\link.ico"
%zipfile% "%sourcepath%\MOTRd\login.ico"
%zipfile% "%sourcepathbin%fastJSON.dll"
%zipfile% "%sourcepathmime%MimeTypes.config"
%zipfile% "%sourcepathbin%MOTRd.exe"
%zipfile% "%sourcepathbin%MOTRd.exe.Config"
%zipfile% "%sourcepathbin%WebSockets.dll"
%zipfile% "%sourcepathbin%BouncyCastle.Crypto.dll"
%zipfile% "%sourcepathbin%Effortless.Net.Encryption.dll"
%zipfile% "%sourcepathbin%LiteDB.dll"
%zipfile% "%sourcepathbin%DM.MovieApi.dll"
%zipfile% "%sourcepathbin%Newtonsoft.Json.dll"
%zipfile% "%sourcepathbin%Firebase.NET.dll"
%zipfile% "%sourcepathbin%RestSharp.dll"

REM Now pack the webfiles
set archivename2="WebFiles.zip"
set sourcepath2="%sourcepath%\MOTRd\WebFiles"
del /S %archivename2%

REM Copy the Android installfile motr.apk to the webfiles
mkdir %sourcepath2%\android
del /S %sourcepath2%\android\motr.apk
set sourcepath3=C:\Users\Large\source\repos\MOTRApp\MOTRApp\MOTRApp.Android\bin\Release
copy %sourcepath3%\com.larswerner.motr-Signed.apk %sourcepath2%\android\motr.apk

REM Copy all the webfiles 
mkdir WebFiles
xcopy %sourcepath2% Webfiles /E /Y
xcopy "%sourcepathmime%MimeTypes.config" %sourcepath2% /Y
7za.exe a -tzip %archivename2% "Webfiles"
rmdir WebFiles /S /Q

REM Now launch the NSIS installer
"C:\Program Files (x86)\NSIS\Bin\makensis" MOTRdInstaller.nsi
del /S %archivename%
del /S %archivename2%
pause
exit
