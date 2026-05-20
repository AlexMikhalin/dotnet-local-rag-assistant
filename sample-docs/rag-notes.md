# Local RAG Notes

Retrieval augmented generation is a pattern where an application searches external knowledge before asking a language model to answer.

The indexing step reads documents, splits them into chunks, creates embeddings, and stores the vectors with metadata in a vector database.

The retrieval step embeds the user's question, searches for similar chunks, and sends the best matching context to the language model.

This project uses Ollama for local embeddings and chat completion. It uses Qdrant as the local vector database. The console application is written in C# and targets modern .NET.

Useful portfolio points for this project include document ingestion, embeddings, vector search, Docker, local AI models, prompt construction, and source attribution.
