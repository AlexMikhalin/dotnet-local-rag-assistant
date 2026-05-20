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
dotnet run --project .\src\LocalRag.Console
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
dotnet run --project .\src\LocalRag.Console -- status
```

Index the sample documents:

```powershell
dotnet run --project .\src\LocalRag.Console -- ingest sample-docs
```

Search without generating an answer:

```powershell
dotnet run --project .\src\LocalRag.Console -- search "What does this project use Qdrant for?"
```

Ask a RAG question:

```powershell
dotnet run --project .\src\LocalRag.Console -- ask "What skills does this project show for a portfolio?"
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

## What This Demonstrates

- Document ingestion
- Chunking
- Embeddings
- Vector search
- RAG prompt construction
- Retrieval confidence scoring
- Source attribution
- Local AI workflow without paid cloud APIs
