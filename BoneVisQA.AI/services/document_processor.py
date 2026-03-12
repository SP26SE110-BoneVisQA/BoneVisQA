"""
Offline pipeline: extract text from medical PDFs, chunk, embed with local sentence-transformers,
and upsert into Supabase document_chunks.
"""
import uuid
from pathlib import Path
from typing import Any

import fitz  # PyMuPDF
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
        }
        rows.append(row)
    if not rows:
        return
    client.table("document_chunks").upsert(rows).execute()
