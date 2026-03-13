"""
Offline pipeline: extract text from medical PDFs, chunk, embed with local sentence-transformers,
and upsert into Supabase document_chunks.
"""
import io
import tempfile
import uuid
from pathlib import Path
from typing import Any

import fitz  # PyMuPDF
import requests
from sentence_transformers import SentenceTransformer

from config import CHUNK_OVERLAP, CHUNK_SIZE, SUPABASE_KEY, SUPABASE_URL

# Load once at module level (zero-cost local embeddings, 100% stable)
embedding_model = SentenceTransformer("paraphrase-multilingual-MiniLM-L12-v2")


def _chunk_text(text: str, chunk_size: int = CHUNK_SIZE, overlap: int = CHUNK_OVERLAP) -> list[str]:
    """
    Split text into overlapping chunks.
    """
    if not text or not text.strip():
        return []
    chunks: list[str] = []
    start = 0
    text = text.strip()
    while start < len(text):
        end = start + chunk_size
        chunk = text[start:end]
        if chunk.strip():
            chunks.append(chunk.strip())
        start = end - overlap
        if start >= len(text):
            break
    return chunks


def extract_text_from_pdf(pdf_path: str | Path) -> str:
    """
    Extract raw text from a PDF file page by page using PyMuPDF (fitz).
    """
    path = Path(pdf_path)
    if not path.exists():
        raise FileNotFoundError(f"PDF not found: {path}")
    full_text: list[str] = []
    doc = fitz.open(path)
    try:
        for page in doc:
            full_text.append(page.get_text())
    finally:
        doc.close()
    return "\n\n".join(full_text)


def embed_text(text: str) -> list[float]:
    """Generate embedding for text using local sentence-transformers (no API calls)."""
    return embedding_model.encode(text).tolist()


def process_pdf_to_chunks(pdf_path: str | Path) -> list[dict[str, Any]]:
    """
    Extract text from PDF, chunk it, and embed each chunk.
    Returns a list of dicts with keys: content, chunk_order, embedding.
    """
    text = extract_text_from_pdf(pdf_path)
    text_chunks = _chunk_text(text)
    chunks_with_embeddings: list[dict[str, Any]] = []
    for i, content in enumerate(text_chunks):
        embedding = embed_text(content)
        chunks_with_embeddings.append({
            "content": content,
            "chunk_order": i,
            "embedding": embedding,
        })
    return chunks_with_embeddings


def upsert_chunks_to_supabase(
    doc_id: uuid.UUID,
    chunks: list[dict[str, Any]],
) -> None:
    """
    Insert chunk records into Supabase document_chunks table.
    Each chunk has: doc_id, content, chunk_order, embedding.
    """
    if not SUPABASE_URL or not SUPABASE_KEY:
        raise ValueError("SUPABASE_URL and SUPABASE_KEY must be set")
    from supabase import create_client
    client = create_client(SUPABASE_URL, SUPABASE_KEY)
    rows = []
    for c in chunks:
        row = {
            "id": str(uuid.uuid4()),
            "doc_id": str(doc_id),
            "content": c["content"],
            "chunk_order": c["chunk_order"],
            "embedding": c["embedding"],
            "is_flagged": False,
        }
        rows.append(row)
    if not rows:
        return
    client.table("document_chunks").upsert(rows).execute()


def _get_supabase_client():
    """Get Supabase client instance."""
    if not SUPABASE_URL or not SUPABASE_KEY:
        raise ValueError("SUPABASE_URL and SUPABASE_KEY must be set")
    from supabase import create_client
    return create_client(SUPABASE_URL, SUPABASE_KEY)


def _download_pdf_from_url(file_url: str) -> bytes:
    """Download PDF content from URL."""
    resp = requests.get(file_url, timeout=60)
    resp.raise_for_status()
    return resp.content


def _extract_text_from_pdf_bytes(pdf_bytes: bytes) -> str:
    """Extract text from PDF bytes using PyMuPDF."""
    full_text: list[str] = []
    with fitz.open(stream=pdf_bytes, filetype="pdf") as doc:
        for page in doc:
            full_text.append(page.get_text())
    return "\n\n".join(full_text)


def _update_document_status(doc_id: str, status: str) -> None:
    """Update the indexing_status of a document in Supabase."""
    try:
        client = _get_supabase_client()
        client.table("documents").update({"indexing_status": status}).eq("id", doc_id).execute()
    except Exception as e:
        print(f"Warning: Failed to update document status: {e}")


def _delete_existing_chunks(doc_id: str) -> None:
    """Delete all existing chunks for a document."""
    client = _get_supabase_client()
    client.table("document_chunks").delete().eq("doc_id", doc_id).execute()


def process_pdf_from_url(doc_id: str, file_url: str) -> None:
    """
    Full ingestion pipeline for PDF from URL:
    1. Download PDF
    2. Extract text
    3. Chunk and embed
    4. Upsert to Supabase
    5. Update document status
    """
    try:
        _update_document_status(doc_id, "Processing")
        
        pdf_bytes = _download_pdf_from_url(file_url)
        text = _extract_text_from_pdf_bytes(pdf_bytes)
        
        text_chunks = _chunk_text(text)
        chunks_with_embeddings: list[dict[str, Any]] = []
        
        for i, content in enumerate(text_chunks):
            embedding = embed_text(content)
            chunks_with_embeddings.append({
                "content": content,
                "chunk_order": i,
                "embedding": embedding,
            })
        
        _delete_existing_chunks(doc_id)
        upsert_chunks_to_supabase(uuid.UUID(doc_id), chunks_with_embeddings)
        _update_document_status(doc_id, "Completed")
        
        print(f"Successfully processed document {doc_id}: {len(chunks_with_embeddings)} chunks")
        
    except Exception as e:
        print(f"Error processing document {doc_id}: {e}")
        _update_document_status(doc_id, "Failed")
        raise


def reindex_document(doc_id: str) -> None:
    """
    Re-index an existing document:
    1. Fetch document metadata from Supabase
    2. Delete existing chunks
    3. Re-process from stored file_path URL
    """
    try:
        client = _get_supabase_client()
        result = client.table("documents").select("file_path").eq("id", doc_id).single().execute()
        
        if not result.data or not result.data.get("file_path"):
            raise ValueError(f"Document {doc_id} not found or has no file_path")
        
        file_url = result.data["file_path"]
        process_pdf_from_url(doc_id, file_url)
        
    except Exception as e:
        print(f"Error reindexing document {doc_id}: {e}")
        _update_document_status(doc_id, "Failed")
        raise
