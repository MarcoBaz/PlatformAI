from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    HRFlowable, KeepTogether
)
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_JUSTIFY
from datetime import date

OUTPUT = "/sessions/gallant-vibrant-franklin/mnt/PlatformAI/PlatformAI_AI_Roadmap.pdf"

# ── Palette colori ─────────────────────────────────────────────────────────────
NAVY       = colors.HexColor("#1E3A5F")
BLUE       = colors.HexColor("#2563EB")
LIGHT_BLUE = colors.HexColor("#EFF6FF")
GRAY_BG    = colors.HexColor("#F8FAFC")
GRAY_BORDER= colors.HexColor("#E2E8F0")
GRAY_TEXT  = colors.HexColor("#64748B")
GREEN      = colors.HexColor("#16A34A")
AMBER      = colors.HexColor("#D97706")
WHITE      = colors.white
BLACK      = colors.HexColor("#0F172A")

# ── Documento ─────────────────────────────────────────────────────────────────
doc = SimpleDocTemplate(
    OUTPUT,
    pagesize=A4,
    leftMargin=2.2*cm, rightMargin=2.2*cm,
    topMargin=2.5*cm, bottomMargin=2.5*cm,
    title="PlatformAI – Roadmap Studio AI",
    author="PlatformAI",
)

W = A4[0] - 4.4*cm   # larghezza utile

# ── Stili ─────────────────────────────────────────────────────────────────────
base = getSampleStyleSheet()

def style(name, **kw):
    return ParagraphStyle(name, **kw)

S_COVER_TITLE = style("CoverTitle",
    fontName="Helvetica-Bold", fontSize=28, leading=34,
    textColor=WHITE, alignment=TA_LEFT, spaceAfter=8)

S_COVER_SUB = style("CoverSub",
    fontName="Helvetica", fontSize=13, leading=18,
    textColor=colors.HexColor("#CBD5E1"), alignment=TA_LEFT)

S_COVER_META = style("CoverMeta",
    fontName="Helvetica", fontSize=10,
    textColor=colors.HexColor("#94A3B8"), alignment=TA_LEFT)

S_SECTION = style("Section",
    fontName="Helvetica-Bold", fontSize=15, leading=20,
    textColor=NAVY, spaceBefore=22, spaceAfter=6)

S_STEP_TITLE = style("StepTitle",
    fontName="Helvetica-Bold", fontSize=12, leading=16,
    textColor=NAVY, spaceAfter=4)

S_BODY = style("Body",
    fontName="Helvetica", fontSize=10, leading=15,
    textColor=BLACK, alignment=TA_JUSTIFY, spaceAfter=6)

S_HINT = style("Hint",
    fontName="Helvetica-Oblique", fontSize=9, leading=13,
    textColor=GRAY_TEXT, spaceAfter=4)

S_CODE = style("Code",
    fontName="Courier", fontSize=9, leading=13,
    textColor=colors.HexColor("#1E40AF"),
    backColor=LIGHT_BLUE, spaceAfter=6,
    leftIndent=10, rightIndent=10, borderPadding=6)

S_BULLET = style("Bullet",
    fontName="Helvetica", fontSize=10, leading=15,
    textColor=BLACK, bulletIndent=6, leftIndent=16,
    spaceAfter=3)

S_FOOTER = style("Footer",
    fontName="Helvetica", fontSize=8,
    textColor=GRAY_TEXT, alignment=TA_CENTER)

# ── Funzioni helper ────────────────────────────────────────────────────────────

def hr(color=GRAY_BORDER, width=1):
    return HRFlowable(width="100%", thickness=width, color=color, spaceAfter=10, spaceBefore=4)

def bullet(text):
    return Paragraph(f"<bullet>&bull;</bullet> {text}", S_BULLET)

