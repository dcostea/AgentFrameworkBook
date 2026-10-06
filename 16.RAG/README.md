# Giving Robby evidence before answering

Five .NET 10 console apps explain retrieval-augmented generation (RAG) using the **same eight Robby subsystem documents**. Retrieval stays visible in each `Program.cs`. Every answer goes through a Microsoft Agent Framework `ChatClientAgent`, with retrieved evidence supplied by `RetrievedEvidenceProvider`, an `AIContextProvider`.

All facts and numeric policies are **fictional teaching data**. These apps answer questions about documents; they do not sense hazards, certify safety, approve actual missions, or control hardware.

| Order | Project | New retrieval concept |
|---|---|---|
| 1 | `KeywordRag` | PostgreSQL full-text search through MCP |
| 2 | `VectorRag` | Persistent PostgreSQL + pgvector exact cosine search |
| 3 | `GraphRag` | Curated Neo4j incoming dependency paths through MCP |
| 4 | `KeywordVectorRag` | Local BM25 + pgvector, reciprocal rank fusion (RRF), optional LLM reranking |
| 5 | `KeywordVectorGraphRag` | Add graph evidence to the same hybrid pipeline |

## Retrieve first, then run the agent

```text
question -> explicit retrieval -> print evidence -> save evidence in a fresh AgentSession
         -> RetrievedEvidenceProvider -> ChatClientAgent -> cited answer

keyword: question -> MCP search_keywords -> PostgreSQL tsvector + ts_rank_cd
vector:  question -> MEAI embedding -> PostgreSQL pgvector cosine search
 graph:  service  -> MCP search_graph -> Neo4j incoming DEPENDS_ON*0..2

hybrid:  MCP list_documents -> C# BM25 over the complete tiny corpus
         + pgvector (+ MCP search_graph)
         -> up to 5 candidates per retriever -> RRF -> up to 6 fused candidates
         -> optional LLM reranking -> top 3 evidence documents -> agent
```

`RagShared\RagAnswer.cs` creates `RobbyKnowledgeAgent`, creates a fresh session for each question, attaches the selected hits, and calls `AIAgent.RunAsync`. `RetrievedEvidenceProvider` uses `ProviderSessionState<SearchHit[]>`; it never stores a question's evidence in an instance field. The field holds only the session-state accessor. The provider contributes a separate user-role JSON message, not extra system instructions. Agent instructions ask for `[doc-2]`-style citations and acknowledge insufficient evidence.

