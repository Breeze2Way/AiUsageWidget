$ErrorActionPreference = 'Stop'

dotnet publish `
    'src\AiUsageWidget\AiUsageWidget.csproj' `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o 'publish'

Copy-Item -LiteralPath 'src\AiUsageWidget\悬浮球.ico' -Destination 'publish\悬浮球.ico' -Force

Write-Host "Published to $((Resolve-Path 'publish').Path)"
