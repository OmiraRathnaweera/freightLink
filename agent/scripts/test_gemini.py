from pathlib import Path
from dotenv import load_dotenv
from google import genai
import os

try:
    from google.genai.models import Models, AsyncModels
    Models._logged_afc_warning = True
    AsyncModels._logged_afc_warning = True
except Exception:
    pass

AGENT_ROOT = Path(__file__).resolve().parent.parent
load_dotenv(AGENT_ROOT / ".env")

api_key = os.getenv("GEMINI_API_KEY")
configured_model = os.getenv("GEMINI_MODEL", "gemini-3.5-flash")

print("=" * 60)
print("     FREIGHTLINK — GOOGLE GEMINI API CONNECTIVITY TEST")
print("=" * 60)
print(f"API Key:       {api_key[:8]}...{api_key[-4:] if api_key else 'NONE'}")
print(f"Configured:    {configured_model}")
print("-" * 60)

client = genai.Client(api_key=api_key)

# Test candidate models in order: configured model, gemini-3.5-flash, gemini-3.5-flash-lite
models_to_try = [configured_model]
for fallback in ("gemini-3.5-flash", "gemini-3.5-flash-lite"):
    if fallback not in models_to_try:
        models_to_try.append(fallback)

for model in models_to_try:
    print(f"\nTesting Model: {model}...")
    try:
        response = client.models.generate_content(
            model=model,
            contents="Say hello in one enthusiastic sentence.",
        )
        print(f"  [SUCCESS] -> {response.text.strip()}")
        print(f"\n=> Recommendation: Model '{model}' is active and operational on your key!")
        break
    except Exception as exc:
        err_msg = str(exc)
        if "404" in err_msg or "NOT_FOUND" in err_msg:
            print("  [404 NOT_FOUND] Model retired/deprecated by Google for new users.")
        elif "429" in err_msg or "RESOURCE_EXHAUSTED" in err_msg:
            print("  [429 QUOTA EXCEEDED] Daily limit reached on this specific model.")
        elif "503" in err_msg or "UNAVAILABLE" in err_msg:
            print("  [503 HIGH DEMAND] Google Cloud temporary demand spike.")
        else:
            print(f"  [ERROR] {err_msg[:80]}...")

print("=" * 60)