@echo off
setlocal DisableDelayedExpansion
set "target=%~1"
if not defined target set "target=%~dp0."
"%~dp0名称提取工具.exe" --quick stem "%target%"
