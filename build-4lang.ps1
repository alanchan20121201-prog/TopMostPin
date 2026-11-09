# 產出四個語言版本：TopMostPin-zh-CN / -zh-TW / -en / -ja
# 語言由編譯期常數 LANG_* 固定在各自 EXE 裡，執行時不會判斷系統區域。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root 'publish'
$langs = @('zh-CN', 'zh-TW', 'en', 'ja')

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($lang in $langs) {
    Write-Host "`n=== 建置 $lang ===" -ForegroundColor Cyan
    $dir = Join-Path $outDir $lang
    dotnet publish (Join-Path $root 'TopMostPin.csproj') -c Release -r win-x64 `
        --self-contained false -p:PublishSingleFile=true -p:AppLang=$lang -o $dir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失敗：$lang" }

    $exe = Join-Path $dir 'TopMostPin.exe'
    $target = Join-Path $outDir "TopMostPin-$lang.exe"
    Copy-Item $exe $target -Force
    Write-Host "✔ $target" -ForegroundColor Green
}

Write-Host "`n四個語言 EXE 已產出於 publish/" -ForegroundColor Green
