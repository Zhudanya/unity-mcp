"""Tests for editor_ui_automation MCP tool."""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

# Mock the imports that require Unity connection
mock_context = MagicMock()
mock_context.session = MagicMock()


@pytest.fixture
def mock_unity_instance():
    return {"id": "test-instance", "name": "TestUnity", "port": 8080}


@pytest.fixture
def mock_send():
    with patch(
        "services.tools.editor_ui_automation.send_with_unity_instance"
    ) as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch(
        "services.tools.editor_ui_automation.get_unity_instance_from_context"
    ) as mock:
        mock.return_value = {
            "id": "test-instance",
            "name": "TestUnity",
            "port": 8080,
        }
        yield mock


class TestEditorUIAutomationParams:
    """Test parameter building and validation."""

    @pytest.mark.asyncio
    async def test_snapshot_basic_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="snapshot",
        )

        mock_send.assert_called_once()
        call_args = mock_send.call_args
        params = call_args[0][3]  # 4th positional arg is params dict
        assert params["action"] == "snapshot"
        assert "windowFilter" not in params
        assert "maxDepth" not in params

    @pytest.mark.asyncio
    async def test_snapshot_with_filter(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="snapshot",
            window_filter="Inspector",
            max_depth=5,
            include_styles=True,
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["windowFilter"] == "Inspector"
        assert params["maxDepth"] == 5
        assert params["includeStyles"] is True

    @pytest.mark.asyncio
    async def test_click_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="click",
            ref="@e3",
            click_count=2,
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["action"] == "click"
        assert params["ref"] == "@e3"
        assert params["clickCount"] == 2

    @pytest.mark.asyncio
    async def test_type_text_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="type",
            ref="@e5",
            text="Hello World",
            clear=True,
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["action"] == "type"
        assert params["ref"] == "@e5"
        assert params["text"] == "Hello World"
        assert params["clear"] is True

    @pytest.mark.asyncio
    async def test_type_keys_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="type",
            ref="@e5",
            keys=["Ctrl+S", "Enter"],
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["keys"] == ["Ctrl+S", "Enter"]

    @pytest.mark.asyncio
    async def test_drag_element_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="drag",
            from_ref="@e1",
            to_ref="@e10",
            steps=15,
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["action"] == "drag"
        assert params["fromRef"] == "@e1"
        assert params["toRef"] == "@e10"
        assert params["steps"] == 15

    @pytest.mark.asyncio
    async def test_drag_asset_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="drag",
            to_ref="@e10",
            asset_path="Assets/Materials/Red.mat",
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["assetPath"] == "Assets/Materials/Red.mat"
        assert params["toRef"] == "@e10"

    @pytest.mark.asyncio
    async def test_focus_window_params(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="focus_window",
            window_title="Inspector",
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert params["action"] == "focus_window"
        assert params["windowTitle"] == "Inspector"

    @pytest.mark.asyncio
    async def test_none_params_excluded(self, mock_send, mock_get_instance):
        """Verify that None parameters are not sent to Unity."""
        from services.tools.editor_ui_automation import editor_ui_automation

        result = await editor_ui_automation(
            ctx=mock_context,
            action="click",
            ref="@e1",
        )

        call_args = mock_send.call_args
        params = call_args[0][3]
        assert "text" not in params
        assert "keys" not in params
        assert "clear" not in params
        assert "fromRef" not in params
        assert "assetPath" not in params


class TestEditorUIAutomationResponses:
    """Test response handling."""

    @pytest.mark.asyncio
    async def test_success_response(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        mock_send.return_value = {
            "success": True,
            "message": "Snapshot captured.",
            "data": {"elementCount": 42, "refMapVersion": 1},
        }

        result = await editor_ui_automation(ctx=mock_context, action="snapshot")
        assert result["success"] is True
        assert result["message"] == "Snapshot captured."
        assert result["data"]["elementCount"] == 42

    @pytest.mark.asyncio
    async def test_error_response(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        mock_send.return_value = {
            "success": False,
            "message": "Reference '@e99' not found.",
        }

        result = await editor_ui_automation(
            ctx=mock_context, action="click", ref="@e99"
        )
        assert result["success"] is False
        assert "@e99" in result["message"]

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.editor_ui_automation import editor_ui_automation

        mock_send.side_effect = Exception("Connection lost")

        result = await editor_ui_automation(ctx=mock_context, action="snapshot")
        assert result["success"] is False
        assert "Connection lost" in result["message"]
