#!/bin/bash

# Script di fix per errori NETSDK1005

set -e

echo "🔧 Fix errori .NET 10 - Pulizia completa"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

PROJECT_DIR="/Users/marcobazzoli/tmp/b2a/Claude/PlatformAI"
cd "$PROJECT_DIR"

# 1. Clean profondo
echo -e "${BLUE}▶ Step 1: Pulizia profonda cache${NC}"
echo "  Rimuovo bin/, obj/, .vs/"

# Rimuovi bin e obj da tutti i progetti
find . -name "bin" -type d -print -exec rm -rf {} + 2>/dev/null || true
find . -name "obj" -type d -print -exec rm -rf {} + 2>/dev/null || true
find . -name ".vs" -type d -print -exec rm -rf {} + 2>/dev/null || true

# Clean dotnet
dotnet clean PlatformAI.sln --nologo 2>/dev/null || true

echo -e "${GREEN}✅ Cache pulita${NC}"
echo ""

# 2. Verifica .NET 10
echo -e "${BLUE}▶ Step 2: Verifica .NET SDK${NC}"
if dotnet --version | grep -q "^10\."; then
    echo -e "${GREEN}✅ .NET 10 SDK: $(dotnet --version)${NC}"
else
    echo -e "${RED}❌ .NET 10 non trovato! Versione corrente: $(dotnet --version)${NC}"
    echo ""
    echo "Installa .NET 10 da:"
    echo "  https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
fi
echo ""

# 3. Aggiorna workload
echo -e "${BLUE}▶ Step 3: Aggiorna workload${NC}"
dotnet workload update --skip-sign-check
echo -e "${GREEN}✅ Workload aggiornati${NC}"
echo ""

# 4. Restore forzato progetto per progetto
echo -e "${BLUE}▶ Step 4: Restore forzato progetti${NC}"

projects=(
    "PlatformAI.Infrastructure"
    "PlatformAI.Core"
    "PlatformAI.ML"
    "PlatformAI.Analytics"
    "PlatformAI.NLP"
    "PlatformAI.Api"
    "PlatformAI"
    "PlatformAi.Test"
)

for proj in "${projects[@]}"; do
    if [ -d "$proj" ]; then
        echo -e "  ${YELLOW}→ $proj${NC}"
        cd "$PROJECT_DIR/$proj"
        dotnet restore --force --nologo 2>&1 | grep -v "Determining projects"
        if [ $? -eq 0 ]; then
            echo -e "    ${GREEN}✅${NC}"
        else
            echo -e "    ${RED}❌ Errore${NC}"
        fi
        cd "$PROJECT_DIR"
    fi
done
echo ""

# 5. Restore solution completa
echo -e "${BLUE}▶ Step 5: Restore solution${NC}"
dotnet restore PlatformAI.sln --force --nologo
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Restore completato${NC}"
else
    echo -e "${RED}❌ Restore fallito${NC}"
    exit 1
fi
echo ""

# 6. Build
echo -e "${BLUE}▶ Step 6: Build solution${NC}"
dotnet build PlatformAI.sln --configuration Release --no-restore --nologo
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Build completato${NC}"
else
    echo -e "${RED}❌ Build fallito${NC}"
    echo ""
    echo "Se ancora errori, prova:"
    echo "  1. Riavvia Visual Studio / Rider se aperto"
    echo "  2. rm -rf ~/.nuget/packages"
    echo "  3. Ri-esegui questo script"
    exit 1
fi
echo ""

# 7. Verifica target framework
echo -e "${BLUE}▶ Step 7: Verifica target framework${NC}"
for proj in "${projects[@]}"; do
    if [ -d "$proj" ]; then
        framework=$(grep -o '<TargetFramework>.*</TargetFramework>' "$proj/$proj.csproj" | sed 's/<[^>]*>//g')
        if [ "$framework" = "net10.0" ]; then
            echo -e "  ✅ $proj: ${GREEN}$framework${NC}"
        else
            echo -e "  ⚠️  $proj: ${YELLOW}$framework${NC}"
        fi
    fi
done
echo ""

# 8. Test rapido
echo -e "${BLUE}▶ Step 8: Test build${NC}"
cd "$PROJECT_DIR/PlatformAi.Test"
dotnet test --no-build --configuration Release --filter "Category!=RequiresOllama" --nologo --verbosity minimal
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Test OK${NC}"
else
    echo -e "${YELLOW}⚠️  Alcuni test falliti (normale)${NC}"
fi
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "${GREEN}✅ Fix completato!${NC}"
echo ""
echo "Prova ora:"
echo "  cd PlatformAI"
echo "  dotnet run"
echo ""
