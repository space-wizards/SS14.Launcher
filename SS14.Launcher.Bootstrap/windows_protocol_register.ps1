$value = "`"$PSScriptRoot\Space Station 14 Launcher.exe`" `"%1`""
$HKCR = "Registry::HKEY_CLASSES_ROOT"

# Create registry key ss14s://
New-Item -Path "$HKCR\ss14s" -Force
New-Item -Path "$HKCR\ss14s\Shell\Open\Command" -Force

# Create registry key ss14://
New-Item -Path "$HKCR\ss14" -Force
New-Item -Path "$HKCR\ss14\Shell\Open\Command" -Force

# Set registry key values ss14s://
Set-ItemProperty -Path "$HKCR\ss14s" -Name "URL Protocol" -Value ""
Set-ItemProperty -Path "$HKCR\ss14s\Shell\Open\Command" -Name "(default)" -Value $value

# Set registry key values ss14://
Set-ItemProperty -Path "$HKCR\ss14" -Name "URL Protocol" -Value ""
Set-ItemProperty -Path "$HKCR\ss14\Shell\Open\Command" -Name "(default)" -Value $value
