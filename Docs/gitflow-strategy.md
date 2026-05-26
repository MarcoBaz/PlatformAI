# Gitflow & Branching Strategy — PlatformAI

Repository: [MarcoBaz/PlatformAI](https://github.com/MarcoBaz/PlatformAI)  
Azure:      Resource Group `AuraAI` | App Service `IndustrialAI`

---

## Modello di branch

```
main          ←── codice in produzione, protetto
develop       ←── ramo di integrazione feature
feature/*     ←── nuove funzionalità, da develop
release/*     ←── stabilizzazione release, da develop
hotfix/*      ←── fix critici in produzione, da main
bugfix/*      ←── fix non urgenti, da develop
```

### Flusso visivo

```
develop
  │
  ├─── feature/AZ-101-llm-analytics-refactor
  │         │ (PR → develop, squash merge)
  │         ▼
  │       develop  ─── CI gira ad ogni push
  │
  ├─── release/1.2.0
  │         │ (solo bug fix, poi PR → main + tag)
  │         ▼
  │       main  ──→  tag v1.2.0  ──→  deploy IndustrialAI
  │
  └─── hotfix/AZ-202-fix-jwt-null
            │ (PR → main + back-merge → develop)
            ▼
          main  ──→  tag v1.2.1
```

---

## Naming convention

| Tipo    | Pattern                       | Esempio                                    |
|---------|-------------------------------|--------------------------------------------|
| Feature | `feature/AZ-{id}-slug`        | `feature/AZ-101-streaming-conversation`    |
| Release | `release/{semver}`            | `release/2.0.0`                            |
| Hotfix  | `hotfix/AZ-{id}-slug`         | `hotfix/AZ-202-fix-jwt-null`               |
| Bugfix  | `bugfix/AZ-{id}-slug`         | `bugfix/AZ-303-production-data-null-check` |

> Il prefisso `AZ-{id}` collega automaticamente il branch al work item su Azure Boards, abilitando la transizione di stato automatica.

---

## Branch policies (GitHub → Settings → Branches)

### `main` — ramo di produzione

| Policy                          | Valore                                          |
|---------------------------------|-------------------------------------------------|
| Require PR before merging       | ✅ 2 reviewer obbligatori                       |
| Dismiss stale PR approvals      | ✅ (nuovo commit → nuova approvazione richiesta)|
| Require status checks           | ✅ CI pipeline (`azure-pipelines.yml`)          |
| Require conversation resolution | ✅                                              |
| Require linear history          | ✅ (squash o rebase)                            |
| Do not allow bypass             | ✅ (nemmeno gli admin)                          |

### `develop` — ramo di integrazione

| Policy                    | Valore                              |
|---------------------------|-------------------------------------|
| Require PR before merging | ✅ 1 reviewer                       |
| Require status checks     | ✅ CI deve passare                  |

---

## Convenzione commit (Conventional Commits)

```
type(scope): descrizione breve

AB#{work-item-id}

Body opzionale: cosa e perché, non come
```

**Tipi:** `feat` · `fix` · `refactor` · `test` · `ci` · `docs` · `chore`

**Scope suggeriti per PlatformAI:** `analytics` · `nlp` · `ml` · `core` · `infra` · `auth` · `ui`

**Esempi:**
```
feat(analytics): add LLM token usage tracking

AB#101

Tracks prompt and completion tokens per conversation.
Stored in ProductionData.TokenUsage field.
```
```
fix(auth): handle null JWT claim on expired token

AB#202
```

---

## Workflow hotfix (step-by-step)

```bash
# 1. Parti da main aggiornato
git checkout main && git pull origin main
git checkout -b hotfix/AZ-202-fix-jwt-null

# 2. Applica il fix, commit con riferimento al work item
git commit -m "fix(auth): handle null JWT claim on expired token AB#202"

# 3. PR → main (1 reviewer, CI deve passare)
# Dopo il merge:
git tag v1.2.1
git push origin v1.2.1

# 4. Back-merge su develop (evita regressione)
git checkout develop
git merge main --no-ff -m "chore: back-merge hotfix v1.2.1 into develop"
git push origin develop
```

---

## Workflow release (step-by-step)

```bash
# 1. Taglia il branch da develop
git checkout develop && git pull origin develop
git checkout -b release/2.0.0

# 2. Solo bug fix qui (nessuna feature nuova)
# Aggiorna versione in PlatformAI.csproj:
#   <Version>2.0.0</Version>
git commit -m "chore: bump version to 2.0.0"

# 3. PR → main (2 reviewer, CI+SonarQube devono passare)
# Dopo il merge:
git tag v2.0.0 && git push origin v2.0.0

# 4. Back-merge su develop
git checkout develop
git merge release/2.0.0 --no-ff
git push origin develop
```

---

## Integrazione Azure DevOps / GitHub

Anche usando **GitHub** come remote, è possibile collegare Azure DevOps per:

- **Azure Boards** → usa `AB#{id}` nei commit per linkare automaticamente i work item
- **Azure Pipelines** → trigger su `main` e `release/*` tramite GitHub Service Connection
- **Branch policies** → configurabili su GitHub (Settings → Branches → Branch protection rules)

Per il colloquio: GitHub + Azure Pipelines è lo scenario più comune nelle aziende che usano GitHub ma adottano Azure DevOps per CI/CD e Boards.

---

## Stato attuale del repository

| Branch    | Stato      | Note                              |
|-----------|------------|-----------------------------------|
| `main`    | ✅ Esiste  | Collegato a `origin/main` (GitHub)|
| `develop` | ✅ Creato  | Branch di integrazione locale     |

**Prossimi passi:**
1. Push di `develop` su GitHub: `git push origin develop`
2. Configurare Branch Protection Rules su GitHub per `main` e `develop`
3. Collegare GitHub a Azure Pipelines tramite Service Connection
