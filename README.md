# Knowledge Chatbot (.NET + Azure OpenAI)

An extensible Retrieval-Augmented Generation (RAG) knowledge chatbot built with ASP.NET Core,
Semantic Kernel, Azure OpenAI, and PostgreSQL + pgvector. Markdown files are the single source
of truth: they are ingested, chunked, enriched, embedded, and retrieved to answer questions.
Facts found in conversations are captured as **candidate knowledge** pending human approval.

---

## Table of Contents

- [Architecture](#architecture)
- [Solution Layout](#solution-layout)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [1. Clone & build](#1-clone--build)
  - [2. Start PostgreSQL + pgvector](#2-start-postgresql--pgvector)
  - [3. Provision Azure OpenAI](#3-provision-azure-openai)
  - [4. Configure the apps](#4-configure-the-apps)
  - [5. Run the API](#5-run-the-api)
  - [6. Run the ingestion worker](#6-run-the-ingestion-worker)
- [How To…](#how-to)
  - [Ingest knowledge](#ingest-knowledge)
  - [Ask a question (chat)](#ask-a-question-chat)
  - [Stream a chat response](#stream-a-chat-response)
  - [Ask the agent (tool-calling)](#ask-the-agent-tool-calling)
  - [Search the knowledge base](#search-the-knowledge-base)
  - [Review & approve learned knowledge](#review--approve-learned-knowledge)
- [Configuration Reference](#configuration-reference)
- [How It Works](#how-it-works)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [Future Enhancements](#future-enhancements)

---

## Architecture

```text
Markdown Files (Git)
        │
        ▼
Ingestion Worker ── Parse ─ Chunk ─ Enrich ─ Embed ─ Persist
        │
        ▼
PostgreSQL + pgvector
        │
        ▼
Chat API ── Intent ─ Retrieve ─ Context ─ LLM ─ Extract
        │
        ▼
Candidate Knowledge  ──(approval)──►  Knowledge Chunk
```

- **Markdown** is authoritative; the database is the derived, queryable store.
- **pgvector** provides semantic (cosine) similarity search.
- **Azure OpenAI** is used only for chat completion and embeddings.
- **User conversations never overwrite official knowledge** — extracted facts require approval.

---

## Solution Layout

| Project | Responsibility |
| --- | --- |
| `Knowledge.Core` | Domain entities, DTOs, and service interfaces (no infrastructure deps). |
| `Knowledge.Ingestion` | Markdown parsing (Markdig), heading-aware chunking, ingestion pipeline. |
| `Knowledge.Infrastructure` | EF Core + pgvector, repository, Azure OpenAI/Semantic Kernel adapters, the Microsoft Agent Framework agent, search, context builder, chat & approval services, DI wiring. |
| `Knowledge.Api` | Minimal-API HTTP endpoints + Swagger. |
| `Knowledge.Worker` | Background service that watches the `knowledge/` folder and syncs the DB. |
| `Knowledge.Tests` | xUnit unit tests. |

```
nemomark/
├── Knowledge.sln
├── knowledge/               # Markdown source of truth (watched by the worker)
│   └── sample.md
├── src/
│   ├── Knowledge.Api/
│   ├── Knowledge.Core/
│   ├── Knowledge.Infrastructure/
│   ├── Knowledge.Ingestion/
│   └── Knowledge.Worker/
└── tests/
    └── Knowledge.Tests/
```

---

## Prerequisites

- [.NET SDK 9.0+](https://dotnet.microsoft.com/download)
- [PostgreSQL 14+](https://www.postgresql.org/) with the [`pgvector`](https://github.com/pgvector/pgvector) extension
- An [Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/) resource with:
  - a **chat** deployment (e.g. `gpt-4o-mini`)
  - an **embedding** deployment (e.g. `text-embedding-3-small`, 1536 dimensions)
- Docker (optional, easiest way to run PostgreSQL + pgvector locally)

---

## Getting Started

### 1. Clone & build

```powershell
git clone <your-repo-url> nemomark
cd nemomark
dotnet build
```

### 2. Start PostgreSQL + pgvector

Pick whichever option fits your environment. The database name used throughout this guide is
`knowledge`.

#### Option A — Docker (quickest)

The official pgvector image ships PostgreSQL with the extension already available:

```powershell
docker run --name knowledge-pg `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=knowledge `
  -p 5432:5432 `
  -d pgvector/pgvector:pg16
```

#### Option B — Windows native install (EDB installer)

1. Download and run the [EDB PostgreSQL installer](https://www.postgresql.org/download/windows/)
   (PostgreSQL 14 or newer). Note the superuser (`postgres`) password you set during setup.
2. Add PostgreSQL's `bin` folder to your `PATH` (e.g. `C:\Program Files\PostgreSQL\16\bin`) so
   `psql` is available, then create the database:

   ```powershell
   createdb -U postgres knowledge
   ```

3. Install pgvector. The simplest route on Windows is the
   [`pgvector` release for your PostgreSQL version](https://github.com/pgvector/pgvector#windows):
   copy the provided `vector.dll`, `vector.control`, and `vector--*.sql` files into your
   PostgreSQL `lib` and `share\extension` folders, or use the
   [StackBuilder](https://www.postgresql.org/download/windows/) add-on if available.

#### Option C — Package managers

```powershell
# Windows (Chocolatey)
choco install postgresql

# macOS (Homebrew) — Homebrew's formula bundles pgvector
brew install postgresql@16 pgvector

# Debian / Ubuntu
sudo apt-get install -y postgresql postgresql-16-pgvector
```

After installing via a package manager, create the database:

```powershell
createdb -U postgres knowledge
```

#### Enable the extension

The application automatically runs `CREATE EXTENSION IF NOT EXISTS vector` and creates the
schema on startup (see `Knowledge:EnsureDatabaseCreated`). If you manage the database yourself,
connect to the `knowledge` database and enable the extension manually:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

### 3. Provision Azure OpenAI

In the Azure portal (or via CLI) create an Azure OpenAI resource and deploy two models:

| Purpose | Example model | Config key |
| --- | --- | --- |
| Chat completion | `gpt-4o-mini` | `AzureOpenAI:ChatDeployment` |
| Embeddings | `text-embedding-3-small` | `AzureOpenAI:EmbeddingDeployment` |

Note the resource **endpoint** and an **API key**. Chat and embeddings can live on **different
resources** — set `AzureOpenAI:ChatEndpoint` / `AzureOpenAI:ChatApiKey` and
`AzureOpenAI:EmbeddingEndpoint` / `AzureOpenAI:EmbeddingApiKey` for each. If a model-specific
endpoint or key is left empty, the shared `AzureOpenAI:Endpoint` / `AzureOpenAI:ApiKey` values are
used as a fallback.

### 4. Configure the apps

Both `Knowledge.Api` and `Knowledge.Worker` read the same configuration keys. Do **not** commit
secrets — prefer [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
or environment variables over editing `appsettings.json`.

Using user secrets (run once per project):

```powershell
cd src/Knowledge.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:KnowledgeDb" "Host=localhost;Port=5432;Database=knowledge;Username=postgres;Password=postgres"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "AzureOpenAI:ChatDeployment" "gpt-4o-mini"
dotnet user-secrets set "AzureOpenAI:EmbeddingDeployment" "text-embedding-3-small"
```

If chat and embeddings live on **separate resources**, set the per-model overrides instead of (or
in addition to) the shared `Endpoint` / `ApiKey`:

```powershell
dotnet user-secrets set "AzureOpenAI:ChatEndpoint" "https://<chat-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ChatApiKey" "<chat-api-key>"
dotnet user-secrets set "AzureOpenAI:EmbeddingEndpoint" "https://<embedding-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:EmbeddingApiKey" "<embedding-api-key>"
```

Repeat for `src/Knowledge.Worker` (or set the same values as environment variables so both apps
pick them up).

> The default `appsettings.json` values point at `localhost` PostgreSQL and a placeholder Azure
> endpoint so the projects build and start; replace them with real values before use.

### 5. Run the API

```powershell
dotnet run --project src/Knowledge.Api
```

Then open Swagger UI (Development environment) at the URL printed in the console, e.g.
`https://localhost:7xxx/swagger`.

### 6. Run the ingestion worker

The worker watches the `knowledge/` directory, performs a full scan on startup, and reacts to
file add/modify/delete/rename events.

```powershell
dotnet run --project src/Knowledge.Worker
```

Drop or edit `.md` files under `knowledge/` and they are ingested automatically. Only chunks
whose content hash changed are reprocessed.

---

## How To…

Examples below use the API base URL `https://localhost:7xxx` — replace with your actual port.

### Ingest knowledge

**Via the worker (recommended):** add/edit Markdown files under `knowledge/`.

**Via the API — a single file:**

```powershell
curl -k -X POST https://localhost:7xxx/api/ingest/file `
  -H "Content-Type: application/json" `
  -d '{ "sourcePath": "docs/badges.md", "markdown": "# Badges\n\nA badge is..." }'
```

**Via the API — a whole directory (server-side path):**

```powershell
curl -k -X POST https://localhost:7xxx/api/ingest/directory `
  -H "Content-Type: application/json" `
  -d '{ "directory": "C:/Users/mahop/repos/nemomark/knowledge" }'
```

**Remove a document:**

```powershell
curl -k -X DELETE "https://localhost:7xxx/api/ingest/file?sourcePath=docs/badges.md"
```

### Ask a question (chat)

```powershell
curl -k -X POST https://localhost:7xxx/api/chat `
  -H "Content-Type: application/json" `
  -d '{ "message": "What is a badge issuer?" }'
```

Response:

```json
{
  "conversationId": "9f1c...",
  "answer": "A badge issuer is ...",
  "intent": { "intent": "Question", "confidence": 0.96 },
  "citations": [
    { "chunkId": "…", "documentTitle": "Badges", "heading": "Issuer", "score": 0.82 }
  ]
}
```

Pass the returned `conversationId` on subsequent requests to continue the conversation with
history:

```powershell
curl -k -X POST https://localhost:7xxx/api/chat `
  -H "Content-Type: application/json" `
  -d '{ "conversationId": "9f1c...", "message": "And who can verify it?" }'
```

### Stream a chat response

```powershell
curl -k -N -X POST https://localhost:7xxx/api/chat/stream `
  -H "Content-Type: application/json" `
  -d '{ "message": "Explain the ingestion pipeline." }'
```

Tokens are streamed as `text/plain` as they are produced.

### Ask the agent (tool-calling)

The `/api/agent` endpoints are backed by a **Microsoft Agent Framework** agent. Instead of the
fixed RAG pipeline used by `/api/chat`, the agent is given a `search_knowledge` tool and decides
when to query the knowledge base to ground its answer (function calling).

```powershell
curl -k -X POST https://localhost:7xxx/api/agent `
  -H "Content-Type: application/json" `
  -d '{ "message": "How does ingestion detect changed chunks?" }'
```

Streaming:

```powershell
curl -k -N -X POST https://localhost:7xxx/api/agent/stream `
  -H "Content-Type: application/json" `
  -d '{ "message": "Summarize the learning pipeline." }'
```

### Search the knowledge base

Vector similarity search without generating an answer:

```powershell
curl -k "https://localhost:7xxx/api/search?q=embedding%20model"
```

### Review & approve learned knowledge

Facts extracted from conversations are stored as **pending** candidate knowledge. They are never
merged automatically.

**List pending candidates:**

```powershell
curl -k https://localhost:7xxx/api/candidates
```

**Approve** (promotes the fact into an official knowledge chunk):

```powershell
curl -k -X POST https://localhost:7xxx/api/candidates/{id}/approve
```

**Reject** (archives the candidate):

```powershell
curl -k -X POST https://localhost:7xxx/api/candidates/{id}/reject
```

---

## Configuration Reference

| Key | Default | Description |
| --- | --- | --- |
| `ConnectionStrings:KnowledgeDb` | `Host=localhost;...` | PostgreSQL connection string. |
| `AzureOpenAI:Endpoint` | _(placeholder)_ | Shared/fallback Azure OpenAI endpoint (used when a model-specific endpoint is empty). |
| `AzureOpenAI:ApiKey` | _(empty)_ | Shared/fallback Azure OpenAI API key (used when a model-specific key is empty). |
| `AzureOpenAI:ChatDeployment` | `gpt-4o-mini` | Chat completion deployment name. |
| `AzureOpenAI:ChatEndpoint` | _(empty)_ | Chat resource endpoint; falls back to `AzureOpenAI:Endpoint`. |
| `AzureOpenAI:ChatApiKey` | _(empty)_ | Chat resource API key; falls back to `AzureOpenAI:ApiKey`. |
| `AzureOpenAI:EmbeddingDeployment` | `text-embedding-3-small` | Embedding deployment name. |
| `AzureOpenAI:EmbeddingEndpoint` | _(empty)_ | Embedding resource endpoint; falls back to `AzureOpenAI:Endpoint`. |
| `AzureOpenAI:EmbeddingApiKey` | _(empty)_ | Embedding resource API key; falls back to `AzureOpenAI:ApiKey`. |
| `AzureOpenAI:EmbeddingDimensions` | `1536` | Embedding vector size (must match the model & DB column). |
| `Knowledge:EnsureDatabaseCreated` | `true` | Create schema + pgvector extension on startup. |
| `Knowledge:TopK` | `20` | Chunks retrieved from the vector store. |
| `Knowledge:ContextChunks` | `6` | Chunks kept for the prompt after ranking. |
| `Knowledge:ContextTokenBudget` | `3000` | Approx. token budget for the knowledge block. |
| `Knowledge:HistoryMessages` | `8` | Prior messages included as conversation history. |
| `Knowledge:KnowledgeDirectory` | `knowledge` | Directory watched by the worker. |
| `Knowledge:MinExtractionConfidence` | `0.6` | Minimum confidence to store extracted facts. |

> Changing `EmbeddingDimensions` requires a matching embedding model and a fresh `vector(N)`
> column. Re-create the database (or migrate) if you change it.

---

## How It Works

**Ingestion (`Knowledge.Ingestion`)**

1. `MarkdownParser` (Markdig) splits a document into sections along heading boundaries and
   extracts a title.
2. `HeadingAwareChunker` groups sections into ~300–800 token chunks, splits oversized sections on
   paragraph boundaries, and merges tiny trailing chunks.
3. `IngestionService` enriches each new chunk with AI metadata, generates an embedding, and
   persists it. Unchanged chunks (matched by content hash) are reused, so embeddings/metadata are
   not regenerated.

**Retrieval & chat (`Knowledge.Infrastructure`)**

1. `LlmIntentClassifier` classifies the message (`Question`, `Conversation`, `Feedback`,
   `NewKnowledge`, `Correction`).
2. `KnowledgeSearchService` embeds the question and runs a pgvector cosine-similarity search.
3. `ContextBuilder` de-duplicates chunks, respects the token budget, and assembles the prompt
   (system prompt + knowledge + history + question).
4. `SemanticKernelChatClient` calls Azure OpenAI (supports streaming).
5. `LlmKnowledgeExtractor` inspects the user message; qualifying facts are stored as pending
   `CandidateKnowledge`.

**Approval (`ApprovalService`)** — approving a candidate embeds the fact and appends it as a
chunk under a synthetic "Learned Knowledge" document; rejecting only updates its status.

**Agent (`KnowledgeAgent`, Microsoft Agent Framework)** — built with `Microsoft.Agents.AI` /
`Microsoft.Agents.AI.OpenAI`. An `AIAgent` is created from the Azure OpenAI chat deployment via
`chatClient.AsAIAgent(instructions, name)`. On each run the agent is given a `search_knowledge`
`AIFunction` (bound to the request-scoped `IKnowledgeSearchService`) and chooses when to call it,
so answers are grounded in retrieved snippets with `[n]` citations. Exposed via `/api/agent`.
This complements the deterministic `/api/chat` RAG pipeline with an autonomous, tool-calling
alternative.

---

## Testing

```powershell
dotnet test
```

Unit tests cover the Markdown parser, chunker, context builder, and content hashing (no database
or Azure OpenAI required).

---

## Troubleshooting

| Symptom | Likely cause / fix |
| --- | --- |
| `Connection string 'KnowledgeDb' is not configured.` | Set `ConnectionStrings:KnowledgeDb` (user secrets / env / appsettings). |
| Startup error mentioning `vector` type / extension | pgvector not installed; use the `pgvector/pgvector` image or `CREATE EXTENSION vector`. |
| 401 / auth errors from Azure OpenAI | Wrong `Endpoint`, `ApiKey`, or deployment names. |
| Empty answers / no citations | The knowledge base is empty — ingest Markdown first. |
| Dimension mismatch errors | `AzureOpenAI:EmbeddingDimensions` must match the model and the `vector(N)` column; re-create the DB if changed. |
| Worker not picking up files | Confirm files are under `Knowledge:KnowledgeDirectory` and have a `.md`/`.markdown` extension. |

---

## Future Enhancements

Intentionally not implemented yet (see the plan): GraphRAG / Neo4j, hybrid keyword + vector
search, BM25 & reranking, document versioning UI, multiple collections, per-user private
knowledge, conversation summarization, multi-modal documents, and scheduled re-embedding.
# nemomark
