#!/bin/bash

# Script di aggiornamento a .NET 10
# Verifica, pulisce e rebuilda tutti i progetti

set -e

echo "🔄 PlatformAI - Aggiornamento a .NET 10"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

PROJECT_DIR="/Users/marcobazzoli/tmp/b2a/Claude/PlatformAI"

cd "$PROJECT_DIR"

# 1. Verifica .NET 10 installato
echo -e "${BLUE}▶ Step 1: Verifica .NET 10${NC}"
if dotnet --list-sdks | grep -q "10.0"; then
    echo -e "${GREEN}✅ .NET 10 SDK trovato${NC}"
    dotnet --version
else
    echo -e "${YELLOW}⚠️  .NET 10 SDK non trovato!${NC}"
    echo ""
    echo "Installa .NET 10 da:"
    echo "  https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
fi

echo ""

# 2. Clean
echo -e "${BLUE}▶ Step 2: Clean solution${NC}"
dotnet clean PlatformAI.sln --nologo
echo -e "${GREEN}✅ Clean completato${NC}"
echo ""

# 3. Restore
echo -e "${BLUE}▶ Step 3: Restore packages${NC}"
dotnet restore PlatformAI.sln --nologo
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Restore completato${NC}"
else
    echo -e "${YELLOW}⚠️  Restore fallito - controlla errori sopra${NC}"
    exit 1
fi
echo ""

# 4. Build
echo -e "${BLUE}▶ Step 4: Build solution${NC}"
dotnet build PlatformAI.sln --configuration Release --no-restore --nologo
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Build completato${NC}"
else
    echo -e "${YELLOW}⚠️  Build fallito - controlla errori sopra${NC}"
    exit 1
fi
echo ""

# 5. Test
echo -e "${BLUE}▶ Step 5: Run tests (solo unit, no integration)${NC}"
cd PlatformAi.Test
dotnet test --filter "Category!=Integration&Category!=RequiresOllama" --no-build --configuration Release --nologo
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Test completati${NC}"
else
    echo -e "${YELLOW}⚠️  Alcuni test falliti${NC}"
fi
echo ""

# 6. Verifica progetti
echo -e "${BLUE}▶ Step 6: Verifica target framework${NC}"
cd "$PROJECT_DIR"

for proj in PlatformAI.Core PlatformAI.Infrastructure PlatformAI.ML PlatformAI.Analytics PlatformAI.Api PlatformAI PlatformAI.NLP PlatformAi.Test; do
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
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "${GREEN}✅ Aggiornamento a .NET 10 completato!${NC}"
echo ""
echo "Prossimi passi:"
echo "  1. Verifica che tutto compili: dotnet build"
echo "  2. Esegui test: cd PlatformAi.Test && dotnet test"
echo "  3. Avvia app: cd PlatformAI && dotnet run"
echo ""
