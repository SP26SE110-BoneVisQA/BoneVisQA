from fastapi import APIRouter, HTTPException

from models import VQA_Request, VQA_Response
from services.rag_service import generate_diagnostic_answer

router = APIRouter()


@router.post("/visual-rag", response_model=VQA_Response)
def visual_rag(request: VQA_Request) -> VQA_Response:
    """
    Visual Q&A: embed question, retrieve chunks from Supabase, optionally load image,
    call Gemini 1.5 Pro, return structured answer with citations.
    """
    try:
        return generate_diagnostic_answer(request)
    except ValueError as e:
        raise HTTPException(status_code=500, detail=f"Configuration or input error: {str(e)}")
    except (ConnectionError, TimeoutError) as e:
        raise HTTPException(status_code=500, detail=f"Supabase or network error: {str(e)}")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"AI pipeline error: {str(e)}")
