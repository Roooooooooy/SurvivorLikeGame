// Copy temporarily into Assets/Editor. Run in the current action-region Play mode.
// This repro disables damage and leaves the real EnemyBrain and Physics2D movement active.
using System.Collections;
using System.IO;
using RpgGame;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EnemyPushRegression
{
    [MenuItem("工具/角色扮演/回归验证/怪物推挤主角")]
    public static void Run()
    {
        GameFlow.Instance.StartCoroutine(Verify());
    }

    static IEnumerator Verify()
    {
        Directory.CreateDirectory("/tmp/kaykit-destruction");
        const string path = "/tmp/kaykit-destruction/enemy-push.txt";
        File.WriteAllText(path, "Stationary invulnerable player, one real chasing enemy, no attacks\n");
        var motor = PlayerHealth.Player.GetComponent<PlayerMotor>();
        motor.SetControlEnabled(false);
        PlayerHealth.Player.SetInvulnerable(60f);
        ActionEnemy target = null;
        foreach (var enemy in Object.FindObjectsByType<ActionEnemy>(FindObjectsSortMode.None))
        {
            if (target == null && enemy.GetComponent<EnemyBrain>() != null) target = enemy;
            else enemy.gameObject.SetActive(false);
        }
        var brain = target.GetComponent<EnemyBrain>();
        brain.enabled = true; brain.moveSpeed = 2.5f;
        brain.aggroRange = 20f; brain.loseTargetRange = 30f; brain.attackRange = 0f;
        motor.Teleport(new Vector3(-4f, -3f, 0f));
        var body = target.GetComponent<Rigidbody2D>();
        body.position = new Vector2(-4f, -1.5f);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Vector2 start = motor.transform.position;
        yield return new WaitForSeconds(1f);
        float distance = Vector2.Distance(start, motor.transform.position);
        File.AppendAllText(path, (distance <= 0.02f ? "PASS " : "FAIL ") +
            "stationary player displacement=" + distance.ToString("F4") + " (maximum 0.02)\n");
        // One variable changes: leave both bodies but stop the chase input.
        brain.moveSpeed = 0f;
        motor.Teleport(new Vector3(-4f, -3f, 0f));
        body.position = new Vector2(-4f, -1.5f); body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate();
        start = motor.transform.position;
        yield return new WaitForSeconds(0.6f);
        File.AppendAllText(path, "CONTROL zero enemy speed displacement=" + Vector2.Distance(start, motor.transform.position).ToString("F4") + "\n");
        Debug.Log("[EnemyPushRegression] player displacement=" + distance.ToString("F4"));
        EditorApplication.isPlaying = false;
    }
}
