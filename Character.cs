using Godot;
using Godot.Collections;
using System;

public partial class Character : RigidBody3D {
    public const float Speed = 5.0f;
    public const float JumpImpulse = 5.0f;
    public const float MouseRotationSpeed = .002f;
    public const float SmoothFactor = 0.3f;
    public const string movementBlendNodePath = "parameters/Movement/blend_position";
    public const string stateMachineNodePath = "parameters/UpperStateMachine/playback";
    public const string movementScalePath = "parameters/MovementScale/scale";
    public const string armStatePath = "parameters/ArmState/current_state";
    public const string armTransitionSetPath = "parameters/ArmState/transition_request";

    [Export] private RayCast3D groundRaycast;
    [Export] private Node3D cameraSide;
    [Export] private Node3D cameraVert;
    [Export] private AnimationTree animationTree;
    [Export] private Label3D label;

    [Export] private Array<MeshInstance3D> armorMeshes;

    private Vector3 intendedVelocity = Vector3.Zero;
    private bool jumpRequested = false;
    private Quaternion targetRotation = Quaternion.Identity;
    private int armorIndex = 0;
    private ulong idleTimer = 0;

    public override void _Ready() {
        base._Ready();
        targetRotation = GlobalTransform.Basis.GetRotationQuaternion();
        foreach (var mesh in armorMeshes) {
            mesh.Visible = false;
        }
        animationTree.Active = true;
    }

    private void SetAnimationState(string stateName) {
        var pb = animationTree.Get(stateMachineNodePath);
        var playback = pb.As<AnimationNodeStateMachinePlayback>();
        if (playback == null) GD.PrintErr("Playback is null: " + pb.GetType());
        animationTree.Get(stateMachineNodePath).As<AnimationNodeStateMachinePlayback>().Travel(stateName);
    }

    private void SetArmState() {
        animationTree.Set(armTransitionSetPath, "state_0");
    }

    private string GetAnimationState() {
        var pb = animationTree.Get(stateMachineNodePath);
        var playback = pb.As<AnimationNodeStateMachinePlayback>();
        if (playback == null) GD.PrintErr("Playback is null: " + pb.GetType());
        var travelPath = playback.GetTravelPath();
        if (travelPath == null || travelPath.Count == 0) return playback.GetCurrentNode();
        return travelPath[^1];
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
            SetArmState();
        }

        if (@event is InputEventMouseMotion mouseEvent) {
            RotateCamera(mouseEvent.Relative);
            targetRotation = cameraSide.GlobalTransform.Basis.GetRotationQuaternion();
        }

        if (@event.IsActionPressed("switchArmor")) {
            var currentArmor = GetCurrentArmorMesh();
            if (currentArmor != null) currentArmor.Visible = false;
            armorIndex = (armorIndex + 1) % (armorMeshes.Count + 1);
            var newArmor = GetCurrentArmorMesh();
            if (newArmor != null) newArmor.Visible = true;
        }
    }

    private MeshInstance3D GetCurrentArmorMesh() {
        var index = armorIndex - 1;
        if (index < 0) return null;
        return armorMeshes[index];
    }

    bool IsOnGround() {
        return groundRaycast.IsColliding();
    }

    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);
        bool onGround = IsOnGround();

        if (!intendedVelocity.IsZeroApprox()) {
            var velocityDiff = intendedVelocity - (LinearVelocity - new Vector3(0, LinearVelocity.Y, 0));
            ApplyCentralImpulse(velocityDiff.LimitLength(Speed) * Mass);
            intendedVelocity = Vector3.Zero;
        }

        if (jumpRequested && onGround) {
            ApplyCentralImpulse(new Vector3(0, JumpImpulse * Mass, 0));
            jumpRequested = false;
        }

        var currentCameraRotation = cameraSide.GlobalTransform.Basis.GetRotationQuaternion();

        var currentRotation = GlobalTransform.Basis.GetRotationQuaternion();
        Quaternion smoothedRotation = currentRotation.Slerp(targetRotation, SmoothFactor);
        var basis = new Basis(smoothedRotation);
        GlobalTransform = new Transform3D(basis, GlobalTransform.Origin);

        var localVelocity = GlobalTransform.Inverse().Basis * LinearVelocity;
        localVelocity = new Vector3(localVelocity.X, 0, -localVelocity.Z);
        if (localVelocity.IsZeroApprox()) {
            if (idleTimer == 0) {
                idleTimer = Time.GetTicksMsec();
            } else if (Time.GetTicksMsec() - idleTimer > 3000) {
                if (GetAnimationState().Equals("Stand")) SetAnimationState("Idle");
            }
        } else {
            idleTimer = 0;
            if (GetAnimationState().Equals("Idle")) {
                SetAnimationState("Stand");
            }
        }

        SetMovementDirection(localVelocity);
        if (localVelocity.X > 0 && localVelocity.Z > 0) {
            label.Text = "Diagonal Right";
        } else if (localVelocity.X < 0 && localVelocity.Z > 0) {
            label.Text = "Diagonal Left";
        } else if (localVelocity.X > 0 && localVelocity.Z < 0) {
            label.Text = "Diagonal Back Right";
        } else if (localVelocity.X < 0 && localVelocity.Z < 0) {
            label.Text = "Diagonal Back Left";
        } else if (localVelocity.X > 0) {
            label.Text = "Right";
        } else if (localVelocity.X < 0) {
            label.Text = "Left";
        } else if (localVelocity.Z > 0) {
            label.Text = "Forward";
        } else if (localVelocity.Z < 0) {
            label.Text = "Back";
        } else {
            label.Text = "Idle";
        }

        var lerp = Mathf.Lerp(1, Speed, localVelocity.Length() / Speed);
        SetMovementScale(lerp);
        DebugDraw.Line(label.GlobalPosition, label.GlobalPosition + localVelocity, Colors.Blue);
        DebugDraw.Line(label.GlobalPosition, label.GlobalPosition + Vector3.Forward, Colors.Red);

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
