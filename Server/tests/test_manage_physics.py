"""Tests for manage_physics MCP tool."""

import pytest
from unittest.mock import MagicMock, patch

mock_context = MagicMock()
mock_context.session = MagicMock()


@pytest.fixture
def mock_send():
    with patch("services.tools.manage_physics.send_with_unity_instance") as mock:
        mock.return_value = {"success": True, "message": "OK", "data": {}}
        yield mock


@pytest.fixture
def mock_get_instance():
    with patch("services.tools.manage_physics.get_unity_instance_from_context") as mock:
        mock.return_value = {"id": "test", "name": "TestUnity", "port": 8080}
        yield mock


class TestManagePhysicsParams:

    @pytest.mark.asyncio
    async def test_raycast(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="raycast",
            origin=[0, 5, 0],
            direction=[0, -1, 0],
            max_distance=100.0,
            layer_mask="Default,Enemy",
        )
        params = mock_send.call_args[0][3]
        assert params["action"] == "raycast"
        assert params["origin"] == [0, 5, 0]
        assert params["direction"] == [0, -1, 0]
        assert params["maxDistance"] == 100.0
        assert params["layerMask"] == "Default,Enemy"

    @pytest.mark.asyncio
    async def test_overlap_sphere(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="overlap",
            shape="sphere",
            center=[0, 0, 0],
            radius=10.0,
            max_results=5,
        )
        params = mock_send.call_args[0][3]
        assert params["shape"] == "sphere"
        assert params["radius"] == 10.0
        assert params["maxResults"] == 5

    @pytest.mark.asyncio
    async def test_overlap_box(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="overlap",
            shape="box",
            center=[0, 0, 0],
            half_extents=[5, 5, 5],
        )
        params = mock_send.call_args[0][3]
        assert params["halfExtents"] == [5, 5, 5]

    @pytest.mark.asyncio
    async def test_create_physics_material(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="create_physics_material",
            path="Assets/Physics/Ice.physicMaterial",
            dynamic_friction=0.05,
            static_friction=0.1,
            bounciness=0.2,
            friction_combine="Minimum",
        )
        params = mock_send.call_args[0][3]
        assert params["path"] == "Assets/Physics/Ice.physicMaterial"
        assert params["dynamicFriction"] == 0.05
        assert params["frictionCombine"] == "Minimum"

    @pytest.mark.asyncio
    async def test_set_settings_gravity(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="set_settings",
            gravity=[0, -20, 0],
            default_solver_iterations=12,
        )
        params = mock_send.call_args[0][3]
        assert params["gravity"] == [0, -20, 0]
        assert params["defaultSolverIterations"] == 12

    @pytest.mark.asyncio
    async def test_configure_rigidbody(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="configure_rigidbody",
            target="Player",
            mass=10.0,
            drag=0.5,
            is_kinematic=False,
            collision_detection="Continuous",
        )
        params = mock_send.call_args[0][3]
        assert params["target"] == "Player"
        assert params["mass"] == 10.0
        assert params["isKinematic"] is False
        assert params["collisionDetection"] == "Continuous"

    @pytest.mark.asyncio
    async def test_configure_collider(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="configure_collider",
            target="Wall",
            collider_type="box",
            size=[2, 3, 0.5],
            is_trigger=False,
            physics_material="Assets/Physics/Stone.physicMaterial",
        )
        params = mock_send.call_args[0][3]
        assert params["colliderType"] == "box"
        assert params["size"] == [2, 3, 0.5]
        assert params["physicsMaterial"] == "Assets/Physics/Stone.physicMaterial"

    @pytest.mark.asyncio
    async def test_configure_joint(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(
            ctx=mock_context,
            action="configure_joint",
            target="Door",
            joint_type="hinge",
            connected_body="DoorFrame",
            axis=[0, 1, 0],
            use_motor=True,
            motor_velocity=90.0,
            motor_force=50.0,
        )
        params = mock_send.call_args[0][3]
        assert params["jointType"] == "hinge"
        assert params["connectedBody"] == "DoorFrame"
        assert params["useMotor"] is True

    @pytest.mark.asyncio
    async def test_none_params_excluded(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        await manage_physics(ctx=mock_context, action="get_settings")
        params = mock_send.call_args[0][3]
        assert "target" not in params
        assert "origin" not in params
        assert "gravity" not in params


class TestManagePhysicsResponses:

    @pytest.mark.asyncio
    async def test_raycast_hit(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        mock_send.return_value = {
            "success": True,
            "message": "Raycast hit.",
            "data": {"hit": True, "point": [5, 0, 0], "distance": 5.0},
        }
        result = await manage_physics(
            ctx=mock_context, action="raycast",
            origin=[0, 0, 0], direction=[1, 0, 0],
        )
        assert result["success"] is True
        assert result["data"]["hit"] is True

    @pytest.mark.asyncio
    async def test_exception_handling(self, mock_send, mock_get_instance):
        from services.tools.manage_physics import manage_physics
        mock_send.side_effect = Exception("Timeout")
        result = await manage_physics(ctx=mock_context, action="get_settings")
        assert result["success"] is False
        assert "Timeout" in result["message"]
