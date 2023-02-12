echo building....
set VERSION=0.0.0.1
set BUILD_CONFIGURATION=Release
set SERVER_NAME=EQEMU Patcher
set FILELIST_URL=https://github.com/xackery/eqemupatcher/releases/download/latest
set PATCHER_URL=https://github.com/xackery/eqemupatcher/releases/download/latest
"C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\msbuild.exe" /m /p:Configuration=%BUILD_CONFIGURATION% /p:VERSION=%VERSION% /p:SERVER_NAME="%SERVER_NAME%" /p:FILELIST_URL="%FILELIST_URL%" /p:PATCHER_URL="%PATCHER_URL%"