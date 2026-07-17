Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
scriptPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\liveServer.ps1"
shell.Run "powershell.exe -ExecutionPolicy Bypass -File """ & scriptPath & """", 0, False
