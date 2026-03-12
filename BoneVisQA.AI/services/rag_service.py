"""
Online pipeline: embed user question (local sentence-transformers), search Supabase via RPC,
fetch image, build multimodal prompt, call Gemini 1.5 Pro, return structured VQA_Response.
"""
import json
import re
from uuid import UUID

from google import genai
from google.genai import types
import requests
from sentence_transformers import SentenceTransformer

from config import (
    GEMINI_API_KEY,
    RAG_MATCH_COUNT,
    RAG_MATCH_THRESHOLD,
    SUPABASE_KEY,
    SUPABASE_URL,
)
from models import CitationItem, VQA_Request, VQA_Response

# Local embedding model (same as document_processor for retrieval consistency)
embedding_model = SentenceTransformer("paraphrase-multilingual-MiniLM-L12-v2")


def _get_supabase_client():
    if not SUPABASE_URL or not SUPABASE_KEY:
        raise ValueError("SUPABASE_URL and SUPABASE_KEY must be set")
    from supabase import create_client
    return create_client(SUPABASE_URL, SUPABASE_KEY)


def _embed_query(text: str) -> list[float]:
    """Embed the user question using local sentence-transformers (no API calls)."""
    return embedding_model.encode(text).tolist()


def _retrieve_chunks(query_embedding: list[float]) -> list[dict]:
    """
    Call Supabase RPC match_document_chunks and return list of dicts
    with id, content, similarity (or equivalent keys from your RPC).
    """
    client = _get_supabase_client()
    r = client.rpc(
        "match_document_chunks",
        {
            "query_embedding": query_embedding,
            "match_threshold": RAG_MATCH_THRESHOLD,
            "match_count": RAG_MATCH_COUNT,
        },
    ).execute()
    if not r.data:
        return []
    return list(r.data)


def _download_image(image_url: str | None) -> tuple[bytes | None, str]:
    """
    Download image from URL. Returns (bytes, mime_type) or (None, "") on failure.
    """
    if not image_url or not image_url.strip():
        return None, ""
    try:
        resp = requests.get(image_url, timeout=15)
        resp.raise_for_status()
        content_type = resp.headers.get("content-type", "image/jpeg").split(";")[0].strip()
        return resp.content, content_type or "image/jpeg"
    except Exception:
        return None, ""


def _build_prompt(
    question: str,
    retrieved_context: list[dict],
    annotated_coords: str | None,
    language: str | None,
) -> str:
    """Build the medical prompt for the LLM."""
    context_block = ""
    if retrieved_context:
        context_block = "## Retrieved reference context (use only to support your answer):\n\n"
        for i, row in enumerate(retrieved_context, 1):
            content = row.get("content", row.get("content_preview", ""))
            context_block += f"[{i}] {content}\n\n"
    coords_note = ""
    if annotated_coords and annotated_coords.strip():
        coords_note = f"\n\nThe user has indicated an annotated region of interest (bounding box or coordinates): {annotated_coords}. Pay special attention to this region when describing findings and differential diagnosis."
    lang_note = " Respond in the same language as the user's question."
    if language and language.strip():
        lang_note = f" Respond in {language}."
    return f"""You are an Expert Radiologist assisting medical students. Answer the following question based on the provided image and reference context. Be concise, educational, and cite the reference numbers [1], [2], etc. when using the retrieved context.
{context_block}
## User question
{question}
{coords_note}
{lang_note}

You must respond with a valid JSON object only, no other text. Use this exact structure:
{{
  "answerText": "Your full educational answer here.",
  "suggestedDiagnosis": "Primary suggested diagnosis.",
  "differentialDiagnoses": "Other differential diagnoses to consider (brief list or paragraph).",
  "citationChunkIds": ["uuid-1", "uuid-2"]
}}
Use the actual chunk IDs from the retrieved context for citationChunkIds (the 'id' of each chunk you used). If no chunks were provided, use an empty array for citationChunkIds."""


def _parse_gemini_json_response(text: str) -> dict:
    """Extract JSON object from model response (handle markdown code blocks)."""
    text = text.strip()
    match = re.search(r"\{[\s\S]*\}", text)
    if match:
        return json.loads(match.group())
    return json.loads(text)


def generate_diagnostic_answer(request: VQA_Request) -> VQA_Response:
    """
    Full RAG + Gemini pipeline: retrieve chunks, optionally load image,
    call Gemini 1.5 Pro with multimodal prompt, return structured VQA_Response.
    """
    if not GEMINI_API_KEY:
        raise ValueError("GEMINI_API_KEY is not set")

    # Step A: Retrieval
    query_embedding = _embed_query(request.question_text)
    retrieved = _retrieve_chunks(query_embedding)
    id_to_similarity: dict[str, float] = {}
    for row in retrieved:
        rid = row.get("id")
        if rid:
            id_to_similarity[str(rid)] = float(row.get("similarity", 0.0))

    # Step B: Image handling
    image_bytes, mime_type = _download_image(request.image_url)

    # Step C: Prompt
    prompt = _build_prompt(
        request.question_text,
        retrieved,
        request.coordinates,
        request.language,
    )

    # Step D: Call Gemini (new google-genai SDK)
    client = genai.Client(api_key=GEMINI_API_KEY)
    if image_bytes and mime_type:
        contents = [
            types.Part.from_text(prompt),
            types.Part.from_bytes(data=image_bytes, mime_type=mime_type),
        ]
    else:
        contents = [types.Part.from_text(prompt)]
    response = client.models.generate_content(
        model="gemini-1.5-pro",
        contents=contents,
        config=types.GenerateContentConfig(response_mime_type="application/json"),
    )
    try:
        response_text = response.text
    except (AttributeError, ValueError):
        response_text = None
        if getattr(response, "candidates", None) and response.candidates and response.candidates[0].content.parts:
            response_text = response.candidates[0].content.parts[0].text
    if not response_text:
        return VQA_Response(
            answer_text="No response generated.",
            suggested_diagnosis=None,
            differential_diagnoses=None,
            citations=[],
        )

    try:
        parsed = _parse_gemini_json_response(response_text)
    except json.JSONDecodeError:
        return VQA_Response(
            answer_text=response_text[:4000] if response_text else "Parse error.",
            suggested_diagnosis=None,
            differential_diagnoses=None,
            citations=[],
        )

    # Build citations from retrieved chunks and citationChunkIds from LLM
    citation_chunk_ids = parsed.get("citationChunkIds") or []
    citations: list[CitationItem] = []
    for row in retrieved:
        chunk_id = row.get("id")
        if not chunk_id:
            continue
        chunk_id_str = str(chunk_id)
        if chunk_id_str in citation_chunk_ids or not citation_chunk_ids:
            similarity = id_to_similarity.get(chunk_id_str, 0.0)
            content = row.get("content", row.get("content_preview", "")) or ""
            citations.append(CitationItem(
                chunk_id=UUID(chunk_id) if isinstance(chunk_id, str) else chunk_id,
                similarity_score=similarity,
                source_text=content[:500] if content else None,
            ))
    if citation_chunk_ids and not citations:
        for cid in citation_chunk_ids:
            try:
                uid = UUID(cid) if isinstance(cid, str) else cid
                citations.append(CitationItem(
                    chunk_id=uid,
                    similarity_score=id_to_similarity.get(str(cid), 0.0),
                    source_text=None,
                ))
            except (ValueError, TypeError):
                pass

    return VQA_Response(
        answer_text=parsed.get("answerText"),
        suggested_diagnosis=parsed.get("suggestedDiagnosis"),
        differential_diagnoses=parsed.get("differentialDiagnoses"),
        citations=citations,
    )
