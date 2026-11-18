// Temporarily copy into Assets/Editor and run in Region_KayKit Play.
// Restores no persistence itself: stop Play and restore the captured save AND .bak afterwards.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RpgGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class KayKitDestructionRegression
{
    const string Output = "/tmp/kaykit-destruction/destruction.txt";
    static Keyboard keyboard;
    static int passed, failed;

    [MenuItem("工具/角色扮演/回归验证/KayKit 破坏与推挤")]
    public static void Run()
    {
        Directory.CreateDirectory("/tmp/kaykit-destruction");
        passed = failed = 0;
        File.WriteAllText(Output, "Enemy body contact and real keyboard/Hitbox destructible integration\n");
        GameFlow.Instance.StartCoroutine(Drive(Verify()));
    }

    static void Check(bool value, string label)
    {
        if (value) passed++; else failed++;
        File.AppendAllText(Output, (value ? "PASS " : "FAIL ") + label + "\n");
    }

    static IEnumerator Drive(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        while (stack.Count > 0)
        {
            bool more = false; object current = null; Exception error = null;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception e) { error = e; }
            if (error != null) { File.AppendAllText(Output, "EXCEPTION " + error + "\n"); failed++; break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator child) { stack.Push(child); continue; }
            yield return current;
        }
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        File.AppendAllText(Output, $"RESULT passed={passed} failed={failed}\n");
        Debug.Log($"[KayKitDestructionRegression] passed={passed} failed={failed}");
        EditorApplication.isPlaying = false;
    }

    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static IEnumerator Attack(ActionCombatController combat)
    {
        Keys(Key.J); yield return null; yield return null; Keys();
        File.AppendAllText(Output, "ATTACK active=" + combat.IsAttacking + " step=" + combat.CurrentStep + "\n");
        while (combat.IsAttacking) yield return null;
        yield return new WaitForSeconds(0.05f);
    }
    static IEnumerator Capture(string name)
    {
        ScreenCapture.CaptureScreenshot("/tmp/kaykit-destruction/" + name + ".png");
        yield return new WaitForSecondsRealtime(0.12f);
    }
    static void FreezeEnemies()
    {
        foreach (var enemy in Object.FindObjectsByType<ActionEnemy>(FindObjectsSortMode.None))
        { enemy.GetComponent<EnemyBrain>().enabled = false; enemy.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; }
    }

    static IEnumerator Verify()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        yield return new WaitForSeconds(0.4f);
        var health = PlayerHealth.Player; var motor = health.GetComponent<PlayerMotor>();
        var combat = health.GetComponent<ActionCombatController>();
        combat.debugAutoAttack = false; motor.SetControlEnabled(false); health.SetInvulnerable(90f);
        var enemies = Object.FindObjectsByType<ActionEnemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_GoblinWarrior.prefab");
        var chasing = Object.Instantiate(source).GetComponent<ActionEnemy>();
        var brain = chasing.GetComponent<EnemyBrain>();
        brain.moveSpeed = 2.5f; brain.aggroRange = 20f; brain.loseTargetRange = 30f; brain.attackRange = 0f;
        var body = chasing.GetComponent<Rigidbody2D>();

        for (int d = 0; d < 8; d++)
        {
            Vector2 direction = new Vector2(Mathf.Cos(d * Mathf.PI / 4f), Mathf.Sin(d * Mathf.PI / 4f));
            motor.Teleport(new Vector3(-4, -3));
            body.position = new Vector2(-4, -3) + direction * 1.5f; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate();
            Vector2 original = motor.transform.position;
            yield return new WaitForSeconds(0.8f);
            float drift = Vector2.Distance(original, motor.transform.position);
            Check(drift <= 0.02f, "direction " + d + " chasing enemy cannot push stationary player; drift=" + drift.ToString("F4"));
            motor.ExternalVelocity = -direction * 5f;
            yield return new WaitForSeconds(0.15f);
            motor.ExternalVelocity = Vector2.zero;
            Check(Vector2.Dot((Vector2)motor.transform.position - original, -direction) > 0.35f,
                "direction " + d + " player can move away from enemy");
        }
        chasing.gameObject.SetActive(false);
        var crowd = new List<GameObject>();
        motor.Teleport(new Vector3(-4, -3));
        for (int d = 0; d < 4; d++)
        {
            Vector2 direction = new Vector2(Mathf.Cos(d * Mathf.PI / 2f), Mathf.Sin(d * Mathf.PI / 2f));
            var go = Object.Instantiate(source, (Vector3)(new Vector2(-4, -3) + direction * 1.5f), Quaternion.identity);
            var ai = go.GetComponent<EnemyBrain>(); ai.moveSpeed = 3f; ai.aggroRange = 20f; ai.loseTargetRange = 30f; ai.attackRange = 0f;
            crowd.Add(go);
        }
        Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate();
        Vector2 stationary = motor.transform.position;
        yield return new WaitForSeconds(1.1f);
        Check(Vector2.Distance(stationary, motor.transform.position) <= 0.02f, "four simultaneous enemies cannot squeeze player");
        foreach (var go in crowd) Object.Destroy(go); Object.Destroy(chasing.gameObject); yield return null;

        motor.SetControlEnabled(true);
        var props = Object.FindObjectsByType<DestructibleProp>(FindObjectsSortMode.None);
        Check(props.Length == 6, "six ordinary crate/table groups are damageable");
        var chest = Object.FindFirstObjectByType<Chest>();
        Check(chest.GetComponent<DestructibleProp>() == null && chest.GetComponent<IDamageable>() == null,
            "reward chest remains E interaction only");

        foreach (string id in new[] { "entrance_crate", "side_room_table", "side_room_supplies" })
        {
            var prop = props.First(p => p.propId == id);
            var bounds = prop.GetComponent<Collider2D>().bounds;
            motor.Teleport(new Vector3(bounds.min.x - 0.48f, bounds.center.y));
            Keys(Key.D); yield return null; yield return null; Keys();
            yield return new WaitForSeconds(0.2f);
            combat.ResetCombo();
            int before = prop.Health;
            int expected = Mathf.RoundToInt(combat.damage * combat.combo[0].damageMultiplier);
            // Two colliders on the same owner must still receive only one hit.
            var extra = prop.gameObject.AddComponent<BoxCollider2D>(); extra.size = prop.GetComponent<BoxCollider2D>().size;
            extra.offset = prop.GetComponent<BoxCollider2D>().offset;
            int gold = SaveSystem.Current.character.gold;
            File.AppendAllText(Output, "BEFORE " + id + " health=" + before + " player=" + motor.transform.position +
                " facing=" + motor.Facing + " bounds=" + prop.GetComponent<Collider2D>().bounds +
                " enabled=" + combat.enabled + " timeScale=" + Time.timeScale + " keyboard=" + Keyboard.current?.name + "\n");
            yield return Attack(combat);
            File.AppendAllText(Output, "AFTER " + id + " health=" + prop.Health + " expectedDamage=" + expected + "\n");
            Check(prop.Health == before - expected, id + " real J/Hitbox applies one hit despite duplicate colliders");
            Check(!prop.IsDead && prop.FractureEffect != null && prop.GetComponent<Collider2D>().enabled,
                id + " wounded cracked model still blocks movement");
            if (prop.FractureEffect == null)
            {
                combat.RequestAttack(); while (combat.IsAttacking) yield return null;
                File.AppendAllText(Output, "CONTROL direct request health=" + prop.Health + "\n");
            }
            Check(prop.FractureEffect != null && prop.FractureEffect.PieceCount >= 2 && prop.FractureEffect.GetComponentsInChildren<Collider>(true).Length == 0 &&
                prop.FractureEffect.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
                prop.FractureEffect.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                id + " pre-cut wood fragments do not participate in character physics");
            yield return Capture(id + "-cracked");
            int safety = 0;
            while (!prop.IsDead && safety++ < 5)
            {
                Keys(Key.J); yield return null; yield return null; Keys();
                while (!prop.IsDead && combat.IsAttacking) yield return null;
                if (prop.IsDead) yield return Capture(id + "-splitting");
                while (combat.IsAttacking) yield return null;
            }
            Check(prop.IsDead && prop.Health == 0, id + " attacks destroy the prop");
            Check(prop.GetComponentsInChildren<Collider2D>(true).All(c => !c.enabled), id + " all blockers removed on destruction");
            Check(prop.FractureEffect != null && prop.FractureEffect.IsBurst, id + " fragments burst on lethal hit");
            Check(SaveSystem.Current.character.gold == gold, id + " ordinary destruction grants no treasure rewards");
            Check(SaveSystem.Current.world.GetRegion("region_kaykit").HasFlag("destroyed_prop/" + id), id + " destruction state persisted");
            if (id == "entrance_crate")
            {
                Keys(Key.D); yield return new WaitForSeconds(0.4f); Keys(); yield return null;
                Check(motor.transform.position.x > -0.35f, "player walks through the destroyed crate footprint");
            }
            yield return new WaitForSeconds(2f);
            Check(prop.FractureEffect == null, id + " temporary fragments cleaned up");
        }
        Keys();
        GameFlow.Instance.LoadRegion("region_kaykit"); yield return new WaitForSeconds(0.6f); FreezeEnemies();
        foreach (string id in new[] { "entrance_crate", "side_room_table", "side_room_supplies" })
        {
            var prop = Object.FindObjectsByType<DestructibleProp>(FindObjectsSortMode.None).First(p => p.propId == id);
            Check(prop.IsDead && !prop.intactVisual.activeSelf && !prop.GetComponent<Collider2D>().enabled,
                id + " stays destroyed/passable after reload without respawning debris");
        }
        chest = Object.FindFirstObjectByType<Chest>();
        motor = PlayerHealth.Player.GetComponent<PlayerMotor>();
        var point = chest.transform.position; motor.Teleport(point + Vector3.down * 1.6f);
        yield return new WaitForSeconds(0.3f);
        Check(motor.GetComponent<WorldInteractor>().Current == chest && chest.CanInteract, "treasure chest still found by E interaction");
        int originalGold = SaveSystem.Current.character.gold;
        Keys(Key.E); yield return null; yield return null; Keys(); yield return new WaitForSeconds(0.6f);
        Check(!chest.CanInteract && SaveSystem.Current.character.gold == originalGold + chest.gold, "treasure chest E reward still works");
    }

    [MenuItem("工具/角色扮演/回归验证/KayKit 碎裂预览")]
    public static void Preview()
    {
        GameFlow.Instance.StartCoroutine(PreviewFrames());
    }

    static IEnumerator PreviewFrames()
    {
        const string directory = "/tmp/kaykit-destruction/preview";
        Directory.CreateDirectory(directory);
        FreezeEnemies();
        var motor = PlayerHealth.Player.GetComponent<PlayerMotor>(); motor.SetControlEnabled(false);
        foreach (string id in new[] { "entrance_crate", "side_room_table" })
        {
            var prop = Object.FindObjectsByType<DestructibleProp>(FindObjectsSortMode.None).First(p => p.propId == id);
            var bounds = prop.GetComponent<Collider2D>().bounds;
            motor.Teleport(new Vector3(bounds.min.x - 0.48f, bounds.center.y));
            Camera.main.GetComponent<CameraFollow>().enabled = true;
            yield return new WaitForSeconds(0.7f);
            Camera.main.GetComponent<CameraFollow>().enabled = false;
            int frame = 0;
            for (int i = 0; i < 4; i++)
            {
                ScreenCapture.CaptureScreenshot(directory + "/" + id + "_" + (frame++).ToString("D3") + ".png");
                yield return new WaitForSeconds(0.065f);
            }
            prop.TakeDamage(new DamageInfo { amount = 10, sourcePosition = motor.transform.position, source = motor.gameObject });
            for (int i = 0; i < 8; i++)
            {
                ScreenCapture.CaptureScreenshot(directory + "/" + id + "_" + (frame++).ToString("D3") + ".png");
                yield return new WaitForSeconds(0.065f);
            }
            prop.TakeDamage(new DamageInfo { amount = prop.Health, sourcePosition = motor.transform.position, source = motor.gameObject });
            for (int i = 0; i < 33; i++)
            {
                ScreenCapture.CaptureScreenshot(directory + "/" + id + "_" + (frame++).ToString("D3") + ".png");
                yield return new WaitForSeconds(0.065f);
            }
        }
        Debug.Log("[KayKitDestructionPreview] captured actual game frames");
        EditorApplication.isPlaying = false;
    }
}
