using Godot;
using System;

public partial class MainScene : Node3D {
    public override void _Ready() {
        base._Ready();
        // Lock mouse input
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print("Mouse input captured.");
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);
        // Unlock mouse input on Escape
        if (@event.IsActionPressed("ui_cancel")) {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event.IsActionPressed("ui_accept")) {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

}
