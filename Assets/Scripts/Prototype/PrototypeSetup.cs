using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// M1 step 1 only: creates the input adapter, the path planner and one aircraft.
    /// It will be replaced by LevelDirector, which will spawn aircraft from level data.
    /// </summary>
    public sealed class PrototypeSetup : MonoBehaviour
    {
        [SerializeField] Material shapeMaterial;
        [SerializeField] Color aircraftColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] float aircraftSpeed = 1.2f;          // world units per second
        [SerializeField] float aircraftTurnRate = 200f;       // degrees per second, only for the edge turn-back with no path
        [SerializeField] float edgeMargin = 0.8f;             // turn back this far inside the screen edge

        void Awake()
        {
            // Mobile platforms default to 30 FPS. Drawing feels much smoother at 60.
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            Camera camera = Camera.main;

            var input = new GameObject("InputAdapter").AddComponent<InputAdapter>();
            input.Initialise(camera);

            var planner = new GameObject("PathPlanner").AddComponent<PathPlanner>();
            planner.Initialise(input);

            var motion = new AircraftMotion(new Vector2(-4f, 1.5f), 0f, aircraftSpeed, aircraftTurnRate);
            AircraftController.Spawn(motion, camera, shapeMaterial, aircraftColor, edgeMargin);
        }
    }
}
