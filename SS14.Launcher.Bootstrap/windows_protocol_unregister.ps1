$HKCR = "Registry::HKEY_CLASSES_ROOT"

# Remove registry key ss14s://
Remove-Item -Path "$HKCR\ss14s" -Force -Recurse

# Remove registry key ss14://
Remove-Item -Path "$HKCR\ss14" -Force -Recurse
