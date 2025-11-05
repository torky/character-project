using Godot;
using System;

public partial class Character : RigidBody3D {
    public const float Speed = 5.0f;
    public const float JumpImpulse = 5.0f;
    public const float MouseRotationSpeed = .002f;
    public const float SmoothFactor = 0.3f;
    public const string movementBlendNodePath = "parameters/Movement/blend_position";
    public const string stateMachineNodePath = "parameters/UpperStateMachine";
    public const string movementScalePath = "parameters/MovementScale/scale";

    [Export] private RayCast3D groundRaycast;
    [Export] private Node3D cameraSide;
    [Export] private Node3D cameraVert;
    [Export] private AnimationTree animationTree;
    [Export] private Label3D label;

    private Vector3 intendedVelocity = Vector3.Zero;
    private bool jumpRequested = false;
    private Quaternion targetRotation = Quaternion.Identity;

    public override void _Ready() {
        base._Ready();
        targetRotation = GlobalTransform.Basis.GetRotationQuaternion();
    }

    private void SetAnimationState(string stateName) {
        animationTree.Get(stateMachineNodePath).As<AnimationNodeStateMachinePlayback>().Travel(stateName);
    }

    private void SetMovementScale(float scale) {
        animationTree.Set(movementScalePath, scale);
    }

    private void SetMovementDirection(Vector3 direction) {
        animationTree.Set(movementBlendNodePath, new Vector2(direction.X, direction.Z));
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);
        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var direction = (cameraSide.GlobalTransform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        if (!direction.IsZeroApprox()) {
            intendedVelocity = direction * Speed;
        }

        if (@event.IsActionPressed("ui_accept")) {
            jumpRequested = true;
        }

        if (@event.IsActionPressed("attack")) {
            SetAnimationState("Attack");
        }

        if (@event is InputEventMouseMotion mouseEvent) {
            RotateCamera(mouseEvent.Relative);
            targetRotation = cameraSide.GlobalTransform.Basis.GetRotationQuaternion();
        }
    }

    bool IsOnGround() {
        return groundRaycast.IsColliding();
    }

    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);
        if (!intendedVelocity.IsZeroApprox()) {
            var velocityDiff = intendedVelocity - (LinearVelocity - new Vector3(0, LinearVelocity.Y, 0));
            ApplyCentralImpulse(velocityDiff.LimitLength(Speed) * Mass);
            intendedVelocity = Vector3.Zero;
        }

        if (jumpRequested && IsOnGround()) {
            ApplyCentralImpulse(new Vector3(0, JumpImpulse * Mass, 0));
            jumpRequested = false;
        }

        var currentCameraRotation = cameraSide.GlobalTransform.Basis.GetRotationQuaternion();

        var currentRotation = GlobalTransform.Basis.GetRotationQuaternion();
        Quaternion smoothedRotation = currentRotation.Slerp(targetRotation, SmoothFactor);
        var basis = new Basis(smoothedRotation);
        GlobalTransform = new Transform3D(basis, GlobalTransform.Origin);

        var localVelocity = GlobalTransform.Inverse().Basis * LinearVelocity;
        SetMovementDirection(localVelocity);
        var lerp = Mathf.Lerp(1, Speed, localVelocity.Length() / Speed);
        SetMovementScale(lerp);
        label.Text = $"Velocity: {localVelocity}";
        DebugDraw.Line(label.GlobalPosition, label.GlobalPosition + LinearVelocity.Normalized(), Colors.Red);
        DebugDraw.Line(label.GlobalPosition, label.GlobalPosition + localVelocity, Colors.Blue);

        cameraSide.GlobalTransform = new Transform3D(new Basis(currentCameraRotation), cameraSide.GlobalTransform.Origin);
    }

    private void RotateCamera(Vector2 vectorDelta) {
        cameraSide.RotateY(-vectorDelta.X * MouseRotationSpeed);
        cameraVert.RotateX(-vectorDelta.Y * MouseRotationSpeed);
        Vector3 rotation = cameraVert.RotationDegrees;
        rotation.X = Mathf.Clamp(rotation.X, -90, 90); // Prevent over-rotation
        cameraVert.RotationDegrees = rotation;
    }
}
