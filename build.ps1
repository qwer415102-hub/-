$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destination = Join-Path $PSScriptRoot '名称提取工具'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/out:$destination\名称提取工具.exe" "$PSScriptRoot\NameExtractor.cs"
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
foreach ($mode in @(@('提取文件名-不带后缀.bat','stem'), @('提取文件名-带后缀.bat','full'))) {
    $content = "@echo off`r`nsetlocal DisableDelayedExpansion`r`nset `"target=%~1`"`r`nif not defined target set `"target=%~dp0.`"`r`n`"%~dp0名称提取工具.exe`" --quick $($mode[1]) `"%target%`"`r`n"
    [IO.File]::WriteAllText((Join-Path $destination $mode[0]), $content, [Text.Encoding]::GetEncoding(936))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '使用说明.txt') -Destination $destination -Force
Write-Host "已生成：$destination"
