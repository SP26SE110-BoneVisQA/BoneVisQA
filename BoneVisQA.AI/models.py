from typing import Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class VQA_Request(BaseModel):
    """Matches C# VisualQARequestDto (camelCase from .NET)."""
    model_config = ConfigDict(populate_by_name=True)

    student_id: UUID = Field(alias="studentId")
    question_text: str = Field(alias="questionText")
    image_url: Optional[str] = Field(None, alias="imageUrl")
    coordinates: Optional[str] = Field(None, alias="coordinates")
    case_id: Optional[UUID] = Field(None, alias="caseId")
    annotation_id: Optional[UUID] = Field(None, alias="annotationId")
    language: Optional[str] = Field(None, alias="language")


class CitationItem(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    chunk_id: UUID = Field(alias="chunkId")
    similarity_score: float = Field(alias="similarityScore")
    source_text: Optional[str] = Field(None, alias="sourceText")


class VQA_Response(BaseModel):
    """Matches C# VisualQAResponseDto (camelCase for .NET client)."""
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    answer_text: Optional[str] = Field(None, alias="answerText")
    suggested_diagnosis: Optional[str] = Field(None, alias="suggestedDiagnosis")
    differential_diagnoses: Optional[str] = Field(None, alias="differentialDiagnoses")
    citations: list[CitationItem] = Field(default_factory=list, alias="citations")
