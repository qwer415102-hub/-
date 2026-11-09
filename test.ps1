$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot '.verification'
$app = Join-Path $PSScriptRoot '名称提取工具\名称提取工具.exe'
if (-not (Test-Path -LiteralPath $app)) { throw '请先运行 build.ps1' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath $app -Destination $output -Force
& $compiler /nologo /target:exe /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/reference:$app" "/out:$output\Verify.exe" "$PSScriptRoot\Verify.cs"
if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
& (Join-Path $output 'Verify.exe') (Join-Path $output 'preview.png')
if ($LASTEXITCODE -ne 0) { throw '测试失败' }
