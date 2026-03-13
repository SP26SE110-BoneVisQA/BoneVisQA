from fastapi import APIRouter, BackgroundTasks, HTTPException
from pydantic import BaseModel

from models import VQA_Request, VQA_Response
from services.document_processor import process_pdf_from_url, reindex_document
from services.rag_service import generate_diagnostic_answer

router = APIRouter()


class DocumentIngestRequest(BaseModel):
    doc_id: str
    file_url: str


class DocumentIngestResponse(BaseModel):
    status: str
    message: str
    doc_id: str


@router.post("/visual-rag", response_model=VQA_Response)
def visual_rag(request: VQA_Request) -> VQA_Response:
    """
    Visual Q&A: embed question, retrieve chunks from Supabase, optionally load image,
    call OpenRouter Vision LLM, return structured answer with citations.
    """
    try:
        return generate_diagnostic_answer(request)
    except ValueError as e:
        raise HTTPException(status_code=500, detail=f"Configuration or input error: {str(e)}")
    except (ConnectionError, TimeoutError) as e:
        raise HTTPException(status_code=500, detail=f"Supabase or network error: {str(e)}")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"AI pipeline error: {str(e)}")


@router.post("/documents/ingest", response_model=DocumentIngestResponse)
def ingest_document(request: DocumentIngestRequest, background_tasks: BackgroundTasks):
    """
    Ingest a PDF document from URL: download, chunk, embed, and upsert to Supabase.
    Processing happens in background.
    """
    try:
        background_tasks.add_task(
            process_pdf_from_url,
            doc_id=request.doc_id,
            file_url=request.file_url,
        )
        return DocumentIngestResponse(
            status="processing",
            message="Document ingestion started in background.",
            doc_id=request.doc_id,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to start ingestion: {str(e)}")


@router.post("/documents/{doc_id}/reindex", response_model=DocumentIngestResponse)
def reindex_doc(doc_id: str, background_tasks: BackgroundTasks):
    """
    Re-index an existing document: delete old chunks and re-process.
    """
    try:
        background_tasks.add_task(reindex_document, doc_id=doc_id)
        return DocumentIngestResponse(
            status="processing",
            message="Document reindexing started in background.",
            doc_id=doc_id,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to start reindexing: {str(e)}")