def step_card(number, title, color_hex, body_paragraphs):
    """Restituisce una KeepTogether con header colorato + corpo."""
    num_color = colors.HexColor(color_hex)
    header_data = [[
        Paragraph(f"<b>{number}</b>", ParagraphStyle("Num",
            fontName="Helvetica-Bold", fontSize=14, textColor=WHITE,
            alignment=TA_CENTER)),
        Paragraph(title, ParagraphStyle("TH",
            fontName="Helvetica-Bold", fontSize=12, textColor=WHITE,
            leading=15)),
    ]]
    header = Table(header_data, colWidths=[1.2*cm, W-1.2*cm])
    header.setStyle(TableStyle([
        ("BACKGROUND", (0,0), (-1,-1), num_color),
        ("VALIGN",     (0,0), (-1,-1), "MIDDLE"),
        ("LEFTPADDING",(0,0), (0,0), 10),
        ("RIGHTPADDING",(0,0),(0,0), 6),
        ("LEFTPADDING",(1,0), (1,0), 8),
        ("TOPPADDING", (0,0), (-1,-1), 8),
        ("BOTTOMPADDING",(0,0),(-1,-1), 8),
        ("ROUNDEDCORNERS", [6,6,0,0]),
    ]))

    body_items = []
    for p in body_paragraphs:
        body_items.append([p])

    body_table = Table(body_items, colWidths=[W])
    body_table.setStyle(TableStyle([
        ("BACKGROUND", (0,0), (-1,-1), GRAY_BG),
        ("LEFTPADDING",(0,0), (-1,-1), 14),
        ("RIGHTPADDING",(0,0),(-1,-1), 14),
        ("TOPPADDING", (0,0), (0,0),   10),
        ("BOTTOMPADDING",(0,-1),(-1,-1), 10),
        ("TOPPADDING", (0,1), (-1,-1), 4),
        ("BOTTOMPADDING",(0,0),(-1,-2), 4),
        ("BOX", (0,0), (-1,-1), 1, colors.HexColor(color_hex)),
        ("ROUNDEDCORNERS", [0,0,6,6]),
    ]))

    return KeepTogether([header, body_table, Spacer(1, 14)])

# ── Cover page ────────────────────────────────────────────────────────────────

def cover_page():
    cover_data = [[
        Paragraph("PlatformAI", S_COVER_TITLE),
        Paragraph("Roadmap per lo Studio dell'Intelligenza Artificiale", S_COVER_SUB),
        Spacer(1, 0.4*cm),
        Paragraph(f"Documento generato il {date.today().strftime('%d %B %Y')}", S_COVER_META),
    ]]
    cover = Table(cover_data, colWidths=[W])
    cover.setStyle(TableStyle([
        ("BACKGROUND", (0,0), (-1,-1), NAVY),
        ("LEFTPADDING",(0,0), (-1,-1), 24),
        ("RIGHTPADDING",(0,0),(-1,-1), 24),
        ("TOPPADDING", (0,0), (-1,-1), 32),
        ("BOTTOMPADDING",(0,-1),(-1,-1), 32),
        ("ROUNDEDCORNERS", [10,10,10,10]),
    ]))
    return [cover, Spacer(1, 0.8*cm)]

# ── Contenuto ─────────────────────────────────────────────────────────────────

story = []

# Cover
story += cover_page()

# Intro
story.append(Paragraph("Contesto del Progetto", S_SECTION))
story.append(hr())
story.append(Paragraph(
    "Il progetto PlatformAI è stato costruito come piattaforma di base per lo studio applicato "
    "dell'intelligenza artificiale in contesti industriali. L'architettura attuale copre già i "
    "concetti fondamentali: streaming SSE con Azure OpenAI, tool calling, training ML incrementale "
    "con checkpoint, sistema multi-tenant e gestione utenti. "
    "Questo documento descrive i prossimi passi consigliati, ordinati per priorità didattica e "
    "impatto pratico sull'applicativo.", S_BODY))
story.append(Spacer(1, 0.3*cm))

# Cosa è già stato fatto
story.append(Paragraph("Funzionalità già implementate", S_SECTION))
story.append(hr())

done = [
    ("AI Assistant", "Chat con LLM via Azure OpenAI, streaming SSE token-by-token, stop streaming."),
    ("Tool Calling", "Il modello chiama autonomamente tool (get_production_data, get_prediction) per rispondere con dati reali."),
    ("Training ML Incrementale", "Checkpoint-based training con ML.NET, configurazione parametrizzata, seed dati produzione."),
    ("Analytics Streaming", "Grafici ApexCharts generati dinamicamente dalla risposta AI, card predizione ML."),
    ("Multi-tenant", "Isolamento dati per tenant, contesto tenant iniettato nel system prompt."),
    ("Gestione Utenti", "CRUD completo con ruoli, abilita/disabilita, reset password."),
]

