// Reusable Play-mode regression probe. Temporarily copy into Assets/Editor,
// run the menu below in Region_Test, then remove it and restore SaveSystem files.
// The probe changes runtime positions/health only and exits Play without saving the scene.
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RpgGame;

public static class ActionCombatRegression
{
    const string Output = "/tmp/action-combat-fix/regression.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int passed, failed;
    static void Check(bool value, string label)
    {
        if (value) passed++; else failed++;
        File.AppendAllText(Output, (value ? "PASS " : "FAIL ") + label + "\n");
    }
    static void Face(PlayerMotor motor, int d)
    {
        Vector2 direction = new Vector2(Mathf.Cos(d * Mathf.PI / 4f), Mathf.Sin(d * Mathf.PI / 4f));
        typeof(PlayerMotor).GetField("<Facing>k__BackingField", Private).SetValue(motor, direction);
        typeof(Hd2dPixelPlayer).GetField("dir", Private).SetValue(Hd2dPixelPlayer.Instance, d);
    }
    static ActionEnemy MakeTarget(Vector2 position, float radius = 0.35f)
    {
        var go = new GameObject("CombatRegressionTarget");
        go.transform.position = position;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;body.mass = 20f;body.linearDamping = 10f;body.freezeRotation = true;
        go.AddComponent<CircleCollider2D>().radius = radius;
        var enemy = go.AddComponent<ActionEnemy>();enemy.maxHealth = 1000;enemy.currentHealth = 1000;
        return enemy;
    }
    [MenuItem("工具/角色扮演/回归验证拳击与阻挡")]
    public static void Run()
    {
        var combat = Object.FindFirstObjectByType<ActionCombatController>();
        combat.StartCoroutine(Verify(combat));
    }
    static IEnumerator Verify(ActionCombatController combat)
    {
        Directory.CreateDirectory("/tmp/action-combat-fix");
        passed = failed = 0;
        File.WriteAllText(Output, "Combat regression: sleeping targets, range, contact blocking, particles\n");
        var motor = combat.GetComponent<PlayerMotor>();
        Vector3 originalPosition = motor.transform.position;
        combat.debugAutoAttack = false;
        motor.SetControlEnabled(false);
        typeof(ActionCombatController).GetMethod("InterruptAttack", Private).Invoke(combat, null);
        motor.Teleport(new Vector3(-30, -30, 0));
        combat.hitbox.knockback = 0f;
        foreach (var brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        {
            brain.enabled = false;brain.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }
        for (int d = 0; d < 8; d++)
        {
            Face(motor, d);combat.ResetCombo();
            Vector2 direction = new Vector2(Mathf.Cos(d*Mathf.PI/4f),Mathf.Sin(d*Mathf.PI/4f));
            var target = MakeTarget((Vector2)motor.transform.position + direction*0.9f);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            for (int step = 0; step < 3; step++)
            {
                target.GetComponent<Rigidbody2D>().Sleep();motor.GetComponent<Rigidbody2D>().Sleep();
                int before = target.currentHealth;
                combat.RequestAttack();
                Check(combat.CurrentStep == step, "direction " + d + " combo step " + step);
                while (combat.IsAttacking) yield return null;
                int expected = Mathf.RoundToInt(combat.damage * combat.combo[step].damageMultiplier);
                Check(before-target.currentHealth == expected, "direction " + d + " exactly one sleeping-target hit step " + step);
                yield return new WaitForSeconds(0.03f);
            }
            Object.Destroy(target.gameObject);
            yield return null;
        }
        // Tight diagonal/near contacts, excluded rear/far contacts, and duplicate colliders.
        var rangeTarget = MakeTarget(Vector2.zero, 0.1f);
        var child = new GameObject("SecondHurtCollider");child.transform.SetParent(rangeTarget.transform,false);
        child.AddComponent<CircleCollider2D>().radius = 0.1f;
        var aim = typeof(ActionCombatController).GetMethod("AimHitbox", Private);
        for (int d = 0; d < 8; d++)
        {
            Vector2 direction = new Vector2(Mathf.Cos(d*Mathf.PI/4f),Mathf.Sin(d*Mathf.PI/4f));
            Vector2 lateral = new Vector2(-direction.y,direction.x);
            Vector2[] offsets = { direction*0.3f, direction*0.8f+lateral*0.64f, -direction*0.8f, direction*2.4f };
            for (int test = 0; test < offsets.Length; test++)
            {
                rangeTarget.transform.position = motor.transform.position + (Vector3)offsets[test];
                rangeTarget.GetComponent<Rigidbody2D>().position = rangeTarget.transform.position;
                aim.Invoke(combat,new object[]{combat.combo[0],direction});
                int before = rangeTarget.currentHealth;
                combat.hitbox.Activate();
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                combat.hitbox.Deactivate();
                int expected = test < 2 ? combat.damage : 0;
                Check(before-rangeTarget.currentHealth == expected,"direction " + d + " range/dedup case " + test);
            }
        }
        Object.Destroy(rangeTarget.gameObject);yield return null;
        // Walking, sprint-speed and dodge-speed contact in every direction; allow escape.
        for (int d = 0; d < 8; d++)
        {
            motor.Teleport(new Vector3(-30,-30,0));
            Vector2 direction = new Vector2(Mathf.Cos(d*Mathf.PI/4f),Mathf.Sin(d*Mathf.PI/4f));
            var blocker = MakeTarget((Vector2)motor.transform.position + direction*0.8f);
            var body = blocker.GetComponent<Rigidbody2D>();Vector2 origin=body.position;
            Physics2D.SyncTransforms();
            motor.ExternalVelocity=direction*(d%3==0?5f:d%3==1?8f:12f);
            yield return new WaitForSeconds(0.5f);
            motor.ExternalVelocity=Vector2.zero;
            Check(Vector2.Distance(origin,body.position)<0.01f,"direction " + d + " no pushing at walk/sprint/dodge speed");
            Vector2 playerAtContact=motor.transform.position;
            motor.ExternalVelocity=-direction*5f;
            yield return new WaitForSeconds(0.15f);
            motor.ExternalVelocity=Vector2.zero;
            Check(Vector2.Dot((Vector2)motor.transform.position-playerAtContact,-direction)>0.3f,"direction " + d + " can leave contact");
            Object.Destroy(blocker.gameObject);yield return null;
        }
        // Particle pool emits one burst for accepted damage and none for rejected damage.
        motor.Teleport(originalPosition);Face(motor,6);combat.ResetCombo();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_GoblinWarrior.prefab");
        var visual=Object.Instantiate(prefab,originalPosition+Vector3.down*0.95f,Quaternion.identity);
        visual.GetComponent<EnemyBrain>().enabled=false;
        var visualHealth=visual.GetComponent<ActionEnemy>();visualHealth.currentHealth=1000;
        yield return null;
        var fx=Object.FindFirstObjectByType<ActionHitParticles>();
        var pool=(ParticleSystem[])typeof(ActionHitParticles).GetField("pool",Private).GetValue(fx);
        foreach(var ps in pool)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        int poolChildren=fx.transform.childCount;
        int healthBefore=visualHealth.currentHealth;
        combat.RequestAttack();
        while(visualHealth.currentHealth==healthBefore && combat.IsAttacking)yield return null;
        int count=0;foreach(var ps in pool)count+=ps.particleCount;
        Check(count==10,"accepted enemy hit emits ten sparks once");
        Check(fx.transform.childCount==poolChildren,"particle objects reused");
        yield return new WaitForSeconds(0.03f);
        ScreenCapture.CaptureScreenshot("/tmp/action-combat-fix/enemy-impact.png");
        while(combat.IsAttacking)yield return null;
        foreach(var ps in pool)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var playerHealth=PlayerHealth.Player;
        typeof(PlayerHealth).GetField("invincibleUntil",Private).SetValue(playerHealth,0f);
        int playerBefore=playerHealth.currentHealth;
        var info=new DamageInfo{amount=5,hitPoint=playerHealth.transform.position,sourcePosition=visual.transform.position,source=visual};
        playerHealth.TakeDamage(info);
        count=0;foreach(var ps in pool)count+=ps.particleCount;
        Check(playerHealth.currentHealth<playerBefore && count==10,"accepted player hit emits warm sparks");
        playerHealth.TakeDamage(info);
        int rejectedCount=0;foreach(var ps in pool)rejectedCount+=ps.particleCount;
        Check(playerHealth.currentHealth<playerBefore && rejectedCount==count,"invincible hit adds no particles");
        yield return new WaitForSeconds(0.03f);
        ScreenCapture.CaptureScreenshot("/tmp/action-combat-fix/player-impact.png");
        yield return new WaitForSeconds(0.4f);
        File.AppendAllText(Output,"RESULT passed="+passed+" failed="+failed+"\n");
        Debug.Log("[ActionCombatRegression] passed="+passed+" failed="+failed);
        EditorApplication.isPlaying=false;
    }
    [MenuItem("工具/角色扮演/临时截取受击粒子")]
    public static void CaptureVisual()
    {
        Directory.CreateDirectory("/tmp/action-combat-fix");
        var combat=Object.FindFirstObjectByType<ActionCombatController>();
        combat.StartCoroutine(Visual(combat));
    }
    static IEnumerator Visual(ActionCombatController combat)
    {
        var motor=combat.GetComponent<PlayerMotor>();Vector3 original=motor.transform.position;
        motor.SetControlEnabled(false);combat.debugAutoAttack=false;
        foreach(var brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        {brain.enabled=false;brain.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;}
        for(int d=0;d<8;d++)
        {
            motor.Teleport(new Vector3(-30,-30,0));
            Vector2 direction=new Vector2(Mathf.Cos(d*Mathf.PI/4f),Mathf.Sin(d*Mathf.PI/4f));
            Vector2 lateral=new Vector2(-direction.y,direction.x);
            var blocker=MakeTarget((Vector2)motor.transform.position+direction*0.8f);
            var body=blocker.GetComponent<Rigidbody2D>();Vector2 origin=body.position;
            Physics2D.SyncTransforms();motor.ExternalVelocity=direction*5f;
            yield return new WaitForSeconds(0.15f);
            Vector2 contact=motor.transform.position;
            motor.ExternalVelocity=(direction+lateral)*3.5f;
            yield return new WaitForSeconds(0.3f);
            motor.ExternalVelocity=Vector2.zero;
            bool okay=Vector2.Distance(origin,body.position)<0.01f && Vector2.Dot((Vector2)motor.transform.position-contact,lateral)>0.1f;
            File.AppendAllText(Output,(okay?"PASS":"FAIL")+" direction "+d+" can slide around enemy without pushing\n");
            Object.Destroy(blocker.gameObject);yield return null;
        }
        motor.Teleport(original);Face(motor,0);combat.ResetCombo();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_GoblinWarrior.prefab");
        var visual=Object.Instantiate(prefab,original+Vector3.right*1.05f,Quaternion.identity);
        visual.GetComponent<EnemyBrain>().enabled=false;
        var target=visual.GetComponent<ActionEnemy>();target.currentHealth=1000;
        yield return new WaitForSeconds(0.8f);
        combat.RequestAttack();
        while(target.currentHealth==1000 && combat.IsAttacking)yield return null;
        yield return new WaitForSeconds(0.08f);
        ScreenCapture.CaptureScreenshot("/tmp/action-combat-fix/enemy-impact.png");
        while(combat.IsAttacking)yield return null;
        yield return new WaitForSeconds(0.2f);
        var health=PlayerHealth.Player;
        typeof(PlayerHealth).GetField("invincibleUntil",Private).SetValue(health,0f);
        health.TakeDamage(new DamageInfo{amount=5,hitPoint=health.transform.position,sourcePosition=visual.transform.position,source=visual});
        yield return new WaitForSeconds(0.08f);
        ScreenCapture.CaptureScreenshot("/tmp/action-combat-fix/player-impact.png");
        yield return new WaitForSeconds(0.3f);
        File.AppendAllText(Output,"Visual captures completed\n");
        EditorApplication.isPlaying=false;
    }

}
