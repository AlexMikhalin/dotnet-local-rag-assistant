# dotnet-local-rag-assistant

Local-first RAG console application built with C#, Ollama, and Qdrant.

The app indexes local `.md` and `.txt` documents, stores embeddings in Qdrant, retrieves relevant chunks for a question, and asks a local Ollama model to answer using that context.

## Tech Stack

- .NET console application
- Ollama for local embeddings and chat
- Qdrant vector database
- Docker Compose

## Prerequisites

- .NET SDK
- Docker Desktop
- Ollama

Pull the local models:

```powershell
ollama pull nomic-embed-text
ollama pull llama3.2:3b
```

Start Qdrant:

```powershell
docker compose up -d
```

## Run

Start interactive mode:

```powershell
dotnet run --project .\src\LocalRag.Cli
```

Then type commands such as:

```text
status
ingest sample-docs
ask "What does this project use Qdrant for?"
exit
```

Check local services:

```powershell
dotnet run --project .\src\LocalRag.Cli -- status
```

Index the sample documents:

```powershell
dotnet run --project .\src\LocalRag.Cli -- ingest sample-docs
```

Search without generating an answer:

```powershell
dotnet run --project .\src\LocalRag.Cli -- search "What does this project use Qdrant for?"
```

Ask a RAG question:

```powershell
dotnet run --project .\src\LocalRag.Cli -- ask "What skills does this project show for a portfolio?"
```

Recommended smoke-test question:

```powershell
dotnet run --project .\src\LocalRag.Cli -- ask "What does this project use Ollama and Qdrant for?"
```

## Tests

Run all unit tests:

```powershell
dotnet test
```

## Configuration

You can override defaults with environment variables:

```powershell
$env:OLLAMA_URL = "http://localhost:11434"
$env:QDRANT_URL = "http://localhost:6333"
$env:RAG_EMBED_MODEL = "nomic-embed-text"
$env:RAG_CHAT_MODEL = "llama3.2:3b"
$env:RAG_COLLECTION = "local_rag_documents"
```

## Project Structure

```text
src/
  LocalRag.Cli/                         Console app and interactive loop
  LocalRag.Application/                 Use cases, settings, orchestration
  LocalRag.Ingestion/                   Document discovery and chunking
  LocalRag.Retrieval/                   Prompt building, scored chunks, confidence
  LocalRag.Infrastructure.Ollama/       Ollama HTTP client
  LocalRag.Infrastructure.Qdrant/       Qdrant HTTP client and point model
tests/
  LocalRag.Application.Tests/           Application orchestration tests with fakes
  LocalRag.Ingestion.Tests/             Document discovery and chunking tests
  LocalRag.Retrieval.Tests/             Prompt and confidence scoring tests
```

## What This Demonstrates

- Document ingestion
- Chunking
- Embeddings
- Vector search
- RAG prompt construction
- Retrieval confidence scoring
- Source attribution
- Testable layered architecture
- xUnit unit tests
- Local AI workflow without paid cloud APIs
