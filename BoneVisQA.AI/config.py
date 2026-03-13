"""
Environment-based configuration for BoneVisQA AI service.
"""
from dotenv import load_dotenv
import os

# Load environment variables from a .env file (if present)
load_dotenv()

SUPABASE_URL: str = os.getenv("SUPABASE_URL", "")
SUPABASE_KEY: str = os.getenv("SUPABASE_KEY", "")
GEMINI_API_KEY: str = os.getenv("GEMINI_API_KEY", "")

EMBEDDING_MODEL: str = "models/text-embedding-004"
GEMINI_MODEL: str = "models/gemini-1.5-pro"

CHUNK_SIZE: int = 1000
CHUNK_OVERLAP: int = 200
RAG_MATCH_THRESHOLD: float = 0.5
RAG_MATCH_COUNT: int = 3