A context provider enriches an invocation; it does **not** make the model choose when to search. The official [Agent Framework RAG entrypoint](https://learn.microsoft.com/agent-framework/agents/rag) also documents the built-in `TextSearchProvider`, which accepts a retrieval callback and supports pre-invocation retrieval or on-demand search tools. Here retrieval and fusion already produce the selected evidence before `RagAnswer` runs, so we use framework-supported session data rather than repeat retrieval or capture request-specific hits in a provider constructor or callback. A different design could register search tools for model invocation, but that introduces tool-selection and iteration decisions. This chapter deliberately keeps retrieval centrally controlled. MCP is the transport boundary, not the ranking algorithm. The model never produces executable SQL or Cypher.

Each answer uses a fresh session so prior questions and retrieved evidence do not accumulate. Reusing an agent session would retain chat history, even after replacing the provider's current evidence. Persistent vector storage is not persistent conversation memory or workflow checkpointing.

**Limits:** empty retrieval avoids the answer call deterministically. With nonempty but irrelevant evidence, abstention and citation quality remain model behaviors, not guarantees. JSON and ?treat evidence as data? instructions do not eliminate prompt injection. No citation verifier, authorization layer, calibrated relevance threshold, or hardware safety controller is implemented.

## Prerequisites and versions

- .NET SDK 10; projects target `net10.0`.
- Docker with Linux containers. **Vector-only now requires PostgreSQL with pgvector.** It does not require MCP/Neo4j, although the supplied complete stack runs all three services.
- OpenAI access for answers and embeddings; a JSON-mode-capable chat model for optional reranking.
- Pinned packages: `Microsoft.Agents.AI` **1.23.0**, `Microsoft.Extensions.AI.OpenAI` **10.10.1**, `Npgsql` **10.0.3**, `Pgvector` **0.3.2**, `ModelContextProtocol.Core` **2.2.0**. Configuration and test versions are pinned in their project files.
- Containers: `pgvector/pgvector:0.8.2-pg17`, `neo4j:5-community`, MCP Toolbox **1.13.1**. PostgreSQL/Neo4j major tags can receive image updates; they are not digest locks.

No cloud database or Azure deployment is required. Real OpenAI calls are billable and transmit these synthetic documents, selected evidence, and questions to OpenAI. Do not substitute confidential data without appropriate permission.

## Run from this folder

All commands below assume the working directory is `16.RAG`:

```powershell
Set-Location .\16.RAG  # only when starting at the repository root

dotnet user-secrets set 'OpenAI:ApiKey' '<your-openai-api-key>' --project .\VectorRag
dotnet user-secrets set 'OpenAI:ModelId' 'gpt-4.1-mini' --project .\VectorRag
dotnet user-secrets set 'OpenAI:EmbeddingModelId' 'text-embedding-3-small' --project .\VectorRag
```

All five projects share a `UserSecretsId`. Keep existing values if already configured; there is no need to list or export secrets. The primary application configuration remains `ConfigurationBuilder().AddUserSecrets<Program>()`. `Postgres:ConnectionString` may also be stored in UserSecrets for normal development. Two narrowly scoped, process-environment overrides are supported for isolated tests:

- `RAG_POSTGRES_CONNECTION_STRING`: takes precedence over `Postgres:ConnectionString`.
- `RAG_EMBEDDING_MODEL`: takes precedence over `OpenAI:EmbeddingModelId`; useful when that secret is absent. Use `text-embedding-3-small` for this lesson.

The OpenAI API key and chat model are still read internally from UserSecrets. No test script needs to read them.

### Start a new local stack

Choose a **new project name** and free loopback ports; never reuse another project's database volumes. The default host ports are 15432, 17474, 17687 and 15000. If occupied, use an override as shown below rather than stopping the existing service.

```powershell
$project = 'robby-rag-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString('N')
$env:NEO4J_PASSWORD = [guid]::NewGuid().ToString('N')

# Keep these variables in this shell. Do not print or save the connection string.
$env:RAG_POSTGRES_CONNECTION_STRING = "Host=127.0.0.1;Port=15432;Database=rag_samples;Username=rag_demo;Password=$env:POSTGRES_PASSWORD"
$env:RAG_EMBEDDING_MODEL = 'text-embedding-3-small'

dotnet build .\RAGSamples.slnx
docker compose -p $project -f .\compose.yaml up -d --wait --wait-timeout 240 postgres neo4j
& .\Seed.ps1 -ProjectName $project
docker compose -p $project -f .\compose.yaml up -d toolbox
```

Start the databases, seed the corpus, then start Toolbox. Its log reports `Server ready to serve!`. Verify the actual MCP endpoint, not the removed `/api/toolset` API (which returns HTTP 410 in Toolbox 1.13.1):

```powershell
$initialize = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"robby-readiness","version":"1.0"}}}'
Invoke-RestMethod 'http://localhost:15000/mcp/rag' -Method Post -ContentType 'application/json' -Headers @{ Accept = 'application/json, text/event-stream' } -Body $initialize
```

The response contains the negotiated protocol version and server capabilities. The integration test `HttpToolbox_AdvertisesOnlyTheThreeConfiguredReadTools` additionally verifies real `McpClient.ListToolsAsync` negotiation and the three read-tool schemas.

The local example uses database-owner credentials for setup simplicity. The exposed MCP operations are read-only query templates; the credentials themselves are not a production least-privilege design.

Run one lesson:

```powershell
dotnet run --no-build --project .\KeywordRag
dotnet run --no-build --project .\VectorRag
dotnet run --no-build --project .\GraphRag
dotnet run --no-build --project .\KeywordVectorRag
dotnet run --no-build --project .\KeywordVectorGraphRag
```

An empty question exits. `--retrieve-only` skips final answer generation in all five apps; keyword and graph then need no OpenAI key. Vector apps still call the embedding model. Hybrid `--retrieve-only --rerank` still makes the optional reranking call. Answer output is capped at 1024 tokens; reranking output at 512. Exiting a console does not stop the containers.

```powershell
dotnet run --no-build --project .\KeywordVectorRag -- --rerank
dotnet run --no-build --project .\KeywordVectorGraphRag -- --retrieve-only --rerank
```

### Isolated port override

With Docker Compose 2.24.4+, `!override` replaces port mappings instead of appending them. Save a local override outside source control with four currently free ports, for example:

```yaml
services:
  postgres:
    ports: !override ["127.0.0.1:25432:5432"]
  neo4j:
    ports: !override ["127.0.0.1:27474:7474", "127.0.0.1:27687:7687"]
  toolbox:
    ports: !override ["127.0.0.1:25000:5000"]
    command: !override
      - --config=/app/tools.yaml
      - --address=0.0.0.0
      - --port=5000
      - --disable-reload
      - --allowed-hosts=localhost,127.0.0.1
      - --allowed-origins=http://localhost:25000,http://127.0.0.1:25000
```

These numbers are examples, not assertions that the ports are free. Use the same `-p $project -f .\compose.yaml -f $override` arguments for every Compose operation, and seed with `-ComposeFile @('.\compose.yaml', $override) -ProjectName $project`. Update the connection-string port and set `$env:TOOLBOX_MCP_ENDPOINT = 'http://localhost:25000/mcp/rag'`. Named volumes remain scoped to the unique project. Never specify existing external volumes or `container_name` values.

Stop only the stack you created:

```powershell
docker compose -p $project -f .\compose.yaml down
```

This preserves its data. For a **disposable test stack you created**, adding `--volumes` removes only that project's new named volumes. Include the override when used. Do not run global prune commands or delete other databases. Clear the process-environment credentials after cleanup; they are not written to `.env` files.

## One corpus, eight documents

`data\corpus.json` is embedded in `RagShared` and read by `Seed.ps1`. IDs are stable citations, not array indexes.

| ID | Service | Grounded teaching fact |
|---|---|---|
| doc-1 | NavigationService | Routes have IDs and use a position estimate and inspection waypoints. |
| doc-2 | MotorService | A wheel calibration record expires after seven days; an expired record calls for technician review. |
| doc-3 | SafetyService | A suspicious simulated smoke reading marks the mission for human review. |
| doc-4 | TelemetryService | Failed diagnostic deliveries are retried three times; undelivered messages remain queued. |
| doc-5 | MissionService | Inspection missions reference a map revision and waypoints and can be listed by zone/date. |
| doc-6 | LocalizationService | Position estimates include a timestamp/map revision; older than thirty seconds is marked stale. |
| doc-7 | IdentityService | Technician identity gates dashboard access; recovery is not hardware authorization. |
| doc-8 | MaintenanceService | Reviews use a route ID and technician identity; recording a review does not perform repairs. |

The seven arrows are curated `DEPENDS_ON` relationships, not relationships extracted by an LLM:

```text
NavigationService -> MotorService -> SafetyService
NavigationService -> LocalizationService
NavigationService -> TelemetryService
MissionService -> LocalizationService
MaintenanceService -> NavigationService
MaintenanceService -> IdentityService
```

### Ingestion, updates and persistence

`VectorSearch.IndexAsync(documents)` creates the pgvector extension, a model/dimension settings row and `rag_sample_vectors`. It checks the settings before embedding. It hashes each complete document, embeds only new or changed IDs, and atomically upserts text plus vector. Its return value is the number embedded: first startup normally reports **8**, later unchanged processes **0**. Each query still needs one embedding request. The same generator/model and requested **1536 dimensions** are used for documents and queries; equal dimension alone does not make different models compatible.

The settings row rejects switching embedding models in an existing vector collection. Wrong dimensions, zero vectors and nonfinite components fail rather than silently producing invalid cosine rankings. The client supplies the model identifier corresponding to its configured generator; this is compatibility bookkeeping, not independent verification of a remote model's identity.

The table uses PostgreSQL's `<=>` cosine-distance operator; similarity is `1 - distance`, ordered by distance then stable ID. There is **no HNSW or IVFFlat index**: exact scanning is appropriate for eight rows. The primary-key index supports IDs, not approximate nearest neighbors. At larger scale, choose an ANN index after measuring recall, latency, and memory. Do not call this an ANN demonstration.

`Seed.ps1` upserts PostgreSQL text and Neo4j documents/edges separately. Repeating it does not duplicate rows or relationships. Editing a document requires **rebuild, reseed, and run a vector app** so all three views catch up. There is no distributed transaction across stores. Vector text and embeddings update together, but PostgreSQL FTS and Neo4j can temporarily differ after partial ingestion.

Deletion is intentionally not automatic: removing a JSON document/edge does not delete stored entries; renaming a service can leave old graph associations. Keep service identities stable for incremental examples. For removals, graph restructuring, or changing the embedding model, create a **new empty sample stack** and reseed it. This avoids destructive ?reset everything? logic. Production ingestion would need explicit deletion/reconciliation, chunking, provenance/version control and failure recovery; those are not implemented here.

## Questions that reveal the differences

1. **Keyword:** `calibration`, then `zzzxqvnonexistent93`. PostgreSQL uses English stemming, a stored `tsvector`, GIN, `websearch_to_tsquery`, and `ts_rank_cd`. This is **not BM25**. Normal unquoted terms are ANDed, so verbose questions can miss.
2. **Vector:** `How long before Robby's wheel adjustment record needs another review?` tests semantic retrieval without the literal word ?calibration.? Inspect actual ranking rather than assuming the model finds the right document. `What is Robby's warranty expiration date?` is unsupported: the store still returns nearest neighbors, and the answer should acknowledge missing evidence.
3. **Graph:** `What could be affected if SafetyService is unavailable?` yields SafetyService (0 hops), MotorService (1), NavigationService (2). MaintenanceService is 3 hops away and excluded. The paths indicate possible dependency impact, **not measured outages** or an exhaustive impact analysis. Names are matched case-insensitively; if the question names zero or multiple known services, the console asks for one explicit full service name. No LLM entity extraction or synonym expansion occurs.
4. **Hybrid:** `calibration suspicious` shows local BM25 matching either term across different documents, unlike the PostgreSQL AND query. `When does Robby's wheel calibration record expire?` compares BM25 and vectors.
5. **Three-way:** use the SafetyService question again and inspect each candidate list, RRF list and selected context. A graph hit can still be excluded by later cutoffs.

`Bm25Search` scans all corpus rows returned by MCP `list_documents`: lowercased Unicode letter/number tokens, no stemming or stopword removal, `k1 = 1.2`, `b = 0.75`, and positive-IDF BM25. Even nonmatching documents affect corpus statistics. The token `OR` is ordinary text here, not query syntax.

`RankFusion` deduplicates within each list and sums `1 / (60 + rank)` across lists by stable ID. **Ranks, not raw scores**, are combined. Source labels and graph evidence survive fusion. Optional `LlmReranker` receives the original question and up to six candidate documents; it must return an exact ID permutation. Invalid JSON/IDs fall back to RRF; provider failures propagate. Valid output changes order only, never document text, graph evidence or RRF scores. Reranking is not a factuality or relevance guarantee.

## MCP query boundaries

`tools.yaml` exposes exactly `search_keywords(query, top)`, `list_documents()`, and `search_graph(entity, top)` in toolset `rag`. SQL uses `$1`/`$2` values and Cypher uses `$entity`/`$top`; no arbitrary query-execution tool is registered. Search tool limits are clamped to 1..10. Neo4j follows **incoming** `DEPENDS_ON*0..2`, chooses a shortest deterministic path per document, and emits its chain as evidence. The seed script is a separate trusted-local-corpus administration operation, not a model-callable tool.

`McpSearch` consumes Toolbox JSON row blocks or arrays. Malformed rows and tool errors fail; they are not silently treated as no results. Toolbox 1.13.1's Neo4j connector returns one JSON `null` for an empty graph result, explicitly allowed only on that path.

## Validation

```powershell
dotnet test .\RagSamples.Tests\RagSamples.Tests.csproj --filter 'Category!=Database'
```

Offline tests cover hand-calculated BM25 scores, RRF, parsers, reranking, invalid vectors, empty-evidence abstention, and the actual Agent Framework/provider pipeline with a fake `IChatClient`. They do not prove real-model semantic quality.

After starting/seeding an **isolated** stack and configuring its ports:

```powershell
$env:RUN_DATABASE_TESTS = '1'
$env:RAG_VECTOR_TEST_CONNECTION_STRING = $env:RAG_POSTGRES_CONNECTION_STRING
dotnet test .\RagSamples.Tests\RagSamples.Tests.csproj
```

Database tests negotiate real MCP, verify read-tool schemas, keyword/graph hits, unknown and SQL/Cypher-like input, two-hop boundaries, and real pgvector ranking/upsert/persistence/model and dimension rejection. Vector test embeddings are deterministic, test-only vectors, not claims about OpenAI relevance. Vector tests create and remove uniquely named schemas in the explicitly configured local test database. They do not seed/reset existing stores or start services. Keep test runs separate from interactive vector apps.

| Symptom | Check |
|---|---|
| Docker unavailable or address in use | Start the Linux engine; choose free loopback ports and a new project/override. |
| MCP connection refused | Wait for Toolbox HTTP readiness; verify the `TOOLBOX_MCP_ENDPOINT` path `/mcp/rag`. |
| Missing vector extension / missing tables | Use the pgvector image; seed PostgreSQL; run a vector app to ingest embeddings. |
| Model/dimension mismatch | Keep the original embedding model, or create a new empty sample database. Do not mix models. |
| Missing `Postgres:ConnectionString` | Configure UserSecrets or `RAG_POSTGRES_CONNECTION_STRING`; never print the secret. |
| OpenAI authorization/model failure | Verify configured access internally; no fallback to fabricated answers exists. |
| Keyword returns no documents | Try the short keyword `calibration`; PostgreSQL AND matching differs from local BM25. |
| Unrelated vector neighbors | Nearest does not mean relevant; inspect evidence and unsupported-question abstention. |

References: [Agent Framework context providers](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/context-providers), [Agent Framework 1.23.0 source](https://github.com/microsoft/agent-framework/tree/dotnet-1.23.0), [pgvector .NET](https://github.com/pgvector/pgvector-dotnet), [pgvector exact and approximate search](https://github.com/pgvector/pgvector), [MCP Toolbox](https://github.com/googleapis/mcp-toolbox), [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk).
