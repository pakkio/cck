"""Discovers host/port for Blender bridge and Unity connector.

Precedence: env var > hardcoded default. Mirrors mcp-blender's config.py.
"""

import os
from pathlib import Path

DEFAULT_BLENDER_HOST = "127.0.0.1"
DEFAULT_BLENDER_PORT = 9876
DEFAULT_UNITY_PORT = 8090

ENV_BLENDER_HOST = "MCP_BLENDER_HOST"
ENV_BLENDER_PORT = "MCP_BLENDER_PORT"
ENV_UNITY_PORT = "MCP_PIPELINE_UNITY_PORT"


def parse_env_text(text: str) -> dict[str, str]:
    """Parse simple KEY=VALUE lines from .env file text into a dict."""
    values: dict[str, str] = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        if key:
            values[key] = value
    return values


def load_dotenv() -> None:
    """Load .env from standard locations into os.environ (non-destructive)."""
    candidates = [
        Path("~/.mcp-pipeline/.env").expanduser(),
        Path("~/.mcp-blender/.env").expanduser(),
        Path(".env"),
    ]
    for path in candidates:
        if not path.exists():
            continue
        for key, value in parse_env_text(path.read_text()).items():
            os.environ.setdefault(key, value)


def resolve_host_port() -> tuple[str, int]:
    """Return (host, port) for the Blender bridge."""
    host = os.environ.get(ENV_BLENDER_HOST, DEFAULT_BLENDER_HOST)
    port_str = os.environ.get(ENV_BLENDER_PORT, str(DEFAULT_BLENDER_PORT))
    try:
        port = int(port_str)
    except ValueError:
        port = DEFAULT_BLENDER_PORT
    return host, port


def resolve_unity_port() -> int:
    """Return the Unity MCP connector port."""
    port_str = os.environ.get(ENV_UNITY_PORT, str(DEFAULT_UNITY_PORT))
    try:
        return int(port_str)
    except ValueError:
        return DEFAULT_UNITY_PORT
