#requires -Version 5.1

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$script:winTitle = "LIVESERVER_" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
$script:isRunning = $false
$script:procId = 0

function Add-Log($text, $color = "White") {
    $logBox.SelectionStart = $logBox.TextLength
    $logBox.SelectionLength = 0
    $logBox.SelectionColor = [Drawing.Color]::FromName($color)
    $logBox.AppendText("[$([DateTime]::Now.ToString('HH:mm:ss'))] $text`r`n")
    $logBox.ScrollToCaret()
}

function Set-Status($text, $color = "Gray") {
    $statusLabel.Text = "Status: $text"
    $statusLabel.ForeColor = [Drawing.Color]::FromName($color)
}

function Start-Server {
    $path = $pathBox.Text.Trim().Trim('"')
    $port = [int]$portBox.Value
    if (-not (Test-Path -LiteralPath $path)) {
        [Windows.Forms.MessageBox]::Show("Project path does not exist!", "Error", "OK", [Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        return
    }
    if ($script:isRunning) { Add-Log "Server is already running!" "Orange"; return }

    Add-Log "Starting server on port $port..." "Cyan"
    Add-Log "Path: $path" "Cyan"
    Set-Status "Starting..." "Orange"

    try {
        $psi = [Diagnostics.ProcessStartInfo]@{
            FileName = "cmd.exe"
            Arguments = "/c title $script:winTitle & live-server --port=$port --no-browser"
            WorkingDirectory = $path
            WindowStyle = [Diagnostics.ProcessWindowStyle]::Minimized
            UseShellExecute = $true
        }
        $proc = [Diagnostics.Process]::Start($psi)
        $script:procId = $proc.Id
        Start-Sleep -Milliseconds 800

        if (-not $proc.HasExited) {
            $script:isRunning = $true
            $startBtn.Enabled = $false
            $stopBtn.Enabled = $true
            $openBtn.Enabled = $true
            $pathBox.Enabled = $false
            $browseBtn.Enabled = $false
            $portBox.Enabled = $false
            Set-Status "Running on port $port" "Green"
            Add-Log "Server started successfully!" "Green"
            Add-Log "Open browser at http://localhost:$port" "Cyan"
        } else {
            Add-Log "Failed to start. Is live-server installed?" "Red"
            Add-Log "Run: npm install -g live-server" "Red"
            Set-Status "Failed to start" "Red"
        }
    } catch {
        Add-Log "Error: $_" "Red"
        Set-Status "Error" "Red"
    }
}

function Stop-Server {
    if (-not $script:isRunning) { return }
    Add-Log "Stopping server..." "Orange"
    Set-Status "Stopping..." "Orange"

    taskkill /F /FI "WINDOWTITLE eq $script:winTitle*" /T 2> $null | Out-Null

    Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $script:procId" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Stop-Process -Id $script:procId -Force -ErrorAction SilentlyContinue

    $script:isRunning = $false
    $script:procId = 0
    $startBtn.Enabled = $true
    $stopBtn.Enabled = $false
    $openBtn.Enabled = $false
    $pathBox.Enabled = $true
    $browseBtn.Enabled = $true
    $portBox.Enabled = $true
    Set-Status "Stopped" "Gray"
    Add-Log "Server stopped" "Red"
}

# == FORM ==
$form = [Windows.Forms.Form]@{
    Text = "Live Server Controller"
    Size = [Drawing.Size](720, 560)
    StartPosition = "CenterScreen"
    FormBorderStyle = "FixedSingle"
    MaximizeBox = $false
    BackColor = [Drawing.Color]::FromArgb(30, 30, 30)
    ForeColor = "White"
    Font = [Drawing.Font]::new("Segoe UI", 9)
}

# Title
$title = [Windows.Forms.Label]@{
    Text = "Live Server Controller"
    Font = [Drawing.Font]::new("Segoe UI", 15, [Drawing.FontStyle]::Bold)
    Size = [Drawing.Size](540, 35)
    Location = [Drawing.Point](15, 12)
    TextAlign = "MiddleCenter"
    ForeColor = [Drawing.Color]::FromArgb(100, 200, 255)
    BackColor = [Drawing.Color]::FromArgb(45, 45, 45)
}
$form.Controls.Add($title)

# Path
$pathLabel = [Windows.Forms.Label]@{ Text = "Project Path:"; Location = [Drawing.Point](15, 65); Size = [Drawing.Size](80, 25); ForeColor = "LightGray" }
$form.Controls.Add($pathLabel)

$pathBox = [Windows.Forms.TextBox]@{ Location = [Drawing.Point](95, 63); Size = [Drawing.Size](355, 22); BackColor = [Drawing.Color]::FromArgb(50, 50, 50); ForeColor = "White"; BorderStyle = "FixedSingle" }
$pathBox.Add_Leave({ $this.Text = $this.Text.Trim().Trim('"') })
$pathBox.Add_GotFocus({ $this.SelectAll() })
$form.Controls.Add($pathBox)

$browseBtn = [Windows.Forms.Button]@{ Text = "Browse..."; Location = [Drawing.Point](455, 62); Size = [Drawing.Size](100, 25); BackColor = [Drawing.Color]::FromArgb(60, 60, 60); ForeColor = "White"; FlatStyle = "Flat" }
$browseBtn.Add_Click({
    $fbd = [Windows.Forms.FolderBrowserDialog]@{ SelectedPath = $pathBox.Text }
    if ($fbd.ShowDialog() -eq "OK") { $pathBox.Text = $fbd.SelectedPath }
})
$form.Controls.Add($browseBtn)

# Port
$portLabel = [Windows.Forms.Label]@{ Text = "Port:"; Location = [Drawing.Point](15, 100); Size = [Drawing.Size](80, 25); ForeColor = "LightGray" }
$form.Controls.Add($portLabel)

$portBox = [Windows.Forms.NumericUpDown]@{
    Location = [Drawing.Point](95, 98); Size = [Drawing.Size](90, 22)
    Minimum = 1024; Maximum = 65535; Value = 5500
    BackColor = [Drawing.Color]::FromArgb(50, 50, 50); ForeColor = "White"; BorderStyle = "FixedSingle"
}
$form.Controls.Add($portBox)

# Start
$startBtn = [Windows.Forms.Button]@{
    Text = "Start Server"; Location = [Drawing.Point](200, 97); Size = [Drawing.Size](110, 28)
    BackColor = [Drawing.Color]::FromArgb(46, 125, 50); ForeColor = "White"; FlatStyle = "Flat"
}
$startBtn.Add_Click({ Start-Server })
$form.Controls.Add($startBtn)

# Stop
$stopBtn = [Windows.Forms.Button]@{
    Text = "Stop Server"; Location = [Drawing.Point](320, 97); Size = [Drawing.Size](110, 28)
    BackColor = [Drawing.Color]::FromArgb(198, 40, 40); ForeColor = "White"; FlatStyle = "Flat"
    Enabled = $false
}
$stopBtn.Add_Click({ Stop-Server })
$form.Controls.Add($stopBtn)

# Open Browser
$openBtn = [Windows.Forms.Button]@{
    Text = "Open Browser"; Location = [Drawing.Point](440, 97); Size = [Drawing.Size](115, 28)
    BackColor = [Drawing.Color]::FromArgb(60, 60, 60); ForeColor = "White"; FlatStyle = "Flat"
    Enabled = $false
}
$openBtn.Add_Click({
    $port = [int]$portBox.Value
    Start-Process "http://localhost:$port"
})
$form.Controls.Add($openBtn)

# Separator
$sep = [Windows.Forms.Label]@{
    Location = [Drawing.Point](15, 135); Size = [Drawing.Size](540, 1)
    BackColor = [Drawing.Color]::FromArgb(70, 70, 70)
}
$form.Controls.Add($sep)

# Status
$statusLabel = [Windows.Forms.Label]@{ Text = "Status: Stopped"; Location = [Drawing.Point](15, 145); Size = [Drawing.Size](540, 22); ForeColor = "Gray" }
$form.Controls.Add($statusLabel)

# Log box
$logBox = [Windows.Forms.RichTextBox]@{
    Location = [Drawing.Point](15, 172); Size = [Drawing.Size](540, 240)
    ReadOnly = $true; BackColor = [Drawing.Color]::FromArgb(20, 20, 20)
    ForeColor = [Drawing.Color]::FromArgb(200, 200, 200)
    Font = [Drawing.Font]::new("Consolas", 9.5)
    BorderStyle = "None"
}
$form.Controls.Add($logBox)

# Footer
$linkLabel = [Windows.Forms.LinkLabel]@{
    Text = "Created & Developed by Moaaz Besher  |  moaaz-ashraf.netlify.app"
    Location = [Drawing.Point](15, 495); Size = [Drawing.Size](680, 25)
    TextAlign = "MiddleCenter"
    ForeColor = [Drawing.Color]::FromArgb(150, 150, 150)
    ActiveLinkColor = [Drawing.Color]::Cyan
    LinkColor = [Drawing.Color]::FromArgb(100, 200, 255)
    VisitedLinkColor = [Drawing.Color]::FromArgb(100, 200, 255)
    Font = [Drawing.Font]::new("Segoe UI", 9, [Drawing.FontStyle]::Italic)
    LinkBehavior = "HoverUnderline"
}
$linkLabel.Links.Add(0, $linkLabel.Text.Length, "https://moaaz-ashraf.netlify.app/")
$linkLabel.Add_LinkClicked({ [System.Diagnostics.Process]::Start($_.Link.LinkData) })
$form.Controls.Add($linkLabel)

# Drag & drop support
$form.AllowDrop = $true
$form.Add_DragEnter({ $_.Effect = "Copy" })
$form.Add_DragDrop({
    if ($_.Data.GetDataPresent("FileDrop")) {
        $pathBox.Text = $_.Data.GetData("FileDrop")[0]
    }
})

# Handle close -> stop server
$form.Add_FormClosing({
    if ($script:isRunning) { Stop-Server }
})

[Application]::EnableVisualStyles()
[Application]::Run($form)
