using Godot;
using Godot.Collections;

public struct Hit {
    private bool isValid;
    private Vector3 normal;
    private Vector3 position;
    private Node3D hitObject;

    public readonly bool IsValid => isValid;
    public readonly Vector3 Normal => normal;
    public readonly Vector3 Position => position;
    public readonly Node3D Object => hitObject;

    public Hit() {
        isValid = false;
    }

    public Hit(Dictionary dict) {
        this.isValid = true;
        this.normal = dict["normal"].AsVector3();
        this.position = dict["position"].AsVector3();
        this.hitObject = dict["collider"].As<Node3D>();
    }

    public Hit(Vector3 normal, Vector3 position, Node3D hitObject) {
        this.isValid = true;
        this.normal = normal;
        this.position = position;
        this.hitObject = hitObject;
    }
}