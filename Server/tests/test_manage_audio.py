"""Tests for manage_audio MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_audio.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_audio.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManageAudioParams:

    @pytest.mark.asyncio
    async def test_configure_source(self, mock_send, mock_get_instance):
        from services.tools.manage_audio import manage_audio
        await manage_audio(
            ctx=mock_context, action="configure_source",
            target="BGM", clip="Assets/Audio/Theme.ogg",
            volume=0.6, pitch=1.0, spatial_blend=0.0,
            loop=True, play_on_awake=True,
        )
        params = mock_send.call_args[0][3]
        assert params["clip"] == "Assets/Audio/Theme.ogg"
        assert params["volume"] == 0.6
        assert params["spatialBlend"] == 0.0
        assert params["loop"] is True

    @pytest.mark.asyncio
    async def test_configure_3d_source(self, mock_send, mock_get_instance):
        from services.tools.manage_audio import manage_audio
        await manage_audio(
            ctx=mock_context, action="configure_source",
            target="Enemy", spatial_blend=1.0,
            min_distance=1.0, max_distance=20.0,
            rolloff="Logarithmic",
        )
        params = mock_send.call_args[0][3]
        assert params["spatialBlend"] == 1.0
        assert params["minDistance"] == 1.0
        assert params["rolloff"] == "Logarithmic"

    @pytest.mark.asyncio
    async def test_set_import_settings(self, mock_send, mock_get_instance):
        from services.tools.manage_audio import manage_audio
        await manage_audio(
            ctx=mock_context, action="set_import_settings",
            path="Assets/Audio/Music.ogg",
            load_type="Streaming",
            compression_format="Vorbis",
            quality=0.7,
            force_mono=False,
        )
        params = mock_send.call_args[0][3]
        assert params["path"] == "Assets/Audio/Music.ogg"
        assert params["loadType"] == "Streaming"
        assert params["compressionFormat"] == "Vorbis"
        assert params["quality"] == 0.7

    @pytest.mark.asyncio
    async def test_play(self, mock_send, mock_get_instance):
        from services.tools.manage_audio import manage_audio
        await manage_audio(ctx=mock_context, action="play", target="BGM")
        params = mock_send.call_args[0][3]
        assert params["action"] == "play"
        assert params["target"] == "BGM"

    @pytest.mark.asyncio
    async def test_create_mixer(self, mock_send, mock_get_instance):
        from services.tools.manage_audio import manage_audio
        await manage_audio(
            ctx=mock_context, action="create_mixer",
            path="Assets/Audio/MainMixer.mixer",
        )
        params = mock_send.call_args[0][3]
        assert params["path"] == "Assets/Audio/MainMixer.mixer"

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_audio import manage_audio
        mock_send.side_effect = Exception("Connection lost")
        result = await manage_audio(ctx=mock_context, action="get_info", target="BGM")
        assert result["success"] is False
