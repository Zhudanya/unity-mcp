"""Tests for manage_build MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()
mock_context.session = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_build.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_build.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManageBuildParams:

    @pytest.mark.asyncio
    async def test_get_player_settings(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(ctx=mock_context, action="get_player_settings")
        params = mock_send.call_args[0][3]
        assert params["action"] == "get_player_settings"

    @pytest.mark.asyncio
    async def test_set_player_settings(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(
            ctx=mock_context,
            action="set_player_settings",
            company_name="TestCo",
            product_name="TestApp",
            version="2.0.0",
        )
        params = mock_send.call_args[0][3]
        assert params["companyName"] == "TestCo"
        assert params["productName"] == "TestApp"
        assert params["version"] == "2.0.0"

    @pytest.mark.asyncio
    async def test_set_build_scenes(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(
            ctx=mock_context,
            action="set_build_scenes",
            scenes=["Assets/Scenes/Main.unity", "Assets/Scenes/Game.unity"],
        )
        params = mock_send.call_args[0][3]
        assert params["scenes"] == ["Assets/Scenes/Main.unity", "Assets/Scenes/Game.unity"]

    @pytest.mark.asyncio
    async def test_switch_platform(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(ctx=mock_context, action="switch_platform", platform="Android")
        params = mock_send.call_args[0][3]
        assert params["platform"] == "Android"

    @pytest.mark.asyncio
    async def test_build(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(
            ctx=mock_context,
            action="build",
            output_path="Builds/game.apk",
            options=["Development", "AllowDebugging"],
        )
        params = mock_send.call_args[0][3]
        assert params["outputPath"] == "Builds/game.apk"
        assert params["options"] == ["Development", "AllowDebugging"]

    @pytest.mark.asyncio
    async def test_get_scripting_defines(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(
            ctx=mock_context, action="get_scripting_defines", platform="Android"
        )
        params = mock_send.call_args[0][3]
        assert params["platform"] == "Android"

    @pytest.mark.asyncio
    async def test_set_scripting_defines(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(
            ctx=mock_context,
            action="set_scripting_defines",
            defines=["ENABLE_ADS", "DEBUG_MODE"],
        )
        params = mock_send.call_args[0][3]
        assert params["defines"] == ["ENABLE_ADS", "DEBUG_MODE"]

    @pytest.mark.asyncio
    async def test_none_params_excluded(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        await manage_build(ctx=mock_context, action="get_build_settings")
        params = mock_send.call_args[0][3]
        assert "platform" not in params
        assert "scenes" not in params
        assert "outputPath" not in params


class TestManageBuildResponses:

    @pytest.mark.asyncio
    async def test_success_response(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        mock_send.return_value = {
            "success": True,
            "message": "Build succeeded.",
            "data": {"result": "Succeeded"},
        }
        result = await manage_build(ctx=mock_context, action="build", output_path="out.exe")
        assert result["success"] is True

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_build import manage_build
        mock_send.side_effect = Exception("Connection lost")
        result = await manage_build(ctx=mock_context, action="get_build_settings")
        assert result["success"] is False
        assert "Connection lost" in result["message"]
