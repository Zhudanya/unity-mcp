"""Tests for manage_timeline MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_timeline.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_timeline.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManageTimelineParams:

    @pytest.mark.asyncio
    async def test_create_asset(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="create_asset",
            path="Assets/Timelines/Intro.playable",
            director_target="CutsceneManager",
        )
        params = mock_send.call_args[0][3]
        assert params["path"] == "Assets/Timelines/Intro.playable"
        assert params["directorTarget"] == "CutsceneManager"

    @pytest.mark.asyncio
    async def test_add_track(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="add_track",
            timeline="Assets/Timelines/Intro.playable",
            track_type="AnimationTrack",
            name="CameraTrack",
        )
        params = mock_send.call_args[0][3]
        assert params["trackType"] == "AnimationTrack"
        assert params["name"] == "CameraTrack"

    @pytest.mark.asyncio
    async def test_add_clip(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="add_clip",
            timeline="Assets/Timelines/Intro.playable",
            track_name="CameraTrack",
            start_time=0.0, duration=5.0,
            clip_name="CameraSweep",
        )
        params = mock_send.call_args[0][3]
        assert params["trackName"] == "CameraTrack"
        assert params["startTime"] == 0.0
        assert params["duration"] == 5.0
        assert params["clipName"] == "CameraSweep"

    @pytest.mark.asyncio
    async def test_set_clip_properties(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="set_clip_properties",
            timeline="Assets/Timelines/Intro.playable",
            track_name="CameraTrack", clip_index=0,
            start_time=1.0, duration=3.0, time_scale=0.5,
        )
        params = mock_send.call_args[0][3]
        assert params["clipIndex"] == 0
        assert params["timeScale"] == 0.5

    @pytest.mark.asyncio
    async def test_set_binding(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="set_binding",
            director_target="CutsceneManager",
            track_name="CameraTrack",
            bind_target="MainCamera",
        )
        params = mock_send.call_args[0][3]
        assert params["directorTarget"] == "CutsceneManager"
        assert params["bindTarget"] == "MainCamera"

    @pytest.mark.asyncio
    async def test_get_info(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        mock_send.return_value = {
            "success": True,
            "message": "Timeline info.",
            "data": {"duration": 10.0, "trackCount": 3},
        }
        result = await manage_timeline(
            ctx=mock_context, action="get_info",
            timeline="Assets/Timelines/Intro.playable",
        )
        assert result["success"] is True
        assert result["data"]["trackCount"] == 3

    @pytest.mark.asyncio
    async def test_add_marker(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="add_marker",
            timeline="Assets/Timelines/Intro.playable",
            time=5.0,
        )
        params = mock_send.call_args[0][3]
        assert params["time"] == 5.0

    @pytest.mark.asyncio
    async def test_remove_track(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        await manage_timeline(
            ctx=mock_context, action="remove_track",
            timeline="Assets/Timelines/Intro.playable",
            track_name="OldTrack",
        )
        params = mock_send.call_args[0][3]
        assert params["trackName"] == "OldTrack"

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_timeline import manage_timeline
        mock_send.side_effect = Exception("Timeout")
        result = await manage_timeline(
            ctx=mock_context, action="get_info",
            timeline="Assets/Timelines/test.playable",
        )
        assert result["success"] is False
