"""Tests for manage_terrain MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_terrain.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_terrain.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManageTerrainParams:

    @pytest.mark.asyncio
    async def test_create(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="create",
            name="MainTerrain", size=[1000, 200, 1000],
            heightmap_resolution=513,
        )
        params = mock_send.call_args[0][3]
        assert params["name"] == "MainTerrain"
        assert params["size"] == [1000, 200, 1000]
        assert params["heightmapResolution"] == 513

    @pytest.mark.asyncio
    async def test_set_heightmap(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="set_heightmap",
            source_path="Assets/Heightmaps/valley.raw",
        )
        params = mock_send.call_args[0][3]
        assert params["sourcePath"] == "Assets/Heightmaps/valley.raw"

    @pytest.mark.asyncio
    async def test_get_heightmap_requires_region(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="get_heightmap",
            region=[100, 100, 32, 32],
        )
        params = mock_send.call_args[0][3]
        assert params["region"] == [100, 100, 32, 32]

    @pytest.mark.asyncio
    async def test_raise_lower(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="raise_lower",
            position=[250, 0, 250], radius=20.0, strength=0.15,
        )
        params = mock_send.call_args[0][3]
        assert params["position"] == [250, 0, 250]
        assert params["radius"] == 20.0
        assert params["strength"] == 0.15

    @pytest.mark.asyncio
    async def test_smooth(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="smooth",
            position=[100, 0, 100], radius=15.0, iterations=3,
        )
        params = mock_send.call_args[0][3]
        assert params["iterations"] == 3

    @pytest.mark.asyncio
    async def test_add_terrain_layer(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="add_terrain_layer",
            diffuse="Assets/Textures/Grass.png",
            normal="Assets/Textures/Grass_Normal.png",
            tile_size=[15, 15],
        )
        params = mock_send.call_args[0][3]
        assert params["diffuse"] == "Assets/Textures/Grass.png"
        assert params["tileSize"] == [15, 15]

    @pytest.mark.asyncio
    async def test_paint_texture(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="paint_texture",
            layer_index=1, position=[200, 0, 300],
            radius=30.0, opacity=0.8,
        )
        params = mock_send.call_args[0][3]
        assert params["layerIndex"] == 1
        assert params["opacity"] == 0.8

    @pytest.mark.asyncio
    async def test_paint_trees(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        await manage_terrain(
            ctx=mock_context, action="paint_trees",
            prototype_index=0, density=500,
            min_height=0.8, max_height=1.2,
            random_rotation=True,
            area=[100, 100, 400, 400],
        )
        params = mock_send.call_args[0][3]
        assert params["density"] == 500
        assert params["randomRotation"] is True
        assert params["area"] == [100, 100, 400, 400]

    @pytest.mark.asyncio
    async def test_get_info(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        mock_send.return_value = {
            "success": True,
            "message": "Terrain info.",
            "data": {"heightmapResolution": 513, "treeInstanceCount": 1000},
        }
        result = await manage_terrain(ctx=mock_context, action="get_info")
        assert result["success"] is True
        assert result["data"]["heightmapResolution"] == 513

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_terrain import manage_terrain
        mock_send.side_effect = Exception("Timeout")
        result = await manage_terrain(ctx=mock_context, action="get_info")
        assert result["success"] is False
