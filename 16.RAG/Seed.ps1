[CmdletBinding()]
param(
    [string[]]$ComposeFile,
    [string]$ProjectName = 'rag-teaching-samples'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $ComposeFile) {
    $ComposeFile = @((Join-Path $PSScriptRoot 'compose.yaml'))
}
$corpus = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'data\corpus.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[string[]]$composeArguments = @('compose', '--project-name', $ProjectName)
foreach ($file in $ComposeFile) {
    $composeArguments += @('--file', (Resolve-Path -LiteralPath $file).Path)
}

function ConvertTo-SqlLiteral([string]$Value) {
    return "E'" + $Value.Replace('\', '\\').Replace("'", "''") + "'"
}

function ConvertTo-CypherLiteral([string]$Value) {
    return "'" + $Value.Replace('\', '\\').Replace("'", "\'").Replace("`r", '\r').Replace("`n", '\n').Replace("`t", '\t') + "'"
}

$sql = @(@'
BEGIN;
CREATE EXTENSION IF NOT EXISTS vector;
CREATE TABLE IF NOT EXISTS rag_sample_documents (
    id text PRIMARY KEY,
    service text NOT NULL,
    title text NOT NULL,
    body text NOT NULL,
    search_vector tsvector GENERATED ALWAYS AS
        (to_tsvector('english', service || ' ' || title || ' ' || body)) STORED
);
CREATE INDEX IF NOT EXISTS rag_sample_documents_search_vector_idx
    ON rag_sample_documents USING GIN (search_vector);
'@)

# Neo4j schema changes must precede the corpus transaction.
$cypher = @(
    'CREATE CONSTRAINT rag_sample_service_name_unique IF NOT EXISTS FOR (service:RagSampleService) REQUIRE service.name IS UNIQUE;',
    'CREATE CONSTRAINT rag_sample_document_id_unique IF NOT EXISTS FOR (document:RagSampleDocument) REQUIRE document.id IS UNIQUE;',
    ':begin'
)

foreach ($document in $corpus.Documents) {
    $id = ConvertTo-SqlLiteral $document.Id
    $service = ConvertTo-SqlLiteral $document.Service
    $title = ConvertTo-SqlLiteral $document.Title
    $text = ConvertTo-SqlLiteral $document.Text
    $sql += @"
INSERT INTO rag_sample_documents (id, service, title, body)
VALUES ($id, $service, $title, $text)
ON CONFLICT (id) DO UPDATE SET
    service = EXCLUDED.service, title = EXCLUDED.title, body = EXCLUDED.body;
"@

    $id = ConvertTo-CypherLiteral $document.Id
    $service = ConvertTo-CypherLiteral $document.Service
    $title = ConvertTo-CypherLiteral $document.Title
    $text = ConvertTo-CypherLiteral $document.Text
    $cypher += @"
MERGE (service:RagSampleService {name: $service})
MERGE (document:RagSampleDocument {id: $id})
SET document.title = $title, document.text = $text
MERGE (document)-[:DESCRIBES]->(service);
"@
}

foreach ($dependency in $corpus.Dependencies) {
    $source = ConvertTo-CypherLiteral $dependency.From
    $target = ConvertTo-CypherLiteral $dependency.To
    $cypher += @"
MATCH (source:RagSampleService {name: $source}), (target:RagSampleService {name: $target})
MERGE (source)-[:DEPENDS_ON]->(target);
"@
}
$sql += 'COMMIT;'
$cypher += ':commit'

$previousOutputEncoding = $OutputEncoding
try {
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    # Expand credentials only inside containers; shell assignments preserve special characters.
    $postgresCommand = 'PGPASSWORD=$POSTGRES_PASSWORD PGCLIENTENCODING=UTF8 exec psql -U rag_demo -d rag_samples -v ON_ERROR_STOP=1 -X --quiet'
    $sql -join "`n" | & docker @composeArguments exec -T postgres sh -c $postgresCommand
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL seeding failed (exit code $LASTEXITCODE)."
    }

    $neo4jCommand = 'NEO4J_PASSWORD=${NEO4J_AUTH#*/} exec cypher-shell -a bolt://localhost:7687 -u neo4j -d neo4j --format plain --non-interactive --fail-fast'
    $cypher -join "`n" | & docker @composeArguments exec -T neo4j sh -c $neo4jCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Neo4j seeding failed (exit code $LASTEXITCODE)."
    }
}
finally {
    $OutputEncoding = $previousOutputEncoding
}

Write-Host "Seeded $($corpus.Documents.Count) documents and $($corpus.Dependencies.Count) dependencies."
