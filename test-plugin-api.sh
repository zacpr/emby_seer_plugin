#!/bin/bash

# Inseerrtion Plugin API Test Script
# Tests the plugin's REST API endpoints

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
EMBY_URL="${EMBY_URL:-http://localhost:8096}"
EMBY_API_KEY="${EMBY_API_KEY:-}"
EMBY_USER="${EMBY_USER:-}"
EMBY_PASSWORD="${EMBY_PASSWORD:-}"

print_status() {
    local status=$1
    local message=$2
    case $status in
        ok) echo -e "${GREEN}✓${NC} $message" ;;
        warn) echo -e "${YELLOW}⚠${NC} $message" ;;
        error) echo -e "${RED}✗${NC} $message" ;;
        *) echo -e "${BLUE}ℹ${NC} $message" ;;
    esac
}

make_request() {
    local method=$1
    local endpoint=$2
    local data=$3
    local auth_header=""
    
    if [ -n "$EMBY_API_KEY" ]; then
        auth_header="X-Emby-Token: $EMBY_API_KEY"
    elif [ -n "$EMBY_USER" ] && [ -n "$EMBY_PASSWORD" ]; then
        # Use basic auth
        auth_header="Authorization: Basic $(echo -n "$EMBY_USER:$EMBY_PASSWORD" | base64)"
    fi
    
    local url="$EMBY_URL/emby$endpoint"
    
    if [ "$method" = "POST" ] && [ -n "$data" ]; then
        curl -s -X POST -H "$auth_header" -H "Content-Type: application/json" -d "$data" "$url" 2>/dev/null || echo '{"error": "Request failed"}'
    else
        curl -s -X $method -H "$auth_header" "$url" 2>/dev/null || echo '{"error": "Request failed"}'
    fi
}

