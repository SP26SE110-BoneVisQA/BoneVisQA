"""
Environment-based configuration for BoneVisQA AI service.
"""
from dotenv import load_dotenv
import os

# Load environment variables from a .env file (if present)
load_dotenv()

SUPABASE_URL: str = os.getenv("SUPABASE_URL", "")
SUPABASE_KEY: str = os.getenv("SUPABASE_KEY", "")
OPENROUTER_API_KEY: str = os.getenv("OPENROUTER_API_KEY", "")

CHUNK_SIZE: int = 1000
CHUNK_OVERLAP: int = 200
RAG_MATCH_THRESHOLD: float = 0.5
RAG_MATCH_COUNT: int = 3