done_rows = [[
    Paragraph(f"<b>{t}</b>", ParagraphStyle("DoneT", fontName="Helvetica-Bold",
        fontSize=9, textColor=NAVY, leading=13)),
    Paragraph(d, ParagraphStyle("DoneD", fontName="Helvetica",
        fontSize=9, textColor=BLACK, leading=13)),
] for t, d in done]

done_table = Table(done_rows, colWidths=[3.8*cm, W-3.8*cm])
done_table.setStyle(TableStyle([
    ("BACKGROUND", (0,0), (-1,-1), WHITE),
    ("BACKGROUND", (0,0), (0,-1), LIGHT_BLUE),
    ("BOX",        (0,0), (-1,-1), 1, GRAY_BORDER),
    ("INNERGRID",  (0,0), (-1,-1), 0.5, GRAY_BORDER),
    ("LEFTPADDING",(0,0), (-1,-1), 10),
    ("RIGHTPADDING",(0,0),(-1,-1), 10),
    ("TOPPADDING", (0,0), (-1,-1), 7),
    ("BOTTOMPADDING",(0,0),(-1,-1), 7),
    ("VALIGN",     (0,0), (-1,-1), "TOP"),
]))
story.append(done_table)
story.append(Spacer(1, 0.5*cm))

# I prossimi passi
story.append(Paragraph("Prossimi Passi Consigliati", S_SECTION))
story.append(hr())

# Step 1 — RAG
story.append(step_card("1", "RAG – Retrieval-Augmented Generation", "#2563EB", [
    Paragraph(
        "Il passo più impattante per estendere le capacità del modello. Attualmente l'AI risponde "
        "solo con i dati che le vengono passati via tool. Con RAG è possibile caricare documenti "
        "(manuali macchine, SOP, report tecnici) e farglieli \"ricordare\" tramite ricerca vettoriale "
        "semantica.", S_BODY),
    Paragraph("<b>Concetti da studiare:</b>", S_STEP_TITLE),
    bullet("Embedding: come i testi vengono convertiti in vettori numerici"),
    bullet("Vector store: pgvector (PostgreSQL) oppure Qdrant come database vettoriale"),
    bullet("Chunking strategy: come suddividere i documenti in frammenti ottimali"),
    bullet("Similarity search: cosine distance, MMR (Maximum Marginal Relevance)"),
    Spacer(1, 6),
    Paragraph("<b>Integrazione nel progetto:</b>", S_STEP_TITLE),
    Paragraph(
        "Aggiungere un nuovo tool <font name='Courier'>search_documents(query)</font> che il modello "
        "chiama quando ha bisogno di consultare la documentazione. Il tool esegue una ricerca "
        "semantica nel vector store e restituisce i chunk più rilevanti come contesto.", S_BODY),
]))

# Step 2 — Agent loop
story.append(step_card("2", "Agent Loop Multi-Step (ReAct)", "#7C3AED", [
    Paragraph(
        "Oggi il servizio LLMAnalyticsService esegue un singolo ciclo di tool calling. "
        "Il passo successivo è implementare un loop completo: il modello chiama un tool, "
        "osserva il risultato, decide se chiamarne un altro, e continua fino alla risposta finale.",
        S_BODY),
    Paragraph("<b>Esempio di query che lo richiede:</b>", S_STEP_TITLE),
    Paragraph(
        "<i>\"Confronta il rendimento di questa settimana con la stessa settimana del mese scorso "
        "e spiegami perché è diverso\"</i> — richiede: get_data(week_current) → "
        "get_data(week_last_month) → analisi comparativa.", S_HINT),
    Paragraph("<b>Pattern da studiare:</b>", S_STEP_TITLE),
    bullet("ReAct (Reasoning + Acting): il modello ragiona su cosa fare prima di agire"),
    bullet("Tool call loop con FinishReason.ToolCalls già presente nel codice come punto di partenza"),
    bullet("Gestione del contesto: come mantenere la catena di pensiero tra più chiamate"),
]))