# Header
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Inseerrtion Plugin API Test${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

if [ -z "$EMBY_API_KEY" ] && ([ -z "$EMBY_USER" ] || [ -z "$EMBY_PASSWORD" ]); then
    print_status "error" "EMBY_API_KEY or EMBY_USER/EMBY_PASSWORD must be set"
    echo ""
    echo "Usage:"
    echo "  EMBY_API_KEY=your_key $0"
    echo "or"
    echo "  EMBY_USER=admin EMBY_PASSWORD=secret $0"
    exit 1
fi

print_status "info" "Emby URL: $EMBY_URL"
echo ""

# Test 1: Health Check
echo -e "${BLUE}Test 1: Health Endpoint${NC}"
echo "------------------------"
HEALTH_RESPONSE=$(make_request "GET" "/Inseerrtion/Health")

if echo "$HEALTH_RESPONSE" | grep -q "isHealthy"; then
    IS_HEALTHY=$(echo "$HEALTH_RESPONSE" | grep -o '"isHealthy":[a-z]*' | cut -d':' -f2)
    IS_SEERR_CONNECTED=$(echo "$HEALTH_RESPONSE" | grep -o '"isSeerrConnected":[a-z]*' | cut -d':' -f2)
    VERSION=$(echo "$HEALTH_RESPONSE" | grep -o '"version":"[^"]*"' | cut -d'"' -f4)
    MESSAGE=$(echo "$HEALTH_RESPONSE" | grep -o '"message":"[^"]*"' | cut -d'"' -f4)
    
    print_status "ok" "Plugin version: $VERSION"
    
    if [ "$IS_HEALTHY" = "true" ]; then
        print_status "ok" "Plugin is healthy"
    else
        print_status "warn" "Plugin is not healthy"
    fi
    
    if [ "$IS_SEERR_CONNECTED" = "true" ]; then
        print_status "ok" "Connected to Seerr"
    else
        print_status "warn" "Not connected to Seerr"
    fi
    
    print_status "info" "Message: $MESSAGE"
else
    print_status "error" "Health endpoint failed"
    print_status "info" "Response: $HEALTH_RESPONSE"
fi

echo ""

# Test 2: Auth/Me Endpoint
echo -e "${BLUE}Test 2: Auth/Me Endpoint${NC}"
echo "-------------------------"
AUTH_ME_RESPONSE=$(make_request "GET" "/Inseerrtion/Auth/Me")

if echo "$AUTH_ME_RESPONSE" | grep -q "success.*true"; then
    SEERR_USER_ID=$(echo "$AUTH_ME_RESPONSE" | grep -o '"seerrUserId":[0-9]*' | cut -d':' -f2)
    SEERR_USERNAME=$(echo "$AUTH_ME_RESPONSE" | grep -o '"seerrUsername":"[^"]*"' | cut -d'"' -f4)
    IS_ADMIN=$(echo "$AUTH_ME_RESPONSE" | grep -o '"isAdmin":[a-z]*' | cut -d':' -f2)
    
    print_status "ok" "User authenticated with Seerr"
    print_status "info" "Seerr User: $SEERR_USERNAME (ID: $SEERR_USER_ID)"
    
    if [ "$IS_ADMIN" = "true" ]; then
        print_status "info" "User is Seerr admin"
    fi
else
    ERROR=$(echo "$AUTH_ME_RESPONSE" | grep -o '"errorMessage":"[^"]*"' | cut -d'"' -f4)
    print_status "warn" "Not authenticated with Seerr: ${ERROR:-Unknown error}"
    print_status "info" "Run POST /Inseerrtion/Auth to link account"
fi

echo ""

# Test 3: Search Endpoint
echo -e "${BLUE}Test 3: Search Endpoint${NC}"
echo "------------------------"
SEARCH_RESPONSE=$(make_request "GET" "/Inseerrtion/Search?Query=batman")

if echo "$SEARCH_RESPONSE" | grep -q "results"; then
    TOTAL_RESULTS=$(echo "$SEARCH_RESPONSE" | grep -o '"totalResults":[0-9]*' | head -1 | cut -d':' -f2)
    print_status "ok" "Search working - $TOTAL_RESULTS total results"
    
    # Show first result
    FIRST_TITLE=$(echo "$SEARCH_RESPONSE" | grep -o '"title":"[^"]*"' | head -1 | cut -d'"' -f4)
    if [ -n "$FIRST_TITLE" ]; then
        print_status "info" "First result: $FIRST_TITLE"
    fi
else
    print_status "error" "Search endpoint failed"
    print_status "info" "Response: $SEARCH_RESPONSE"
fi

echo ""

# Test 4: Get Requests
echo -e "${BLUE}Test 4: Get Requests Endpoint${NC}"
echo "------------------------------"
REQUESTS_RESPONSE=$(make_request "GET" "/Inseerrtion/Request")

if echo "$REQUESTS_RESPONSE" | grep -q "results"; then
    TOTAL_REQUESTS=$(echo "$REQUESTS_RESPONSE" | grep -o '"totalResults":[0-9]*' | head -1 | cut -d':' -f2)
    print_status "ok" "Requests endpoint working"
    print_status "info" "User has $TOTAL_REQUESTS requests"
else
    ERROR=$(echo "$REQUESTS_RESPONSE" | grep -o '"errorMessage":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR" ]; then
        print_status "warn" "Requests endpoint: $ERROR"
    else
        print_status "error" "Requests endpoint failed"
        print_status "info" "Response: $REQUESTS_RESPONSE"
    fi
fi

echo ""

# Test 5: Create Request (dry run - commented out by default)
echo -e "${BLUE}Test 5: Create Request (Simulated)${NC}"
echo "-----------------------------------"
print_status "info" "To test request creation, run:"
print_status "info" "  curl -X POST -H 'X-Emby-Token: $EMBY_API_KEY' \\"
print_status "info" "    -H 'Content-Type: application/json' \\"
print_status "info" "    -d '{\"MediaType\":\"movie\",\"MediaId\":27205}' \\"
print_status "info" "    '$EMBY_URL/emby/Inseerrtion/Request'"

echo ""

# Summary
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  API Test Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""
echo -e "${GREEN}Plugin API tests completed!${NC}"
echo ""
echo "If all tests passed, the plugin is working correctly."
echo "If you see warnings about authentication, link your account:"
echo "  1. Visit Dashboard → Plugins → Inseerrtion → Configure"
echo "  2. Or call POST /Inseerrtion/Auth"
echo ""
