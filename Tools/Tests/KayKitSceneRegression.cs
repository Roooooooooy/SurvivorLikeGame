// Temporarily copy into Assets/Editor, run the menu in Region_KayKit Play mode,
// then remove the temporary copy and restore both SaveSystem files.
// Uses a virtual keyboard, real PlayerMotor/Physics2D, and the actual interaction path.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using RpgGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class KayKitSceneRegression
{
    const string Output = "/tmp/kaykit-validation/results.txt";
    static Keyboard keyboard;
    static int passed, failed;

    [MenuItem("工具/角色扮演/KayKit/临时验证场景")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Region_KayKit")
            throw new InvalidOperationException("请在 Region_KayKit Play 中运行。");
        Directory.CreateDirectory("/tmp/kaykit-validation");
        passed = failed = 0;
        File.WriteAllText(Output, "KayKit integration: real movement, collision, E interaction, persistence and region routing\n");
        GameFlow.Instance.StartCoroutine(Guarded());
    }

    static void Check(bool value, string label)
    {
        if (value) passed++; else failed++;
        File.AppendAllText(Output, (value ? "PASS " : "FAIL ") + label + "\n");
    }

    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static IEnumerator Walk(Key key, float seconds)
    {
        Keys(key); yield return new WaitForSeconds(seconds);
        Keys(); yield return new WaitForSeconds(0.12f);
    }

    static IEnumerator PressE()
    {
        Keys(Key.E); yield return null; yield return null;
        Keys(); yield return new WaitForSeconds(0.65f);
    }

    static void FreezeEnemies()
    {
        foreach (var brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        {
            brain.enabled = false;
            brain.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }
    }

    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        ScreenCapture.CaptureScreenshot("/tmp/kaykit-validation/" + name + ".png");
        yield return new WaitForSecondsRealtime(0.3f);
    }

    static IEnumerator Guarded()
    {
        var routine = Verify();
        while (true)
        {
            bool more = false; object current = null; Exception error = null;
            try { more = routine.MoveNext(); if (more) current = routine.Current; }
            catch (Exception e) { error = e; }
            if (error != null)
            {
                File.AppendAllText(Output, "EXCEPTION " + error + "\n"); failed++;
                break;
            }
            if (!more) break;
            yield return current;
        }
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        File.AppendAllText(Output, $"RESULT passed={passed} failed={failed}\n");
        Debug.Log($"[KayKitRegression] passed={passed} failed={failed}");
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Verify()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        yield return new WaitForSeconds(0.5f);
        FreezeEnemies();
        var motor = PlayerHealth.Player.GetComponent<PlayerMotor>();
        var interactor = motor.GetComponent<WorldInteractor>();
        var door = Object.FindFirstObjectByType<KayKitDoor>();
        var chest = Object.FindFirstObjectByType<Chest>();
        Check(RegionRuntime.IsAction && GameFlow.Instance.CurrentRegion.id == "region_kaykit", "new region recognized as ActionRPG");
        Check(Vector2.Distance(motor.transform.position, new Vector2(-4, -5)) < 0.1f, "spawn mapped to intended entrance");
        Check(!door.IsOpen && door.passageBlocker.enabled, "door begins closed and solid");
        Check(chest.CanInteract, "chest begins unopened");
        Check(Vector3.Distance(chest.GetComponent<KayKitWorldAnchor>().visual.position, new Vector3(7, 0, 1.5f)) < 0.01f,
            "XY chest and XZ visual agree");
        var floor = GameObject.Find("KayKit_Layout/Modules").GetComponentsInChildren<MeshRenderer>()
            .First(r => r.gameObject.name == "floor_tile_small");
        Check(Mathf.Abs(floor.bounds.max.y) < 0.1f, "plain floor surface at character ground level");
        File.AppendAllText(Output, "FLOOR BOUNDS " + floor.bounds + "\n");
        yield return Capture("entrance");

        // Outer wall and an actual prop stop player motion; no teleport is used to cross either.
        motor.Teleport(new Vector3(-4, -7.8f)); yield return new WaitForFixedUpdate();
        yield return Walk(Key.S, 0.8f);
        Check(motor.transform.position.y > -8.6f, "south boundary blocks real movement");
        motor.Teleport(new Vector3(0, -5)); yield return new WaitForFixedUpdate();
        yield return Walk(Key.D, 0.8f);
        Check(motor.transform.position.x < 0.6f, "east boundary matches wall footprint");
        motor.Teleport(new Vector3(-2.5f, -5)); yield return new WaitForFixedUpdate();
        yield return Walk(Key.D, 0.8f);
        Check(motor.transform.position.x < -1.6f, "crate blocks movement at visible footprint");
        yield return Walk(Key.A, 0.3f);
        Check(motor.transform.position.x < -2.3f, "player can leave crate contact");

        // Closed doorway, actual E key discovery, animation, and traversable open passage.
        motor.Teleport(new Vector3(-0.2f, -1)); yield return new WaitForFixedUpdate();
        yield return Walk(Key.D, 0.5f);
        Check(motor.transform.position.x < 0.55f, "closed door blocks approach");
        Check(interactor.Current == door, "WorldInteractor selects the door");
        yield return Capture("door-closed");
        yield return PressE();
        Check(door.IsOpen && !door.passageBlocker.enabled, "E opens door and clears passage after animation");
        Check(Quaternion.Angle(door.doorLeaf.localRotation, Quaternion.identity) > 50f, "door leaf rotates on its hinge");
        Check(SaveSystem.Current.world.GetRegion("region_kaykit").HasFlag(door.doorId), "door state written to its own region");
        yield return Walk(Key.D, 1f);
        Check(motor.transform.position.x > 3.2f && Mathf.Abs(motor.transform.position.y + 1f) < 0.35f,
            "player walks through doorway into side room");
        yield return Capture("door-open");

        motor.Teleport(new Vector3(7, -0.2f)); yield return new WaitForSeconds(0.5f);
        Check(interactor.Current == chest, "WorldInteractor finds chest from walkable approach");
        int gold = SaveSystem.Current.character.gold;
        float closedLidY = chest.view.lidHinge.GetComponentInChildren<Renderer>().bounds.center.y;
        yield return Capture("chest-closed");
        yield return PressE();
        Check(!chest.CanInteract, "E opens chest once");
        Check(SaveSystem.Current.character.gold == gold + chest.gold, "chest grants exactly configured gold");
        Check(chest.view.lidHinge.GetComponentInChildren<Renderer>().bounds.center.y > closedLidY + 0.15f,
            "lid opens upward instead of into chest");
        chest.Interact();
        Check(SaveSystem.Current.character.gold == gold + chest.gold, "repeat interaction grants no extra rewards");
        yield return Capture("chest-open");

        // Actual scene reload exercises Start/load logic and the registered region asset.
        GameFlow.Instance.LoadRegion("region_kaykit");
        yield return new WaitForSeconds(0.7f);
        FreezeEnemies();
        door = Object.FindFirstObjectByType<KayKitDoor>();
        chest = Object.FindFirstObjectByType<Chest>();
        Check(door.IsOpen && !door.passageBlocker.enabled, "door stays passable after reload");
        Check(!chest.CanInteract && SaveSystem.Current.character.gold == gold + chest.gold, "chest and gold persist after reload");

        var portal = Object.FindFirstObjectByType<Portal>();
        portal.Interact();
        yield return new WaitForSecondsRealtime(0.6f);
        Check(GameFlow.Instance.IsInRun && RegionRuntime.IsSurvivor, "rift enters existing survivor sub-mode");
        GameFlow.Instance.ExitSurvivorRun(null);
        yield return new WaitForSecondsRealtime(0.6f);
        Check(SceneManager.GetActiveScene().name == "Region_KayKit" && RegionRuntime.IsAction,
            "survivor return routes to the new region");
        FreezeEnemies();

        var cam = Camera.main;
        cam.GetComponent<CameraFollow>().enabled = false;
        cam.transform.position = new Vector3(-1f, 19f, -25f);
        cam.transform.LookAt(new Vector3(-1f, 0f, -0.5f));
        yield return Capture("overview");
        File.AppendAllText(Output, "Render triangles " + UnityEditor.UnityStats.triangles + " batches " + UnityEditor.UnityStats.batches + "\n");
    }
}
