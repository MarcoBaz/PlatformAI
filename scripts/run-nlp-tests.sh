#!/bin/bash

# Script per eseguire test NLP Services
# Gestisce automaticamente i diversi tipi di test

set -e

echo "🧪 PlatformAI - NLP Services Test Runner"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Colori per output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Directory test
TEST_DIR="/Users/marcobazzoli/tmp/b2a/Claude/PlatformAI/PlatformAi.Test"

# Funzione per verificare Ollama
check_ollama() {
    if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Funzione per eseguire test
run_tests() {
    local filter=$1
    local description=$2
    
    echo -e "${BLUE}▶ ${description}${NC}"
    cd "$TEST_DIR"
    
    if dotnet test --filter "$filter" --logger "console;verbosity=normal" --nologo; then
        echo -e "${GREEN}✅ ${description} - PASSED${NC}"
        return 0
    else
        echo -e "${RED}❌ ${description} - FAILED${NC}"
        return 1
    fi
}

# Menu
echo "Seleziona tipo di test da eseguire:"
echo ""
echo "1) Unit Test (veloci, no Ollama) ~2s"
echo "2) Integration Test con DB ~5s"
echo "3) Integration Test con Ollama ~2-3min"
echo "4) Tutti i test ~3min"
echo "5) Performance Test ~5min"
echo "0) Exit"
echo ""
read -p "Scelta [0-5]: " choice

case $choice in
    1)
        run_tests "FullyQualifiedName~LLMServiceTests" "Unit Tests"
        ;;
    2)
        run_tests "FullyQualifiedName~NLPQueryOrchestratorTests" "Integration Tests (DB)"
        ;;
    3)
        if check_ollama; then
            echo -e "${GREEN}✅ Ollama disponibile${NC}"
            run_tests "Category=RequiresOllama" "Integration Tests (Ollama)"
        else
            echo -e "${RED}❌ Ollama non disponibile! Avvia: ollama serve${NC}"
            exit 1
        fi
        ;;
    4)
        cd "$TEST_DIR"
        dotnet test --logger "console;verbosity=normal" --nologo
        ;;
    5)
        if check_ollama; then
            run_tests "FullyQualifiedName~Performance" "Performance Tests"
        else
            echo -e "${RED}❌ Ollama richiesto${NC}"
            exit 1
        fi
        ;;
    0)
        exit 0
        ;;
    *)
        echo -e "${RED}Scelta non valida${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}✅ Completato!${NC}"