# Step 3 — Evals
story.append(step_card("3", "Evaluation Framework (Evals)", "#059669", [
    Paragraph(
        "Senza metriche oggettive non è possibile sapere se una modifica al prompt, al modello "
        "o alla pipeline ha migliorato o peggiorato la qualità delle risposte. "
        "Gli evals sono il fondamento di qualsiasi sviluppo AI serio.", S_BODY),
    Paragraph("<b>Metriche principali da misurare:</b>", S_STEP_TITLE),
    bullet("Faithfulness: la risposta è coerente con i dati forniti come contesto?"),
    bullet("Answer relevancy: la risposta è pertinente alla domanda?"),
    bullet("Hallucination rate: il modello inventa dati che non ha ricevuto?"),
    bullet("Tool call accuracy: il modello chiama il tool giusto con i parametri corretti?"),
    Paragraph("<b>Tool consigliati:</b>", S_STEP_TITLE),
    bullet("Promptfoo: framework open-source per testare prompt con dataset"),
    bullet("RAGAS: libreria Python specializzata per valutare pipeline RAG"),
    Paragraph(
        "Nel progetto PlatformAI i dati di produzione reali costituiscono già un ottimo "
        "ground truth per costruire un dataset di domande/risposte attese.", S_BODY),
]))

# Step 4 — Observability
story.append(step_card("4", "Observability LLM (Tracing)", "#D97706", [
    Paragraph(
        "Ogni chiamata al modello è una black box. Integrare un sistema di tracing permette "
        "di avere visibilità completa su ogni interazione: quali token vengono generati, "
        "quali tool vengono chiamati, dove il modello sbaglia e quanto costa ogni conversazione.",
        S_BODY),
    Paragraph("<b>Cosa monitorare:</b>", S_STEP_TITLE),
    bullet("Latenza per token e latenza totale per risposta"),
    bullet("Costo stimato (token input/output × prezzo del modello)"),
    bullet("Traccia completa: system prompt → tool calls → risposta finale"),
    bullet("Tasso di errore e tipologia di errori del modello"),
    Paragraph("<b>Tool consigliati:</b>", S_STEP_TITLE),
    bullet("LangSmith (LangChain): tracing visuale, replay di conversazioni"),
    bullet("Azure AI Foundry tracing: integrazione nativa con Azure OpenAI"),
    bullet("OpenTelemetry: standard aperto per trace distribuiti"),
]))

# Step 5 — Prompt versioning
story.append(step_card("5", "Prompt Versioning e A/B Testing", "#DC2626", [
    Paragraph(
        "Il system prompt in BuildMessagesWithAnalyticsContext è attualmente hardcoded nel codice. "
        "Estrarlo su database permette di versionarlo, modificarlo senza deploy e confrontare "
        "diverse versioni sulle stesse domande.", S_BODY),
    Paragraph("<b>Architettura suggerita:</b>", S_STEP_TITLE),
    bullet("Tabella PromptTemplate: id, version, name, content, isActive, createdAt"),
    bullet("API per attivare/disattivare versioni senza riavviare il backend"),
    bullet("A/B testing: instradare il 50% delle richieste alla versione A e il 50% alla B"),
    bullet("Dashboard per confrontare metriche tra versioni (usa gli Evals del passo 3)"),
    Paragraph(
        "Questo passo insegna un concetto chiave: il prompt è codice, va versionato e testato "
        "con la stessa disciplina del codice sorgente.", S_BODY),
]))

