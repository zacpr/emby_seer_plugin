# Inseerrtion Connection Test Script (PowerShell)
# Tests connectivity to Seerr instance and validates configuration

param(
    [string]$SeerrUrl = $env:SEERR_URL,
    [string]$SeerrApiKey = $env:SEERR_API_KEY,
    [string]$EmbyUrl = $env:EMBY_URL,
    [string]$EmbyApiKey = $env:EMBY_API_KEY
)

# Default values
if (-not $SeerrUrl) { $SeerrUrl = "http://localhost:5055" }
if (-not $EmbyUrl) { $EmbyUrl = "http://localhost:8096" }

# Colors
$Green = "`e[32m"
$Red = "`e[31m"
$Yellow = "`e[33m"
$Blue = "`e[34m"
$Reset = "`e[0m"

function Write-Status {
    param(
        [string]$Status,
        [string]$Message
    )
    
    switch ($Status) {
        "ok" { Write-Host "$Green✓$Reset $Message" }
        "warn" { Write-Host "$Yellow⚠$Reset $Message" }
        "error" { Write-Host "$Red✗$Reset $Message" }
        default { Write-Host "$Blueℹ$Reset $Message" }
    }
}

function Invoke-ApiRequest {
    param(
        [string]$Method = "GET",
        [string]$Url,
        [string]$ApiKey = "",
        [string]$Body = ""
    )
    
    $headers = @{}
    if ($ApiKey) {
        $headers["X-Api-Key"] = $ApiKey
    }
    
    try {
        if ($Method -eq "POST" -and $Body) {
            $headers["Content-Type"] = "application/json"
            return Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers -Body $Body -TimeoutSec 10
        } else {
            return Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers -TimeoutSec 10
        }
    } catch {
        return @{ error = $_.Exception.Message }
    }
}

# Header
Write-Host "$Blue========================================$Reset"
Write-Host "$Blue  Inseerrtion Connection Test Script$Reset"
Write-Host "$Blue========================================$Reset"
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 5) {
    Write-Status "error" "PowerShell 5.0 or higher is required"
    exit 1
}

Write-Status "info" "PowerShell version: $($PSVersionTable.PSVersion)"
Write-Host ""

# Test 1: Check Seerr connectivity
Write-Host "$Blue Test 1: Seerr Connectivity $Reset"
Write-Host "----------------------------"

try {
    $statusUrl = "$SeerrUrl/api/v1/status"
    $statusResponse = Invoke-ApiRequest -Url $statusUrl -ApiKey $SeerrApiKey
    
    if ($statusResponse.version) {
        Write-Status "ok" "Connected to Seerr v$($statusResponse.version)"
        
        if ($statusResponse.updateAvailable) {
            Write-Status "warn" "Update is available for Seerr"
        }
    } else {
        Write-Status "error" "Failed to connect to Seerr at $SeerrUrl"
        if ($statusResponse.error) {
            Write-Status "info" "Error: $($statusResponse.error)"
        }
        exit 1
    }
} catch {
    Write-Status "error" "Failed to connect to Seerr: $_"
    exit 1
}

Write-Host ""

# Test 2: Check Seerr authentication
Write-Host "$Blue Test 2: Seerr Authentication $Reset"
Write-Host "-----------------------------"

if ($SeerrApiKey) {
    try {
        $authUrl = "$SeerrUrl/api/v1/auth/me"
        $authResponse = Invoke-ApiRequest -Url $authUrl -ApiKey $SeerrApiKey
        
        if ($authResponse.id) {
            $username = $authResponse.username -or $authResponse.email -or "unknown"
            Write-Status "ok" "Authenticated as user: $username"
            
            if ($authResponse.userType -eq 4) {
                Write-Status "ok" "User has admin permissions"
            } else {
                Write-Status "warn" "User may not have admin permissions"
            }
        } else {
            Write-Status "error" "Authentication failed - check API key"
        }
    } catch {
        Write-Status "error" "Authentication test failed: $_"
    }
} else {
    Write-Status "warn" "Skipping auth test - no API key provided"
}

Write-Host ""

# Test 3: Test search functionality
Write-Host "$Blue Test 3: Seerr Search $Reset"
Write-Host "--------------------"

try {
    $searchQuery = "inception"
    $searchUrl = "$SeerrUrl/api/v1/search?query=$searchQuery"
    $searchResponse = Invoke-ApiRequest -Url $searchUrl -ApiKey $SeerrApiKey
    
    if ($searchResponse.results) {
        $resultCount = $searchResponse.results.Count
        Write-Status "ok" "Search working - found $resultCount results for '$searchQuery'"
    } else {
        Write-Status "error" "Search returned no results"
    }
} catch {
    Write-Status "error" "Search test failed: $_"
}

Write-Host ""

# Test 4: Check Emby connectivity (if configured)
Write-Host "$Blue Test 4: Emby Connectivity $Reset"
Write-Host "-------------------------"

if ($EmbyApiKey) {
    try {
        $embyStatusUrl = "$EmbyUrl/emby/System/Info"
        $embyResponse = Invoke-ApiRequest -Url $embyStatusUrl -ApiKey $EmbyApiKey
        
        if ($embyResponse.Version) {
            Write-Status "ok" "Connected to Emby v$($embyResponse.Version)"
            
            # Test plugin endpoint
            $pluginUrl = "$EmbyUrl/emby/Inseerrtion/Health"
            $pluginResponse = Invoke-ApiRequest -Url $pluginUrl -ApiKey $EmbyApiKey
            
            if ($pluginResponse.isHealthy -ne $null) {
                Write-Status "ok" "Inseerrtion plugin is responding"
                
                if ($pluginResponse.isSeerrConnected) {
                    Write-Status "ok" "Plugin reports Seerr is connected"
                } else {
                    Write-Status "warn" "Plugin reports Seerr is not connected"
                }
            } else {
                Write-Status "error" "Inseerrtion plugin not responding"
                Write-Status "info" "Make sure the plugin is installed and Emby is restarted"
            }
        } else {
            Write-Status "error" "Failed to connect to Emby"
        }
    } catch {
        Write-Status "error" "Emby connection failed: $_"
    }
} else {
    Write-Status "warn" "Skipping Emby tests - no EMBY_API_KEY provided"
}

Write-Host ""

# Summary
Write-Host "$Blue========================================$Reset"
Write-Host "$Blue  Test Summary$Reset"
Write-Host "$Blue========================================$Reset"

Write-Status "info" "Seerr URL: $SeerrUrl"
Write-Status "info" "Emby URL: $EmbyUrl"

Write-Host ""
Write-Status "ok" "Basic connectivity tests completed!"
Write-Host ""
Write-Host "To test the full plugin functionality:"
Write-Host "  1. Install the plugin DLL in Emby's plugins folder"
Write-Host "  2. Restart Emby server"
Write-Host "  3. Configure the plugin in Dashboard → Plugins → Inseerrtion"
Write-Host "  4. Visit the Request Media page to search and request content"
Write-Host ""
