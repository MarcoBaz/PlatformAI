#!/bin/bash

set -e

# ─────────────────────────────────────────
# CONFIGURAZIONE
# ─────────────────────────────────────────
FRONTEND_DIR="/Users/marcobazzoli/tmp/Claude/PlatformAI/platformAI-ui"
BACKEND_DIR="/Users/marcobazzoli/tmp/Claude/PlatformAI/PlatformAI"
WWWROOT_DIR="$BACKEND_DIR/wwwroot"
PUBLISH_DIR="$BACKEND_DIR/publish"
ZIP_PATH="$BACKEND_DIR/app.zip"

RESOURCE_GROUP="AuraAI"
APP_NAME="IndustrialAI"

# ─────────────────────────────────────────
# COLORI
# ─────────────────────────────────────────
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
fail() { echo -e "${RED}[✗]${NC} $1"; exit 1; }

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  🚀 Deploy IndustrialAI → Azure"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# ─────────────────────────────────────────
# 1. VERIFICA PREREQUISITI
# ─────────────────────────────────────────
log "Verifica prerequisiti..."
command -v node  >/dev/null 2>&1 || fail "Node.js non trovato"
command -v ng    >/dev/null 2>&1 || fail "Angular CLI non trovato (npm install -g @angular/cli)"
command -v dotnet >/dev/null 2>&1 || fail ".NET SDK non trovato"
command -v az    >/dev/null 2>&1 || fail "Azure CLI non trovato"
command -v zip   >/dev/null 2>&1 || fail "zip non trovato"

# ─────────────────────────────────────────
# 2. BUILD ANGULAR
# ─────────────────────────────────────────
log "Build Angular (production)..."
cd "$FRONTEND_DIR"
ng build --configuration=production || fail "Build Angular fallito"
log "Build Angular completato"

# ─────────────────────────────────────────
# 3. COPIA FILES IN WWWROOT
# ─────────────────────────────────────────
log "Copia files Angular in wwwroot..."
rm -rf "$WWWROOT_DIR"
mkdir -p "$WWWROOT_DIR"
cp -r "$FRONTEND_DIR/dist/fuse/browser/." "$WWWROOT_DIR/"
log "Files copiati in $WWWROOT_DIR"

# ─────────────────────────────────────────
# 4. BUILD BACKEND .NET
# ─────────────────────────────────────────
log "Build e publish backend .NET..."
cd "$BACKEND_DIR"
rm -rf "$PUBLISH_DIR"
dotnet publish -c Release -r linux-x64 --self-contained false -o "$PUBLISH_DIR" || fail "Publish .NET fallito"
log "Publish .NET completato"

# ─────────────────────────────────────────
# 5. CREA ZIP
# ─────────────────────────────────────────
log "Creazione app.zip..."
rm -f "$ZIP_PATH"
cd "$PUBLISH_DIR"
zip -r "$ZIP_PATH" . -x "*.pdb" || fail "Creazione zip fallita"
log "app.zip creato ($(du -sh "$ZIP_PATH" | cut -f1))"

# ─────────────────────────────────────────
# 6. DEPLOY SU AZURE
# ─────────────────────────────────────────
log "Deploy su Azure ($APP_NAME)..."
az webapp deploy \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_NAME" \
    --src-path "$ZIP_PATH" \
    --type zip \
    --async false || fail "Deploy Azure fallito"

# ─────────────────────────────────────────
# 7. PULIZIA
# ─────────────────────────────────────────
log "Pulizia file temporanei..."
rm -rf "$PUBLISH_DIR"
rm -f "$ZIP_PATH"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "  ${GREEN}✅ Deploy completato!${NC}"
echo "  🌐 https://industrialai.azurewebsites.net"
echo "  📖 https://industrialai.azurewebsites.net/swagger"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
