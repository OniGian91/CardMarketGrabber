# Save as: Start-All.ps1

# Define the paths and commands
$grafanaPath = "C:\Program Files\GrafanaLabs\grafana\bin"
$grafanaCommand = "cd `"$grafanaPath`" && start grafana.exe server"

$cardGrabberDrive = "D:"
$cardGrabberPath = "D:\Projects\CardMarketGrabber\CardGrabber\bin\Debug\net8.0"
$cardGrabberCommand = "cd `"$cardGrabberPath`" && start CardGrabber.exe"

# Combine commands into one string
$combinedCommand = "$grafanaCommand && $cardGrabberDrive && $cardGrabberCommand"

# Run in elevated Command Prompt
Start-Process "cmd.exe" -ArgumentList "/k", $combinedCommand -Verb RunAs

# Wait a bit for servers to initialize (optional, tweak as needed)
Start-Sleep -Seconds 5

# Launch Chrome with the Grafana dashboard URL
Start-Process "chrome.exe" "http://localhost:3001/d/bekrrzsv3qfi8c/sellers"
