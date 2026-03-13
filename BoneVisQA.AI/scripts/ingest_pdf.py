"""
Standalone PDF ingestion script for BoneVisQA.
Loads a PDF, inserts a document record into Supabase, chunks and embeds the text,
then upserts chunks into document_chunks.
Run from project root: python scripts/ingest_pdf.py --pdf path/to/file.pdf --title "Document Title"
"""
import argparse
import sys
import uuid
from pathlib import Path

# Ensure project root is on path when running script directly
_SCRIPT_DIR = Path(__file__).resolve().parent
_PROJECT_ROOT = _SCRIPT_DIR.parent
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

# Load .env before importing config or services (they use os.getenv)
import dotenv
dotenv.load_dotenv(_PROJECT_ROOT / ".env")

from config import SUPABASE_KEY, SUPABASE_URL
from services.document_processor import process_pdf_to_chunks, upsert_chunks_to_supabase


def main() -> None:
    parser = argparse.ArgumentParser(description="Ingest a medical PDF into Supabase for RAG.")
    parser.add_argument("--pdf", required=True, help="Path to the PDF file (e.g. data/co-xuong-khop.pdf)")
    parser.add_argument("--title", required=True, help="Document title for the documents table")
    args = parser.parse_args()

    pdf_path = Path(args.pdf)
    title = args.title.strip()
    if not title:
        print("Error: Document title cannot be empty.")
        sys.exit(1)
    if not pdf_path.exists():
        print(f"Error: PDF file not found: {pdf_path}")
        sys.exit(1)
    if not pdf_path.is_file():
        print(f"Error: Path is not a file: {pdf_path}")
        sys.exit(1)

    if not SUPABASE_URL or not SUPABASE_KEY:
        print("Error: SUPABASE_URL and SUPABASE_KEY must be set (use .env or environment).")
        sys.exit(1)

    try:
        from supabase import create_client
        client = create_client(SUPABASE_URL, SUPABASE_KEY)
    except Exception as e:
        print(f"Error: Failed to connect to Supabase: {e}")
        sys.exit(1)

    # Step 1: Insert document and get doc_id
    print("Inserting document record into Supabase...")
    try:
        file_path_str = str(pdf_path)
        insert = client.table("documents").insert({
            "title": title,
            "file_path": file_path_str,
        }).execute()
        if not insert.data or len(insert.data) == 0:
            print("Error: Document insert did not return an id.")
            sys.exit(1)
        doc_id = uuid.UUID(insert.data[0]["id"])
        print(f"  Created document id: {doc_id}")
    except Exception as e:
        print(f"Error: Failed to insert document: {e}")
        sys.exit(1)

    # Step 2: Chunk and embed PDF
    print("Chunking PDF and generating embeddings...")
    try:
        chunks = process_pdf_to_chunks(pdf_path)
        print(f"  Produced {len(chunks)} chunks.")
    except FileNotFoundError as e:
        print(f"Error: {e}")
        sys.exit(1)
    except ValueError as e:
        print(f"Error (check GEMINI_API_KEY): {e}")
        sys.exit(1)
    except Exception as e:
        print(f"Error: Failed to process PDF: {e}")
        sys.exit(1)

    if not chunks:
        print("Warning: No chunks produced. Document record was still created.")
        sys.exit(0)

    # Step 3: Chunks already have content, chunk_order, embedding; doc_id is used in Step 4.
    # Step 4: Upsert chunks to Supabase
    print("Uploading chunks to Supabase...")
    try:
        upsert_chunks_to_supabase(doc_id, chunks)
        print("Done!")
    except Exception as e:
        print(f"Error: Failed to upsert chunks: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