# Step 6 — Fine-tuning
story.append(step_card("6", "Fine-Tuning del Modello", "#0891B2", [
    Paragraph(
        "Con i dati di produzione accumulati è possibile creare un dataset di conversazioni reali "
        "(domanda + risposta ideale) e fare fine-tuning su Azure OpenAI. "
        "È il passo più avanzato ma anche il più potente per adattare il modello al dominio "
        "industriale specifico.", S_BODY),
    Paragraph("<b>Prerequisiti prima di iniziare:</b>", S_STEP_TITLE),
    bullet("Almeno 50-100 esempi domanda/risposta di alta qualità (meglio 500+)"),
    bullet("Sistema di evals funzionante per misurare il miglioramento post-tuning"),
    bullet("Budget Azure per il training (ordine di grandezza: decine di dollari per job base)"),
    Paragraph("<b>Formato dati per Azure OpenAI fine-tuning:</b>", S_STEP_TITLE),
    Paragraph(
        '{"messages": [{"role": "system", "content": "..."}, '
        '{"role": "user", "content": "..."}, '
        '{"role": "assistant", "content": "..."}]}', S_CODE),
    Paragraph(
        "Il fine-tuning permette di ridurre la lunghezza del system prompt (il modello "
        "ha già interiorizzato le istruzioni), abbassare la latenza e migliorare la qualità "
        "delle risposte dominio-specifiche.", S_BODY),
]))

# Roadmap visuale
story.append(Spacer(1, 0.3*cm))
story.append(Paragraph("Ordine di Implementazione Consigliato", S_SECTION))
story.append(hr())

steps = [
    ("RAG",              "Impatto immediato, risultati visibili",           "#2563EB"),
    ("Agent Loop",       "Sblocca query complesse multi-step",              "#7C3AED"),
    ("Evals",            "Fondamentale, costruirlo presto",                 "#059669"),
    ("Observability",    "Debugging e ottimizzazione continua",             "#D97706"),
    ("Prompt Versioning","Disciplina ingegneristica del prompt",            "#DC2626"),
    ("Fine-Tuning",      "Fase matura, richiede dataset e budget",         "#0891B2"),
]

arrow = "  →  "
chain_parts = []
for i, (name, _, color_hex) in enumerate(steps):
    chain_parts.append((name, color_hex))

# Tabella sequenza
seq_data = []
for i, (name, desc, color_hex) in enumerate(steps):
    num_cell = Paragraph(f"<b>{i+1}</b>", ParagraphStyle("SN",
        fontName="Helvetica-Bold", fontSize=11, textColor=WHITE, alignment=TA_CENTER))
    name_cell = Paragraph(f"<b>{name}</b>", ParagraphStyle("SName",
        fontName="Helvetica-Bold", fontSize=10, textColor=colors.HexColor(color_hex)))
    desc_cell = Paragraph(desc, ParagraphStyle("SDesc",
        fontName="Helvetica", fontSize=9, textColor=GRAY_TEXT))
    seq_data.append([num_cell, name_cell, desc_cell])

seq_table = Table(seq_data, colWidths=[1.0*cm, 4.2*cm, W-5.2*cm])
seq_table.setStyle(TableStyle([
    *[("BACKGROUND", (0,i), (0,i), colors.HexColor(steps[i][2])) for i in range(len(steps))],
    ("BACKGROUND", (1,0), (-1,-1), WHITE),
    ("BOX",        (0,0), (-1,-1), 1, GRAY_BORDER),
    ("INNERGRID",  (0,0), (-1,-1), 0.5, GRAY_BORDER),
    ("LEFTPADDING",(0,0), (-1,-1), 8),
    ("RIGHTPADDING",(0,0),(-1,-1), 10),
    ("TOPPADDING", (0,0), (-1,-1), 8),
    ("BOTTOMPADDING",(0,0),(-1,-1), 8),
    ("VALIGN",     (0,0), (-1,-1), "MIDDLE"),
]))
story.append(seq_table)

story.append(Spacer(1, 0.5*cm))
story.append(Paragraph(
    "I passi 1 e 2 danno risultati visibili subito nell'applicativo. "
    "Il passo 3 (Evals) dovrebbe essere avviato in parallelo appena possibile: "
    "è molto più difficile costruire un sistema di valutazione retroattivamente "
    "quando ci sono già molte versioni del prompt in circolazione. "
    "Fine-tuning è il traguardo finale del percorso.", S_BODY))

# Footer
story.append(Spacer(1, 1*cm))
story.append(hr(GRAY_BORDER))
story.append(Paragraph(
    f"PlatformAI – Documento interno | {date.today().strftime('%d/%m/%Y')} | "
    "Generato automaticamente come supporto allo studio dell'AI",
    S_FOOTER))

# ── Build ──────────────────────────────────────────────────────────────────────
doc.build(story)
print(f"PDF generato: {OUTPUT}")
