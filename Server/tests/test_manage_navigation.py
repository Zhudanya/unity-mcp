"""Tests for manage_navigation MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_navigation.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_navigation.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManageNavigationParams:

    @pytest.mark.asyncio
    async def test_bake(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        await manage_navigation(
            ctx=mock_context, action="bake",
            agent_radius=0.5, agent_height=2.0, max_slope=45, step_height=0.4,
        )
        params = mock_send.call_args[0][3]
        assert params["agentRadius"] == 0.5
        assert params["agentHeight"] == 2.0
        assert params["maxSlope"] == 45
        assert params["stepHeight"] == 0.4

    @pytest.mark.asyncio
    async def test_test_path(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        await manage_navigation(
            ctx=mock_context, action="test_path",
            start=[0, 0, 0], end=[50, 0, 30],
        )
        params = mock_send.call_args[0][3]
        assert params["start"] == [0, 0, 0]
        assert params["end"] == [50, 0, 30]

    @pytest.mark.asyncio
    async def test_configure_agent(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        await manage_navigation(
            ctx=mock_context, action="configure_agent",
            target="Guard_01", speed=3.5, stopping_distance=1.0,
            area_mask_names="Walkable,Road",
        )
        params = mock_send.call_args[0][3]
        assert params["target"] == "Guard_01"
        assert params["speed"] == 3.5
        assert params["areaMaskNames"] == "Walkable,Road"

    @pytest.mark.asyncio
    async def test_configure_obstacle(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        await manage_navigation(
            ctx=mock_context, action="configure_obstacle",
            target="Barricade", shape="box", size=[2, 1.5, 0.5], carve=True,
        )
        params = mock_send.call_args[0][3]
        assert params["shape"] == "box"
        assert params["carve"] is True

    @pytest.mark.asyncio
    async def test_set_area(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        await manage_navigation(
            ctx=mock_context, action="set_area",
            index=3, name="Water", cost=5.0,
        )
        params = mock_send.call_args[0][3]
        assert params["index"] == 3
        assert params["name"] == "Water"
        assert params["cost"] == 5.0

    @pytest.mark.asyncio
    async def test_add_offmesh_link(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        await manage_navigation(
            ctx=mock_context, action="add_offmesh_link",
            target="CliffEdge", end_target="CliffBottom", bidirectional=False,
        )
        params = mock_send.call_args[0][3]
        assert params["endTarget"] == "CliffBottom"
        assert params["bidirectional"] is False

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_navigation import manage_navigation
        mock_send.side_effect = Exception("Timeout")
        result = await manage_navigation(ctx=mock_context, action="get_settings")
        assert result["success"] is False
