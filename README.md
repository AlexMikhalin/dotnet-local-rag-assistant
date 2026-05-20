# dotnet-local-rag-assistant

Local-first **RAG (Retrieval-Augmented Generation)** console application built with **C#/.NET**, **Ollama as the local LLM runtime**, and **Qdrant as the vector database**.

The app indexes local `.md` and `.txt` documents, generates embeddings with Ollama, stores vectors and metadata in Qdrant, retrieves relevant context with vector search, and asks a local LLM to answer with grounded source context.

This project demonstrates a practical end-to-end **LLM RAG pipeline in C#** without paid cloud APIs: document ingestion, chunking, embeddings, vector database storage, semantic retrieval, prompt construction, retrieval confidence, and source attribution.

## RAG Pipeline

```text
Local documents
  -> document discovery
  -> text chunking
  -> embeddings via Ollama
  -> vector storage in Qdrant
  -> user question
  -> question embedding
  -> semantic search in Qdrant
  -> retrieved context + source metadata
  -> grounded prompt
  -> local LLM answer via Ollama
  -> retrieval confidence + sources
```

## Tech Stack

- C# / .NET console application
- Ollama as the local LLM runtime for embeddings and chat generation
- Qdrant as the vector database for semantic search
- RAG architecture with retrieval confidence and source attribution
- Docker Compose for local infrastructure
- xUnit tests for ingestion, retrieval, and application orchestration

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
ask "How does this C# project implement a local LLM RAG pipeline with Ollama and Qdrant?"
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
dotnet run --project .\src\LocalRag.Cli -- ask "How does this C# project implement a local LLM RAG pipeline with Ollama and Qdrant?"
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
- Local LLM integration with Ollama
- Embeddings generation
- Vector database integration with Qdrant
- Semantic search over indexed documents
- RAG prompt construction and grounding
- Retrieval confidence scoring
- Source attribution
- Application ports for testable AI/vector infrastructure
- Testable layered architecture
- xUnit unit tests
- Local AI workflow without paid cloud APIs
