"""Tests for manage_2d MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_2d.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_2d.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManage2DParams:

    @pytest.mark.asyncio
    async def test_tilemap_set_tile(self, mock_send, mock_get_instance):
        from services.tools.manage_2d import manage_2d
        await manage_2d(
            ctx=mock_context, action="tilemap_set_tile",
            target="FloorTilemap",
            position=[5, 3],
            tile="Assets/Tiles/Floor.asset",
        )
        params = mock_send.call_args[0][3]
        assert params["position"] == [5, 3]
        assert params["tile"] == "Assets/Tiles/Floor.asset"

    @pytest.mark.asyncio
    async def test_tilemap_fill(self, mock_send, mock_get_instance):
        from services.tools.manage_2d import manage_2d
        await manage_2d(
            ctx=mock_context, action="tilemap_fill",
            target="FloorTilemap",
            from_pos=[0, 0], to_pos=[9, 7],
            tile="Assets/Tiles/Floor.asset",
        )
        params = mock_send.call_args[0][3]
        assert params["fromPos"] == [0, 0]
        assert params["toPos"] == [9, 7]

    @pytest.mark.asyncio
    async def test_tilemap_clear(self, mock_send, mock_get_instance):
        from services.tools.manage_2d import manage_2d
        await manage_2d(ctx=mock_context, action="tilemap_clear", target="WallTilemap")
        params = mock_send.call_args[0][3]
        assert params["target"] == "WallTilemap"

    @pytest.mark.asyncio
    async def test_create_sprite_atlas(self, mock_send, mock_get_instance):
        from services.tools.manage_2d import manage_2d
        await manage_2d(
            ctx=mock_context, action="create_sprite_atlas",
            path="Assets/Atlas/UI.spriteatlas",
        )
        params = mock_send.call_args[0][3]
        assert params["path"] == "Assets/Atlas/UI.spriteatlas"

    @pytest.mark.asyncio
    async def test_atlas_add_folders(self, mock_send, mock_get_instance):
        from services.tools.manage_2d import manage_2d
        await manage_2d(
            ctx=mock_context, action="atlas_add_folders",
            atlas="Assets/Atlas/UI.spriteatlas",
            folders=["Assets/Sprites/UI/"],
        )
        params = mock_send.call_args[0][3]
        assert params["folders"] == ["Assets/Sprites/UI/"]

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_2d import manage_2d
        mock_send.side_effect = Exception("Timeout")
        result = await manage_2d(ctx=mock_context, action="tilemap_get_info")
        assert result["success"] is False
