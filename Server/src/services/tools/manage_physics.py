from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="physics",
    description=(
        "Physics system tools: raycast, overlap queries, physics material creation, "
        "global physics settings, and rigidbody/collider/joint configuration. "
        "Actions: "
        "raycast - Cast a ray and return hit info (point, normal, distance, collider). "
        "raycast_all - Cast a ray and return all hits. "
        "overlap - Find colliders in a sphere or box region. "
        "create_physics_material - Create a PhysicMaterial asset with friction and bounce settings. "
        "get_settings - Read global physics settings (gravity, solver iterations). Reads from project settings. "
        "set_settings - Persistently update global physics settings (modifies ProjectSettings/DynamicsManager). "
        "configure_rigidbody - Add/configure Rigidbody (mass, drag, kinematic, collision detection). "
        "configure_collider - Add/configure collider (box, sphere, capsule, mesh) with shape params. "
        "configure_joint - Add/configure joint (fixed, hinge, spring) with connected body."
    ),
    annotations=ToolAnnotations(
        title="Manage Physics",
        destructiveHint=True,
    ),
)
async def manage_physics(
    ctx: Context,
    action: Annotated[
        Literal[
            "raycast", "raycast_all", "overlap",
            "create_physics_material",
            "get_settings", "set_settings",
            "configure_rigidbody", "configure_collider", "configure_joint",
        ],
        "Action to perform.",
    ],
    target: Annotated[str | int, "Target GameObject (name, path, or instance ID)"] | None = None,
    # --- raycast ---
    origin: Annotated[list[float], "Ray origin [x, y, z]"] | None = None,
    direction: Annotated[list[float], "Ray direction [x, y, z]"] | None = None,
    max_distance: Annotated[float, "Maximum ray distance (default 1000)"] | None = None,
    layer_mask: Annotated[str, "Layer mask: comma-separated names or int (e.g., 'Default,Enemy')"] | None = None,
    max_results: Annotated[int, "Max results for raycast_all/overlap (default 20)"] | None = None,
    # --- overlap ---
    shape: Annotated[str, "Overlap shape: sphere, box (default sphere)"] | None = None,
    center: Annotated[list[float], "Overlap center [x, y, z]"] | None = None,
    radius: Annotated[float, "Sphere radius or capsule radius"] | None = None,
    half_extents: Annotated[list[float], "Box half extents [x, y, z]"] | None = None,
    # --- physics material ---
    path: Annotated[str, "Asset path for physics material"] | None = None,
    dynamic_friction: Annotated[float, "Dynamic friction (0-1)"] | None = None,
    static_friction: Annotated[float, "Static friction (0-1)"] | None = None,
    bounciness: Annotated[float, "Bounciness (0-1)"] | None = None,
    friction_combine: Annotated[str, "Friction combine mode: Average, Minimum, Maximum, Multiply"] | None = None,
    bounce_combine: Annotated[str, "Bounce combine mode: Average, Minimum, Maximum, Multiply"] | None = None,
    # --- global settings ---
    gravity: Annotated[list[float], "Gravity vector [x, y, z]"] | None = None,
    default_solver_iterations: Annotated[int, "Default solver iterations"] | None = None,
    bounce_threshold: Annotated[float, "Bounce threshold"] | None = None,
    sleep_threshold: Annotated[float, "Sleep threshold"] | None = None,
    # --- rigidbody ---
    mass: Annotated[float, "Rigidbody mass"] | None = None,
    drag: Annotated[float, "Rigidbody drag"] | None = None,
    angular_drag: Annotated[float, "Rigidbody angular drag"] | None = None,
    is_kinematic: Annotated[bool, "Is kinematic"] | None = None,
    use_gravity: Annotated[bool, "Use gravity"] | None = None,
    interpolation: Annotated[str, "Interpolation: None, Interpolate, Extrapolate"] | None = None,
    collision_detection: Annotated[str, "Collision detection: Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative"] | None = None,
    constraints: Annotated[str, "Rigidbody constraints enum value"] | None = None,
    # --- collider ---
    collider_type: Annotated[str, "Collider type: box, sphere, capsule, mesh"] | None = None,
    size: Annotated[list[float], "Box collider size [x, y, z]"] | None = None,
    height: Annotated[float, "Capsule height"] | None = None,
    is_trigger: Annotated[bool, "Is trigger collider"] | None = None,
    convex: Annotated[bool, "MeshCollider convex"] | None = None,
    physics_material: Annotated[str, "Path to PhysicMaterial asset to assign"] | None = None,
    # --- joint ---
    joint_type: Annotated[str, "Joint type: fixed, hinge, spring"] | None = None,
    connected_body: Annotated[str, "Connected body GameObject name"] | None = None,
    break_force: Annotated[float, "Joint break force"] | None = None,
    break_torque: Annotated[float, "Joint break torque"] | None = None,
    axis: Annotated[list[float], "Hinge joint axis [x, y, z]"] | None = None,
    use_motor: Annotated[bool, "Use hinge motor"] | None = None,
    motor_velocity: Annotated[float, "Hinge motor target velocity"] | None = None,
    motor_force: Annotated[float, "Hinge motor force"] | None = None,
    spring: Annotated[float, "Spring joint spring force"] | None = None,
    damper: Annotated[float, "Spring joint damper"] | None = None,
    min_distance: Annotated[float, "Spring joint min distance"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "target": target,
        "origin": origin,
        "direction": direction,
        "maxDistance": max_distance,
        "layerMask": layer_mask,
        "maxResults": max_results,
        "shape": shape,
        "center": center,
        "radius": radius,
        "halfExtents": half_extents,
        "path": path,
        "dynamicFriction": dynamic_friction,
        "staticFriction": static_friction,
        "bounciness": bounciness,
        "frictionCombine": friction_combine,
        "bounceCombine": bounce_combine,
        "gravity": gravity,
        "defaultSolverIterations": default_solver_iterations,
        "bounceThreshold": bounce_threshold,
        "sleepThreshold": sleep_threshold,
        "mass": mass,
        "drag": drag,
        "angularDrag": angular_drag,
        "isKinematic": is_kinematic,
        "useGravity": use_gravity,
        "interpolation": interpolation,
        "collisionDetection": collision_detection,
        "constraints": constraints,
        "colliderType": collider_type,
        "size": size,
        "height": height,
        "isTrigger": is_trigger,
        "convex": convex,
        "physicsMaterial": physics_material,
        "jointType": joint_type,
        "connectedBody": connected_body,
        "breakForce": break_force,
        "breakTorque": break_torque,
        "axis": axis,
        "useMotor": use_motor,
        "motorVelocity": motor_velocity,
        "motorForce": motor_force,
        "spring": spring,
        "damper": damper,
        "minDistance": min_distance,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry,
            unity_instance,
            "manage_physics",
            params_dict,
        )

        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_physics.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}

    except Exception as e:
        return {"success": False, "message": f"Python error in manage_physics: {str(e)}"}
