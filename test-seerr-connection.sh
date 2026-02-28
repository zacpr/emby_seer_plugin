#!/bin/bash

# Inseerrtion Connection Test Script
# Tests connectivity to Seerr instance and validates configuration

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
SEERR_URL="${SEERR_URL:-http://localhost:5055}"
SEERR_API_KEY="${SEERR_API_KEY:-}"
EMBY_URL="${EMBY_URL:-http://localhost:8096}"
EMBY_API_KEY="${EMBY_API_KEY:-}"

# Print header
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Inseerrtion Connection Test Script${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Function to print status
print_status() {
    local status=$1
    local message=$2
    if [ "$status" = "ok" ]; then
        echo -e "${GREEN}✓${NC} $message"
    elif [ "$status" = "warn" ]; then
        echo -e "${YELLOW}⚠${NC} $message"
    elif [ "$status" = "error" ]; then
        echo -e "${RED}✗${NC} $message"
    else
        echo -e "${BLUE}ℹ${NC} $message"
    fi
}

# Function to make HTTP requests
make_request() {
    local method=$1
    local url=$2
    local api_key=$3
    local data=$4
    
    local headers=""
    if [ -n "$api_key" ]; then
        headers="-H X-Api-Key:$api_key"
    fi
    
    if [ "$method" = "POST" ] && [ -n "$data" ]; then
        curl -s -X POST $headers -H "Content-Type: application/json" -d "$data" "$url" 2>/dev/null || echo '{"error": "Connection failed"}'
    else
        curl -s -X $method $headers "$url" 2>/dev/null || echo '{"error": "Connection failed"}'
    fi
}

# Check dependencies
echo -e "${BLUE}Checking dependencies...${NC}"
if command -v curl &> /dev/null; then
    print_status "ok" "curl is installed"
else
    print_status "error" "curl is required but not installed"
    exit 1
fi

if command -v jq &> /dev/null; then
    print_status "ok" "jq is installed"
else
    print_status "warn" "jq is not installed (JSON output will be raw)"
fi

echo ""

# Test 1: Check Seerr connectivity
echo -e "${BLUE}Test 1: Seerr Connectivity${NC}"
echo "----------------------------"

if [ -z "$SEERR_API_KEY" ]; then
    print_status "warn" "SEERR_API_KEY not set - trying public status endpoint"
    STATUS_URL="$SEERR_URL/api/v1/status"
    STATUS_RESPONSE=$(make_request "GET" "$STATUS_URL" "")
else
    STATUS_URL="$SEERR_URL/api/v1/status"
    STATUS_RESPONSE=$(make_request "GET" "$STATUS_URL" "$SEERR_API_KEY")
fi

if echo "$STATUS_RESPONSE" | grep -q "version"; then
    VERSION=$(echo "$STATUS_RESPONSE" | grep -o '"version":"[^"]*"' | cut -d'"' -f4)
    print_status "ok" "Connected to Seerr v$VERSION"
    
    if echo "$STATUS_RESPONSE" | grep -q "updateAvailable.*true"; then
        print_status "warn" "Update is available for Seerr"
    fi
else
    print_status "error" "Failed to connect to Seerr at $SEERR_URL"
    print_status "info" "Response: $STATUS_RESPONSE"
    exit 1
fi

echo ""

# Test 2: Check Seerr authentication
echo -e "${BLUE}Test 2: Seerr Authentication${NC}"
echo "-----------------------------"

if [ -n "$SEERR_API_KEY" ]; then
    AUTH_URL="$SEERR_URL/api/v1/auth/me"
    AUTH_RESPONSE=$(make_request "GET" "$AUTH_URL" "$SEERR_API_KEY")
    
    if echo "$AUTH_RESPONSE" | grep -q "id"; then
        USERNAME=$(echo "$AUTH_RESPONSE" | grep -o '"username":"[^"]*"' | cut -d'"' -f4 2>/dev/null || echo "unknown")
        print_status "ok" "Authenticated as user: $USERNAME"
        
        # Check permissions
        if echo "$AUTH_RESPONSE" | grep -q '"permissions".*1'; then
            print_status "ok" "User has admin permissions"
        else
            print_status "warn" "User may not have admin permissions"
        fi
    else
        print_status "error" "Authentication failed - check API key"
        print_status "info" "Response: $AUTH_RESPONSE"
    fi
else
    print_status "warn" "Skipping auth test - no API key provided"
fi

echo ""

# Test 3: Test search functionality
echo -e "${BLUE}Test 3: Seerr Search${NC}"
echo "--------------------"

SEARCH_QUERY="inception"
SEARCH_URL="$SEERR_URL/api/v1/search?query=$SEARCH_QUERY"
SEARCH_RESPONSE=$(make_request "GET" "$SEARCH_URL" "$SEERR_API_KEY")

if echo "$SEARCH_RESPONSE" | grep -q "results"; then
    RESULT_COUNT=$(echo "$SEARCH_RESPONSE" | grep -o '"results":\[' | wc -l)
    print_status "ok" "Search working - found results for '$SEARCH_QUERY'"
else
    print_status "error" "Search failed"
    print_status "info" "Response: $SEARCH_RESPONSE"
fi

echo ""

# Test 4: Check Emby connectivity (if configured)
echo -e "${BLUE}Test 4: Emby Connectivity${NC}"
echo "-------------------------"

if [ -n "$EMBY_API_KEY" ]; then
    EMBY_STATUS_URL="$EMBY_URL/emby/System/Info"
    EMBY_RESPONSE=$(make_request "GET" "$EMBY_STATUS_URL" "$EMBY_API_KEY")
    
    if echo "$EMBY_RESPONSE" | grep -q "Version"; then
        EMBY_VERSION=$(echo "$EMBY_RESPONSE" | grep -o '"Version":"[^"]*"' | cut -d'"' -f4)
        print_status "ok" "Connected to Emby v$EMBY_VERSION"
        
        # Test plugin endpoint
        PLUGIN_URL="$EMBY_URL/emby/Inseerrtion/Health"
        PLUGIN_RESPONSE=$(make_request "GET" "$PLUGIN_URL" "$EMBY_API_KEY")
        
        if echo "$PLUGIN_RESPONSE" | grep -q "isHealthy"; then
            print_status "ok" "Inseerrtion plugin is responding"
            
            if echo "$PLUGIN_RESPONSE" | grep -q "isSeerrConnected.*true"; then
                print_status "ok" "Plugin reports Seerr is connected"
            else
                print_status "warn" "Plugin reports Seerr is not connected"
            fi
        else
            print_status "error" "Inseerrtion plugin not responding"
            print_status "info" "Make sure the plugin is installed and Emby is restarted"
        fi
    else
        print_status "error" "Failed to connect to Emby"
    fi
else
    print_status "warn" "Skipping Emby tests - no EMBY_API_KEY provided"
fi

echo ""

# Summary
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Test Summary${NC}"
echo -e "${BLUE}========================================${NC}"

print_status "info" "Seerr URL: $SEERR_URL"
print_status "info" "Emby URL: $EMBY_URL"

echo ""
echo -e "${GREEN}Basic connectivity tests completed!${NC}"
echo ""
echo "To test the full plugin functionality:"
echo "  1. Install the plugin DLL in Emby's plugins folder"
echo "  2. Restart Emby server"
echo "  3. Configure the plugin in Dashboard → Plugins → Inseerrtion"
echo "  4. Visit the Request Media page to search and request content"
echo ""
